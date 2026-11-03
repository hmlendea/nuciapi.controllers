# Behaviour: Request Processing

## Overview

This document specifies the exact behavioural contract for request processing in NuciAPI.Controllers. Every behaviour is testable and versioned.

## Behaviour Specification

### B-RP-001: Null Request Handling

**Given** a controller action calls `ProcessRequest(null, action, auth)`
**When** the request parameter is null
**Then** the method returns `BadRequest(NuciApiErrorResponse.InvalidRequest)`
**And** the action delegate is never invoked
**And** authorisation is never checked

```csharp
// Test
[Test]
public void NullRequest_Returns400_ActionNotInvoked()
{
    var invoked = false;
    var result = controller.ProcessRequest(null, () => invoked = true, auth);

    Assert.IsInstanceOf<BadRequestObjectResult>(result);
    Assert.IsFalse(invoked);
}
```

### B-RP-002: Null Authorisation Handling

**Given** a controller action calls `ProcessRequest(request, action, null)`
**When** the authorisation parameter is null
**Then** the method throws `NotImplementedException` with message "This endpoint has no authorisation specified."
**And** the action delegate is never invoked

```csharp
// Test
[Test]
public void NullAuthorisation_ThrowsNotImplementedException()
{
    var ex = Assert.Throws<NotImplementedException>(() =>
        controller.ProcessRequest(request, () => { }, null));

    Assert.AreEqual("This endpoint has no authorisation specified.", ex.Message);
}
```

### B-RP-003: Authorisation Failure

**Given** a controller action calls `ProcessRequest(request, action, auth)`
**When** `auth.Authorise(Request)` throws `AuthenticationException`
**Then** the exception propagates out of `ProcessRequest`
**And** the action delegate is never invoked
**And** the exception message is preserved

```csharp
// Test
[Test]
public void AuthorisationFailure_PropagatesException_ActionNotInvoked()
{
    var auth = new FailingAuthorisation();
    var invoked = false;

    var ex = Assert.Throws<AuthenticationException>(() =>
        controller.ProcessRequest(request, () => invoked = true, auth));

    Assert.IsFalse(invoked);
    Assert.AreEqual("Invalid API key.", ex.Message);
}
```

### B-RP-004: Successful Authorisation

**Given** a controller action calls `ProcessRequest(request, action, auth)`
**When** `auth.Authorise(Request)` completes without exception
**Then** the action delegate is invoked exactly once
**And** the result of the action determines the response

```csharp
// Test
[Test]
public void SuccessfulAuthorisation_InvokesActionOnce()
{
    var auth = NuciApiAuthorisation.None;
    var invokeCount = 0;

    controller.ProcessRequest(request, () => invokeCount++, auth);

    Assert.AreEqual(1, invokeCount);
}
```

### B-RP-005: Action Returns Value (Non-GET)

**Given** `ProcessRequest<TRequest, TResponse>` is called with non-GET method
**When** the action returns a non-null `TResponse`
**Then** the method returns `Ok(response)` (200)

```csharp
// Test
[Test]
public void PostAction_ReturnsValue_Returns200()
{
    controller.ControllerContext = new ControllerContext
    {
        HttpContext = new DefaultHttpContext { Request = { Method = "POST" } }
    };

    var result = controller.ProcessRequest(request, () => new Response { Id = 1 }, auth);

    Assert.IsInstanceOf<OkObjectResult>(result);
    Assert.AreEqual(1, ((Response)((OkObjectResult)result).Value).Id);
}
```

### B-RP-006: Action Returns Value (GET)

**Given** `ProcessRequest<TRequest, TResponse>` is called with GET method
**When** the action returns a non-null `TResponse`
**Then** the method returns `Ok(response)` (200)

```csharp
// Test
[Test]
public void GetAction_ReturnsValue_Returns200()
{
    controller.ControllerContext = new ControllerContext
    {
        HttpContext = new DefaultHttpContext { Request = { Method = "GET" } }
    };

    var result = controller.ProcessRequest(request, () => new Response { Id = 1 }, auth);

    Assert.IsInstanceOf<OkObjectResult>(result);
}
```

### B-RP-007: Action Returns Null (GET)

**Given** `ProcessRequest<TRequest, TResponse>` is called with GET method
**When** the action returns null
**Then** the method returns `NotFound(NuciApiErrorResponse.NotFound)` (404)

```csharp
// Test
[Test]
public void GetAction_ReturnsNull_Returns404()
{
    controller.ControllerContext = new ControllerContext
    {
        HttpContext = new DefaultHttpContext { Request = { Method = "GET" } }
    };

    var result = controller.ProcessRequest(request, () => (Response?)null, auth);

    Assert.IsInstanceOf<NotFoundObjectResult>(result);
    var error = (NuciApiErrorResponse)((NotFoundObjectResult)result).Value;
    Assert.AreEqual(NuciApiErrorResponse.NotFound, error);
}
```

### B-RP-008: Action Returns Null (Non-GET)

**Given** `ProcessRequest<TRequest, TResponse>` is called with non-GET method
**When** the action returns null
**Then** the method returns `Ok(null)` (200 with null body)

```csharp
// Test
[Test]
public void PostAction_ReturnsNull_Returns200WithNull()
{
    controller.ControllerContext = new ControllerContext
    {
        HttpContext = new DefaultHttpContext { Request = { Method = "POST" } }
    };

    var result = controller.ProcessRequest(request, () => (Response?)null, auth);

    Assert.IsInstanceOf<OkObjectResult>(result);
    Assert.IsNull(((OkObjectResult)result).Value);
}
```

### B-RP-009: Action Throws Exception

**Given** `ProcessRequest` is called
**When** the action delegate throws an exception
**Then** the exception propagates out of `ProcessRequest`
**And** no response wrapping occurs

```csharp
// Test
[Test]
public void ActionThrows_ExceptionPropagates()
{
    var ex = Assert.Throws<InvalidOperationException>(() =>
        controller.ProcessRequest(request, () => throw new InvalidOperationException("fail"), auth));

    Assert.AreEqual("fail", ex.Message);
}
```

### B-RP-010: Async Overload Behaviour

**Given** `ProcessRequestAsync` is called
**When** the async action delegate is awaited
**Then** all synchronous behaviours (B-RP-001 through B-RP-009) apply identically
**And** `ConfigureAwait(false)` is used on the await

```csharp
// Test
[Test]
public async Task Async_NullRequest_Returns400()
{
    var result = await controller.ProcessRequestAsync(null, () => Task.CompletedTask, auth);

    Assert.IsInstanceOf<BadRequestObjectResult>(result);
}
```

### B-RP-011: HMAC Token Extraction

**Given** `GetHmacToken()` is called
**When** the `X-HMAC` header is present
**Then** the header value is returned as string
**When** the `X-HMAC` header is absent
**Then** null is returned
**When** multiple `X-HMAC` headers exist
**Then** the first value is returned

```csharp
// Test
[Test]
public void GetHmacToken_HeaderPresent_ReturnsValue()
{
    controller.Request.Headers["X-HMAC"] = "signature123";

    var token = controller.GetHmacToken();

    Assert.AreEqual("signature123", token);
}

[Test]
public void GetHmacToken_HeaderAbsent_ReturnsNull()
{
    var token = controller.GetHmacToken();

    Assert.IsNull(token);
}
```

### B-RP-012: AuthoriseRequest Delegation

**Given** `AuthoriseRequest(auth)` is called
**When** auth is not null
**Then** `auth.Authorise(Request)` is called exactly once
**And** the returned task is returned

```csharp
// Test
[Test]
public void AuthoriseRequest_CallsAuthorisationAuthorise()
{
    var auth = new TrackingAuthorisation();

    controller.AuthoriseRequest(auth);

    Assert.AreEqual(1, auth.AuthoriseCallCount);
}
```

## Behaviour Matrix

| Scenario | Request | Auth | Action Returns | HTTP Method | Result |
|----------|---------|------|----------------|-------------|--------|
| B-RP-001 | null | any | N/A | any | 400 InvalidRequest |
| B-RP-002 | valid | null | N/A | any | NotImplementedException |
| B-RP-003 | valid | fails | N/A | any | AuthenticationException |
| B-RP-004 | valid | succeeds | value | any | 200 OK |
| B-RP-005 | valid | succeeds | value | POST/PUT/DELETE | 200 OK |
| B-RP-006 | valid | succeeds | value | GET | 200 OK |
| B-RP-007 | valid | succeeds | null | GET | 404 NotFound |
| B-RP-008 | valid | succeeds | null | POST/PUT/DELETE | 200 OK (null body) |
| B-RP-009 | valid | succeeds | throws | any | Exception propagates |
| B-RP-010 | (async) | (async) | (async) | any | Same as sync |
| B-RP-011 | N/A | N/A | N/A | N/A | Header value or null |
| B-RP-012 | N/A | valid | N/A | N/A | Calls auth.Authorise |

## Edge Cases

### E-RP-001: Request Method Null

**Given** `Request.Method` is null
**When** `ProcessRequest<TRequest, TResponse>` evaluates null response
**Then** treats as non-GET (returns 200 for null)

### E-RP-002: Custom HTTP Method

**Given** `Request.Method` is "PATCH" (or any non-GET)
**When** action returns null
**Then** returns 200 OK with null body

### E-RP-003: Action Returns Task<T> (Wrong Overload)

**Given** consumer passes `Func<Task<TResponse>>` to sync `ProcessRequest<TRequest, TResponse>`
**When** compiled
**Then** compiler error (type mismatch)

### E-RP-004: Generic Type Inference

**Given** `ProcessRequest(request, () => new Response(), auth)`
**When** called without explicit type args
**Then** compiler infers `TRequest` from request, `TResponse` from lambda return

## Versioning

| Behaviour | Since Version | Changed In | Notes |
|-----------|---------------|------------|-------|
| B-RP-001 | 1.0.0 | — | Core contract |
| B-RP-002 | 1.0.0 | — | Core contract |
| B-RP-003 | 1.0.0 | — | Core contract |
| B-RP-004 | 1.0.0 | — | Core contract |
| B-RP-005 | 1.0.0 | — | Core contract |
| B-RP-006 | 1.0.0 | — | Core contract |
| B-RP-007 | 1.0.0 | — | Core contract |
| B-RP-008 | 1.0.0 | — | Core contract |
| B-RP-009 | 1.0.0 | — | Core contract |
| B-RP-010 | 2.0.0 | 2.0.0 | Added async overloads |
| B-RP-011 | 1.0.0 | — | Core contract |
| B-RP-012 | 1.0.0 | — | Core contract |

## Test Coverage Requirements

| Behaviour | Unit Tests | Integration Tests |
|-----------|------------|-------------------|
| B-RP-001 | Required | Required |
| B-RP-002 | Required | Required |
| B-RP-003 | Required | Required |
| B-RP-004 | Required | Required |
| B-RP-005 | Required | Required |
| B-RP-006 | Required | Required |
| B-RP-007 | Required | Required |
| B-RP-008 | Required | Required |
| B-RP-009 | Required | Required |
| B-RP-010 | Required | Required |
| B-RP-011 | Required | Optional |
| B-RP-012 | Required | Optional |