# API Usage Examples

## Overview

This document provides comprehensive usage examples for NuciAPI.Controllers, covering common patterns and best practices.

## Basic Controller Setup

### Minimal Controller

```csharp
using Microsoft.AspNetCore.Mvc;
using NuciAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : NuciApiController
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet("{id:guid}")]
    public ActionResult GetUser(Guid id)
    {
        return ProcessRequest(new GetUserRequest { Id = id }, () =>
        {
            var user = _userService.GetById(id);
            return user != null ? new UserResponse { Data = user } : null;
        }, NuciApiAuthorisation.ApiKey("api-key"));
    }
}
```

### Request/Response Models

```csharp
public class GetUserRequest
{
    public Guid Id { get; set; }
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
}
```

## Authorisation Patterns

### API Key Authorisation

```csharp
[HttpPost("create")]
public ActionResult CreateUser(CreateUserRequest request)
{
    return ProcessRequest(request, () =>
    {
        var user = _userService.Create(request.Email, request.DisplayName);
        return new UserResponse { Data = user };
    }, NuciApiAuthorisation.ApiKey("admin-key"));
}
```

### No Authorisation (Public Endpoint)

```csharp
[HttpGet("health")]
public ActionResult HealthCheck()
{
    return ProcessRequest(new HealthRequest(), () =>
    {
        return new HealthResponse { Status = "Healthy", Timestamp = DateTime.UtcNow };
    }, NuciApiAuthorisation.None);
}
```

### Custom Authorisation (JWT)

```csharp
public class JwtAuthorisation : NuciApiAuthorisation
{
    private readonly IJwtValidator _validator;

    public JwtAuthorisation(IJwtValidator validator)
    {
        _validator = validator;
    }

    public override async Task Authorise(HttpRequest request)
    {
        var authHeader = request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            throw new AuthenticationException("Missing or invalid Authorization header.");
        }

        var token = authHeader["Bearer ".Length..];
        var principal = await _validator.ValidateAsync(token);

        if (principal == null)
        {
            throw new AuthenticationException("Invalid or expired token.");
        }

        request.HttpContext.User = principal;
    }
}

// Usage
[HttpGet("profile")]
public ActionResult GetProfile()
{
    return ProcessRequest(new ProfileRequest(), () =>
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var profile = _userService.GetProfile(Guid.Parse(userId!));
        return new ProfileResponse { Data = profile };
    }, new JwtAuthorisation(_jwtValidator));
}
```

## Async Patterns

### Async Action with Database

```csharp
[HttpPost("transfer")]
public async Task<ActionResult> Transfer(TransferRequest request)
{
    return await ProcessRequestAsync(request, async () =>
    {
        await _accountService.TransferAsync(request.FromAccount, request.ToAccount, request.Amount);
        return new TransferResponse { Success = true };
    }, NuciApiAuthorisation.ApiKey("banking-key"));
}
```

### Async with External API Call

```csharp
[HttpGet("weather/{city}")]
public async Task<ActionResult> GetWeather(string city)
{
    return await ProcessRequestAsync(new WeatherRequest { City = city }, async () =>
    {
        var response = await _httpClient.GetAsync($"https://api.weather.com/v1/{city}");
        response.EnsureSuccessStatusCode();
        var weather = await response.Content.ReadFromJsonAsync<WeatherData>();
        return new WeatherResponse { Data = weather };
    }, NuciApiAuthorisation.ApiKey("weather-key"));
}
```

### Parallel Async Operations

```csharp
[HttpGet("dashboard")]
public async Task<ActionResult> GetDashboard()
{
    return await ProcessRequestAsync(new DashboardRequest(), async () =>
    {
        var userTask = _userService.GetCurrentUserAsync();
        var statsTask = _statsService.GetUserStatsAsync();
        var notificationsTask = _notificationService.GetRecentAsync(10);

        await Task.WhenAll(userTask, statsTask, notificationsTask);

        return new DashboardResponse
        {
            User = userTask.Result,
            Stats = statsTask.Result,
            Notifications = notificationsTask.Result
        };
    }, NuciApiAuthorisation.ApiKey("dashboard-key"));
}
```

## HMAC Authentication

### Extracting HMAC Token

```csharp
[HttpPost("secure-action")]
public ActionResult SecureAction(SecureRequest request)
{
    var hmacToken = GetHmacToken();

    if (hmacToken == null)
    {
        return BadRequest(new { error = "Missing HMAC signature" });
    }

    // Validate HMAC (consumer implementation)
    if (!_hmacValidator.Validate(request, hmacToken))
    {
        return Unauthorized(new { error = "Invalid HMAC signature" });
    }

    return ProcessRequest(request, () =>
    {
        var result = _secureService.Process(request);
        return new SecureResponse { Data = result };
    }, NuciApiAuthorisation.None); // HMAC validated manually
}
```

### HMAC Validation Service

```csharp
public class HmacValidationService
{
    private readonly string _secret;
    private readonly TimeSpan _tolerance = TimeSpan.FromMinutes(5);

    public HmacValidationService(IConfiguration config)
    {
        _secret = config["Hmac:Secret"]!;
    }

    public bool Validate<T>(T payload, string signature)
    {
        var json = JsonSerializer.Serialize(payload);
        var expected = ComputeHmac(json);
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromBase64String(signature), expected);
    }

    private byte[] ComputeHmac(string data)
    {
        using var hmac = new HMACSHA256(Convert.FromBase64String(_secret));
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    }
}
```

## Error Handling Patterns

### Custom Exception Filter

```csharp
public class ApiExceptionFilter : IExceptionFilter
{
    private readonly ILogger<ApiExceptionFilter> _logger;

    public ApiExceptionFilter(ILogger<ApiExceptionFilter> logger)
    {
        _logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        var correlationId = context.HttpContext.TraceIdentifier;

        switch (context.Exception)
        {
            case ValidationException ex:
                _logger.LogWarning(ex, "Validation failed | CorrelationId: {CorrelationId}", correlationId);
                context.Result = new BadRequestObjectResult(new
                {
                    error = "ValidationError",
                    message = ex.Message,
                    details = ex.Errors
                });
                context.ExceptionHandled = true;
                break;

            case NotFoundException ex:
                _logger.LogInformation(ex, "Not found | CorrelationId: {CorrelationId}", correlationId);
                context.Result = new NotFoundObjectResult(new
                {
                    error = "NotFound",
                    message = ex.Message
                });
                context.ExceptionHandled = true;
                break;

            case UnauthorizedAccessException ex:
                _logger.LogWarning(ex, "Unauthorized | CorrelationId: {CorrelationId}", correlationId);
                context.Result = new UnauthorizedObjectResult(new
                {
                    error = "Unauthorized",
                    message = ex.Message
                });
                context.ExceptionHandled = true;
                break;

            default:
                _logger.LogError(context.Exception, "Unhandled error | CorrelationId: {CorrelationId}", correlationId);
                context.Result = new ObjectResult(new
                {
                    error = "InternalError",
                    message = "An unexpected error occurred."
                })
                {
                    StatusCode = 500
                };
                context.ExceptionHandled = true;
                break;
        }
    }
}

// Register in Program.cs
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ApiExceptionFilter>();
});
```

### Business Logic Exceptions

```csharp
public class InsufficientFundsException : Exception
{
    public Guid AccountId { get; }
    public decimal Available { get; }
    public decimal Requested { get; }

    public InsufficientFundsException(Guid accountId, decimal available, decimal requested)
        : base($"Account {accountId} has insufficient funds. Available: {available}, Requested: {requested}")
    {
        AccountId = accountId;
        Available = available;
        Requested = requested;
    }
}

// In controller
[HttpPost("withdraw")]
public ActionResult Withdraw(WithdrawRequest request)
{
    return ProcessRequest(request, () =>
    {
        var balance = _accountService.GetBalance(request.AccountId);
        if (balance < request.Amount)
        {
            throw new InsufficientFundsException(request.AccountId, balance, request.Amount);
        }

        _accountService.Withdraw(request.AccountId, request.Amount);
        return new WithdrawResponse { Success = true, NewBalance = balance - request.Amount };
    }, NuciApiAuthorisation.ApiKey("banking-key"));
}
```

## Validation Patterns

### FluentValidation Integration

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

        RuleFor(x => x.DisplayName)
            .MaximumLength(100).WithMessage("Display name too long");
    }
}

// In controller
[HttpPost("register")]
public ActionResult Register(CreateUserRequest request)
{
    return ProcessRequest(request, () =>
    {
        var user = _userService.Create(request.Email, request.Password, request.DisplayName);
        return new UserResponse { Data = user };
    }, NuciApiAuthorisation.None);
}
```

### Model Validation (Data Annotations)

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

// Automatic validation via [ApiController] attribute
[HttpPut("profile")]
public ActionResult UpdateProfile(UpdateProfileRequest request)
{
    // ModelState.IsValid checked automatically by [ApiController]
    return ProcessRequest(request, () =>
    {
        var user = _userService.UpdateProfile(User.GetUserId(), request.DisplayName, request.Email);
        return new ProfileResponse { Data = user };
    }, NuciApiAuthorisation.ApiKey("user-key"));
}
```

## Pagination and Filtering

### Paginated List Endpoint

```csharp
public class PagedRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; }
}

public class PagedResponse<T> : ApiResponse
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

[HttpGet("users")]
public ActionResult GetUsers([FromQuery] PagedRequest request)
{
    return ProcessRequest(request, () =>
    {
        var (users, totalCount) = _userService.GetPaged(
            request.Page, request.PageSize, request.Search, request.SortBy, request.SortDescending);

        return new PagedResponse<UserData>
        {
            Items = users,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount
        };
    }, NuciApiAuthorisation.ApiKey("admin-key"));
}
```

## File Upload

### Multipart Form Data

```csharp
[HttpPost("upload")]
public async Task<ActionResult> UploadFile(IFormFile file)
{
    var request = new UploadRequest { File = file };

    return await ProcessRequestAsync(request, async () =>
    {
        if (file.Length > 10 * 1024 * 1024) // 10 MB
        {
            throw new ValidationException("File too large. Maximum 10 MB.");
        }

        var allowedTypes = new[] { "image/jpeg", "image/png", "application/pdf" };
        if (!allowedTypes.Contains(file.ContentType))
        {
            throw new ValidationException("Invalid file type. Allowed: JPEG, PNG, PDF.");
        }

        var fileId = await _fileService.SaveAsync(file);
        return new UploadResponse { FileId = fileId, FileName = file.FileName };
    }, NuciApiAuthorisation.ApiKey("upload-key"));
}

public class UploadRequest
{
    public IFormFile File { get; set; } = default!;
}

public class UploadResponse : ApiResponse
{
    public Guid FileId { get; set; }
    public string FileName { get; set; } = string.Empty;
}
```

## Health Checks

### Liveness and Readiness

```csharp
[HttpGet("health/live")]
public ActionResult Liveness()
{
    return ProcessRequest(new HealthRequest(), () =>
    {
        return new HealthResponse { Status = "Alive", Timestamp = DateTime.UtcNow };
    }, NuciApiAuthorisation.None);
}

[HttpGet("health/ready")]
public async Task<ActionResult> Readiness()
{
    return await ProcessRequestAsync(new HealthRequest(), async () =>
    {
        var dbOk = await _dbContext.Database.CanConnectAsync();
        var cacheOk = await _cache.PingAsync();
        var externalOk = await _externalService.HealthCheckAsync();

        var status = dbOk && cacheOk && externalOk ? "Ready" : "Degraded";

        return new HealthResponse
        {
            Status = status,
            Timestamp = DateTime.UtcNow,
            Checks = new Dictionary<string, bool>
            {
                ["database"] = dbOk,
                ["cache"] = cacheOk,
                ["external"] = externalOk
            }
        };
    }, NuciApiAuthorisation.None);
}
```

## Testing Examples

### Unit Test for Controller

```csharp
[TestFixture]
public class UsersControllerTests
{
    private UsersController _controller;
    private Mock<IUserService> _userServiceMock;

    [SetUp]
    public void SetUp()
    {
        _userServiceMock = new Mock<IUserService>();
        _controller = new UsersController(_userServiceMock.Object);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [Test]
    public void GetUser_ReturnsUser_WhenExists()
    {
        var userId = Guid.NewGuid();
        var user = new UserData { Id = userId, Email = "test@example.com" };
        _userServiceMock.Setup(x => x.GetById(userId)).Returns(user);

        _controller.Request.Headers["Authorization"] = "Bearer api-key";

        var result = _controller.GetUser(userId);

        Assert.IsInstanceOf<OkObjectResult>(result);
        var response = (UserResponse)((OkObjectResult)result).Value;
        Assert.AreEqual(userId, response.Data!.Id);
    }

    [Test]
    public void GetUser_Returns404_WhenNotExists()
    {
        var userId = Guid.NewGuid();
        _userServiceMock.Setup(x => x.GetById(userId)).Returns((UserData?)null);

        _controller.Request.Headers["Authorization"] = "Bearer api-key";

        var result = _controller.GetUser(userId);

        Assert.IsInstanceOf<NotFoundObjectResult>(result);
    }

    [Test]
    public void GetUser_Returns401_WhenInvalidKey()
    {
        _controller.Request.Headers["Authorization"] = "Bearer wrong-key";

        var ex = Assert.Throws<AuthenticationException>(() =>
            _controller.GetUser(Guid.NewGuid()));

        Assert.AreEqual("Invalid API key.", ex.Message);
    }
}
```

### Integration Test

```csharp
[TestFixture]
public class UsersControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public UsersControllerIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new("Bearer", "test-api-key");
    }

    [Test]
    public async Task GetUser_Returns200_WhenUserExists()
    {
        var response = await _client.GetAsync("/api/users/12345678-1234-1234-1234-123456789012");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.IsNotNull(user.Data);
    }

    [Test]
    public async Task CreateUser_Returns200_WhenValid()
    {
        var request = new CreateUserRequest
        {
            Email = "newuser@example.com",
            DisplayName = "New User"
        };

        var response = await _client.PostAsJsonAsync("/api/users", request);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }
}
```

## Complete Program.cs Example

```csharp
var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Configuration.AddUserSecrets<Program>();

// Services
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ApiExceptionFilter>();
})
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Domain services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddHttpClient<IWeatherService, WeatherService>();

// Validation
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Authentication
builder.Services.AddSingleton<NuciApiAuthorisation>(sp =>
    NuciApiAuthorisation.ApiKey(sp.GetRequiredService<IConfiguration>()["ApiKey"]!));

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
```