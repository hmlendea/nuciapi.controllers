# Behaviour: Authorisation

## Overview

This document specifies the exact behavioural contract for authorisation strategies in NuciAPI.Controllers.

## Behaviour Specification

### B-AUTH-001: None Authorisation

**Given** `NuciApiAuthorisation.None` is used
**When** `Authorise(HttpRequest)` is called
**Then** the method returns a completed task immediately
**And** no headers are inspected
**And** no exceptions are thrown

```csharp
// Test
[Test]
public async Task NoneAuthorisation_AlwaysSucceeds()
{
    var auth = NuciApiAuthorisation.None;
    var request = new DefaultHttpContext().Request;

    await auth.Authorise(request); // No exception
}
```

### B-AUTH-002: ApiKey Authorisation - Valid Key

**Given** `NuciApiAuthorisation.ApiKey("secret-key")` is used
**When** `Authorise(HttpRequest)` is called with `Authorization: Bearer secret-key` header
**Then** the method returns a completed task
**And** no exception is thrown

```csharp
// Test
[Test]
public async Task ApiKeyAuthorisation_ValidKey_Succeeds()
{
    var auth = NuciApiAuthorisation.ApiKey("secret-key");
    var request = new DefaultHttpContext().Request;
    request.Headers["Authorization"] = "Bearer secret-key";

    await auth.Authorise(request); // No exception
}
```

### B-AUTH-003: ApiKey Authorisation - Missing Header

**Given** `NuciApiAuthorisation.ApiKey("secret-key")` is used
**When** `Authorise(HttpRequest)` is called with no `Authorization` header
**Then** the method throws `AuthenticationException` with message "Missing authorisation data."

```csharp
// Test
[Test]
public async Task ApiKeyAuthorisation_MissingHeader_Throws()
{
    var auth = NuciApiAuthorisation.ApiKey("secret-key");
    var request = new DefaultHttpContext().Request;
    // No Authorization header

    var ex = await Assert.ThrowsAsync<AuthenticationException>(() =>
        auth.Authorise(request));

    Assert.AreEqual("Missing authorisation data.", ex.Message);
}
```

### B-AUTH-004: ApiKey Authorisation - Wrong Scheme

**Given** `NuciApiAuthorisation.ApiKey("secret-key")` is used
**When** `Authorise(HttpRequest)` is called with `Authorization: Basic ...` or other non-Bearer scheme
**Then** the method throws `AuthenticationException` with message "Missing authorisation data."

```csharp
// Test
[Test]
public async Task ApiKeyAuthorisation_WrongScheme_Throws()
{
    var auth = NuciApiAuthorisation.ApiKey("secret-key");
    var request = new DefaultHttpContext().Request;
    request.Headers["Authorization"] = "Basic dXNlcjpwYXNz";

    var ex = await Assert.ThrowsAsync<AuthenticationException>(() =>
        auth.Authorise(request));

    Assert.AreEqual("Missing authorisation data.", ex.Message);
}
```

### B-AUTH-005: ApiKey Authorisation - Invalid Key

**Given** `NuciApiAuthorisation.ApiKey("secret-key")` is used
**When** `Authorise(HttpRequest)` is called with `Authorization: Bearer wrong-key`
**Then** the method throws `AuthenticationException` with message "Invalid API key."

```csharp
// Test
[Test]
public async Task ApiKeyAuthorisation_InvalidKey_Throws()
{
    var auth = NuciApiAuthorisation.ApiKey("secret-key");
    var request = new DefaultHttpContext().Request;
    request.Headers["Authorization"] = "Bearer wrong-key";

    var ex = await Assert.ThrowsAsync<AuthenticationException>(() =>
        auth.Authorise(request));

    Assert.AreEqual("Invalid API key.", ex.Message);
}
```

### B-AUTH-006: ApiKey Authorisation - Empty Key

**Given** `NuciApiAuthorisation.ApiKey("")` is used (empty key)
**When** `Authorise(HttpRequest)` is called with `Authorization: Bearer ` (empty token)
**Then** the method returns a completed task (empty string equals empty string)

```csharp
// Test
[Test]
public async Task ApiKeyAuthorisation_EmptyKey_MatchesEmptyToken()
{
    var auth = NuciApiAuthorisation.ApiKey("");
    var request = new DefaultHttpContext().Request;
    request.Headers["Authorization"] = "Bearer ";

    await auth.Authorise(request); // No exception - empty matches empty
}
```

### B-AUTH-007: ApiKey Authorisation - Case Sensitivity

**Given** `NuciApiAuthorisation.ApiKey("Secret-Key")` is used
**When** `Authorise(HttpRequest)` is called with `Authorization: Bearer secret-key` (different case)
**Then** the method throws `AuthenticationException` with message "Invalid API key."

```csharp
// Test
[Test]
public async Task ApiKeyAuthorisation_CaseSensitive()
{
    var auth = NuciApiAuthorisation.ApiKey("Secret-Key");
    var request = new DefaultHttpContext().Request;
    request.Headers["Authorization"] = "Bearer secret-key";

    var ex = await Assert.ThrowsAsync<AuthenticationException>(() =>
        auth.Authorise(request));

    Assert.AreEqual("Invalid API key.", ex.Message);
}
```

### B-AUTH-008: ApiKey Authorisation - Whitespace Handling

**Given** `NuciApiAuthorisation.ApiKey("secret-key")` is used
**When** `Authorise(HttpRequest)` is called with `Authorization: Bearer  secret-key` (extra space)
**Then** the method throws `AuthenticationException` (token includes leading space)

```csharp
// Test
[Test]
public async Task ApiKeyAuthorisation_ExtraWhitespace_Fails()
{
    var auth = NuciApiAuthorisation.ApiKey("secret-key");
    var request = new DefaultHttpContext().Request;
    request.Headers["Authorization"] = "Bearer  secret-key"; // Two spaces

    var ex = await Assert.ThrowsAsync<AuthenticationException>(() =>
        auth.Authorise(request));

    Assert.AreEqual("Invalid API key.", ex.Message);
}
```

### B-AUTH-009: Custom Authorisation - Success

**Given** a custom `NuciApiAuthorisation` implementation
**When** `Authorise(HttpRequest)` completes without exception
**Then** the request is considered authorised
**And** processing continues

```csharp
// Test
public class CustomAuth : NuciApiAuthorisation
{
    public override Task Authorise(HttpRequest request) => Task.CompletedTask;
}

[Test]
public async Task CustomAuthorisation_Success_Continues()
{
    var auth = new CustomAuth();
    var request = new DefaultHttpContext().Request;

    await auth.Authorise(request); // No exception
}
```

### B-AUTH-010: Custom Authorisation - Failure

**Given** a custom `NuciApiAuthorisation` implementation
**When** `Authorise(HttpRequest)` throws `AuthenticationException`
**Then** the exception propagates
**And** request processing stops

```csharp
// Test
public class FailingAuth : NuciApiAuthorisation
{
    public override Task Authorise(HttpRequest request)
        => throw new AuthenticationException("Custom failure");
}

[Test]
public async Task CustomAuthorisation_Failure_Propagates()
{
    var auth = new FailingAuth();
    var request = new DefaultHttpContext().Request;

    var ex = await Assert.ThrowsAsync<AuthenticationException>(() =>
        auth.Authorise(request));

    Assert.AreEqual("Custom failure", ex.Message);
}
```

### B-AUTH-011: Authorisation Factory Methods

**Given** `NuciApiAuthorisation.None`
**Then** returns the singleton `NuciApiNoneAuthorisation.None` instance

**Given** `NuciApiAuthorisation.ApiKey("key")`
**Then** returns a new `NuciApiKeyAuthorisation` instance with `Key = "key"`

```csharp
// Test
[Test]
public void None_ReturnsSingleton()
{
    var auth1 = NuciApiAuthorisation.None;
    var auth2 = NuciApiAuthorisation.None;

    Assert.AreSame(auth1, auth2);
    Assert.IsInstanceOf<NuciApiNoneAuthorisation>(auth1);
}

[Test]
public void ApiKey_ReturnsNewInstance()
{
    var auth1 = NuciApiAuthorisation.ApiKey("key");
    var auth2 = NuciApiAuthorisation.ApiKey("key");

    Assert.AreNotSame(auth1, auth2);
    Assert.IsInstanceOf<NuciApiKeyAuthorisation>(auth1);
    Assert.AreEqual("key", ((NuciApiKeyAuthorisation)auth1).Key);
}
```

### B-AUTH-012: Key Property Immutability

**Given** `NuciApiKeyAuthorisation` instance
**When** constructed with key
**Then** `Key` property is readonly and cannot be changed

```csharp
// Test
[Test]
public void ApiKeyAuthorisation_KeyIsReadonly()
{
    var auth = new NuciApiKeyAuthorisation("key");

    Assert.AreEqual("key", auth.Key);
    // No setter - compile-time enforcement
}
```

## Behaviour Matrix

| Scenario | Authorisation | Header | Result |
|----------|---------------|--------|--------|
| B-AUTH-001 | None | Any | Success |
| B-AUTH-002 | ApiKey("key") | Bearer key | Success |
| B-AUTH-003 | ApiKey("key") | Missing | AuthenticationException("Missing authorisation data.") |
| B-AUTH-004 | ApiKey("key") | Basic/Other | AuthenticationException("Missing authorisation data.") |
| B-AUTH-005 | ApiKey("key") | Bearer wrong | AuthenticationException("Invalid API key.") |
| B-AUTH-006 | ApiKey("") | Bearer (empty) | Success |
| B-AUTH-007 | ApiKey("Key") | Bearer key | AuthenticationException("Invalid API key.") |
| B-AUTH-008 | ApiKey("key") | Bearer␣␣key | AuthenticationException("Invalid API key.") |
| B-AUTH-009 | Custom (success) | Any | Success |
| B-AUTH-010 | Custom (fail) | Any | AuthenticationException(custom message) |
| B-AUTH-011 | Factory methods | N/A | Correct instances |
| B-AUTH-012 | Key property | N/A | Readonly |

## Edge Cases

### E-AUTH-001: Multiple Authorization Headers

**Given** multiple `Authorization` headers
**When** `ApiKeyAuthorisation` reads header
**Then** ASP.NET Core returns first value (standard behavior)

### E-AUTH-002: Authorization Header with Comma

**Given** `Authorization: Bearer key1, key2`
**When** parsed
**Then** token is `"key1, key2"` (full value after "Bearer ")

### E-AUTH-003: Null Request in Authorise

**Given** `Authorise(null)` called
**When** custom authorisation accesses `request.Headers`
**Then** `NullReferenceException` (consumer bug, not library concern)

### E-AUTH-004: Key with Special Characters

**Given** `ApiKey("key-with_underscore.and.dots")`
**When** matched against same header
**Then** succeeds (exact string match)

## Versioning

| Behaviour | Since Version | Changed In | Notes |
|-----------|---------------|------------|-------|
| B-AUTH-001 | 1.0.0 | — | Core contract |
| B-AUTH-002 | 1.0.0 | — | Core contract |
| B-AUTH-003 | 1.0.0 | — | Core contract |
| B-AUTH-004 | 1.0.0 | — | Core contract |
| B-AUTH-005 | 1.0.0 | — | Core contract |
| B-AUTH-006 | 1.0.0 | — | Edge case |
| B-AUTH-007 | 1.0.0 | — | Security: case-sensitive |
| B-AUTH-008 | 1.0.0 | — | Strict parsing |
| B-AUTH-009 | 1.0.0 | — | Extensibility |
| B-AUTH-010 | 1.0.0 | — | Extensibility |
| B-AUTH-011 | 1.0.0 | — | Factory pattern |
| B-AUTH-012 | 1.0.0 | — | Immutability |

## Test Coverage Requirements

| Behaviour | Unit Tests | Integration Tests |
|-----------|------------|-------------------|
| B-AUTH-001 | Required | Optional |
| B-AUTH-002 | Required | Required |
| B-AUTH-003 | Required | Required |
| B-AUTH-004 | Required | Required |
| B-AUTH-005 | Required | Required |
| B-AUTH-006 | Required | Optional |
| B-AUTH-007 | Required | Required |
| B-AUTH-008 | Required | Optional |
| B-AUTH-009 | Required | Optional |
| B-AUTH-010 | Required | Optional |
| B-AUTH-011 | Required | Optional |
| B-AUTH-012 | Required | Optional |

## Security Considerations

| Behaviour | Security Implication |
|-----------|---------------------|
| B-AUTH-003/004 | Generic error message prevents header enumeration |
| B-AUTH-005 | Generic error message prevents key enumeration |
| B-AUTH-007 | Case sensitivity prevents confusion attacks |
| B-AUTH-008 | Strict parsing prevents bypass via whitespace |
| B-AUTH-006 | Empty key allowed (consumer choice, not recommended) |

**Note:** Library does not use constant-time comparison. Consumer should use `CryptographicOperations.FixedTimeEquals` for high-security scenarios.