# Design Decisions

## Decision Log

| ID | Date | Decision | Rationale | Alternatives Considered |
|----|------|----------|-----------|------------------------|
| DD-001 | 2024-01 | Abstract base controller with `ProcessRequest` pattern | Encapsulates cross-cutting concerns (auth, validation, error wrapping) | Attributes/filters, middleware, action filters |
| DD-002 | 2024-01 | Authorisation as strategy pattern (`NuciApiAuthorisation` hierarchy) | Open/closed principle, testable, extensible | Single enum, delegate, interface only |
| DD-003 | 2024-01 | No logging in library | Zero dependencies, consumer control, security | `ILogger` abstraction, `Microsoft.Extensions.Logging` |
| DD-004 | 2024-01 | No configuration system | Simplicity, no secrets, explicit behavior | `IOptions`, `appsettings.json` binding |
| DD-005 | 2024-01 | HMAC token extraction built-in | Common pattern, reduces boilerplate | Consumer implements, separate package |
| DD-006 | 2024-01 | Sync + async overloads for `ProcessRequest` | Flexibility, gradual migration, no forced async | Async only, sync only |
| DD-007 | 2024-01 | `ConfigureAwait(false)` on all async | No sync context capture, deadlock avoidance | Default capture, explicit context |
| DD-008 | 2024-01 | Null request → 400 with `NuciApiErrorResponse` | Fail fast, structured error, no null propagation | Throw `ArgumentNullException`, return null |
| DD-009 | 2024-01 | Null response on GET → 404 | REST convention, explicit not-found | Return 200 with null, throw exception |
| DD-010 | 2024-01 | `NotImplementedException` for missing authorisation | Fail fast at development time | Silent allow, default deny, compile-time check |
| DD-011 | 2024-01 | Target .NET 10.0, LangVersion latest | Modern language features, performance | .NET 8 LTS, .NET 9 |
| DD-012 | 2024-01 | `ImplicitUsings` disabled, `Nullable` enabled | Explicit dependencies, null safety | ImplicitUsings on, Nullable disabled |
| DD-013 | 2024-01 | GPL-3.0-or-later license | Copyleft, community protection | MIT, Apache-2.0, LGPL |
| DD-014 | 2024-01 | Single NuGet package (NuciAPI.Controllers) | Simplicity, co-located types | Split packages (Auth, Core, HMAC) |
| DD-015 | 2024-01 | Dependency on NuciAPI 3.5.1 (core primitives) | Shared types, `NuciApiErrorResponse` | Re-implement, copy-paste |

## Detailed Rationale

### DD-001: ProcessRequest Pattern

**Problem:** Controllers need consistent auth, validation, error handling, response wrapping.

**Solution:** Abstract base class with protected `ProcessRequest` methods.

```csharp
// Consumer writes:
return ProcessRequest(request, () => _service.DoWork(), auth);

// Library handles:
// 1. Null request check → 400
// 2. Authorisation → 401/403
// 3. Action execution
// 4. Null response (GET) → 404
// 5. Exception → 500 (via middleware)
// 6. Success → 200/201 with response
```

**Why not filters/attributes?**
- Filters run after model binding, can't easily wrap action delegate
- Attributes require registration, discovery
- Base class is explicit, discoverable, testable

**Why not middleware?**
- Middleware is global, not per-endpoint
- Can't access typed request/response easily
- Authorisation varies per endpoint

### DD-002: Authorisation Strategy Pattern

**Problem:** Different auth schemes (none, API key, JWT, HMAC, custom).

**Solution:** Abstract `NuciApiAuthorisation` with factory methods.

```csharp
// Built-in
NuciApiAuthorisation.None
NuciApiAuthorisation.ApiKey("key")

// Extensible
public class JwtAuthorisation : NuciApiAuthorisation
{
    public override async Task Authorise(HttpRequest request)
    {
        // JWT validation
    }
}
```

**Why not enum?**
- Can't attach behavior to enum
- Adding scheme = modifying library

**Why not delegate?**
- Less discoverable
- No shared state (e.g., API key)
- Harder to test

### DD-003: No Logging

**Decision:** Zero logging in library.

**Rationale:**
1. **No dependency** on `Microsoft.Extensions.Logging` or Serilog
2. **Consumer control** over format, sinks, levels, sampling
3. **Security** - no accidental secret logging
4. **Performance** - zero allocation in hot path
5. **Simplicity** - no `ILogger` injection, no DI registration

**Consumer adds:**
```csharp
// Middleware logs request/response
app.UseSerilogRequestLogging();
```

### DD-004: No Configuration

**Decision:** Library reads no configuration.

**Rationale:**
1. **No secrets** - API keys, HMAC secrets are consumer responsibility
2. **Explicit behavior** - no hidden toggles
3. **Testability** - no config to mock
4. **Deployment** - no `appsettings.json` to manage

**Consumer configures:**
```csharp
builder.Services.AddSingleton<NuciApiAuthorisation>(sp =>
    NuciApiAuthorisation.ApiKey(sp.GetRequiredService<IOptions<AuthOptions>>().Value.Key));
```

### DD-005: Built-in HMAC Extraction

**Decision:** `GetHmacToken()` method on controller.

**Rationale:**
- Common pattern in NuciAPI ecosystem
- Reduces boilerplate in every controller
- Consistent header name (`X-HMAC`)

```csharp
// Consumer usage
var hmac = GetHmacToken(); // Reads X-HMAC header
if (hmac != null) { /* validate */ }
```

**Alternative:** Consumer reads header directly. **Rejected:** Inconsistent, error-prone.

### DD-006: Sync + Async Overloads

**Decision:** Both `ProcessRequest` and `ProcessRequestAsync`.

**Rationale:**
- Legacy codebases may not be fully async
- Gradual migration path
- No forced async allocation for sync actions
- Consistent with ASP.NET Core patterns

```csharp
// Sync action
ProcessRequest(req, () => _service.SyncWork(), auth);

// Async action
await ProcessRequestAsync(req, () => _service.AsyncWork(), auth);
```

### DD-007: ConfigureAwait(false)

**Decision:** All async continuations use `ConfigureAwait(false)`.

**Rationale:**
- ASP.NET Core has no synchronization context
- Prevents deadlocks if consumer uses `.Result`/`.Wait()`
- Thread pool continuation is faster
- Library doesn't assume UI context

### DD-008: Null Request → 400

**Decision:** Automatic validation, structured error response.

```csharp
if (request is null)
    return BadRequest(NuciApiErrorResponse.InvalidRequest);
```

**Rationale:**
- Fail fast, clear error
- `NuciApiErrorResponse` is from NuciAPI (shared contract)
- Consumer doesn't need null checks

### DD-009: Null Response → 404 (GET)

**Decision:** `return NotFound(NuciApiErrorResponse.NotFound)` for null on GET.

**Rationale:**
- REST convention: GET returns 404 for missing resource
- Distinguishes "not found" from "empty result" (empty list = 200)
- Consistent with `NuciApiErrorResponse.NotFound`

### DD-010: NotImplementedException for Missing Auth

**Decision:** Throw if `authorisation` parameter is null.

```csharp
if (authorisation is null)
    throw new NotImplementedException("This endpoint has no authorisation specified.");
```

**Rationale:**
- **Fail fast at development time** - catches missing auth immediately
- **Explicit opt-in** - every endpoint must choose auth strategy
- **Security by default** - no accidental public endpoints
- **Not runtime error** - caught in testing, not production

**Alternatives rejected:**
- Silent allow → security risk
- Default deny → breaks legitimate public endpoints
- Compile-time → not possible with current C#

### DD-011: Target .NET 10.0

**Decision:** Target net10.0, LangVersion latest.

**Rationale:**
- Access to latest language features (primary constructors, collection expressions, etc.)
- Performance improvements in .NET 10
- NuciAPI 3.5.1 targets net10.0
- Library is forward-looking, not LTS-bound

### DD-012: ImplicitUsings Disabled, Nullable Enabled

**Decision:** Explicit usings, nullable reference types on.

**Rationale:**
- **Explicit usings** - clear dependencies, no hidden imports
- **Nullable enabled** - compile-time null safety, catches bugs
- Consistent with NuciAPI core library

### DD-013: GPL-3.0-or-later

**Decision:** Copyleft license.

**Rationale:**
- Protects community contributions
- Ensures derivatives remain open
- Aligns with NuciAPI ecosystem licensing

### DD-014: Single Package

**Decision:** One NuGet package: `NuciAPI.Controllers`.

**Rationale:**
- All types co-located, tightly coupled
- Simpler consumption: `dotnet add package NuciAPI.Controllers`
- No version diamond problems
- Small package (~50 KB)

### DD-015: Dependency on NuciAPI 3.5.1

**Decision:** Reference NuciAPI for `NuciApiErrorResponse` and primitives.

**Rationale:**
- Shared error response contract across ecosystem
- Avoids duplication
- Version aligned with ecosystem

## Revisiting Decisions

| Decision | Revisit Trigger | Process |
|----------|-----------------|---------|
| DD-001 | New cross-cutting concern | RFC in repo, maintain backward compat |
| DD-002 | New auth scheme needed | Add to hierarchy, no breaking change |
| DD-003 | Consumer demands library logging | Add optional `ILogger` parameter (opt-in) |
| DD-004 | Feature flags needed | Consumer implements, library unchanged |
| DD-011 | .NET 10 EOL | Update to next version, maintain compat |
| DD-013 | License compatibility issue | Legal review, community discussion |

## Decision Template (For Future)

```markdown
## DD-XXX: [Title]

**Date:** YYYY-MM-DD
**Status:** Accepted | Superseded | Rejected
**Context:** [Problem statement]
**Decision:** [What was decided]
**Rationale:** [Why]
**Consequences:** [Positive/negative]
**Alternatives:** [What else considered]
**Related:** [DD-YYY, issues, PRs]
```