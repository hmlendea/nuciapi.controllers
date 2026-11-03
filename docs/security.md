# Security Model

This document elaborates on the root [SECURITY.md](../SECURITY.md) with implementation-level detail.

## Authentication vs Authorisation

| Concept | Library Support | Consumer Responsibility |
|---------|-----------------|------------------------|
| **Authentication** (identity verification) | ✅ Provided via `NuciApiAuthorisation` strategies | Select strategy, manage secrets |
| **Authorisation** (permission checks) | ❌ Not implemented | Implement in action logic or middleware |

The library verifies **who** is calling (authentication). The consumer decides **what** they can do (authorisation).

## Supported Authentication Strategies

### 1. API Key (`NuciApiKeyAuthorisation`)

```csharp
NuciApiAuthorisation.ApiKey("secret-key")
```

**Mechanism:**
- Consumer provides key at call site
- `Authorization: Bearer <key>` header required
- Case-sensitive string comparison (`Key.Equals(token)`)
- Throws `AuthenticationException("Invalid API key.")` on mismatch

**Security Properties:**
| Property | Status | Notes |
|----------|--------|-------|
| Timing-safe | ❌ | `string.Equals` vulnerable to timing attacks |
| Key in logs | ✅ No | Generic error messages |
| Key rotation | Consumer | Library has no key management |
| Transport security | Consumer | Requires HTTPS (ASP.NET Core) |

### 2. None (`NuciApiNoneAuthorisation`)

```csharp
NuciApiAuthorisation.None
```

**Mechanism:**
- No header validation
- Short-circuits in `NuciApiAuthorisation.Authorise`
- Always succeeds

**Use Cases:**
- Public endpoints
- Health checks
- Development/testing

### 3. Custom Strategies

```csharp
public class JwtAuthorisation : NuciApiAuthorisation
{
    public JwtAuthorisation() : base("Jwt") { }

    protected override void PerformAuthorisation(string token)
    {
        // Validate JWT: signature, claims, expiration
    }
}
```

**Consumer Controls:**
- Token format (JWT, opaque, certificate)
- Validation logic (signature, claims, introspection)
- Error messages
- Caching/revocation

## Threat Model

### Assets
- API keys (consumer-managed)
- HMAC tokens (request-scoped)
- Request/response data (consumer-defined)

### Threats & Mitigations

| Threat | Library Mitigation | Consumer Mitigation |
|--------|-------------------|---------------------|
| Credential theft | Generic error messages | HTTPS, key rotation, short expiry |
| Timing attacks | ❌ None | Constant-time comparison in custom strategy |
| Replay attacks | ❌ None | Nonce/timestamp in HMAC, validate in action |
| Brute force | ❌ None | Rate limiting middleware |
| Man-in-the-middle | ❌ None | HTTPS enforcement (HSTS) |
| Header injection | `FirstOrDefault()` single value | Input validation |

### Trust Boundaries

```
┌─────────────────────────────────────────────────────┐
│              Consumer Process                       │
│  ┌─────────────────────────────────────────────┐   │
│  │ ASP.NET Core Pipeline                       │   │
│  │  ┌─────────────────────────────────────┐   │   │
│  │  │ NuciAPI.Controllers (Library)       │   │   │
│  │  │  - No secrets stored                │   │   │
│  │  │  - No external calls                │   │   │
│  │  │  - In-memory only                   │   │   │
│  │  └─────────────────────────────────────┘   │   │
│  └─────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────┘
```

- Library executes entirely in consumer's process
- No separate trust boundary
- Consumer controls network exposure, TLS, hosting

## Secret Management

### Library Behaviour
- **No secret storage**: Keys passed at call site, not persisted
- **No configuration reading**: No `IConfiguration`, no env var access
- **No logging**: Keys never logged

### Consumer Responsibilities
```csharp
// Good: Key from Key Vault
var key = await _keyVaultClient.GetSecretAsync("api-key");

// Good: Key from environment
var key = Environment.GetEnvironmentVariable("API_KEY")
    ?? throw new InvalidOperationException("API_KEY not set");

// Bad: Hard-coded
var key = "hardcoded-secret"; // NEVER DO THIS
```

## HMAC Security

### Library Role
- Extracts `X-HMAC` header
- URL-decodes value
- Assigns to `request.HmacToken`
- **No validation performed**

### Consumer Validation Required
```csharp
protected override ActionResult ProcessRequest<TRequest, TResponse>(
    TRequest request, Func<TResponse> action, NuciApiAuthorisation auth)
{
    // Validate HMAC before base processing
    if (!ValidateHmac(request.HmacToken, request))
        return Unauthorized();

    return base.ProcessRequest(request, action, auth);
}

private bool ValidateHmac(string hmacToken, TRequest request)
{
    if (string.IsNullOrEmpty(hmacToken)) return false;

    // Verify signature, timestamp, nonce
    var expected = ComputeHmac(request, _hmacKey);
    return CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(hmacToken),
        Encoding.UTF8.GetBytes(expected));
}
```

## Error Information Disclosure

### Library Errors
| Exception | Message | Information Disclosed |
|-----------|---------|----------------------|
| `NotImplementedException` | "This endpoint has no authorisation specified." | Programming error only |
| `AuthenticationException` | "Missing authorisation data." | Header absence only |
| `AuthenticationException` | "Invalid API key." | Key validity only (not key value) |

### Consumer Should
- Wrap in exception middleware
- Return generic 401 without stack traces in production
- Log full details server-side only

## Compliance Considerations

| Standard | Library Impact | Consumer Action |
|----------|----------------|-----------------|
| GDPR | No personal data processed | Document data flows |
| PCI DSS | No card data handled | Segregate payment endpoints |
| SOC 2 | No audit logging | Add audit middleware |
| HIPAA | No PHI handled | Encrypt PHI at application layer |

## Security Checklist for Consumers

- [ ] Use HTTPS only (enforce via HSTS)
- [ ] Store API keys in Key Vault / secret manager
- [ ] Rotate keys regularly
- [ ] Implement rate limiting
- [ ] Validate HMAC in custom base controller
- [ ] Configure exception middleware (no stack traces in prod)
- [ ] Add audit logging for auth events
- [ ] Implement authorisation (permissions) in actions
- [ ] Use constant-time comparison for custom strategies
- [ ] Monitor for authentication failures