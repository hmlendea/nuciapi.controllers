# Troubleshooting

## Common Issues

### 1. "Authorization header is required"

**Symptom:** 401 Unauthorized with message "Authorization header is required"

**Cause:** Request missing `Authorization` header

**Fix:**
```http
GET /api/resource HTTP/1.1
Authorization: Bearer your-api-key
```

**Debug:**
```csharp
// Check header in middleware
app.Use(async (context, next) =>
{
    var auth = context.Request.Headers.Authorization;
    _logger.LogDebug("Auth header: {Auth}", auth);
    await next();
});
```

### 2. "Invalid API key"

**Symptom:** 401 Unauthorized with message "Invalid API key"

**Cause:** Key mismatch between client and server

**Fix:**
- Verify key in configuration matches client
- Check for trailing spaces/newlines
- Ensure key not rotated without client update

**Debug:**
```csharp
// Log key comparison (masked)
_logger.LogInformation("Expected: {Expected}, Provided: {Provided}",
    MaskKey(expectedKey), MaskKey(providedKey));

string MaskKey(string key) => key.Length > 8
    ? key[..4] + "****" + key[^4..]
    : "****";
```

### 3. "Authorization header must use Bearer scheme"

**Symptom:** 401 with message about Bearer scheme

**Cause:** Header format incorrect

**Wrong:**
```http
Authorization: ApiKey your-key
Authorization: your-key
Authorization: Basic your-key
```

**Correct:**
```http
Authorization: Bearer your-key
```

### 4. HMAC Validation Fails

**Symptom:** 401/400 on HMAC-protected endpoints

**Causes & Fixes:**

| Cause | Fix |
|-------|-----|
| Clock skew > 5 min | Sync server/client clocks (NTP) |
| Wrong algorithm | Match `HMACSHA256` on both sides |
| Payload mismatch | Ensure identical serialization |
| Missing timestamp | Include `X-HMAC-Timestamp` header |
| Replay attack detected | Don't reuse timestamps |

**Debug:**
```csharp
// Log HMAC components
_logger.LogDebug("HMAC Validation: KeyId={KeyId}, Timestamp={Ts}, Payload={Payload}, Signature={Sig}",
    keyId, timestamp, payload, signature);
```

### 5. "ProcessRequest not found" / Compile Errors

**Symptom:** CS0103, CS1061, or method not found

**Causes:**
- Wrong NuciAPI.Controllers version
- Missing `using NuciAPI.Controllers;`
- Target framework mismatch

**Fix:**
```xml
<PackageReference Include="NuciAPI.Controllers" Version="2.3.1" />
<TargetFramework>net10.0</TargetFramework>
```

### 6. Async Method Not Awaited

**Symptom:** Warning CS4014, unexpected behavior

**Wrong:**
```csharp
public ActionResult Get()
{
    ProcessRequestAsync(req, () => DoWorkAsync(), auth); // Missing await!
    return Ok();
}
```

**Correct:**
```csharp
public async Task<ActionResult> Get()
{
    return await ProcessRequestAsync(req, () => DoWorkAsync(), auth);
}
```

### 7. Dependency Injection Fails

**Symptom:** `InvalidOperationException: Unable to resolve service`

**Causes:**
- `NuciApiAuthorisation` not registered
- Wrong lifetime (singleton vs scoped)

**Fix:**
```csharp
// Program.cs
builder.Services.AddSingleton<NuciApiAuthorisation>(sp =>
    NuciApiAuthorisation.ApiKey(config["ApiKey"]));

// Or scoped if key rotates
builder.Services.AddScoped<NuciApiAuthorisation>(sp =>
    NuciApiAuthorisation.ApiKey(sp.GetRequiredService<IKeyProvider>().GetKey()));
```

## Debugging Techniques

### Enable Detailed Logging

```json
// appsettings.Development.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Debug",
      "NuciAPI.Controllers": "Trace"
    }
  }
}
```

### Request Inspection Middleware

```csharp
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("Request: {Method} {Path}",
        context.Request.Method, context.Request.Path);

    logger.LogDebug("Headers: {Headers}",
        context.Request.Headers
            .Where(h => h.Key.StartsWith("Authorization") || h.Key.StartsWith("X-"))
            .ToDictionary(h => h.Key, h => h.Value.ToString()));

    if (context.Request.ContentLength > 0 && context.Request.ContentLength < 10000)
    {
        context.Request.EnableBuffering();
        var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
        context.Request.Body.Position = 0;
        logger.LogDebug("Body: {Body}", body);
    }

    await next();

    logger.LogInformation("Response: {StatusCode}", context.Response.StatusCode);
});
```

### Correlation ID Tracking

```csharp
// Middleware
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
        ?? context.TraceIdentifier;

    context.Items["CorrelationId"] = correlationId;
    context.Response.Headers["X-Correlation-ID"] = correlationId;

    using (LogContext.PushProperty("CorrelationId", correlationId))
    {
        await next();
    }
});

// In controller
_logger.LogInformation("Processing request"); // Auto-includes CorrelationId
```

## Error Code Reference

| HTTP | Error Code | Message | Resolution |
|------|------------|---------|------------|
| 400 | InvalidRequest | The request was invalid | Check request body/validation |
| 401 | Unauthorized | Authorization header is required | Add Authorization header |
| 401 | Unauthorized | Invalid API key | Verify key matches server |
| 401 | Unauthorized | Authorization header must use Bearer scheme | Use `Bearer ` prefix |
| 404 | NotFound | The requested resource was not found | Check endpoint URL |
| 500 | InternalServerError | An error occurred | Check server logs |

## Performance Issues

### High Latency

**Check:**
1. Database query performance (EF Core logging)
2. External API calls (timeouts, retries)
3. Synchronous I/O in async methods
4. Thread pool starvation

**Fix:**
```csharp
// Use async all the way
public async Task<ActionResult> Get()
{
    return await ProcessRequestAsync(req, async () =>
    {
        var data = await _repo.GetAsync(id); // Async!
        return data;
    }, auth);
}
```

### Memory Pressure

**Check:**
1. Large object allocations in hot path
2. Unclosed streams/connections
3. Cache without eviction

**Fix:**
```csharp
// Use pooling
var buffer = ArrayPool<byte>.Shared.Rent(size);
try { ... } finally { ArrayPool<byte>.Shared.Return(buffer); }

// Configure cache limits
services.AddMemoryCache(options =>
{
    options.SizeLimit = 100_000_000; // 100MB
});
```

## Health Checks

### Liveness Probe

```csharp
// Program.cs
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy());

app.MapHealthChecks("/health/live");
```

### Readiness Probe

```csharp
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "db")
    .AddRedis(redisConnectionString, name: "redis")
    .AddUrlGroup(new Uri("https://api.external.com/health"), name: "external-api");

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
```

## Diagnostic Commands

### .NET Diagnostics

```bash
# List processes
dotnet-trace ps

# Collect trace
dotnet-trace collect --process-id <PID> --providers Microsoft.AspNetCore

# Dump analysis
dotnet-dump collect --process-id <PID>
dotnet-dump analyze <dump-file>

# Counters
dotnet-counters monitor --process-id <PID> System.Runtime
```

### Log Analysis

```bash
# Filter by correlation ID
grep "CorrelationId.*abc-123" logs/app.log

# Error summary
grep "ERROR" logs/app.log | cut -d' ' -f5- | sort | uniq -c | sort -rn

# Slow requests
grep "Elapsed" logs/app.log | awk '$NF > 1000' | head -20
```

## Escalation

### When to Open Issue

- Reproducible bug in library code
- Documentation unclear/incorrect
- Feature request with use case

### Information to Include

1. NuciAPI.Controllers version
2. .NET version (`dotnet --version`)
3. Minimal reproduction code
4. Expected vs actual behavior
5. Full exception stack trace
6. Relevant logs (masked)

### Security Issues

**Email:** security@nuciapi.com
**Do not** file public issues for vulnerabilities.

## Quick Reference Card

```
┌─────────────────────────────────────────────────────────────┐
│                    TROUBLESHOOTING QUICK REF                │
├─────────────────────────────────────────────────────────────┤
│ 401 "header required"    → Add Authorization: Bearer <key> │
│ 401 "invalid key"        → Verify key matches config       │
│ 401 "Bearer scheme"      → Use "Bearer " prefix exactly    │
│ 400 "invalid request"    → Check model validation          │
│ 500                      → Check server logs               │
│ HMAC fail                → Check clock, algorithm, payload │
│ DI fail                  → Register NuciApiAuthorisation   │
│ CS4014 warning           → Add await to async call         │
│ High latency             → Profile DB, external calls      │
│ Memory leak              → Check cache limits, pooling     │
└─────────────────────────────────────────────────────────────┘
```