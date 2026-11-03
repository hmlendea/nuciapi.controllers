# Integrations

## Overview

NuciAPI.Controllers integrates with the broader .NET ecosystem. This document covers common integration patterns.

## ASP.NET Core Integrations

### Dependency Injection

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Framework services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// NuciAPI.Controllers
builder.Services.AddSingleton<NuciApiAuthorisation>(sp =>
    NuciApiAuthorisation.ApiKey(builder.Configuration["ApiKey"]));

// Your services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

var app = builder.Build();
```

### Middleware Pipeline

```csharp
var app = builder.Build();

// Order matters!
app.UseHttpsRedirection();           // 1. HTTPS
app.UseRouting();                    // 2. Routing
app.UseAuthentication();             // 3. AuthN (if using)
app.UseAuthorization();              // 4. AuthZ (if using)

// Custom middleware (correlation, logging)
app.UseMiddleware<CorrelationMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

app.MapControllers();                // 5. Endpoints
app.Run();
```

### Health Checks

```csharp
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Default"))
    .AddRedis(builder.Configuration.GetConnectionString("Redis"))
    .AddUrlGroup(new Uri("https://api.dependency.com/health"), "external-api");

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false // Liveness: only self
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
```

## Authentication Integrations

### JWT Bearer (ASP.NET Core Identity)

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://identity.example.com";
        options.Audience = "nuciapi";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true
        };
    });

// Combine with NuciAPI.Controllers
// NuciApiAuthorisation handles API keys
// JWT middleware handles token validation
// Both can coexist for different endpoints
```

### API Key + JWT Hybrid

```csharp
// Program.cs - Register both
builder.Services.AddAuthentication()
    .AddJwtBearer("jwt", options => { ... })
    .AddScheme<ApiKeyAuthOptions, ApiKeyAuthHandler>("apikey", _ => { });

// Controller chooses per-endpoint
[HttpGet("public")]
[Authorize(AuthenticationSchemes = "apikey")]
public ActionResult PublicEndpoint() { ... }

[HttpGet("private")]
[Authorize(AuthenticationSchemes = "jwt")]
public ActionResult PrivateEndpoint() { ... }
```

## ORM Integrations

### Entity Framework Core

```csharp
// DbContext
public class AppDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<ApiKey> ApiKeys { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApiKey>()
            .HasIndex(k => k.KeyHash)
            .IsUnique();
    }
}

// Registration
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString)
           .EnableSensitiveDataLogging(builder.Environment.IsDevelopment())
           .LogTo(Console.WriteLine, LogLevel.Information));

// Usage in service
public class UserService
{
    private readonly AppDbContext _db;
    
    public async Task<User> CreateAsync(CreateUserRequest request)
    {
        var user = new User { ... };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }
}
```

### Dapper

```csharp
// Registration
builder.Services.AddScoped<IDbConnection>(_ => 
    new NpgsqlConnection(connectionString));

// Repository
public class DapperUserRepository : IUserRepository
{
    private readonly IDbConnection _db;
    
    public async Task<User?> GetByIdAsync(Guid id)
    {
        return await _db.QueryFirstOrDefaultAsync<User>(
            "SELECT * FROM users WHERE id = @Id", new { Id = id });
    }
}
```

## Caching Integrations

### Redis (Distributed)

```csharp
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "NuciApi:";
});

// Usage
public class CachedUserService
{
    private readonly IDistributedCache _cache;
    private readonly IUserRepository _repo;
    
    public async Task<UserDto> GetAsync(Guid id)
    {
        var key = $"user:{id}";
        var cached = await _cache.GetStringAsync(key);
        if (cached != null) return JsonSerializer.Deserialize<UserDto>(cached);
        
        var user = await _repo.GetByIdAsync(id);
        if (user != null)
        {
            await _cache.SetStringAsync(key, JsonSerializer.Serialize(user),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
                });
        }
        return user;
    }
}
```

### In-Memory (Single Instance)

```csharp
builder.Services.AddMemoryCache();

// Usage
public class MemoryCachedService
{
    private readonly IMemoryCache _cache;
    
    public T GetOrCreate<T>(string key, Func<T> factory, TimeSpan ttl)
    {
        return _cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = ttl;
            return factory();
        });
    }
}
```

## Logging Integrations

### Serilog

```csharp
// Program.cs
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "NuciAPI.Controllers")
    .WriteTo.Console(outputTemplate: 
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.Seq("http://seq:5341")
    .CreateLogger();

builder.Host.UseSerilog();

// appsettings.json
{
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      { "Name": "Console" },
      { "Name": "Seq", "Args": { "serverUrl": "http://seq:5341" } }
    ]
  }
}
```

### Structured Logging in Controllers

```csharp
public class UsersController : NuciApiController
{
    private readonly ILogger<UsersController> _logger;
    
    public UsersController(ILogger<UsersController> logger, ...)
    {
        _logger = logger;
    }
    
    [HttpGet("{id}")]
    public ActionResult<UserDto> Get(Guid id)
    {
        return ProcessRequest(new GetUserRequest { Id = id }, () =>
        {
            _logger.LogInformation("Fetching user {UserId}", id);
            var user = _users.GetById(id);
            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found", id);
                return null;
            }
            _logger.LogDebug("User {UserId} retrieved", id);
            return new UserDto(user);
        }, _auth);
    }
}
```

## Monitoring Integrations

### OpenTelemetry

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddSource("NuciAPI.Controllers")
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri("http://jaeger:4317");
        }))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddOtlpExporter());

// Custom activity source
public static class Diagnostics
{
    public static readonly ActivitySource Source = new("NuciAPI.Controllers");
}

// In controller
using var activity = Diagnostics.Source.StartActivity("ProcessRequest");
activity?.SetTag("user.id", request.UserId);
```

### Prometheus / Grafana

```csharp
// Metrics endpoint
builder.Services.AddMetrics()
    .AddPrometheusExporter();

app.MapPrometheusScrapingEndpoint();

// Custom metrics
public class MetricsService
{
    private readonly Counter<int> _requestCounter;
    private readonly Histogram<double> _requestDuration;
    
    public MetricsService(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("NuciAPI.Controllers");
        _requestCounter = meter.CreateCounter<int>("requests_total");
        _requestDuration = meter.CreateHistogram<double>("request_duration_seconds");
    }
    
    public void RecordRequest(string endpoint, double duration, bool success)
    {
        _requestCounter.Add(1, new("endpoint", endpoint), new("success", success));
        _requestDuration.Record(duration, new("endpoint", endpoint));
    }
}
```

## Message Queue Integrations

### RabbitMQ

```csharp
builder.Services.AddSingleton<IConnectionFactory>(_ => 
    new ConnectionFactory { Uri = new Uri(rabbitMqConnectionString) });

builder.Services.AddScoped<IMessagePublisher, RabbitMqPublisher>();

// Usage
public class OrderService
{
    private readonly IMessagePublisher _publisher;
    
    public async Task CreateOrderAsync(CreateOrderRequest request)
    {
        var order = await _repo.CreateAsync(request);
        await _publisher.PublishAsync("orders.created", new OrderCreatedEvent(order));
        return order;
    }
}
```

### Azure Service Bus

```csharp
builder.Services.AddSingleton<ServiceBusClient>(_ => 
    new ServiceBusClient(serviceBusConnectionString));

builder.Services.AddScoped<IMessagePublisher, ServiceBusPublisher>();
```

## External API Integrations

### HttpClient with Polly

```csharp
builder.Services.AddHttpClient<IExternalApi, ExternalApiClient>(client =>
{
    client.BaseAddress = new Uri("https://api.external.com/");
    client.DefaultRequestHeaders.Add("User-Agent", "NuciAPI.Controllers");
})
.AddTransientHttpErrorPolicy(p => p.WaitAndRetryAsync(3, _ => TimeSpan.FromSeconds(1)))
.AddTransientHttpErrorPolicy(p => p.CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));

// Usage
public class ExternalApiClient : IExternalApi
{
    private readonly HttpClient _http;
    
    public async Task<ExternalData> GetDataAsync(string id)
    {
        var response = await _http.GetAsync($"data/{id}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ExternalData>();
    }
}
```

## Testing Integrations

### Testcontainers

```csharp
// PostgreSQL
var postgres = new PostgreSqlBuilder()
    .WithImage("postgres:16")
    .WithDatabase("test")
    .WithUsername("test")
    .WithPassword("test")
    .Build();

// Redis
var redis = new RedisBuilder()
    .WithImage("redis:7-alpine")
    .Build();

// RabbitMQ
var rabbit = new RabbitMqBuilder()
    .WithImage("rabbitmq:3-management")
    .Build();
```

### WireMock (HTTP Mocking)

```csharp
var server = WireMockServer.Start();
server.Given(Request.Create().WithPath("/api/data").UsingGet())
    .RespondWith(Response.Create()
        .WithStatusCode(200)
        .WithBodyAsJson(new { value = "mocked" }));

// Inject into HttpClient
var httpClient = new HttpClient { BaseAddress = server.Urls[0] };
```

## Deployment Integrations

### Docker

```dockerfile
# Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["NuciAPI.Controllers/NuciAPI.Controllers.csproj", "NuciAPI.Controllers/"]
RUN dotnet restore "NuciAPI.Controllers/NuciAPI.Controllers.csproj"
COPY . .
WORKDIR "/src/NuciAPI.Controllers"
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NuciAPI.Controllers.dll"]
```

### Kubernetes

```yaml
# deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: nuciapi
spec:
  replicas: 3
  selector:
    matchLabels:
      app: nuciapi
  template:
    metadata:
      labels:
        app: nuciapi
    spec:
      containers:
      - name: nuciapi
        image: nuciapi:2.3.1
        ports:
        - containerPort: 8080
        envFrom:
        - configMapRef:
            name: nuciapi-config
        - secretRef:
            name: nuciapi-secrets
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 8080
          initialDelaySeconds: 5
        livenessProbe:
          httpGet:
            path: /health/live
            port: 8080
          initialDelaySeconds: 10
---
apiVersion: v1
kind: Service
metadata:
  name: nuciapi
spec:
  selector:
    app: nuciapi
  ports:
  - port: 80
    targetPort: 8080
```

### GitHub Actions (CI/CD)

```yaml
# .github/workflows/dotnet.yml
name: .NET

on:
  push:
    branches: [dev, main]
  pull_request:
    branches: [dev, main]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v4
    - uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '10.0.x'
    - run: dotnet restore
    - run: dotnet build --configuration Release --no-restore
    - run: dotnet test --configuration Release --no-build --verbosity normal
    
  publish:
    needs: build
    if: github.event_name == 'push' && github.ref == 'refs/heads/main'
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v4
    - uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '10.0.x'
    - run: dotnet pack NuciAPI.Controllers/NuciAPI.Controllers.csproj -c Release -o artifacts
    - uses: actions/upload-artifact@v4
      with:
        name: nuget-package
        path: artifacts/*.nupkg
    - run: dotnet nuget push artifacts/*.nupkg --api-key ${{ secrets.NUGET_KEY }} --source https://api.nuget.org/v3/index.json
```

## Integration Checklist

- [ ] DI container configured correctly
- [ ] Middleware order correct
- [ ] Health checks implemented
- [ ] Logging structured and correlated
- [ ] Metrics exposed
- [ ] Tracing configured
- [ ] Caching strategy defined
- [ ] Database migrations automated
- [ ] Secrets managed securely
- [ ] CI/CD pipeline validated
- [ ] Container image builds
- [ ] Kubernetes manifests tested