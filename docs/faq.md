# Frequently Asked Questions

## General

### What is NuciAPI.Controllers?

A .NET library providing base controller classes and authorisation abstractions for building secure ASP.NET Core REST APIs. It standardises request processing, API key authentication, and HMAC validation.

### Who should use this library?

Teams building ASP.NET Core REST APIs who want:
- Consistent request/response handling
- Built-in API key and HMAC authorisation
- Minimal configuration overhead
- Integration with NuciAPI ecosystem

### What .NET versions are supported?

| Version | Status |
|---------|--------|
| .NET 10.0 | Current (2.3.x) |
| .NET 9.0 | Maintenance (2.2.x) |
| .NET 8.0 | Maintenance (2.1.x) |
| .NET 7.0 | EOL (1.x) |

### Is this an official Microsoft library?

No. This is a community library by Horațiu Mlendea, part of the NuciAPI ecosystem.

## Installation

### How do I install it?

```bash
dotnet add package NuciAPI.Controllers --version 2.3.1
```

Requires .NET 10.0 SDK.

### Does it work with .NET Framework?

No. .NET Core / .NET 5+ only.

### Can I use it with Minimal APIs?

Not directly. It's designed for Controller-based APIs. For Minimal APIs, extract the patterns manually.

## Authorisation

### How does API key authentication work?

```csharp
// Register
builder.Services.AddSingleton<NuciApiAuthorisation>(sp =>
    NuciApiAuthorisation.ApiKey(config["ApiKey"]));

// Client sends
Authorization: Bearer your-api-key
```

### Can I use multiple API keys?

Yes, implement custom `NuciApiAuthorisation`:

```csharp
public class MultiKeyAuthorisation : NuciApiAuthorisation
{
    private readonly HashSet<string> _validKeys;
    
    protected override Task PerformAuthorisation(HttpRequest request)
    {
        var header = request.Headers.Authorization.FirstOrDefault();
        var key = header?.Replace("Bearer ", "");
        if (key == null || !_validKeys.Contains(key))
            throw new UnauthorizedAccessException("Invalid API key");
    }
}
```

### How do I rotate keys without downtime?

Use `IOptionsMonitor` for hot reload:

```csharp
builder.Services.AddSingleton<NuciApiAuthorisation>(sp =>
{
    var monitor = sp.GetRequiredService<IOptionsMonitor<AuthOptions>>();
    return NuciApiAuthorisation.ApiKey(monitor.CurrentValue.ApiKey);
});
```

### What is HMAC authentication?

Hash-based Message Authentication Code. Client signs request with secret, server verifies.

```
Client: HMACSHA256(secret, payload + timestamp) → X-HMAC header
Server: Recomputes, compares (constant-time)
```

### How do I enable HMAC?

Library provides `GetHmacToken()` to extract header. Validation is consumer responsibility:

```csharp
var hmac = GetHmacToken();
var timestamp = Request.Headers["X-HMAC-Timestamp"];
var valid = _hmacValidator.Validate(payload, hmac, timestamp);
```

## Controllers

### What does ProcessRequest do?

Wraps action in standard pattern:
1. Run authorisation
2. Execute action
3. Return Ok/OkObjectResult
4. Handle exceptions → standard error responses

### Sync vs Async overloads?

| Method | Use For |
|--------|---------|
| `ProcessRequest` | CPU-bound, fast operations |
| `ProcessRequestAsync` | I/O-bound, database, HTTP calls |

**Prefer async** for scalability.

### Can I customise error responses?

Yes, catch exceptions before `ProcessRequest` or use middleware:

```csharp
try 
{
    return ProcessRequest(req, () => work(), auth);
}
catch (ValidationException ex)
{
    return BadRequest(new { error = "ValidationError", message = ex.Message });
}
```

### Does it support ProblemDetails (RFC 7807)?

Not yet. Planned for v3.0. Currently uses `NuciApiErrorResponse`.

## Integration

### How do I add Swagger/OpenAPI?

```csharp
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
```

### How do I add logging?

```csharp
builder.Logging.AddConsole();
builder.Services.AddLogging(logging =>
{
    logging.AddFilter("NuciAPI.Controllers", LogLevel.Trace);
});
```

### Can I use it with Entity Framework Core?

Yes, completely independent. Register DbContext normally:

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));
```

### Does it work with MediatR?

Yes. Use in controller:

```csharp
[HttpPost]
public async Task<ActionResult> Create(CreateRequest request)
{
    return await ProcessRequestAsync(request, async () =>
    {
        return await _mediator.Send(new CreateCommand(request));
    }, _auth);
}
```

## Testing

### How do I test controllers?

```csharp
// Unit test with mock
var mockAuth = new Mock<NuciApiAuthorisation>();
mockAuth.Setup(a => a.Authorise(It.IsAny<HttpRequest>()))
    .Returns(Task.CompletedTask);

var controller = new TestController();
controller.ControllerContext = new ControllerContext
{
    HttpContext = new DefaultHttpContext()
};

var result = controller.ProcessRequest(req, () => "ok", mockAuth.Object);
```

### How do I integration test?

```csharp
var factory = new WebApplicationFactory<Program>()
    .WithWebHostBuilder(b => b.ConfigureServices(s =>
        s.AddSingleton<NuciApiAuthorisation>(NuciApiAuthorisation.None)));

var client = factory.CreateClient();
var response = await client.GetAsync("/api/test");
```

## Troubleshooting

### Why do I get 401 "Authorization header is required"?

Request missing `Authorization: Bearer <key>` header.

### Why do I get 401 "Invalid API key"?

Key in request doesn't match server configuration. Check:
- Trailing spaces
- Correct environment config
- Key rotation timing

### Why does HMAC validation fail?

Common causes:
- Clock skew > 5 minutes (sync NTP)
- Payload serialization mismatch
- Wrong algorithm (must be HMACSHA256)
- Reused timestamp (replay protection)

### Why CS4014 "await missing" warning?

```csharp
// Wrong
public ActionResult Get() 
{
    ProcessRequestAsync(req, () => WorkAsync(), auth);
    return Ok();
}

// Correct
public async Task<ActionResult> Get()
{
    return await ProcessRequestAsync(req, () => WorkAsync(), auth);
}
```

## Migration

### Upgrading from 1.x to 2.x?

Breaking: Target framework changed. Update csproj:
```xml
<TargetFramework>net10.0</TargetFramework>
```
Update NuciAPI package to 3.5.1.

### Upgrading from 2.1 to 2.3?

No code changes. Update:
- `<TargetFramework>net10.0</TargetFramework>`
- `NuciAPI` to 3.5.1
- `global.json` SDK to 10.0.100

### Is v3.0 coming?

Planned. Breaking changes:
- ProblemDetails (RFC 7807)
- Async-only overloads
- CancellationToken support
- Default `NuciApiAuthorisation.None`

## Licensing

### What license?

GPL-3.0-or-later. See [LICENSE](../LICENSE).

### Can I use in commercial projects?

Yes, but GPL requires source distribution. Consult legal for your use case.

### Is there a commercial license?

Not currently. Contact maintainer for inquiries.

## Support

### Where do I report bugs?

https://github.com/hmlendea/nuciapi.controllers/issues

### Where do I ask questions?

https://github.com/hmlendea/nuciapi.controllers/discussions

### Security vulnerabilities?

Email: security@nuciapi.com (do not file public issue)

### Is there professional support?

Not officially. Community support via GitHub Discussions.