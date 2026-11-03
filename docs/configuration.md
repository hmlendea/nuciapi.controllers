# Configuration

## Library Configuration

### No Configuration System

NuciAPI.Controllers has **no configuration system**:

| Feature | Present? |
|---------|----------|
| `IOptions` / `IOptionsSnapshot` | ❌ |
| `appsettings.json` binding | ❌ |
| Environment variables | ❌ |
| Command-line args | ❌ |
| Configuration providers | ❌ |
| `IConfiguration` injection | ❌ |

### Why No Configuration?

| Reason | Explanation |
|--------|-------------|
| Simplicity | Zero-config library, works out of the box |
| No secrets | Library doesn't store credentials |
| No behavior toggles | All behavior explicit in code |
| Consumer control | Consumer decides how to configure authorisation |

## Consumer Configuration

### Authorisation Configuration (Consumer Responsibility)

```csharp
// appsettings.json
{
  "NuciApi": {
    "ApiKey": "prod-key-from-keyvault",
    "KeyHeaderName": "X-API-Key",  // Optional custom header
    "RequireHttps": true
  }
}

// Program.cs
builder.Services.Configure<NuciApiOptions>(builder.Configuration.GetSection("NuciApi"));

builder.Services.AddSingleton<NuciApiAuthorisation>(sp =>
{
    var options = sp.GetRequiredService<IOptions<NuciApiOptions>>().Value;
    return NuciApiAuthorisation.ApiKey(options.ApiKey);
});

public class NuciApiOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string KeyHeaderName { get; set; } = "Authorization";
    public bool RequireHttps { get; set; } = true;
}
```

### HMAC Configuration (Consumer)

```csharp
// appsettings.json
{
  "Hmac": {
    "HeaderName": "X-HMAC",
    "Algorithm": "HMACSHA256",
    "TimestampToleranceSeconds": 300,
    "ReplayCacheSize": 10000
  }
}

// Consumer HMAC validation service
public class HmacValidationService
{
    private readonly HmacOptions _options;
    private readonly ConcurrentDictionary<string, DateTime> _replayCache = new();

    public HmacValidationService(IOptions<HmacOptions> options)
    {
        _options = options.Value;
    }

    public bool Validate(string payload, string signature, string timestamp)
    {
        // Check timestamp tolerance
        if (!long.TryParse(timestamp, out var ts) ||
            Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - ts) > _options.TimestampToleranceSeconds)
            return false;

        // Check replay
        var cacheKey = $"{timestamp}:{signature}";
        if (!_replayCache.TryAdd(cacheKey, DateTime.UtcNow))
            return false;

        // Cleanup old entries
        var cutoff = DateTime.UtcNow.AddSeconds(-_options.TimestampToleranceSeconds * 2);
        foreach (var kvp in _replayCache)
            if (kvp.Value < cutoff) _replayCache.TryRemove(kvp.Key, out _);

        // Verify signature
        var expected = ComputeHmac(payload, timestamp);
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromBase64String(signature),
            expected);
    }
}
```

## ASP.NET Core Configuration Integration

### Standard Patterns

```csharp
// Program.cs - Minimal API style
var builder = WebApplication.CreateBuilder(args);

// 1. Configuration sources (automatic)
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

// 2. Options pattern
builder.Services.Configure<ApiOptions>(builder.Configuration.GetSection("Api"));
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection("Auth"));

// 3. Validation
builder.Services.AddOptions<AuthOptions>()
    .BindConfiguration("Auth")
    .ValidateDataAnnotations()
    .ValidateOnStart();

// 4. DI registration
builder.Services.AddSingleton<NuciApiAuthorisation>(sp =>
{
    var authOptions = sp.GetRequiredService<IOptions<AuthOptions>>().Value;
    return NuciApiAuthorisation.ApiKey(authOptions.ApiKey);
});

var app = builder.Build();
```

### Environment-Specific Configuration

```
appsettings.json              # Base (committed)
appsettings.Development.json  # Local dev (gitignored)
appsettings.Staging.json      # Staging (gitignored or separate repo)
appsettings.Production.json   # Prod (gitignored, from Key Vault)
```

```json
// appsettings.Development.json
{
  "NuciApi": {
    "ApiKey": "dev-local-key",
    "RequireHttps": false
  },
  "Logging": {
    "LogLevel": { "Default": "Debug" }
  }
}
```

## Secrets Management

### Development

```bash
# User secrets (local only)
dotnet user-secrets init
dotnet user-secrets set "NuciApi:ApiKey" "dev-secret-key"
```

```csharp
// Program.cs - Development only
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}
```

### Production

| Platform | Mechanism |
|----------|-----------|
| Azure | Key Vault + `AddAzureKeyVault` |
| AWS | Secrets Manager / Parameter Store |
| Kubernetes | Sealed Secrets / External Secrets Operator |
| Docker | Docker Secrets / env vars |
| Generic | Environment variables |

```csharp
// Azure Key Vault
builder.Configuration.AddAzureKeyVault(
    new Uri($"https://{keyVaultName}.vault.azure.net/"),
    new DefaultAzureCredential());

// AWS Secrets Manager
builder.Configuration.AddSecretsManager(
    configurator: options => options.SecretFilter = "prod/nuciapi/*");
```

## Configuration Validation

### Data Annotations

```csharp
public class AuthOptions
{
    [Required]
    [MinLength(32)]
    public string ApiKey { get; set; } = string.Empty;

    [Range(1, 3600)]
    public int TokenExpiryMinutes { get; set; } = 60;

    [Url]
    public string? KeyRotationEndpoint { get; set; }
}
```

### Custom Validation

```csharp
builder.Services.AddOptions<AuthOptions>()
    .BindConfiguration("Auth")
    .Validate(options =>
    {
        // Cross-field validation
        if (options.RequireHttps && string.IsNullOrEmpty(options.ApiKey))
            return false;
        return true;
    }, "API key required when HTTPS enforced")
    .ValidateOnStart();
```

## Configuration at Runtime

### Reload on Change

```csharp
// IOptionsSnapshot - reloads per request
public class MyController : NuciApiController
{
    private readonly IOptionsSnapshot<AuthOptions> _authOptions;

    public MyController(IOptionsSnapshot<AuthOptions> authOptions)
    {
        _authOptions = authOptions;
    }

    [HttpPost]
    public ActionResult Action(Request request)
    {
        // Gets latest config on each request
        var auth = NuciApiAuthorisation.ApiKey(_authOptions.Value.ApiKey);
        return ProcessRequest(request, () => { ... }, auth);
    }
}
```

### IOptionsMonitor for Notifications

```csharp
public class AuthKeyRotator : IHostedService
{
    private readonly IOptionsMonitor<AuthOptions> _options;
    private readonly IApiKeyProvider _keyProvider;
    private IDisposable? _subscription;

    public AuthKeyRotator(IOptionsMonitor<AuthOptions> options, IApiKeyProvider keyProvider)
    {
        _options = options;
        _keyProvider = keyProvider;
    }

    public Task StartAsync(CancellationToken ct)
    {
        _subscription = _options.OnChange(async (newOptions, name) =>
        {
            await _keyProvider.RotateKeyAsync(newOptions.ApiKey);
        });
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        _subscription?.Dispose();
        return Task.CompletedTask;
    }
}
```

## Configuration Schema (Documentation)

### For Consumer Documentation

```markdown
## Configuration Reference

| Key | Type | Required | Default | Description |
|-----|------|----------|---------|-------------|
| `NuciApi:ApiKey` | string | Yes | — | API key for authorisation |
| `NuciApi:KeyHeaderName` | string | No | `Authorization` | Header name for API key |
| `NuciApi:RequireHttps` | bool | No | `true` | Enforce HTTPS for requests |
| `Hmac:HeaderName` | string | No | `X-HMAC` | HMAC signature header |
| `Hmac:Algorithm` | string | No | `HMACSHA256` | HMAC algorithm |
| `Hmac:TimestampToleranceSeconds` | int | No | `300` | Max timestamp drift |
| `Hmac:ReplayCacheSize` | int | No | `10000` | Replay cache entries |
```

## Configuration Anti-Patterns

| Anti-Pattern | Problem | Solution |
|--------------|---------|----------|
| Hardcoded keys in code | Secrets in repo | Use user secrets / Key Vault |
| `Configuration["key"]` everywhere | No validation, no DI | Use `IOptions<T>` |
| No `ValidateOnStart` | Fail at runtime | Add validation |
| Single `appsettings.json` for all envs | Prod secrets in repo | Environment-specific files |
| Storing HMAC keys in config | Key rotation hard | Use Key Vault with rotation |

## Library: No Configuration = No Config Issues

| Issue | Library Status |
|-------|----------------|
| Config loading failure | Impossible |
| Missing config at startup | Impossible |
| Config validation failure | Impossible |
| Hot reload complexity | None |
| Config schema drift | None |
| Secret leakage via config | None |