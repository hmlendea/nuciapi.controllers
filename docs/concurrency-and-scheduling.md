# Concurrency and Scheduling

## Execution Model

### Per-Request Statelessness

NuciAPI.Controllers is **entirely stateless** at the library level:

| Aspect | Behaviour |
|--------|-----------|
| Instance lifetime | Per-request (ASP.NET Core controller activation) |
| Shared mutable state | None |
| Static fields | None |
| Caches | None |
| Connection pools | None |
| Background tasks | None |

Each HTTP request gets a fresh `NuciApiController` instance (via standard ASP.NET Core controller activation). No data persists between requests.

### Thread Safety

| Component | Thread Safety | Notes |
|-----------|---------------|-------|
| `NuciApiController` | Not thread-safe (per-request) | Single-threaded per request |
| `NuciApiAuthorisation` | Immutable after construction | Safe for concurrent reads |
| `NuciApiKeyAuthorisation` | Immutable (readonly `Key`) | Safe for concurrent reads |
| `NuciApiNoneAuthorisation` | Singleton (`None` property) | Stateless, thread-safe |
| `ProcessRequest` methods | Single-threaded per call | No internal parallelism |

**Consumer Responsibility:** If consumer stores `NuciApiKeyAuthorisation` as singleton, it's thread-safe (readonly key). If consumer creates new instance per request, also safe.

## Async Support

### Async Overloads

```csharp
// Async, no response
protected async Task<ActionResult> ProcessRequestAsync<TRequest>(
    TRequest request, Func<Task> action, NuciApiAuthorisation authorisation)

// Async, typed response
protected async Task<ActionResult> ProcessRequestAsync<TRequest, TResponse>(
    TRequest request, Func<Task<TResponse>> action, NuciApiAuthorisation authorisation)
```

### ConfigureAwait(false)

All async overloads use `ConfigureAwait(false)`:

```csharp
await action().ConfigureAwait(false);
```

**Implications:**
- No synchronization context capture
- Continuation runs on thread pool
- Avoids deadlocks in legacy sync-over-async scenarios
- Compatible with ASP.NET Core (no sync context by default)

### Async Flow

```
Request Thread
    │
    ├── Sync: validation, authorisation, HMAC extraction
    │
    ├── Async: await action().ConfigureAwait(false)
    │       │
    │       └── Thread pool thread executes action
    │
    └── Sync: response wrapping (on thread pool thread)
```

**No async in library code except action delegate await.**

## Synchronization Primitives

### Library Uses None

| Primitive | Used? | Location |
|-----------|-------|----------|
| `lock` | ❌ | — |
| `Monitor` | ❌ | — |
| `SemaphoreSlim` | ❌ | — |
| `ReaderWriterLockSlim` | ❌ | — |
| `Interlocked` | ❌ | — |
| `ConcurrentDictionary` | ❌ | — |
| `Channel` | ❌ | — |

### Consumer May Need

| Scenario | Primitive |
|----------|-----------|
| Shared service in action | `SemaphoreSlim` for rate limiting |
| Caching in consumer | `ConcurrentDictionary`, `IMemoryCache` |
| Background processing | `Channel`, `BackgroundService` |
| Distributed locking | Redis, database |

## Parallelism

### Library: No Internal Parallelism

- Single request → single thread (except async await)
- No `Parallel.ForEach`, `Task.WhenAll`, `PLINQ`
- No batching, no pipelining

### Consumer: Full Control

```csharp
// Consumer can parallelize in action
public ActionResult BatchProcess(BatchRequest request)
{
    return ProcessRequest(request, () =>
    {
        var results = request.Items
            .AsParallel()
            .Select(item => _processor.Process(item))
            .ToList();
        
        return new BatchResponse { Results = results };
    }, NuciApiAuthorisation.ApiKey("key"));
}
```

## Resource Management

### Library: No Resources Held

| Resource | Library | Consumer |
|----------|---------|----------|
| Database connections | ❌ | ✅ Via DI |
| HTTP clients | ❌ | ✅ Via `IHttpClientFactory` |
| File handles | ❌ | ✅ |
| Memory streams | ❌ | ✅ |
| Timers | ❌ | ✅ |
| Cancellation tokens | ❌ (not used) | ✅ Pass to async ops |

### Cancellation Tokens

**Library does not use `CancellationToken`:**

```csharp
// Current signature - no cancellation
protected async Task<ActionResult> ProcessRequestAsync<TRequest>(
    TRequest request, Func<Task> action, NuciApiAuthorisation authorisation)

// Consumer must handle cancellation in action
protected async Task<ActionResult> ProcessRequestAsync<TRequest>(
    TRequest request, Func<Task> action, NuciApiAuthorisation authorisation)
{
    // Consumer wraps action with cancellation
    return await ProcessRequestAsync(request, async () =>
    {
        await action(); // Consumer's action should accept CancellationToken
    }, authorisation);
}
```

**Recommendation:** Consumer actions should accept `CancellationToken` and pass to downstream async calls.

## Scheduling

### No Background Scheduling

| Feature | Library | Consumer |
|---------|---------|----------|
| `IHostedService` | ❌ | ✅ |
| `BackgroundService` | ❌ | ✅ |
| `Timer` | ❌ | ✅ |
| `Hangfire` / `Quartz` | ❌ | ✅ |
| Cron jobs | ❌ | ✅ |

### Request Scheduling

ASP.NET Core handles request scheduling:
- Kestrel thread pool
- Request queuing (configurable)
- No library interference

## Capacity Limits

### Library-Imposed Limits

| Limit | Value | Configurable? |
|-------|-------|---------------|
| Max concurrent requests | None (ASP.NET Core) | No |
| Request timeout | None (ASP.NET Core) | No |
| Request body size | None (ASP.NET Core) | No |
| Header size | None (ASP.NET Core) | No |
| HMAC token length | None | No |

### Consumer-Imposed Limits (Recommended)

```csharp
// Program.cs
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 10 * 1024 * 1024; // 10 MB
});

builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024;
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
});
```

## Concurrency Patterns for Consumers

### 1. Singleton Authorisation (Thread-Safe)

```csharp
// Program.cs
builder.Services.AddSingleton<NuciApiAuthorisation>(sp =>
    NuciApiAuthorisation.ApiKey(sp.GetRequiredService<IApiKeyProvider>().GetKey()));

// Controller
public class MyController : NuciApiController
{
    [HttpPost("action")]
    public ActionResult Action(Request request)
    {
        // Same authorisation instance used concurrently - SAFE (readonly Key)
        return ProcessRequest(request, () => { ... }, _authorisation);
    }
}
```

### 2. Per-Request Authorisation (Also Safe)

```csharp
// Controller
[HttpPost("action")]
public ActionResult Action(Request request)
{
    // New instance per request - SAFE
    var auth = NuciApiAuthorisation.ApiKey(_keyProvider.GetKey());
    return ProcessRequest(request, () => { ... }, auth);
}
```

### 3. Shared State in Consumer (Needs Synchronization)

```csharp
// Consumer service with shared state
public class RateLimitedService
{
    private readonly SemaphoreSlim _semaphore = new(100); // Max 100 concurrent
    
    public async Task<Result> ProcessAsync(Request request, CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct);
        try { return await DoWork(request); }
        finally { _semaphore.Release(); }
    }
}
```

## Deadlock Avoidance

### Library: No Deadlock Risk

- No `lock` statements
- No `Task.Result` / `Task.Wait()`
- `ConfigureAwait(false)` everywhere
- No synchronization context dependency

### Consumer: Avoid These

```csharp
// BAD: Sync-over-async deadlock risk
public ActionResult BadAction(Request request)
{
    var result = ProcessRequestAsync(request, async () => 
    {
        await _service.DoAsync(); // Captures context if not ConfigureAwait(false)
    }, auth).Result; // BLOCKS - deadlock in ASP.NET Core sync context
    
    return result;
}

// GOOD: Fully async
public async Task<ActionResult> GoodAction(Request request)
{
    return await ProcessRequestAsync(request, async () => 
    {
        await _service.DoAsync();
    }, auth);
}
```

## Performance Characteristics

| Metric | Library Overhead |
|--------|------------------|
| Allocation per request | ~200 bytes (delegate, request validation) |
| CPU per request | ~0.01ms (validation, header parsing) |
| Lock contention | Zero |
| GC pressure | Minimal (short-lived objects) |
| Thread pool usage | Only during `await action()` |

## Testing Concurrency

### Load Test Pattern

```csharp
[Test]
public async Task ConcurrentRequests_AllSucceed()
{
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", "key");
    
    var tasks = Enumerable.Range(0, 100)
        .Select(_ => client.PostAsJsonAsync("/api/test", new TestRequest()))
        .ToArray();
    
    var responses = await Task.WhenAll(tasks);
    
    Assert.IsTrue(responses.All(r => r.IsSuccessStatusCode));
}
```

### Stress Test: Shared Authorisation

```csharp
[Test]
public async Task SharedAuthorisation_ThreadSafe()
{
    var auth = NuciApiAuthorisation.ApiKey("key");
    var controller = new TestController();
    
    var tasks = Enumerable.Range(0, 1000)
        .Select(i => Task.Run(() => 
        {
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
            controller.Request.Headers["Authorization"] = "Bearer key";
            return controller.TestAction(new TestRequest(), auth);
        }))
        .ToArray();
    
    var results = await Task.WhenAll(tasks);
    Assert.IsTrue(results.All(r => r is OkObjectResult));
}
```