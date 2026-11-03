# Troubleshooting Guide

## Quick Diagnosis

| Symptom | Likely Cause | Quick Fix |
|---------|--------------|-----------|
| 401 "header required" | Missing Authorization header | Add `Authorization: Bearer <key>` |
| 401 "invalid key" | Key mismatch | Verify config matches client |
| 401 "Bearer scheme" | Wrong header format | Use exactly `Bearer <key>` |
| 400 "invalid request" | Model validation failed | Check required fields, formats |
| 500 | Server exception | Check logs, enable detailed errors |
| CS4014 warning | Missing await | Add `await` to async call |
| DI exception | Service not registered | Register in Program.cs |

## Step-by-Step Debugging

### 1. Verify Authorisation Registration

```csharp
// Program.cs - Check this exists
builder.Services.AddSingleton<NuciApiAuthorisation>(sp =>
    NuciApiAuthorisation.ApiKey(config["ApiKey"]));
```

**Test:** Add temporary endpoint
```csharp
app.MapGet("/debug/auth", (NuciApiAuthorisation auth) => 
    Results.Ok(new { Type = auth.GetType().Name }));
```

### 2. Inspect Request Headers

```csharp
// Temporary middleware
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Auth Header: {Auth}", 
        context.Request.Headers.Authorization);
    await next();
});
```

### 3. Check Key Value

```csharp
// In controller or service
var expected = _config["ApiKey"];
var provided = Request.Headers.Authorization.ToString().Replace("Bearer ", "");
_logger.LogInformation("Match: {Match}", expected == provided);
```

### 4. Enable Detailed Errors (Development Only)

```csharp
// Program.cs
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}
```

## Common Scenarios

### Scenario: Works Locally, Fails in Production

**Checklist:**
- [ ] `appsettings.Production.json` has correct `ApiKey`
- [ ] Key Vault / env var populated
- [ ] HTTPS enforced (production requirement)
- [ ] Load balancer not stripping headers
- [ ] Clock sync for HMAC (NTP)

### Scenario: Intermittent 401s

**Causes:**
- Key rotation without client update
- Multiple instances with different configs
- Cache stale (if using distributed cache for keys)

**Fix:**
```csharp
// Use IOptionsMonitor for hot reload
builder.Services.AddSingleton<NuciApiAuthorisation>(sp =>
{
    var monitor = sp.GetRequiredService<IOptionsMonitor<AuthOptions>>();
    return NuciApiAuthorisation.ApiKey(monitor.CurrentValue.ApiKey);
});
```

### Scenario: HMAC Always Fails

**Debug Checklist:**
```csharp
// Log all components
_logger.LogDebug("HMAC Debug: KeyId={KeyId} Ts={Ts} Algo={Algo} PayloadLen={Len} Sig={Sig}",
    keyId, timestamp, algorithm, payload.Length, signature);
```

**Verify:**
- [ ] Same algorithm (HMACSHA256)
- [ ] Timestamp within 5 minutes
- [ ] Payload identical (serialization, encoding)
- [ ] Secret correct (not rotated)
- [ ] No proxy modifying body

### Scenario: Async Methods Hang

**Cause:** Deadlock from `.Result` or `.Wait()`

**Wrong:**
```csharp
public ActionResult Get()
{
    var result = ProcessRequestAsync(req, () => DoWorkAsync(), auth).Result;
    return result;
}
```

**Correct:**
```csharp
public async Task<ActionResult> Get()
{
    return await ProcessRequestAsync(req, () => DoWorkAsync(), auth);
}
```

### Scenario: Swagger Shows No Endpoints

**Fixes:**
```csharp
// Ensure controllers discovered
builder.Services.AddControllers();

// Add API explorer
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });
    
    // Include XML comments
    var xml = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var path = Path.Combine(AppContext.BaseDirectory, xml);
    if (File.Exists(path)) c.IncludeXmlComments(path);
});
```

## Logging for Diagnosis

### Structured Logging Setup

```csharp
// Program.cs
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddLogging(logging =>
{
    logging.AddFilter("NuciAPI.Controllers", LogLevel.Trace);
    logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
});
```

### Correlation ID

```csharp
// Middleware (early in pipeline)
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() 
        ?? context.TraceIdentifier;
    
    context.Items["CorrelationId"] = correlationId;
    context.Response.Headers["X-Correlation-ID"] = correlationId;
    
    using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId))
    {
        await next();
    }
});
```

### Log Query Examples

```bash
# All errors for correlation ID
grep "CorrelationId.*abc-123" logs/app.log | grep ERROR

# Failed authorisations
grep "Invalid API key" logs/app.log | tail -20

# Slow requests (>1s)
grep "Elapsed.*[1-9][0-9][0-9][0-9]" logs/app.log

# HMAC failures
grep "HMAC" logs/app.log | grep -i fail
```

## Environment-Specific Issues

### Development

| Issue | Fix |
|-------|-----|
| HTTPS cert warning | `dotnet dev-certs https --trust` |
| User secrets not working | `dotnet user-secrets init` |
| Port in use | Change `applicationUrl` in launchSettings.json |

### Docker

| Issue | Fix |
|-------|-----|
| Config not loaded | Check volume mount for appsettings |
| Key Vault access | Managed identity / workload identity |
| Health check fails | Expose correct port, check path |

### Kubernetes

| Issue | Fix |
|-------|-----|
| Pods crash on startup | Check readiness/liveness probes |
| ConfigMap not updated | Rollout restart deployment |
| Service mesh strips headers | Configure Istio/Linkerd passthrough |

## Performance Diagnosis

### High CPU

```bash
# Profile
dotnet-trace collect -p <PID> --providers Microsoft-DotNETCore-SampleProfiler
```

### High Memory

```bash
# Dump
dotnet-dump collect -p <PID>
dotnet-dump analyze <dump> -c "dumpheap -stat"
```

### Thread Pool Starvation

```csharp
// Check in code
ThreadPool.GetAvailableThreads(out var worker, out var io);
_logger.LogWarning("ThreadPool: Worker={Worker}, IO={IO}", worker, io);
```

## When to Escalate

### Open GitHub Issue When:

- [ ] Reproducible with minimal code
- [ ] Not a configuration issue
- [ ] Library bug suspected
- [ ] Documentation gap found

### Include:

1. NuciAPI.Controllers version
2. .NET version (`dotnet --info`)
3. Minimal reproduction (GitHub repo or gist)
4. Expected vs actual behavior
5. Full stack trace
6. Relevant logs (sanitized)

### Security Issues

**Email:** security@nuciapi.com
**Do not** create public issues.

## Quick Commands Reference

```bash
# Test endpoint
curl -v -H "Authorization: Bearer key" https://api.example.com/health

# Check headers received
curl -v -H "Authorization: Bearer key" https://api.example.com/debug/headers

# Test HMAC
curl -v -H "X-HMAC: sig" -H "X-HMAC-Timestamp: 1234567890" \
  -H "Content-Type: application/json" \
  -d '{"test":true}' \
  https://api.example.com/hmac-endpoint

# View logs (Docker)
docker logs <container> --tail 100 -f

# View logs (K8s)
kubectl logs -l app=myapi --tail 100 -f

# Check config (K8s)
kubectl get configmap myapi-config -o yaml
```