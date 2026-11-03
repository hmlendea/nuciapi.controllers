# Change Guide

## Overview

This document guides consumers through upgrading NuciAPI.Controllers versions. It covers breaking changes, migration steps, and compatibility information.

## Version History

| Version | Release Date | .NET | NuciAPI | Breaking Changes |
|---------|--------------|------|---------|------------------|
| 2.3.1 | 2026-11 | 10.0 | 3.5.1 | No |
| 2.3.0 | 2026-10 | 10.0 | 3.5.0 | No |
| 2.2.0 | 2026-09 | 9.0 | 3.4.0 | Yes (net9.0) |
| 2.1.0 | 2026-08 | 8.0 | 3.3.0 | Yes (net8.0) |
| 2.0.0 | 2026-07 | 8.0 | 3.2.0 | Yes (async overloads) |
| 1.0.0 | 2026-06 | 8.0 | 3.1.0 | Initial |

## Upgrading to 2.3.x (Current)

### From 2.2.x (net9.0 → net10.0)

**Breaking:** Target framework changed from net9.0 to net10.0.

**Migration Steps:**

1. Update project to .NET 10:
   ```xml
   <TargetFramework>net10.0</TargetFramework>
   ```

2. Update NuciAPI dependency:
   ```xml
   <PackageReference Include="NuciAPI" Version="3.5.1" />
   ```

3. Update `global.json` (if used):
   ```json
   { "sdk": { "version": "10.0.100" } }
   ```

4. Rebuild and test.

**No code changes required** - API surface unchanged.

### From 2.1.x (net8.0 → net10.0)

**Breaking:** Two major version jumps (net8.0 → net9.0 → net10.0).

**Migration Steps:**

1. Follow net9.0 migration (see 2.2.x section)
2. Follow net10.0 migration (see 2.3.x section)
3. Review .NET 9 and 10 breaking changes:
   - [.NET 9 Breaking Changes](https://learn.microsoft.com/dotnet/core/compatibility/9.0)
   - [.NET 10 Breaking Changes](https://learn.microsoft.com/dotnet/core/compatibility/10.0)

## Upgrading to 2.0.0 (Async Overloads Added)

### From 1.x to 2.0

**Breaking:** New async overloads added. No existing API removed.

**New Methods (2.0.0):**
```csharp
// Async, no response
Task<ActionResult> ProcessRequestAsync<TRequest>(TRequest request, Func<Task> action, NuciApiAuthorisation authorisation)

// Async, typed response
Task<ActionResult> ProcessRequestAsync<TRequest, TResponse>(TRequest request, Func<Task<TResponse>> action, NuciApiAuthorisation authorisation)
```

**Migration:** Optional. Existing sync code continues to work.

**Recommended:** Migrate to async for I/O-bound operations:
```csharp
// Before (sync)
public ActionResult GetData(Request request)
{
    return ProcessRequest(request, () => _service.GetData(), auth);
}

// After (async)
public async Task<ActionResult> GetData(Request request)
{
    return await ProcessRequestAsync(request, () => _service.GetDataAsync(), auth);
}
```

## Breaking Change Categories

### Category 1: Target Framework Change

| Version | From | To | Impact |
|---------|------|-----|--------|
| 2.3.0 | net9.0 | net10.0 | Recompile required |
| 2.2.0 | net8.0 | net9.0 | Recompile required |
| 2.1.0 | net7.0 | net8.0 | Recompile required |

**Action:** Update `<TargetFramework>`, rebuild.

### Category 2: Dependency Version Change

| Version | NuciAPI | Impact |
|---------|---------|--------|
| 2.3.0 | 3.5.0 → 3.5.1 | Patch, no breaking changes |
| 2.2.0 | 3.3.x → 3.4.0 | Minor, check NuciAPI changelog |
| 2.0.0 | 3.1.x → 3.2.0 | Minor, check NuciAPI changelog |

**Action:** Update PackageReference, rebuild.

### Category 3: API Surface Change

| Version | Change | Impact |
|---------|--------|--------|
| 2.0.0 | Added async overloads | None (additive) |
| 3.0.0 (planned) | ProblemDetails, CancellationToken | High |

**Action:** Review release notes, test.

## Migration Checklist

### Pre-Upgrade

- [ ] Read release notes for target version
- [ ] Check NuciAPI changelog for dependency version
- [ ] Review .NET version breaking changes
- [ ] Run full test suite on current version
- [ ] Backup / commit current state

### Upgrade

- [ ] Update `<TargetFramework>` in csproj
- [ ] Update `NuciAPI` PackageReference
- [ ] Update `global.json` SDK version
- [ ] Run `dotnet restore`
- [ ] Run `dotnet build`
- [ ] Run `dotnet test`

### Post-Upgrade

- [ ] Run integration tests
- [ ] Test all authorisation flows
- [ ] Test HMAC extraction
- [ ] Test error responses
- [ ] Verify Swagger/OpenAPI generation
- [ ] Deploy to staging
- [ ] Monitor error rates

## Compatibility Matrix

| Consumer .NET | NuciAPI.Controllers | NuciAPI | Status |
|---------------|---------------------|---------|--------|
| net10.0 | 2.3.x | 3.5.x | Supported |
| net9.0 | 2.2.x | 3.4.x | Maintenance |
| net8.0 | 2.1.x | 3.3.x | Maintenance |
| net7.0 | 1.x | 3.1.x | EOL |

**Policy:** Each major version supports one .NET LTS + current.

## Deprecation Notices

### Current Deprecations

| API | Since | Removal | Replacement |
|-----|-------|---------|-------------|
| Sync `ProcessRequest` | 2.0 | 3.0 (planned) | Async `ProcessRequestAsync` |

### Planned Deprecations (v3.0)

| API | Reason | Replacement |
|-----|--------|-------------|
| `NuciApiErrorResponse` | RFC 7807 compliance | `ProblemDetails` |
| Sync overloads | Async-first | Async only |
| `NotImplementedException` for missing auth | Better default | `NuciApiAuthorisation.None` default |

## Rollback Procedure

If upgrade causes issues:

```bash
# 1. Revert csproj changes
git checkout HEAD -- NuciAPI.Controllers.csproj

# 2. Restore packages
dotnet restore

# 3. Rebuild
dotnet build

# 4. If NuGet package published, unlist
dotnet nuget delete NuciAPI.Controllers <version> --api-key $KEY
```

## Testing Upgrades

### Automated Compatibility Test

```csharp
// Test project targeting multiple frameworks
<TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>

// CI matrix tests all frameworks
```

### Consumer Upgrade Test

```bash
# In consumer repo
dotnet add package NuciAPI.Controllers --version 2.3.1
dotnet build
dotnet test
```

## Support Timeline

| Version | Release | Active Support | Security Fixes | EOL |
|---------|---------|----------------|----------------|-----|
| 2.3.x | 2026-11 | Yes | Yes | 2027-11 |
| 2.2.x | 2026-09 | No | Yes (critical) | 2027-09 |
| 2.1.x | 2026-08 | No | Yes (critical) | 2027-08 |
| 1.x | 2026-06 | No | No | 2026-12 |

**Active support:** Current major version only.
**Security fixes:** Current + previous major for 12 months.

## Getting Help

- **GitHub Issues:** Bug reports, migration questions
- **Discussions:** Design questions, best practices
- **NuGet:** Package-specific issues
- **Email:** security@nuciapi.com (security only)