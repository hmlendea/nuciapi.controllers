# Invariants

## Runtime Invariants

### Controller Lifecycle

| Invariant | Description | Enforcement |
|-----------|-------------|-------------|
| `controller.Request != null` | ASP.NET Core guarantees populated request | Framework |
| `controller.Response != null` | ASP.NET Core guarantees response | Framework |
| `controller.User != null` | ClaimsPrincipal (may be unauthenticated) | Framework |
| Fresh instance per request | Controller activated per request | ASP.NET Core DI |

### ProcessRequest Preconditions

| Invariant | Checked By | Violation Result |
|-----------|------------|------------------|
| `request != null` | `ProcessRequest` | `400 BadRequest(InvalidRequest)` |
| `authorisation != null` | `ProcessRequest` | `NotImplementedException` |
| `action != null` | Caller (compile-time) | Compiler error |

### ProcessRequest Postconditions

| Invariant | Guarantee |
|-----------|-----------|
| Returns `ActionResult` | Never null |
| Null request → 400 | `BadRequest(NuciApiErrorResponse.InvalidRequest)` |
| Null response on GET → 404 | `NotFound(NuciApiErrorResponse.NotFound)` |
| Non-null response → 200/201 | `Ok(response)` or `Created(...)` |
| Exception → propagates | Caught by middleware → 500 |

### Authorisation Invariants

| Invariant | Description |
|-----------|-------------|
| `NuciApiAuthorisation.None` always succeeds | No-op, returns completed task |
| `NuciApiAuthorisation.ApiKey(key)` validates `Authorization: Bearer <key>` | Case-sensitive key match |
| Custom authorisation implements `Authorise(HttpRequest)` | Must throw `AuthenticationException` on failure |
| `Authorise` called before action | In `ProcessRequest` flow |

### HMAC Extraction Invariants

| Invariant | Description |
|-----------|-------------|
| `GetHmacToken()` reads `X-HMAC` header | Returns null if missing |
| Header value returned as-is | No parsing, no validation |
| Multiple headers → first value | Standard ASP.NET Core behavior |

## Compile-Time Invariants

### Type Safety

| Invariant | Enforcement |
|-----------|-------------|
| `TRequest` is reference type | Consumer responsibility (no constraint) |
| `TResponse` is reference type | Consumer responsibility |
| `action` returns `TResponse` or `void` | Compiler |
| `authorisation` is `NuciApiAuthorisation` | Compiler |

### Nullability (Nullable Enabled)

| Invariant | Enforcement |
|-----------|-------------|
| `request` parameter not null | Caller must ensure (validated at runtime) |
| `authorisation` parameter not null | Caller must ensure (validated at runtime) |
| `action` delegate not null | Compiler |
| Return value not null | Library ensures |

## Behavioral Invariants

### Request Processing Order

```
1. Validate request != null
2. Authorise request (authorisation.Authorise)
3. Execute action delegate
4. If GET and result == null → 404
5. Else → wrap result (Ok/Created)
```

**Never reordered, never skipped.**

### Exception Propagation

| Exception Type | Source | Propagates? |
|----------------|--------|-------------|
| `AuthenticationException` | Authorisation | Yes → middleware → 401 |
| `NotImplementedException` | Missing authorisation | Yes → middleware → 500 |
| `ArgumentNullException` | Null request | No → caught → 400 |
| Action delegate exception | Consumer code | Yes → middleware → 500 |

### Thread Safety Invariants

| Invariant | Scope |
|-----------|-------|
| No shared mutable state | Library-wide |
| `NuciApiKeyAuthorisation.Key` readonly | Instance |
| `NuciApiNoneAuthorisation.None` singleton | Process |
| Controller instance per request | Request |

## API Contract Invariants

### NuciApiController

| Method | Invariant |
|--------|-----------|
| `ProcessRequest<TRequest>` | Validates request, authorises, executes, wraps |
| `ProcessRequest<TRequest,TResponse>` | Same + generic response |
| `ProcessRequestAsync<TRequest>` | Async version, `ConfigureAwait(false)` |
| `ProcessRequestAsync<TRequest,TResponse>` | Async version, `ConfigureAwait(false)` |
| `GetHmacToken()` | Returns `X-HMAC` header or null |
| `AuthoriseRequest(authorisation)` | Calls `authorisation.Authorise(Request)` |

### NuciApiAuthorisation

| Member | Invariant |
|--------|-----------|
| `None` | Singleton, stateless, always succeeds |
| `ApiKey(string)` | Creates instance with readonly `Key` |
| `Authorise(HttpRequest)` | Abstract, must be implemented |
| `PerformAuthorisation(HttpRequest)` | Protected, called by base `Authorise` |

### NuciApiKeyAuthorisation

| Member | Invariant |
|--------|-----------|
| `Key` | Readonly, set in constructor |
| `PerformAuthorisation` | Throws `AuthenticationException` if `Authorization: Bearer <key>` missing or mismatch |

### NuciApiNoneAuthorisation

| Member | Invariant |
|--------|-----------|
| `None` | Static readonly singleton |
| `PerformAuthorisation` | Returns `Task.CompletedTask` |

## Data Invariants

### NuciApiErrorResponse (from NuciAPI)

| Field | Invariant |
|-------|-----------|
| `InvalidRequest` | Static readonly, `Error = "InvalidRequest"` |
| `NotFound` | Static readonly, `Error = "NotFound"` |
| `Error` property | Non-null string |
| `Message` property | Non-null string |

## Security Invariants

| Invariant | Description |
|-----------|-------------|
| No secrets logged | Library has no logging |
| No secrets in memory beyond request | Headers read, used, discarded |
| API key compared with `FixedTimeEquals` | Not in library (consumer responsibility) |
| HMAC token not validated by library | Extraction only, validation is consumer code |

## Versioning Invariants

| Invariant | Policy |
|-----------|--------|
| Public API surface | Semantic versioning |
| Breaking change → major version | Yes |
| New overload → minor version | Yes |
| Bug fix → patch version | Yes |
| `NuciApiErrorResponse` from NuciAPI | Tracks NuciAPI version |

## Testing Invariants

| Invariant | Test Coverage |
|-----------|---------------|
| Null request → 400 | Unit test |
| Null authorisation → NotImplementedException | Unit test |
| Valid authorisation → action executes | Unit test |
| Null response on GET → 404 | Unit test |
| Non-null response → 200 | Unit test |
| Exception in action → propagates | Unit test |
| HMAC extraction returns header | Unit test |
| Authorisation strategies work | Unit test |

## Invariant Violation Handling

| Violation | Detection | Response |
|-----------|-----------|----------|
| Null request | Runtime check | 400 response |
| Null authorisation | Runtime check | NotImplementedException |
| Null action | Compile error | Build fails |
| Missing auth header | Authorisation check | AuthenticationException |
| Invalid API key | Authorisation check | AuthenticationException |
| Consumer exception | Try/catch in middleware | 500 response |

## Documenting New Invariants

When adding code, document new invariants here:

```markdown
### [Component] Invariants

| Invariant | Description | Enforcement |
|-----------|-------------|-------------|
| [New invariant] | [Description] | [Compile-time / Runtime / Test] |
```