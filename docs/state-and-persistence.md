# State and Persistence

## Library State

### No Internal State

NuciAPI.Controllers maintains **zero internal state**:

| State Type | Present? |
|------------|----------|
| Static fields | No |
| Instance fields (beyond base) | No |
| Caches | No |
| Connection pools | No |
| Session data | No |
| In-memory stores | No |

### Why Stateless?

| Reason | Explanation |
|--------|-------------|
| Thread safety | No shared mutable state = no races |
| Scalability | Any instance handles any request |
| Testability | No setup/teardown needed |
| Reliability | No state corruption possible |
| Simplicity | No lifecycle management |

## Request-Scoped State

### HttpContext (ASP.NET Core)

```csharp
// Available in controller via base.ControllerContext.HttpContext
public class MyController : NuciApiController
{
    public ActionResult Get()
    {
        var request = HttpContext.Request;
        var response = HttpContext.Response;
        var user = HttpContext.User;
        var items = HttpContext.Items; // Per-request dictionary
        var session = HttpContext.Session; // If configured
        var features = HttpContext.Features;

        // Correlation ID from middleware
        var correlationId = HttpContext.TraceIdentifier;

        return ProcessRequest(new Request(), () => { ... }, auth);
    }
}
```

### Items Dictionary (Per-Request)

```csharp
// Middleware sets
app.Use(async (context, next) =>
{
    context.Items["CorrelationId"] = context.TraceIdentifier;
    context.Items["RequestStart"] = DateTime.UtcNow;
    await next();
});

// Controller reads
var correlationId = HttpContext.Items["CorrelationId"] as string;
var start = (DateTime?)HttpContext.Items["RequestStart"];
```

## Consumer State Management

### Database (EF Core)

```csharp
// DbContext - scoped per request
public class AppDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<ApiKey> ApiKeys { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseNpgsql(connectionString);
    }
}

// Registration
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// Usage in service
public class UserService
{
    private readonly AppDbContext _db;

    public UserService(AppDbContext db) => _db = db;

    public async Task<User> CreateAsync(CreateUserRequest request)
    {
        var user = new User { Email = request.Email, ... };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }
}
```

### Redis (Distributed Cache)

```csharp
// Registration
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "NuciApi:";
});

// Usage
public class RateLimitService
{
    private readonly IDistributedCache _cache;

    public RateLimitService(IDistributedCache cache) => _cache = cache;

    public async Task<bool> CheckLimitAsync(string key, int limit, TimeSpan window)
    {
        var current = await _cache.GetStringAsync(key);
        var count = int.TryParse(current, out var c) ? c : 0;

        if (count >= limit) return false;

        await _cache.SetStringAsync(key, (count + 1).ToString(), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = window
        });
        return true;
    }
}
```

### In-Memory (Single Instance)

```csharp
// Registration
builder.Services.AddMemoryCache();

// Usage
public class TokenCache
{
    private readonly IMemoryCache _cache;

    public TokenCache(IMemoryCache cache) => _cache = cache;

    public string? GetToken(string key) => _cache.Get<string>(key);

    public void SetToken(string key, string token, TimeSpan ttl)
    {
        _cache.Set(key, token, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl,
            Size = token.Length // Optional: limit memory
        });
    }
}
```

## Authorisation State

### API Key Storage

```csharp
// Database model
public class ApiKey
{
    public Guid Id { get; set; }
    public string KeyHash { get; set; } = string.Empty; // BCrypt/Argon2
    public string Name { get; set; } = string.Empty;
    public string? OwnerId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public List<string> Scopes { get; set; } = new();
}

// Validation service
public class ApiKeyValidator
{
    private readonly AppDbContext _db;

    public ApiKeyValidator(AppDbContext db) => _db = db;

    public async Task<bool> ValidateAsync(string providedKey)
    {
        // Hash the provided key
        var hash = HashKey(providedKey);

        // Lookup
        var apiKey = await _db.ApiKeys
            .FirstOrDefaultAsync(k => k.KeyHash == hash && k.IsActive);

        if (apiKey == null) return false;
        if (apiKey.ExpiresAt < DateTime.UtcNow) return false;

        // Update last used
        apiKey.LastUsedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return true;
    }
}
```

### HMAC Key Storage

```csharp
// Consumer stores HMAC secrets
public class HmacCredential
{
    public Guid Id { get; set; }
    public string KeyId { get; set; } = string.Empty; // Public identifier
    public string SecretHash { get; set; } = string.Empty; // Hashed secret
    public string Algorithm { get; set; } = "HMACSHA256";
    public DateTime CreatedAt { get; set; }
    public DateTime? RotatedAt { get; set; }
    public bool IsActive { get; set; } = true;
}

// Validation
public class HmacValidator
{
    private readonly AppDbContext _db;

    public async Task<bool> ValidateAsync(string keyId, string payload, string signature, string timestamp)
    {
        var cred = await _db.HmacCredentials
            .FirstOrDefaultAsync(c => c.KeyId == keyId && c.IsActive);

        if (cred == null) return false;

        // Verify timestamp
        if (!long.TryParse(timestamp, out var ts) ||
            Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - ts) > 300)
            return false;

        // Verify signature
        var secret = GetSecret(cred); // From secure store
        var expected = ComputeHmac(cred.Algorithm, secret, payload, timestamp);

        return CryptographicOperations.FixedTimeEquals(
            Convert.FromBase64String(signature), expected);
    }
}
```

## Session State (If Needed)

### ASP.NET Core Session

```csharp
// Program.cs
builder.Services.AddDistributedMemoryCache(); // Or Redis
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

app.UseSession();

// Usage
public class SessionController : NuciApiController
{
    public ActionResult Set(string key, string value)
    {
        HttpContext.Session.SetString(key, value);
        return Ok();
    }

    public ActionResult Get(string key)
    {
        var value = HttpContext.Session.GetString(key);
        return value != null ? Ok(value) : NotFound();
    }
}
```

## Persistence Patterns

### Repository Pattern

```csharp
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByEmailAsync(string email);
    Task<User> AddAsync(User user);
    Task UpdateAsync(User user);
    Task DeleteAsync(Guid id);
}

public class EfUserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    public EfUserRepository(AppDbContext db) => _db = db;

    public async Task<User?> GetByIdAsync(Guid id)
        => await _db.Users.FindAsync(id);

    public async Task<User?> GetByEmailAsync(string email)
        => await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

    public async Task<User> AddAsync(User user)
    {
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    public async Task UpdateAsync(User user)
    {
        _db.Users.Update(user);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user != null)
        {
            _db.Users.Remove(user);
            await _db.SaveChangesAsync();
        }
    }
}
```

### Unit of Work

```csharp
public interface IUnitOfWork : IDisposable
{
    IUserRepository Users { get; }
    IApiKeyRepository ApiKeys { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public class EfUnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;

    public EfUnitOfWork(AppDbContext db) => _db = db;

    public IUserRepository Users => new EfUserRepository(_db);
    public IApiKeyRepository ApiKeys => new EfApiKeyRepository(_db);

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await _db.SaveChangesAsync(ct);

    public void Dispose() => _db.Dispose();
}
```

## State Anti-Patterns

| Anti-Pattern | Problem | Solution |
|--------------|---------|----------|
| Static `ConcurrentDictionary` cache | Memory leak, no eviction | `IMemoryCache` / `IDistributedCache` |
| Singleton DbContext | Thread safety, stale data | Scoped DbContext |
| Session for API tokens | CSRF, scaling issues | Stateless JWT / API keys |
| In-memory rate limiting | Doesn't work multi-instance | Redis distributed cache |
| Storing secrets in DB plaintext | Breach = total compromise | Hash (BCrypt/Argon2) / Key Vault |

## State Checklist

- [ ] Library: No internal state (verified)
- [ ] Request state: Use `HttpContext.Items`
- [ ] Database: Scoped DbContext, repository pattern
- [ ] Cache: `IDistributedCache` for multi-instance
- [ ] Session: Avoid for APIs; use stateless auth
- [ ] Secrets: Never plaintext; hash or Key Vault
- [ ] Rate limiting: Distributed (Redis)
- [ ] Correlation: Middleware → `HttpContext.TraceIdentifier`
- [ ] Health checks: Verify DB, cache, external deps