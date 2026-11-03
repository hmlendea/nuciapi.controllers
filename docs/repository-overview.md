# Repository Overview

## Purpose

This repository contains **NuciAPI.Controllers** - a .NET library providing base controller classes and authorisation abstractions for building secure ASP.NET Core REST APIs.

## What This Library Does

| Feature | Description |
|---------|-------------|
| **NuciApiController** | Abstract base controller with `ProcessRequest`/`ProcessRequestAsync` overloads for consistent request handling |
| **NuciApiAuthorisation** | Abstract authorisation base with factory methods for `None` and `ApiKey` strategies |
| **HMAC Support** | `GetHmacToken()` for extracting HMAC signatures from headers |
| **Error Responses** | Standardised `NuciApiErrorResponse` integration (from NuciAPI package) |

## What This Library Does NOT Do

| Feature | Status | Alternative |
|---------|--------|-------------|
| Data access / ORM | ❌ | EF Core, Dapper |
| Business logic | ❌ | Consumer implements |
| Authentication (identity) | ❌ | ASP.NET Core Identity, IdentityServer |
| Rate limiting | ❌ | AspNetCoreRateLimit, custom |
| CORS | ❌ | ASP.NET Core CORS middleware |
| Swagger/OpenAPI | ❌ | Swashbuckle.AspNetCore |
| Logging framework | ❌ | Serilog, built-in ILogger |
| Configuration system | ❌ | Consumer uses IConfiguration |

## Target Audience

- **Primary:** .NET developers building REST APIs with ASP.NET Core
- **Secondary:** Teams standardising on NuciAPI ecosystem
- **Use Case:** APIs requiring consistent request processing, API key auth, HMAC validation

## Key Benefits

1. **Consistency** - Uniform request/response handling across controllers
2. **Security** - Built-in API key and HMAC authorisation patterns
3. **Simplicity** - Minimal API surface, no configuration complexity
4. **Performance** - Zero overhead, stateless, async-first
5. **Testability** - Abstract base classes, easy to mock

## Technology Stack

| Component | Version |
|-----------|---------|
| .NET | 10.0 |
| ASP.NET Core | 10.0 (via Microsoft.AspNetCore.App) |
| NuciAPI | 3.5.1 |
| Language | C# 13 (LangVersion latest) |
| Nullable | Enabled |
| ImplicitUsings | Disabled |

## Package Information

| Property | Value |
|----------|-------|
| Package ID | NuciAPI.Controllers |
| Current Version | 2.3.1 |
| License | GPL-3.0-or-later |
| Authors | Horațiu Mlendea |
| Repository | https://github.com/hmlendea/nuciapi.controllers |
| NuGet | https://www.nuget.org/packages/NuciAPI.Controllers |

## Repository Structure

```
nuciapi.controllers/
├── .github/
│   └── workflows/
│       └── dotnet.yml          # CI/CD pipeline
├── NuciAPI.Controllers/        # Main library project
│   ├── NuciAPI.Controllers.csproj
│   ├── NuciApiController.cs    # Base controller
│   ├── NuciApiAuthorisation.cs # Auth base + factories
│   ├── NuciApiKeyAuthorisation.cs
│   ├── NuciApiNoneAuthorisation.cs
│   └── obj/                    # Build artifacts (gitignored)
├── docs/                       # Technical documentation
│   ├── INDEX.md
│   ├── architecture.md
│   ├── components/
│   ├── flows/
│   ├── api-reference/
│   ├── guides/
│   └── ... (30+ files)
├── NuciAPI.Controllers.slnx    # SDK-style solution
├── README.md                   # Project overview
├── PRIVACY.md                  # Privacy statement
├── SECURITY.md                 # Security policy
├── CONTRIBUTING.md             # Contribution guide
├── CODE_OF_CONDUCT.md          # Community standards
├── ARCHITECTURE.md             # Architecture overview
├── LICENSE                     # GPL-3.0-or-later
└── .gitignore
```

## Development Workflow

### Branching

```
dev (default) ← feature/* ← PR
  ↓
main (releases only)
```

### Versioning

- **Scheme:** Semantic Versioning (MAJOR.MINOR.PATCH)
- **Current:** 2.3.1
- **Next:** 2.4.0 (minor) or 3.0.0 (major)

### Release Process

1. Update version in `.csproj`
2. Update `CHANGELOG.md` (if exists)
3. Create PR to `main`
4. Tag release: `git tag v2.3.1`
5. GitHub Action publishes to NuGet

## Quality Gates

| Check | Tool | Threshold |
|-------|------|-----------|
| Build | `dotnet build` | Zero errors |
| Tests | `dotnet test` | 100% pass |
| Coverage | coverlet | ≥ 80% lines |
| Static Analysis | Roslyn analyzers | Zero warnings |
| Security | `dotnet list package --vulnerable` | Zero critical |

## Dependencies

### Direct

| Package | Version | Purpose |
|---------|---------|---------|
| Microsoft.AspNetCore.App | 10.0 (framework) | ASP.NET Core APIs |
| NuciAPI | 3.5.1 | Error responses, core types |

### Transitive (Key)

| Package | Version | Via |
|---------|---------|-----|
| System.Text.Json | 10.0 | ASP.NET Core |
| Microsoft.Extensions.* | 10.0 | ASP.NET Core |

## Compatibility

| Consumer Target | Supported Version |
|-----------------|-------------------|
| net10.0 | 2.3.x (current) |
| net9.0 | 2.2.x (maintenance) |
| net8.0 | 2.1.x (maintenance) |
| net7.0 | 1.x (EOL) |

## Support Policy

| Version | Status | Support Until |
|---------|--------|---------------|
| 2.3.x | Active | 2027-11 |
| 2.2.x | Security only | 2027-09 |
| 2.1.x | Security only | 2027-08 |
| 1.x | EOL | 2026-12 |

## Links

- **Source:** https://github.com/hmlendea/nuciapi.controllers
- **NuGet:** https://www.nuget.org/packages/NuciAPI.Controllers
- **Issues:** https://github.com/hmlendea/nuciapi.controllers/issues
- **Discussions:** https://github.com/hmlendea/nuciapi.controllers/discussions
- **Security:** security@nuciapi.com