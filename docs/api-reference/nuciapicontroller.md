# NuciApiController API Reference

## Class Definition

```csharp
namespace NuciAPI.Controllers
{
    public abstract class NuciApiController : ControllerBase
    {
        // ProcessRequest overloads
        // Helper methods
    }
}
```

**Base Class**: `Microsoft.AspNetCore.Mvc.ControllerBase`
**Namespace**: `NuciAPI.Controllers`
**Assembly**: `NuciAPI.Controllers.dll` (NuGet: `NuciAPI.Controllers` 2.3.1)

---

## ProcessRequest Overloads

### 1. ProcessRequest<TRequest> (Sync, No Response)

```csharp
protected ActionResult ProcessRequest<TRequest>(
    TRequest request,
    Action action,
    NuciApiAuthorisation authorisation)
    where TRequest : NuciApiRequest
```

**Parameters:**
| Name | Type | Description |
|------|------|-------------|
| `request` | `TRequest` | Request DTO (must derive from `NuciApiRequest`) |
| `action` | `Action` | Synchronous delegate to execute |
| `authorisation` | `NuciApiAuthorisation` | Authorisation strategy (factory method result) |

**Returns:** `ActionResult` — `Ok(NuciApiSuccessResponse.Default)` on success

**Errors:**
| Condition | Result |
|-----------|--------|
| `request == null` | `BadRequest(NuciApiErrorResponse.InvalidRequest)` |
| `authorisation == null` | `NotImplementedException` |
| `authorisation.Authorise()` throws | `AuthenticationException` propagates |
| `action()` throws | Exception propagates |

**Example:**
```csharp
[HttpPost("notify")]
public ActionResult Notify(NotificationRequest request)
{
    return ProcessRequest(request, () =>
    {
        _notificationService.Send(request.Message);
    }, NuciApiAuthorisation.ApiKey("key"));
}
```

---

### 2. ProcessRequestAsync<TRequest> (Async, No Response)

```csharp
protected async Task<ActionResult> ProcessRequestAsync<TRequest>(
    TRequest request,
    Func<Task> action,
    NuciApiAuthorisation authorisation)
    where TRequest : NuciApiRequest
```

**Parameters:**
| Name | Type | Description |
|------|------|-------------|
| `request` | `TRequest` | Request DTO |
| `action` | `Func<Task>` | Asynchronous delegate to execute |
| `authorisation` | `NuciApiAuthorisation` | Authorisation strategy |

**Returns:** `Task<ActionResult>` — `Ok(NuciApiSuccessResponse.Default)` on success

**Errors:** Same as sync overload

**Async Behaviour:** Uses `await action().ConfigureAwait(false)`

**Example:**
```csharp
[HttpPost("process")]
public async Task<ActionResult> ProcessAsync(ProcessRequest request)
{
    return await ProcessRequestAsync(request, async () =>
    {
        await _processor.ExecuteAsync(request.Data);
    }, NuciApiAuthorisation.ApiKey("key"));
}
```

---

### 3. ProcessRequest<TRequest, TResponse> (Sync, Typed Response)

```csharp
protected ActionResult ProcessRequest<TRequest, TResponse>(
    TRequest request,
    Func<TResponse> action,
    NuciApiAuthorisation authorisation)
    where TRequest : NuciApiRequest
    where TResponse : NuciApiResponse
```

**Parameters:**
| Name | Type | Description |
|------|------|-------------|
| `request` | `TRequest` | Request DTO |
| `action` | `Func<TResponse>` | Synchronous delegate returning response |
| `authorisation` | `NuciApiAuthorisation` | Authorisation strategy |

**Returns:** `ActionResult` — `Ok(response)` or `NotFound(NuciApiErrorResponse.NotFound)`

**Errors:**
| Condition | Result |
|-----------|--------|
| `request == null` | `BadRequest(NuciApiErrorResponse.InvalidRequest)` |
| `authorisation == null` | `NotImplementedException` |
| `authorisation.Authorise()` throws | `AuthenticationException` propagates |
| `action()` throws | Exception propagates |
| `action()` returns `null` && HTTP GET | `NotFound(NuciApiErrorResponse.NotFound)` |
| `action()` returns `null` && not GET | `Ok(null)` (200 with null body) |

**Example:**
```csharp
[HttpGet("users/{id}")]
public ActionResult GetUser(GetUserRequest request)
{
    return ProcessRequest(request, () =>
    {
        var user = _userService.GetById(request.Id);
        return user != null
            ? new UserResponse { Name = user.Name, Email = user.Email }
            : null; // Triggers 404 for GET
    }, NuciApiAuthorisation.ApiKey("key"));
}
```

---

### 4. ProcessRequestAsync<TRequest, TResponse> (Async, Typed Response)

```csharp
protected async Task<ActionResult> ProcessRequest<TRequest, TResponse>(
    TRequest request,
    Func<Task<TResponse>> action,
    NuciApiAuthorisation authorisation)
    where TRequest : NuciApiRequest
    where TResponse : NuciApiResponse
```

**Parameters:**
| Name | Type | Description |
|------|------|-------------|
| `request` | `TRequest` | Request DTO |
| `action` | `Func<Task<TResponse>>` | Asynchronous delegate returning response |
| `authorisation` | `NuciApiAuthorisation` | Authorisation strategy |

**Returns:** `Task<ActionResult>` — `Ok(response)` or `NotFound(NuciApiErrorResponse.NotFound)`

**Errors:** Same as sync typed overload

**Async Behaviour:** Uses `await action().ConfigureAwait(false)`

**Example:**
```csharp
[HttpGet("reports/{id}")]
public async Task<ActionResult> GetReportAsync(GetReportRequest request)
{
    return await ProcessRequestAsync(request, async () =>
    {
        var report = await _reportService.GenerateAsync(request.Id);
        return report != null
            ? new ReportResponse { Data = report.Data }
            : null; // Triggers 404 for GET
    }, NuciApiAuthorisation.ApiKey("key"));
}
```

---

## Helper Methods

### GetHeaderValue

```csharp
protected string GetHeaderValue(string headerName)
```

**Parameters:**
| Name | Type | Description |
|------|------|-------------|
| `headerName` | `string` | Header name (case-insensitive) |

**Returns:** `string` — First header value, or `null` if absent

**Implementation:**
```csharp
=> Request.Headers[headerName].FirstOrDefault();
```

**Usage in Library:**
- `Authorization` header for authorisation
- `X-HMAC` header for HMAC extraction

**Consumer Usage:**
```csharp
var correlationId = GetHeaderValue("X-Correlation-ID");
var clientVersion = GetHeaderValue("X-Client-Version");
```

---

## Protected Methods (Override Points)

### AuthoriseRequest

```csharp
private void AuthoriseRequest(NuciApiAuthorisation authorisation)
```

**Not overridable** (private). Custom authorisation via strategy parameter.

### RetrieveHmacTokenFromHeaders

```csharp
private void RetrieveHmacTokenFromHeaders<TRequest>(TRequest request)
    where TRequest : NuciApiRequest
```

**Not overridable** (private). Custom HMAC handling via:
1. Override `ProcessRequest` in derived base controller
2. Pre-process in action before calling base
3. Custom authorisation strategy that validates HMAC

---

## Inherited from ControllerBase

Key members consumers commonly use:

| Member | Type | Description |
|--------|------|-------------|
| `Request` | `HttpRequest` | Current HTTP request |
| `Response` | `HttpResponse` | Current HTTP response |
| `HttpContext` | `HttpContext` | Full HTTP context |
| `User` | `ClaimsPrincipal` | Authenticated user |
| `ModelState` | `ModelStateDictionary` | Model validation state |
| `Ok(object)` | `ActionResult` | 200 OK with body |
| `BadRequest(object)` | `ActionResult` | 400 Bad Request |
| `NotFound(object)` | `ActionResult` | 404 Not Found |
| `Unauthorized()` | `ActionResult` | 401 Unauthorized |
| `StatusCode(int)` | `ActionResult` | Custom status code |

---

## Generic Constraints Summary

| Method | TRequest Constraint | TResponse Constraint |
|--------|---------------------|----------------------|
| `ProcessRequest<TRequest>` | `: NuciApiRequest` | — |
| `ProcessRequestAsync<TRequest>` | `: NuciApiRequest` | — |
| `ProcessRequest<TRequest, TResponse>` | `: NuciApiRequest` | `: NuciApiResponse` |
| `ProcessRequestAsync<TRequest, TResponse>` | `: NuciApiRequest` | `: NuciApiResponse` |

---

## Response Types (from NuciAPI.Responses)

### NuciApiSuccessResponse
```csharp
public static NuciApiSuccessResponse Default { get; }
```
Used for no-response overloads.

### NuciApiErrorResponse
```csharp
public static NuciApiErrorResponse InvalidRequest { get; }
public static NuciApiErrorResponse NotFound { get; }
```
Used for 400 and 404 responses.

---

## Usage Patterns

### Pattern 1: Simple Command (No Response)
```csharp
[HttpPost("action")]
public ActionResult DoAction(CommandRequest request)
{
    return ProcessRequest(request, () =>
    {
        _service.Execute(request);
    }, NuciApiAuthorisation.ApiKey("key"));
}
```

### Pattern 2: Query with Response
```csharp
[HttpGet("item/{id}")]
public ActionResult GetItem(GetItemRequest request)
{
    return ProcessRequest(request, () =>
    {
        return _repository.Get(request.Id); // Returns ItemResponse or null
    }, NuciApiAuthorisation.ApiKey("key"));
}
```

### Pattern 3: Async Command
```csharp
[HttpPost("async-action")]
public async Task<ActionResult> DoAsyncAction(CommandRequest request)
{
    return await ProcessRequestAsync(request, async () =>
    {
        await _service.ExecuteAsync(request);
    }, NuciApiAuthorisation.ApiKey("key"));
}
```

### Pattern 4: No Authentication
```csharp
[HttpGet("public")]
public ActionResult PublicEndpoint(PublicRequest request)
{
    return ProcessRequest(request, () =>
    {
        return new PublicResponse { Data = "public" };
    }, NuciApiAuthorisation.None);
}
```

---

## Related Documentation

- [Architecture](../architecture.md#components)
- [Request Processing Flow](../flows/request-processing.md)
- [Authorisation Flow](../flows/authorisation.md)
- [HMAC Extraction Flow](../flows/hmac-extraction.md)
- [Host and Composition](../components/host-and-composition.md)