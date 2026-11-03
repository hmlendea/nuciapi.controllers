# Logging

## Logging Framework

**Library: No Built-in Logging**

NuciAPI.Controllers does not use any logging framework (Serilog, NLog, Microsoft.Extensions.Logging, etc.). It emits **zero log entries** during execution.

**Consumer Responsibility:** All logging must be implemented by the consumer application.

## Why No Logging?

| Reason | Explanation |
|--------|-------------|
| Library purity | No side effects, no dependencies on logging abstractions |
| Consumer control | Consumer chooses framework, sinks, format, levels |
| Performance | Zero allocation/overhead for logging in hot path |
| Security | No risk of accidental secret leakage in library logs |
| Simplicity | No configuration, no `ILogger` injection, no DI registration |

## Consumer Logging Integration

### Recommended: ASP.NET Core Built-in Logging

```csharp
// Program.cs
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Structured logging (Serilog example)
builder.Host.UseSerilog((context, config) =>
{
    config.ReadFrom.Configuration(context.Configuration)
          .Enrich.FromLogContext()
          .Enrich.WithProperty("Application", "MyApi")
          .WriteTo.Console(outputTemplate: 
              "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");
});
```

### Correlation IDs

```csharp
// Middleware for correlation ID
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() 
        ?? context.TraceIdentifier;
    
    using (LogContext.PushProperty("CorrelationId", correlationId))
    using (LogContext.PushProperty("RequestPath", context.Request.Path))
    using (LogContext.PushProperty("RequestMethod", context.Request.Method))
    {
        await next();
    }
});
```

## Log Levels & Semantics

| Level | When to Use | Library Relevance |
|-------|-------------|-------------------|
| `Trace` | Verbose diagnostic detail | N/A |
| `Debug` | Internal state for debugging | N/A |
| `Information` | Request/response, business events | Consumer: log each request |
| `Warning` | Recoverable issues, retries | Consumer: auth failures, validation |
| `Error` | Handled exceptions, failures | Consumer: 5xx, business errors |
| `Critical` | System instability, data loss | Consumer: startup failures |

## Structured Fields (Recommended)

Every log entry should include:

| Field | Source | Example |
|-------|--------|---------|
| `Timestamp` | Logger | `2026-11-03T10:30:45.123Z` |
| `Level` | Logger | `Information` |
| `Message` | Consumer | `HTTP POST /api/users completed` |
| `CorrelationId` | `HttpContext.TraceIdentifier` | `0HM8V7K2B3G4H` |
| `RequestId` | `X-Request-ID` header or generated | `req-abc123` |
| `UserId` | `User.FindFirst(ClaimTypes.NameIdentifier)` | `user-456` |
| `HttpMethod` | `Request.Method` | `POST` |
| `HttpPath` | `Request.Path` | `/api/users` |
| `HttpStatusCode` | `Response.StatusCode` | `201` |
| `DurationMs` | Stopwatch | `42` |
| `ClientIp` | `HttpContext.Connection.RemoteIpAddress` | `192.168.1.1` |

## Correlation Across Components

### Request-Level Correlation

```csharp
// In middleware or filter
var correlationId = context.TraceIdentifier;
LogContext.PushProperty("CorrelationId", correlationId);

// In controller
_logger.LogInformation("Processing {Action} for user {UserId}", 
    nameof(GetUser), userId);

// In service
_logger.LogDebug("Querying database for {Entity} {Id}", "User", id);

// In downstream HTTP call
request.Headers.Add("X-Correlation-ID", correlationId);
```

### Distributed Tracing (OpenTelemetry)

```csharp
// Program.cs
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource("NuciAPI.Controllers") // If library added tracing
        .AddOtlpExporter());
```

**Note:** Library has no `ActivitySource` — consumer must add if desired.

## Sinks / Destinations

| Sink | Use Case | Configuration |
|------|----------|---------------|
| Console | Development, containers | `AddConsole()` |
| File | Persistent logs, rotation | Serilog `WriteTo.File()` |
| Seq | Structured log search | Serilog `WriteTo.Seq()` |
| Elasticsearch | Centralized logging | Serilog `WriteTo.Elasticsearch()` |
| Application Insights | Azure monitoring | `AddApplicationInsights()` |
| Datadog | APM + logs | Datadog tracer + log shipper |
| Loki | Grafana stack | Promtail + Loki |

## Redaction / Masking

### Library: No Secrets to Redact

The library processes no secrets. Headers are read in-memory only.

### Consumer: Must Redact

```csharp
// Serilog destructuring policy
Log.Logger = new LoggerConfiguration()
    .Destructure.ByTransforming<HttpRequest>(req => new
    {
        Method = req.Method,
        Path = req.Path,
        Headers = req.Headers
            .Where(h => !SensitiveHeaders.Contains(h.Key))
            .ToDictionary(h => h.Key, h => h.Value.ToString()),
        // Body excluded entirely
    })
    .CreateLogger();

private static readonly HashSet<string> SensitiveHeaders = new(StringComparer.OrdinalIgnoreCase)
{
    "Authorization",
    "X-API-Key",
    "X-HMAC",
    "Cookie",
    "Set-Cookie",
    "X-CSRF-Token"
};
```

### Fields to Never Log

| Field | Reason |
|-------|--------|
| `Authorization` header | Contains credentials |
| `X-HMAC` header | Signature material |
| API keys | Secrets |
| Request bodies with PII | Privacy |
| Response bodies with tokens | Security |
| Connection strings | Infrastructure secrets |

## Retention & Rotation

| Environment | Retention | Rotation |
|-------------|-----------|----------|
| Development | 1-7 days | Size-based (10MB) |
| Staging | 30 days | Daily |
| Production | 90 days - 1 year | Daily, compressed |
| Audit/Security | 1-7 years | Immutable, WORM storage |

## Sampling

### When to Sample

| Scenario | Sampling Strategy |
|----------|-------------------|
| High-volume APIs | 10-100% of requests, 100% of errors |
| Debugging production | Temporary 100% |
| Cost control | Tail-based (keep errors, sample success) |

### Implementation (Serilog)

```csharp
// Sample 10% of Information, 100% of Warning+
.WriteTo.Console(restrictedToMinimumLevel: LogLevel.Information)
.WriteTo.Console(restrictedToMinimumLevel: LogLevel.Warning)

// Or custom sampler
.Enrich.With<SamplingEnricher>()
```

## Performance Considerations

### Async Logging

```csharp
// Serilog async sink
.WriteTo.Async(a => a.Console())
.WriteTo.Async(a => a.File("logs/app-.txt", rollingInterval: RollingInterval.Day))
```

### Avoid in Hot Path

| Anti-Pattern | Better Approach |
|--------------|-----------------|
| `Log.Information($"Processing {request.Id}")` | `Log.Information("Processing {RequestId}", request.Id)` (deferred formatting) |
| Logging full request/response bodies | Log only metadata, correlation IDs |
| Synchronous file writes | Async sinks, buffered writes |
| Blocking on log flush | Fire-and-forget, background workers |

### Library Impact

| Operation | Library Overhead |
|-----------|------------------|
| `ProcessRequest` entry | Zero (no logging) |
| Authorisation | Zero |
| HMAC extraction | Zero |
| Action execution | Zero |
| Response wrapping | Zero |

## Error Logging

### Exception Logging Pattern

```csharp
try
{
    await _service.DoWorkAsync(request);
}
catch (Exception ex)
{
    _logger.LogError(ex, 
        "Operation failed: {Operation} | User: {UserId} | CorrelationId: {CorrelationId}",
        nameof(DoWorkAsync), userId, correlationId);
    throw; // Re-throw for middleware
}
```

### What to Include

| Element | Include? |
|---------|----------|
| Exception type | ✅ Yes |
| Exception message | ✅ Yes |
| Stack trace | ✅ Yes (Error level) |
| Inner exceptions | ✅ Yes |
| Request context | ✅ Yes (correlation, user, path) |
| Sensitive data | ❌ No (redact) |

## Audit Logging

### Security-Relevant Events

| Event | Log Level | Fields |
|-------|-----------|--------|
| Authentication success | `Information` | UserId, AuthMethod, CorrelationId |
| Authentication failure | `Warning` | Reason, ClientIp, CorrelationId |
| Authorisation denial | `Warning` | UserId, Resource, Action, CorrelationId |
| API key rotation | `Information` | KeyId (not value), RotatedBy, CorrelationId |
| HMAC validation failure | `Warning` | ClientIp, CorrelationId |
| Privilege escalation attempt | `Error` | UserId, TargetRole, CorrelationId |

### Audit Sink Separation

```csharp
// Separate audit logger
var auditLog = new LoggerConfiguration()
    .WriteTo.Async(a => a.File("audit/audit-.json", 
        rollingInterval: RollingInterval.Day,
        formatter: new JsonFormatter()))
    .CreateLogger();

// Usage
auditLog.Information("AuthSuccess {UserId} {Method} {CorrelationId}", 
    userId, "ApiKey", correlationId);
```

## Configuration

### Library: No Configuration

No logging configuration exists in the library.

### Consumer: Standard ASP.NET Core

```json
// appsettings.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning",
      "MyApp": "Debug"
    },
    "Console": {
      "FormatterName": "json",
      "FormatterOptions": {
        "SingleLine": true,
        "IncludeScopes": true
      }
    }
  },
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      { "Name": "Async", "Args": { "configure": [{ "Name": "Console" }] } },
      { "Name": "Async", "Args": { "configure": [{ "Name": "File", "Args": { "path": "logs/app-.txt", "rollingInterval": "Day" } }] } }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithThreadId"]
  }
}
```

### Runtime Level Changes

```csharp
// Change level at runtime (Serilog)
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.ControlledBy(new LoggingLevelSwitch(LogEventLevel.Debug))
    .CreateLogger();

// Or via ILoggerFactory
loggerFactory.AddFilter("MyApp", LogLevel.Debug);
```

## Summary: Library vs Consumer

| Aspect | Library | Consumer |
|--------|---------|----------|
| Framework | None | Choice (Serilog, NLog, built-in) |
| Log output | None | All |
| Correlation | None | Must implement |
| Redaction | N/A | Must implement |
| Performance impact | Zero | Depends on config |
| Security audit | N/A | Must implement |
| Configuration | None | Full control |