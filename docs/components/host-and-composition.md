# Host and Composition: Consumer Integration

## Purpose

Documents how consumers integrate NuciAPI.Controllers into their ASP.NET Core applications, including controller derivation, dependency injection, and pipeline configuration.

## Consumer Controller Implementation

### Minimal Controller

```csharp
using Microsoft.AspNetCore.Mvc;
using NuciAPI.Controllers;
using NuciAPI.Requests;
using NuciAPI.Responses;

public class MyController : NuciApiController
{
    [HttpPost("endpoint")]
    public ActionResult MyEndpoint(MyRequest request)
    {
        return ProcessRequest(request, () =>
        {
            // Business logic
        }, NuciApiAuthorisation.ApiKey("secret"));
    }
}
```

### Request DTO

```csharp
using NuciAPI.Requests;

public class MyRequest : NuciApiRequest
{
    public string Parameter { get; set; }
    // HmacToken inherited from NuciApiRequest
}
```

### Response DTO

```csharp
using NuciAPI.Responses;

public class MyResponse : NuciApiResponse
{
    public string Result { get; set; }
}
```

## ASP.NET Core Pipeline Integration

### No Library Registration Required

The library has **no** `IServiceCollection` extensions, no `AddNuciApiControllers()`, no middleware. Consumers:

1. Reference the NuGet package
2. Derive controllers from `NuciApiController`
3. Call `ProcessRequest` overloads in actions
4. Standard ASP.NET Core controller activation handles the rest

### Controller Activation

- Controllers instantiated per-request by ASP.NET Core
- `NuciApiController` derives from `ControllerBase`
- Model binding populates `TRequest` from request body/query/route
- `Request`/`Response`/`HttpContext` available via `ControllerBase`

### Routing

Standard attribute routing:
```csharp
[ApiController]
[Route("api/[controller]")]
public class MyController : NuciApiController { ... }
```

## Dependency Injection

### Library Has No DI Registrations

- `NuciApiAuthorisation` strategies instantiated by consumer at call site
- `NuciApiKeyAuthorisation` created via `NuciApiAuthorisation.ApiKey(key)`
- `NuciApiNoneAuthorisation` accessed via `NuciApiAuthorisation.None`
- Consumer manages API key lifetime (singleton, scoped, transient)

### Recommended Consumer Pattern

```csharp
// Program.cs or Startup.cs
builder.Services.AddSingleton<IApiKeyProvider, ConfigurationApiKeyProvider>();

// Controller
public class MyController : NuciApiController
{
    private readonly IApiKeyProvider _keyProvider;

    public MyController(IApiKeyProvider keyProvider)
    {
        _keyProvider = keyProvider;
    }

    [HttpPost("endpoint")]
    public ActionResult MyEndpoint(MyRequest request)
    {
        return ProcessRequest(request, () => { ... }, 
            NuciApiAuthorisation.ApiKey(_keyProvider.GetKey()));
    }
}
```

## Configuration Integration

### Library Has No Configuration

- No `IOptions`, no `appsettings.json` binding
- HMAC header name (`X-HMAC`) hard-coded
- Authorization header scheme (`Bearer`) hard-coded
- Consumer manages all secrets (Key Vault, env vars, config)

### Consumer Configuration Example

```json
// appsettings.json
{
  "ApiKeys": {
    "Default": "secret-from-key-vault"
  }
}
```

```csharp
public interface IApiKeyProvider
{
    string GetKey();
}

public class ConfigurationApiKeyProvider : IApiKeyProvider
{
    private readonly IConfiguration _config;
    public ConfigurationApiKeyProvider(IConfiguration config) => _config = config;
    public string GetKey() => _config["ApiKeys:Default"] 
        ?? throw new InvalidOperationException("API key not configured");
}
```

## Error Handling Integration

### Library Throws

- `NotImplementedException` — authorisation not specified (programming error)
- `AuthenticationException` — missing/invalid authorisation (expected failure)

### Consumer Handles

```csharp
// Program.cs
app.UseExceptionHandler("/error");

// Or middleware
app.Use(async (context, next) =>
{
    try { await next(); }
    catch (AuthenticationException ex)
    {
        context.Response.StatusCode = 401;
        await context.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
});
```

## Testing Integration

### Unit Testing Consumer Controllers

```csharp
[Test]
public void MyEndpoint_ReturnsOk_WhenValidRequest()
{
    var controller = new MyController(keyProvider);
    controller.ControllerContext = new ControllerContext
    {
        HttpContext = new DefaultHttpContext()
    };
    controller.Request.Headers["Authorization"] = "Bearer secret";
    controller.Request.Headers["X-HMAC"] = "hmac-value";

    var result = controller.MyEndpoint(new MyRequest { Parameter = "test" });

    Assert.IsInstanceOf<OkObjectResult>(result);
}
```

### Integration Testing

```csharp
[Test]
public async Task MyEndpoint_Returns401_WhenInvalidKey()
{
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", "wrong");
    
    var response = await client.PostAsJsonAsync("/api/my/endpoint", new MyRequest());
    
    Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
}
```

## Build & Runtime Requirements

| Requirement | Version | Notes |
|-------------|---------|-------|
| .NET SDK | 10.0 | Library targets net10.0 |
| ASP.NET Core | 10.0 | Framework reference |
| NuciAPI | 3.5.1 | Package reference (transitive) |

## Version Compatibility

| NuciAPI.Controllers | NuciAPI | ASP.NET Core | .NET |
|---------------------|---------|--------------|------|
| 2.3.1 | 3.5.1 | 10.0 | 10.0 |

Consumer must use compatible versions; breaking changes in NuciAPI require coordinated update.