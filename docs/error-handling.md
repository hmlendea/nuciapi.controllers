# Error Handling

## Error Taxonomy

### Library-Originated Errors

| Error Type | Trigger | HTTP Status | Response Body |
|------------|---------|-------------|---------------|
| `ArgumentNullException` (request) | `request == null` in `ProcessRequest` | 400 | `NuciApiErrorResponse.InvalidRequest` |
| `NotImplementedException` | `authorisation == null` in `AuthoriseRequest` | 500 | None (unhandled) |
| `AuthenticationException` | Missing Authorization header | 401 | Exception message |
| `AuthenticationException` | Invalid API key | 401 | Exception message |
| `AuthenticationException` | Custom strategy failure | 401 | Exception message |

### Consumer-Originated Errors

| Error Type | Trigger | HTTP Status | Handling |
|------------|---------|-------------|----------|
| Action delegate exception | Business logic throws | 500 | Exception middleware |
| `NullReferenceException` | Consumer code bug | 500 | Exception middleware |
| `InvalidOperationException` | Configuration missing | 500 | Exception middleware |
| Custom exceptions | Consumer-defined | Varies | Consumer middleware |

### Framework Errors

| Error Type | Trigger | HTTP Status |
|------------|---------|-------------|
| Model binding failure | Invalid request body | 400 |
| Route not found | No matching endpoint | 404 |
| Method not allowed | Wrong HTTP verb | 405 |
| Request too large | Body size limit | 413 |

## Handling Patterns

### 1. Library Validation Errors (Automatic)

```csharp
// Null request → automatic 400
if (request is null)
    return BadRequest(NuciApiErrorResponse.InvalidRequest);
```

**Consumer action:** None required. Returns structured error response.

### 2. Authentication Errors (Exception-Based)

```csharp
// In NuciApiAuthorisation.Authorise
if (string.IsNullOrEmpty(authorisationData))
    throw new AuthenticationException("Missing authorisation data.");

// In NuciApiKeyAuthorisation.PerformAuthorisation
if (!Key.Equals(apiKey))
    throw new AuthenticationException("Invalid API key.");
```

**Consumer action:** Configure exception middleware to convert to 401.

```csharp
// Program.cs
app.UseExceptionHandler("/error");

// Or custom middleware
app.Use(async (context, next) =>
{
    try { await next(); }
    catch (AuthenticationException ex)
    {
        context.Response.StatusCode = 401;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
});
```

### 3. Programming Errors (NotImplementedException)

```csharp
// In NuciApiController.AuthoriseRequest
if (authorisation is null)
    throw new NotImplementedException("This endpoint has no authorisation specified.");
```

**Consumer action:** Fix code — always pass authorisation to `ProcessRequest`.

### 4. Business Logic Errors (Consumer-Defined)

```csharp
[HttpPost("transfer")]
public ActionResult Transfer(TransferRequest request)
{
    return ProcessRequest(request, () =>
    {
        if (request.Amount > _accountService.GetBalance(request.FromAccount))
            throw new InsufficientFundsException(request.FromAccount);
        
        _accountService.Transfer(request.FromAccount, request.ToAccount, request.Amount);
    }, NuciApiAuthorisation.ApiKey("key"));
}
```

**Consumer action:** Map to appropriate HTTP status via middleware or filters.

```csharp
// Exception filter
public class ApiExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        switch (context.Exception)
        {
            case InsufficientFundsException ex:
                context.Result = new ObjectResult(new { error = ex.Message })
                { StatusCode = 400 };
                context.ExceptionHandled = true;
                break;
            case NotFoundException ex:
                context.Result = new NotFoundObjectResult(new { error = ex.Message });
                context.ExceptionHandled = true;
                break;
        }
    }
}

// Register in Program.cs
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ApiExceptionFilter>();
});
```

## Error Response Formats

### NuciApiErrorResponse (Library)

```json
// 400 Bad Request - Null request
{
  "error": "InvalidRequest",
  "message": "The request was invalid."
}

// 404 Not Found - Null response on GET
{
  "error": "NotFound",
  "message": "The requested resource was not found."
}
```

### AuthenticationException (Library)

```json
// 401 Unauthorized - Missing header
{
  "error": "Missing authorisation data."
}

// 401 Unauthorized - Invalid key
{
  "error": "Invalid API key."
}
```

### Consumer Standard Format (Recommended)

```json
// 400 Bad Request - Validation
{
  "error": "ValidationError",
  "message": "One or more fields are invalid.",
  "details": [
    { "field": "amount", "code": "GreaterThanZero", "message": "Amount must be positive" }
  ]
}

// 401 Unauthorized
{
  "error": "Unauthorized",
  "message": "Authentication failed."
}

// 403 Forbidden
{
  "error": "Forbidden",
  "message": "Insufficient permissions."
}

// 404 Not Found
{
  "error": "NotFound",
  "message": "Resource not found."
}

// 409 Conflict
{
  "error": "Conflict",
  "message": "Resource already exists."
}

// 500 Internal Server Error
{
  "error": "InternalError",
  "message": "An unexpected error occurred."
}
```

## Recovery Patterns

### 1. Retryable Errors

| Error | Retry Strategy |
|-------|----------------|
| Transient network (downstream) | Exponential backoff, max 3 retries |
| Database deadlock | Immediate retry, max 2 retries |
| Rate limited (429) | Respect `Retry-After` header |

### 2. Non-Retryable Errors

| Error | Action |
|-------|--------|
| 400 Bad Request | Fix request, don't retry |
| 401 Unauthorized | Re-authenticate, then retry |
| 403 Forbidden | Check permissions, don't retry |
| 404 Not Found | Resource gone, don't retry |
| 500 Internal Error | Alert ops, retry after fix |

### 3. Circuit Breaker (Consumer)

```csharp
// Polly example
var circuitBreaker = Policy
    .Handle<HttpRequestException>()
    .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));

await circuitBreaker.ExecuteAsync(() => _httpClient.GetAsync(url));
```

## Logging Errors

### Library: No Logging

The library emits **no logs**. Consumers must add logging.

### Consumer: Structured Error Logging

```csharp
app.Use(async (context, next) =>
{
    try { await next(); }
    catch (Exception ex)
    {
        _logger.LogError(ex, 
            "Request failed: {Method} {Path} | User: {User} | CorrelationId: {CorrelationId}",
            context.Request.Method,
            context.Request.Path,
            context.User?.Identity?.Name ?? "anonymous",
            context.TraceIdentifier);
        throw;
    }
});
```

### Log Fields for Errors

| Field | Value |
|-------|-------|
| `level` | `Error` or `Warning` |
| `message` | Human-readable description |
| `exception.type` | Full exception type name |
| `exception.message` | Exception message |
| `exception.stacktrace` | Full stack trace |
| `http.method` | GET, POST, etc. |
| `http.path` | Request path |
| `http.status_code` | Response status |
| `user.id` | Authenticated user ID |
| `correlation_id` | `HttpContext.TraceIdentifier` |
| `request.id` | Request ID (if available) |

## Testing Error Scenarios

### Unit Test: Null Request

```csharp
[Test]
public void ProcessRequest_ReturnsBadRequest_WhenRequestNull()
{
    var controller = new TestController();
    var result = controller.TestAction(null, NuciApiAuthorisation.None);
    
    Assert.IsInstanceOf<BadRequestObjectResult>(result);
    var error = (result as BadRequestObjectResult).Value as NuciApiErrorResponse;
    Assert.AreEqual(NuciApiErrorResponse.InvalidRequest, error);
}
```

### Unit Test: Authentication Failure

```csharp
[Test]
public void ProcessRequest_ThrowsAuthenticationException_WhenInvalidKey()
{
    var controller = new TestController();
    controller.ControllerContext = new ControllerContext
    {
        HttpContext = new DefaultHttpContext()
    };
    controller.Request.Headers["Authorization"] = "Bearer wrong-key";
    
    var ex = Assert.Throws<AuthenticationException>(() =>
        controller.TestAction(new TestRequest(), NuciApiAuthorisation.ApiKey("correct-key")));
    
    Assert.AreEqual("Invalid API key.", ex.Message);
}
```

### Integration Test: Full Pipeline

```csharp
[Test]
public async Task Endpoint_Returns401_WhenMissingAuth()
{
    var client = _factory.CreateClient();
    // No Authorization header
    
    var response = await client.PostAsJsonAsync("/api/test", new TestRequest());
    
    Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
    Assert.AreEqual("Missing authorisation data.", body.Error);
}
```

## Error Handling Checklist

- [ ] Exception middleware configured for 401/500
- [ ] Custom exception filter for business errors
- [ ] Structured error response format defined
- [ ] Error logging with correlation IDs
- [ ] No stack traces in production responses
- [ ] Retry policies for transient failures
- [ ] Circuit breakers for downstream dependencies
- [ ] Alerting on 5xx error rates
- [ ] Documentation of error codes for API consumers