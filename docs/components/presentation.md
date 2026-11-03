# Presentation Layer: NuciApiController

## Purpose

`NuciApiController` is the abstract base class that consumer controllers derive from. It provides standardised request processing, authorisation orchestration, HMAC token extraction, and response wrapping for ASP.NET Core REST APIs.

## Location

`NuciAPI.Controllers/NuciApiController.cs`

## Public API Surface

### ProcessRequest Overloads

| Method | Signature | Use Case |
|--------|-----------|----------|
| `ProcessRequest<TRequest>` | `ActionResult ProcessRequest<TRequest>(TRequest request, Action action, NuciApiAuthorisation authorisation)` | Fire-and-forget actions returning success response |
| `ProcessRequestAsync<TRequest>` | `Task<ActionResult> ProcessRequestAsync<TRequest>(TRequest request, Func<Task> action, NuciApiAuthorisation authorisation)` | Async fire-and-forget actions |
| `ProcessRequest<TRequest, TResponse>` | `ActionResult ProcessRequest<TRequest, TResponse>(TRequest request, Func<TResponse> action, NuciApiAuthorisation authorisation)` | Sync actions returning typed response |
| `ProcessRequestAsync<TRequest, TResponse>` | `Task<ActionResult> ProcessRequest<TRequest, TResponse>(TRequest request, Func<Task<TResponse>> action, NuciApiAuthorisation authorisation)` | Async actions returning typed response |

### Helper Methods

| Method | Signature | Description |
|--------|-----------|-------------|
| `GetHeaderValue` | `string GetHeaderValue(string headerName)` | Retrieves first value of request header |

## Internal Implementation

### Private Methods

| Method | Signature | Responsibility |
|--------|-----------|----------------|
| `AuthoriseRequest` | `void AuthoriseRequest(NuciApiAuthorisation authorisation)` | Validates authorisation not null, extracts Authorization header, delegates to strategy |
| `RetrieveHmacTokenFromHeaders<TRequest>` | `void RetrieveHmacTokenFromHeaders<TRequest>(TRequest request)` | Extracts X-HMAC header, URL-decodes, assigns to request.HmacToken |

## Execution Flow

```
ProcessRequest called
    │
    ├── request null? → BadRequest(InvalidRequest)
    │
    ├── AuthoriseRequest(authorisation)
    │       │
    │       ├── authorisation null? → NotImplementedException
    │       │
    │       └── authorisation.Authorise(GetHeaderValue("Authorization"))
    │
    ├── RetrieveHmacTokenFromHeaders(request)
    │       │
    │       └── X-HMAC present? → request.HmacToken = UrlDecode(value)
    │
    └── Execute action delegate
            │
            ├── TResponse overload: action returns response
            │       │
            │       ├── response null && GET? → NotFound(NotFound)
            │       │
            │       └── Ok(response)
            │
            └── No-response overload: action() → Ok(SuccessResponse.Default)
```

## Constraints & Invariants

1. **Request Validation First**: Null check occurs before any authorisation or HMAC processing.
2. **Authorisation Before HMAC**: If authorisation fails, HMAC is never extracted.
3. **Single Authorisation**: One `NuciApiAuthorisation` instance per request.
4. **GET Null Handling**: Only applies to `TResponse` overloads; returns 404 for null GET responses.
5. **Async Context**: `ConfigureAwait(false)` used throughout; no sync context capture.
6. **Exception Propagation**: `AuthenticationException` from authorisation propagates; action delegate exceptions propagate.

## Consumer Usage Pattern

```csharp
public class MyController : NuciApiController
{
    [HttpPost("endpoint")]
    public ActionResult MyEndpoint(MyRequest request)
    {
        return ProcessRequest(request, () =>
        {
            // Business logic
        }, NuciApiAuthorisation.ApiKey("secret"));
    }
}
```

## Dependencies

- `Microsoft.AspNetCore.Mvc.ControllerBase` (base class)
- `NuciAPI.Requests.NuciApiRequest` (generic constraint)
- `NuciAPI.Responses.NuciApiResponse` / `NuciApiErrorResponse` / `NuciApiSuccessResponse` (response types)
- `NuciApiAuthorisation` (strategy parameter)
- `System.Net.WebUtility` (URL decoding)
- `System.Linq` (header enumeration)