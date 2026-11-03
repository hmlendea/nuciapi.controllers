# Data Model

## Overview

NuciAPI.Controllers defines **no domain data models**. It provides only infrastructure types for request processing, authorisation, and error responses. Consumers define their own domain models.

## Library-Defined Types

### NuciApiController (Base Class)

```csharp
public abstract class NuciApiController : ControllerBase
{
    // Protected methods for request processing
    protected ActionResult ProcessRequest<TRequest>(TRequest request, Action action, NuciApiAuthorisation authorisation);
    protected ActionResult ProcessRequest<TRequest, TResponse>(TRequest request, Func<TResponse> action, NuciApiAuthorisation authorisation);
    protected Task<ActionResult> ProcessRequestAsync<TRequest>(TRequest request, Func<Task> action, NuciApiAuthorisation authorisation);
    protected Task<ActionResult> ProcessRequestAsync<TRequest, TResponse>(TRequest request, Func<Task<TResponse>> action, NuciApiAuthorisation authorisation);
    
    // HMAC extraction
    protected string? GetHmacToken();
    
    // Authorisation helper
    protected Task AuthoriseRequest(NuciApiAuthorisation authorisation);
}
```

### NuciApiAuthorisation (Abstract Base)

```csharp
public abstract class NuciApiAuthorisation
{
    // Factory methods
    public static NuciApiNoneAuthorisation None { get; }
    public static NuciApiKeyAuthorisation ApiKey(string key);
    
    // Abstract authorisation method
    public abstract Task Authorise(HttpRequest request);
    
    // Protected implementation
    protected abstract Task PerformAuthorisation(HttpRequest request);
}
```

### NuciApiKeyAuthorisation

```csharp
public sealed class NuciApiKeyAuthorisation : NuciApiAuthorisation
{
    public string Key { get; } // Readonly, set via constructor
    
    public NuciApiKeyAuthorisation(string key);
    
    protected override Task PerformAuthorisation(HttpRequest request);
}
```

### NuciApiNoneAuthorisation

```csharp
public sealed class NuciApiNoneAuthorisation : NuciApiAuthorisation
{
    public static readonly NuciApiNoneAuthorisation None = new();
    
    private NuciApiNoneAuthorisation() { }
    
    protected override Task PerformAuthorisation(HttpRequest request)
        => Task.CompletedTask;
}
```

## NuciAPI-Defined Types (Transitive)

### NuciApiErrorResponse (from NuciAPI 3.5.1)

```csharp
// In NuciAPI package
public class NuciApiErrorResponse
{
    public string Error { get; }
    public string Message { get; }
    
    public static NuciApiErrorResponse InvalidRequest { get; }
    public static NuciApiErrorResponse NotFound { get; }
}
```

**Values:**
- `InvalidRequest`: `Error = "InvalidRequest"`, `Message = "The request was invalid."`
- `NotFound`: `Error = "NotFound"`, `Message = "The requested resource was not found."`

## Consumer-Defined Models (Patterns)

### Request Base Class

```csharp
public abstract class ApiRequest
{
    public string? CorrelationId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class CreateUserRequest : ApiRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    [MinLength(12)]
    public string Password { get; set; } = string.Empty;
    
    [StringLength(100)]
    public string? DisplayName { get; set; }
}
```

### Response Base Class

```csharp
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

public class UserResponse : ApiResponse
{
    public UserData? Data { get; set; }
}

public class UserData
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
```

### Paginated Response

```csharp
public class PagedResponse<T> : ApiResponse where T : class
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
```

### Error Response Mapping

| Library Response | Consumer Model | HTTP Status |
|------------------|----------------|-------------|
| `BadRequest(NuciApiErrorResponse.InvalidRequest)` | `ValidationErrorResponse` | 400 |
| `NotFound(NuciApiErrorResponse.NotFound)` | `NotFoundErrorResponse` | 404 |
| `Unauthorized()` | `UnauthorizedErrorResponse` | 401 |
| `StatusCode(500)` | `InternalErrorResponse` | 500 |

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
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });
```

### DateTime Format

```csharp
// ISO 8601 UTC (recommended)
options.JsonSerializerOptions.Converters.Add(new IsoDateTimeConverter
{
    DateTimeFormat = "yyyy-MM-ddTHH:mm:ss.fffZ"
});
```

### Null Handling

```csharp
// Ignore nulls in responses
options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;

// But include nulls in requests (for validation)
```

## Validation

### Data Annotations

```csharp
public class UpdateProfileRequest
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string DisplayName { get; set; } = string.Empty;
    
    [EmailAddress]
    public string? Email { get; set; }
    
    [Phone]
    public string? PhoneNumber { get; set; }
    
    [Url]
    public string? Website { get; set; }
}
```

### FluentValidation

```csharp
public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator(IUserService userService)
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format")
            .MustAsync(async (email, ct) => !await userService.ExistsAsync(email))
            .WithMessage("Email already registered");
            
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(12).WithMessage("Password must be at least 12 characters")
            .Matches("[A-Z]").WithMessage("Password must contain uppercase")
            .Matches("[a-z]").WithMessage("Password must contain lowercase")
            .Matches("[0-9]").WithMessage("Password must contain number")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain special character");
    }
}
```

## Mapping

### AutoMapper

```csharp
public class UserProfile : Profile
{
    public UserProfile()
    {
        CreateMap<CreateUserRequest, User>()
            .ForMember(d => d.Id, opt => opt.MapFrom(_ => Guid.NewGuid()))
            .ForMember(d => d.CreatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow));
            
        CreateMap<User, UserData>();
        CreateMap<User, UserResponse>()
            .ForMember(d => d.Data, opt => opt.MapFrom(src => src));
    }
}
```

### Manual Mapping

```csharp
// In service
public UserData MapToData(User user)
{
    return new UserData
    {
        Id = user.Id,
        Email = user.Email,
        DisplayName = user.DisplayName,
        CreatedAt = user.CreatedAt
    };
}
```

## Versioning Models

### V1 → V2 Evolution

```csharp
// V1
public class UserResponseV1 : ApiResponse
{
    public UserDataV1? Data { get; set; }
}

public class UserDataV1
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty; // Combined name
}

// V2 (breaking: split name)
public class UserResponseV2 : ApiResponse
{
    public UserDataV2? Data { get; set; }
}

public class UserDataV2
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? DisplayName { get; set; } // Computed
}
```

## Data Model Checklist

- [ ] Request models inherit from `ApiRequest` (correlation, timestamp)
- [ ] Response models inherit from `ApiResponse` (success, error, correlation)
- [ ] Validation attributes on all required fields
- [ ] Nullable reference types enabled (`#nullable enable`)
- [ ] JSON serialization configured (camelCase, ignore nulls)
- [ ] OpenAPI documentation generated
- [ ] Versioning strategy defined
- [ ] Error response models match `NuciApiErrorResponse` codes
- [ ] Pagination models for list endpoints
- [ ] Mapping layer between API and domain models