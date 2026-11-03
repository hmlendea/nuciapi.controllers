# Components: Integration Models

## Overview

This document describes the data models used for integration between NuciAPI.Controllers consumers and external systems. The library itself defines no integration models — it provides the controller base and authorisation infrastructure. Consumers define their own request/response models.

## Request Models

### Base Request Pattern

```csharp
// Consumer-defined request base
public abstract class ApiRequest
{
    public string? CorrelationId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

// Example concrete request
public class CreateUserRequest : ApiRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string Password { get; set; } = string.Empty;

    public string? DisplayName { get; set; }
}
```

### HMAC-Signed Requests

```csharp
// For HMAC-authenticated endpoints
public class SignedRequest<T> : ApiRequest where T : class
{
    public T Payload { get; set; } = default!;
    public string Signature { get; set; } = string.Empty;
    public long Timestamp { get; set; }
    public string Nonce { get; set; } = string.Empty;
}
```

## Response Models

### Base Response Pattern

```csharp
// Consumer-defined response base
public abstract class ApiResponse
{
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool Success => Error == null;
    public ApiError? Error { get; set; }
}

public class ApiError
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, string[]>? Details { get; set; }
}
```

### Success Responses

```csharp
// Single resource
public class UserResponse : ApiResponse
{
    public UserData Data { get; set; } = new();
}

public class UserData
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

// Collection
public class PagedResponse<T> : ApiResponse where T : class
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
```

### Error Responses (Aligning with NuciApiErrorResponse)

```csharp
// Maps to NuciApiErrorResponse.InvalidRequest (400)
public class ValidationErrorResponse : ApiResponse
{
    public ValidationErrorResponse()
    {
        Error = new ApiError { Code = "InvalidRequest", Message = "The request was invalid." };
    }

    public List<ValidationError> ValidationErrors { get; set; } = new();
}

public class ValidationError
{
    public string Field { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

// Maps to NuciApiErrorResponse.NotFound (404)
public class NotFoundErrorResponse : ApiResponse
{
    public NotFoundErrorResponse(string message = "The requested resource was not found.")
    {
        Error = new ApiError { Code = "NotFound", Message = message };
    }
}
```

## Authorisation Models

### API Key Model

```csharp
// Consumer's API key representation
public class ApiKey
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string[] Scopes { get; set; } = Array.Empty<string>();
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; } = true;
}
```

### HMAC Credential Model

```csharp
// For HMAC authentication
public class HmacCredential
{
    public string KeyId { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty; // Base64 encoded
    public string Algorithm { get; set; } = "HMACSHA256";
    public DateTime? ExpiresAt { get; set; }
    public string[] AllowedIps { get; set; } = Array.Empty<string>();
}
```

## Webhook Models

### Outbound Webhook

```csharp
// Consumer sends webhooks to external systems
public class WebhookPayload<T>
{
    public string EventId { get; set; } = Guid.NewGuid().ToString();
    public string EventType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public T Data { get; set; } = default!;
    public WebhookMetadata Metadata { get; set; } = new();
}

public class WebhookMetadata
{
    public string Source { get; set; } = "NuciAPI.Controllers";
    public string Version { get; set; } = "1.0";
    public string? CorrelationId { get; set; }
}
```

### Inbound Webhook (Consumer Receives)

```csharp
// Consumer endpoint for receiving webhooks
[HttpPost("webhooks/{provider}")]
public ActionResult ReceiveWebhook(string provider, [FromBody] JsonElement payload)
{
    return ProcessRequest(payload, () =>
    {
        _webhookProcessor.Process(provider, payload);
    }, NuciApiAuthorisation.None); // Or HMAC validation
}
```

## Integration Patterns

### 1. Request/Response (Synchronous)

```
Consumer → NuciAPI.Controllers → Business Logic → Response
```

### 2. Async with Polling

```csharp
[HttpPost("jobs")]
public ActionResult StartJob(JobRequest request)
{
    return ProcessRequest(request, () =>
    {
        var jobId = _jobService.Start(request);
        return new JobStartedResponse { JobId = jobId, StatusUrl = $"/jobs/{jobId}" };
    }, auth);
}

[HttpGet("jobs/{jobId}")]
public ActionResult GetJobStatus(Guid jobId)
{
    return ProcessRequest(new JobStatusRequest { JobId = jobId }, () =>
    {
        var status = _jobService.GetStatus(jobId);
        return status switch
        {
            JobStatus.Completed => new JobResponse { Status = status, Result = _jobService.GetResult(jobId) },
            JobStatus.Failed => new JobResponse { Status = status, Error = _jobService.GetError(jobId) },
            _ => new JobResponse { Status = status }
        };
    }, auth);
}
```

### 3. Webhook Callback

```csharp
// Consumer registers callback
public class RegisterWebhookRequest : ApiRequest
{
    public string CallbackUrl { get; set; } = string.Empty;
    public string[] Events { get; set; } = Array.Empty<string>();
    public string? Secret { get; set; } // For HMAC signing
}

// External system calls consumer's webhook
// Consumer validates HMAC, processes event
```

### 4. Batch Operations

```csharp
public class BatchRequest<T>
{
    public List<T> Items { get; set; } = new();
    public bool ContinueOnError { get; set; } = false;
}

public class BatchResponse<T>
{
    public List<BatchItemResult<T>> Results { get; set; } = new();
    public int SuccessCount => Results.Count(r => r.Success);
    public int FailureCount => Results.Count(r => !r.Success);
}

public class BatchItemResult<T>
{
    public int Index { get; set; }
    public bool Success { get; set; }
    public T? Data { get; set; }
    public ApiError? Error { get; set; }
}
```

## Serialization

### JSON (Default)

```csharp
// Program.cs
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
```

### Content Negotiation

```csharp
// Consumer can add XML, Protobuf, etc.
builder.Services.AddControllers()
    .AddXmlSerializerFormatters()
    .AddProtobufFormatters(); // Custom
```

## Versioning Models

### Header-Based Versioning

```csharp
// Request header: Accept: application/vnd.myapi.v2+json
public class VersionedRequest
{
    [FromHeader(Name = "Accept")]
    public string? AcceptHeader { get; set; }

    public int GetVersion()
    {
        if (string.IsNullOrEmpty(AcceptHeader)) return 1;
        var match = Regex.Match(AcceptHeader, @"v(\d+)");
        return match.Success ? int.Parse(match.Groups[1].Value) : 1;
    }
}
```

### URL-Based Versioning

```csharp
// Route: api/v2/users
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class UsersController : NuciApiController
{
    // ...
}
```

## Model Validation

### Data Annotations (Consumer)

```csharp
public class CreateOrderRequest : ApiRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int CustomerId { get; set; }

    [Required]
    [MinLength(1)]
    public List<OrderItemRequest> Items { get; set; } = new();

    [CreditCard]
    public string? PaymentToken { get; set; }
}

public class OrderItemRequest
{
    [Required]
    public Guid ProductId { get; set; }

    [Range(1, 1000)]
    public int Quantity { get; set; }
}
```

### FluentValidation (Consumer)

```csharp
public class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator(IProductService products)
    {
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).SetValidator(new OrderItemValidator(products));
        RuleFor(x => x.PaymentToken).CreditCard().When(x => x.PaymentToken != null);
    }
}
```

## Mapping Between Layers

### Request → Domain

```csharp
// Consumer maps in action or service
public ActionResult CreateOrder(CreateOrderRequest request)
{
    return ProcessRequest(request, () =>
    {
        var domainOrder = _mapper.Map<Order>(request);
        var created = _orderService.Create(domainOrder);
        return _mapper.Map<OrderResponse>(created);
    }, auth);
}
```

### Domain → Response

```csharp
// AutoMapper profile
public class OrderProfile : Profile
{
    public OrderProfile()
    {
        CreateMap<Order, OrderResponse>()
            .ForMember(d => d.CorrelationId, opt => opt.Ignore());
        CreateMap<OrderItem, OrderItemResponse>();
    }
}
```

## OpenAPI / Swagger Integration

```csharp
// Program.cs
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });

    // API Key auth
    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = "Authorization",
        Description = "API Key: Bearer {key}"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" }
            },
            Array.Empty<string>()
        }
    });
});
```

## Integration Model Checklist

- [ ] Request models inherit from `ApiRequest` (correlation, timestamp)
- [ ] Response models inherit from `ApiResponse` (success, error, correlation)
- [ ] Validation attributes on all required fields
- [ ] Nullable reference types enabled
- [ ] JSON serialization configured (camelCase, ignore nulls)
- [ ] OpenAPI documentation generated
- [ ] Versioning strategy defined
- [ ] Error response models match `NuciApiErrorResponse` codes
- [ ] Batch operations supported for high-volume endpoints
- [ ] Webhook models defined for event-driven integrations