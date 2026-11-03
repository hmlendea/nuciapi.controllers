# Architecture (Detailed)

This document elaborates on the root [ARCHITECTURE.md](../ARCHITECTURE.md) with implementation-level detail for component owners and contributors.

## System Boundary

The library boundary is the `NuciAPI.Controllers` namespace and its public types:
- `NuciApiController` (abstract base class)
- `NuciApiAuthorisation` (abstract base class with factory methods)
- `NuciApiKeyAuthorisation` (concrete strategy)
- `NuciApiNoneAuthorisation` (concrete strategy)

Everything outside this namespace is external: consumer code, ASP.NET Core framework, NuciAPI package.

## Internal Module Decomposition

```
NuciAPI.Controllers/
├── NuciApiController.cs          # Public contract: request processing, authorisation orchestration, HMAC extraction
├── NuciApiAuthorisation.cs       # Public contract: authorisation strategy base, factory methods, Bearer parsing
├── NuciApiKeyAuthorisation.cs    # Implementation: API key validation
└── NuciApiNoneAuthorisation.cs   # Implementation: no-op authorisation
```

### Module Responsibilities

| Module | Public Surface | Internal Detail |
|--------|----------------|-----------------|
| `NuciApiController` | 4 `ProcessRequest` overloads, `GetHeaderValue` | `AuthoriseRequest`, `RetrieveHmacTokenFromHeaders` |
| `NuciApiAuthorisation` | `Name`, `None`, `ApiKey(key)`, `Authorise(data)`, `ToString()` | `PerformAuthorisation` (abstract), Bearer regex parsing |
| `NuciApiKeyAuthorisation` | `Key` property | `PerformAuthorisation` (string.Equals comparison) |
| `NuciApiNoneAuthorisation` | (internal constructor) | `PerformAuthorisation` (no-op) |

## Dependency Graph (Internal)

```
NuciApiController
    ├── NuciApiAuthorisation (abstract)
    │       ├── NuciApiKeyAuthorisation
    │       └── NuciApiNoneAuthorisation
    ├── Microsoft.AspNetCore.Mvc.ControllerBase
    ├── NuciAPI.Requests.NuciApiRequest
    ├── NuciAPI.Responses.NuciApiResponse
    ├── NuciAPI.Responses.NuciApiErrorResponse
    ├── NuciAPI.Responses.NuciApiSuccessResponse
    ├── System.Net.WebUtility
    └── System.Linq
```

## Runtime Execution Model

### Request Processing Pipeline

```
Consumer Action
    │
    ▼
ProcessRequest<TRequest>(request, action, auth)
    │
    ├── Validate request ≠ null → BadRequest(InvalidRequest)
    │
    ├── AuthoriseRequest(auth)
    │       │
    │       ├── auth is null → NotImplementedException
    │       │
    │       └── auth.Authorise(Authorization header)
    │               │
    │               ├── Name == "None" → return (success)
    │               │
    │               ├── Authorization header missing/empty → AuthenticationException
    │               │
    │               └── Strip "Bearer " prefix (case-insensitive)
    │                       │
    │                       └── PerformAuthorisation(token)
    │                               ├── NuciApiKeyAuthorisation: Key.Equals(token) → AuthenticationException on mismatch
    │                               └── NuciApiNoneAuthorisation: no-op
    │
    ├── RetrieveHmacTokenFromHeaders(request)
    │       │
    │       ├── GetHeaderValue("X-HMAC")
    │       │
    │       └── If present: request.HmacToken = UrlDecode(value)
    │
    └── Execute action delegate
            │
            ├── Sync: action() → Ok(response) or Ok(SuccessResponse.Default)
            │
            └── Async: await action().ConfigureAwait(false) → Ok(response) or Ok(SuccessResponse.Default)
                    │
                    └── If GET && response == null → NotFound(NotFound)
```

### Key Implementation Details

1. **Null Request Check**: First operation in all overloads; returns `BadRequest(NuciApiErrorResponse.InvalidRequest)` before any authorisation.

2. **Authorisation Order**: Authorisation runs before HMAC extraction. If authorisation fails, HMAC is never processed.

3. **Bearer Token Parsing**: Regex `^Bearer\s+` with `IgnoreCase` strips the scheme. The remaining token is passed to `PerformAuthorisation`.

4. **HMAC Extraction**: Header name hard-coded as `X-HMAC`. Value is URL-decoded via `WebUtility.UrlDecode` and assigned to `request.HmacToken` (property from `NuciApiRequest` base class).

4. **GET Null Response Handling**: Only for `ProcessRequest<TRequest, TResponse>` overloads. If HTTP method is GET (case-insensitive) and action returns null, returns `NotFound(NuciApiErrorResponse.NotFound)`.

5. **Async Configuration**: All async overloads use `ConfigureAwait(false)` to avoid capturing synchronization context.

## Extension Points (Architectural)

### 1. Custom Authorisation Strategy

```csharp
public class CustomAuthorisation : NuciApiAuthorisation
{
    public CustomAuthorisation() : base("Custom") { }

    protected override void PerformAuthorisation(string token)
    {
        // Custom validation logic
        if (!IsValid(token))
            throw new AuthenticationException("Custom failure");
    }
}

// Consumer usage:
return ProcessRequest(request, action, new CustomAuthorisation());
```

**Contract**: Must derive from `NuciApiAuthorisation`, implement `PerformAuthorisation(string)`. Constructor must call `base(name)`.

### 2. Custom Request Processing

```csharp
public abstract class MyApiController : NuciApiController
{
    protected override ActionResult ProcessRequest<TRequest>(TRequest request, Action action, NuciApiAuthorisation auth)
    {
        // Pre-processing
        var result = base.ProcessRequest(request, action, auth);
        // Post-processing
        return result;
    }
}
```

**Contract**: Override any `ProcessRequest`/`ProcessRequestAsync` overload. Call `base` to preserve standard behaviour.

### 3. Custom Response Types

```csharp
public class MyResponse : NuciApiResponse { ... }

// Consumer usage:
return ProcessRequest<MyRequest, MyResponse>(request, () => new MyResponse(), auth);
```

**Contract**: `TResponse` must derive from `NuciApiResponse`. Null handling for GET applies.

## Design Constraints (Implementation-Level)

| Constraint | Location | Impact |
|------------|----------|--------|
| Single authorisation per request | `ProcessRequest` signature | Composite auth requires consumer wrapper |
| Fixed HMAC header (`X-HMAC`) | `RetrieveHmacTokenFromHeaders` | Not configurable; fork to change |
| Fixed Bearer scheme parsing | `NuciApiAuthorisation.Authorise` | Not configurable; new scheme = new strategy |
| No logging/telemetry | Entire library | Consumer must add |
| No DI registration | Entire library | Consumer controls instantiation |
| `ImplicitUsings` disabled | Project file | Explicit `using` required in consumer code |
| Nullable enabled | Project file | Null annotations on public API |

## Versioning & Compatibility

- **NuGet Package**: `NuciAPI.Controllers` version 2.3.1
- **Target Framework**: `net10.0` only
- **NuciAPI Dependency**: 3.5.1 (package reference)
- **ASP.NET Core**: Framework reference to `Microsoft.AspNetCore.App` (version resolved by consumer's runtime)

Breaking changes require major version bump:
- `ProcessRequest` signature changes
- `NuciApiAuthorisation` factory method changes
- `NuciApiRequest.HmacToken` property changes (depends on NuciAPI package)
- Header names (`Authorization`, `X-HMAC`) changes