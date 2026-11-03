# Application Services: Authorisation Strategies

## Purpose

The authorisation subsystem provides a Strategy pattern implementation for request authentication. Consumers select a strategy via factory methods on `NuciApiAuthorisation` and pass it to `ProcessRequest`.

## Location

- `NuciAPI.Controllers/NuciApiAuthorisation.cs` — Abstract base, factory methods, Bearer parsing
- `NuciAPI.Controllers/NuciApiKeyAuthorisation.cs` — API key validation strategy
- `NuciAPI.Controllers/NuciApiNoneAuthorisation.cs` — No-op strategy

## Public API Surface

### NuciApiAuthorisation (Abstract Base)

| Member | Type | Description |
|--------|------|-------------|
| `Name` | `string` (read-only) | Strategy identifier ("ApiKey", "None") |
| `None` | `static NuciApiNoneAuthorisation` | Factory: no-op authorisation |
| `ApiKey(string key)` | `static NuciApiKeyAuthorisation` | Factory: API key validation |
| `Authorise(string authorisationData)` | `void` | Entry point: parses Bearer token, delegates to `PerformAuthorisation` |
| `PerformAuthorisation(string authorisationData)` | `protected abstract void` | Strategy-specific validation |
| `ToString()` | `string` | Returns `"{Name}Authorisation"` |

### NuciApiKeyAuthorisation

| Member | Type | Description |
|--------|------|-------------|
| `Key` | `string` (read-only) | Configured API key (from factory) |
| `PerformAuthorisation(string apiKey)` | `protected override void` | Validates `Key.Equals(apiKey)`; throws `AuthenticationException` on mismatch |

### NuciApiNoneAuthorisation

| Member | Type | Description |
|--------|------|-------------|
| Constructor | `internal NuciApiNoneAuthorisation()` | Creates instance with `Name = "None"` |
| `PerformAuthorisation(string authorisationData)` | `protected override void` | No-op (always succeeds) |

## Execution Flow

```
NuciApiController.AuthoriseRequest(auth)
    │
    ▼
auth.Authorise(AuthorizationHeader)
    │
    ├── auth.Name == "None"? → return (success)
    │
    ├── AuthorizationHeader null/empty? → AuthenticationException("Missing authorisation data.")
    │
    └── Strip "Bearer " prefix (Regex ^Bearer\s+, IgnoreCase)
            │
            ▼
    auth.PerformAuthorisation(token)
            │
            ├── NuciApiKeyAuthorisation: Key.Equals(token)? → return : AuthenticationException("Invalid API key.")
            │
            └── NuciApiNoneAuthorisation: return (no-op)
```

## Key Implementation Details

### Bearer Token Parsing

```csharp
Regex.Replace(
    authorisationData.Trim(),
    @"^Bearer\s+", "",
    RegexOptions.IgnoreCase)
```

- Trims whitespace
- Removes "Bearer " prefix case-insensitively
- Passes remainder to `PerformAuthorisation`

### API Key Comparison

```csharp
if (!Key.Equals(apiKey))
{
    throw new AuthenticationException("Invalid API key.");
}
```

- Uses `string.Equals` (ordinal, case-sensitive)
- Not constant-time; vulnerable to timing attacks if keys are secret
- Throws `AuthenticationException` with generic message (no key leakage)

### None Strategy

```csharp
internal NuciApiNoneAuthorisation() : base("None") { }

protected override void PerformAuthorisation(string authorisationData)
{
    // No authorisation
}
```

- Internal constructor prevents direct instantiation
- Accessed only via `NuciApiAuthorisation.None` property
- `Authorise` method short-circuits for `Name == "None"`

## Extension: Custom Authorisation Strategy

```csharp
public class JwtAuthorisation : NuciApiAuthorisation
{
    public JwtAuthorisation() : base("Jwt") { }

    protected override void PerformAuthorisation(string token)
    {
        // Validate JWT signature, claims, expiration
        if (!ValidateJwt(token))
            throw new AuthenticationException("Invalid JWT");
    }
}

// Usage:
return ProcessRequest(request, action, new JwtAuthorisation());
```

**Requirements:**
- Derive from `NuciApiAuthorisation`
- Call `base(name)` in constructor
- Implement `PerformAuthorisation(string)`
- Throw `AuthenticationException` on failure

## Security Considerations

| Aspect | Current Implementation | Gap |
|--------|------------------------|-----|
| Timing attacks | `string.Equals` (not constant-time) | API key comparison vulnerable |
| Token exposure | Generic error messages | No key/token in exceptions |
| Scheme flexibility | Hard-coded Bearer only | New scheme = new strategy class |
| Authorisation (permissions) | Not implemented | Consumer responsibility |
| Rate limiting | Not implemented | Consumer responsibility |

## Dependencies

- `System.Security.Authentication.AuthenticationException` (thrown on failure)
- `System.Text.RegularExpressions.Regex` (Bearer parsing)
- `NuciApiAuthorisation` base class (for concrete strategies)