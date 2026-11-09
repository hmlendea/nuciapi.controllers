[![Donate](https://img.shields.io/badge/-%E2%99%A5%20Donate-%23ff69b4)](https://hmlendea.go.ro/funding)
[![Latest Release](https://img.shields.io/github/v/release/hmlendea/nuciapi.controllers)](https://github.com/hmlendea/nuciapi.controllers/releases/latest)
[![Build Status](https://github.com/hmlendea/nuciapi.controllers/actions/workflows/dotnet.yml/badge.svg)](https://github.com/hmlendea/nuciapi.controllers/actions/workflows/dotnet.yml)
[![Security Scan](https://github.com/hmlendea/nuciapi.controllers/actions/workflows/security.yml/badge.svg)](https://github.com/hmlendea/nuciapi.controllers/actions/workflows/security.yml)
[![NuGet](https://img.shields.io/nuget/v/NuciAPI.Controllers)](https://nuget.org/packages/NuciAPI.Controllers)
[![License](https://img.shields.io/github/license/hmlendea/nuciapi.controllers)](https://github.com/hmlendea/nuciapi.controllers/blob/master/LICENSE)

# NuciAPI.Controllers

NuciAPI.Controllers provides base controller classes and authorisation abstractions for building REST APIs with ASP.NET Core.

## 📑 Table of Contents

- [Capabilities](#-capabilities)
- [Usage](#-usage)
- [Installation](#-installation)
  - [Package Manager Installation](#-package-manager-installation)
- [Compatibility](#-compatibility)
- [Integrations](#-integrations)
- [Authentication and Authorisation](#-authentication-and-authorisation)
- [Extensibility](#-extensibility)
- [Privacy and Data](#-privacy-and-data)
- [Development](#-development)
- [Architecture](#-architecture)
- [Documentation](#-documentation)
- [API Reference](#-api-reference)
- [Data Formats and Protocols](#-data-formats-and-protocols)
- [Versioning](#-versioning)
- [Troubleshooting](#-troubleshooting)
- [FAQ](#-faq)
- [Contributing](#-contributing)
- [Code of Conduct](#-code-of-conduct)
- [Security](#-security)
- [Project Engagement](#-project-engagement)
- [License](#-license)

## 📦 Installation

[![Obtain it from NuGet](https://raw.githubusercontent.com/hmlendea/readme-assets/master/badges/stores/nuget.png)](https://nuget.org/packages/NuciAPI.Controllers)

### Package Manager Installation

```bash
dotnet add package NuciAPI.Controllers
```

Or, via the `Package Manager Console`:
```powershell
Install-Package NuciAPI.Controllers
```

## ✨ Capabilities

- Abstract base controller with request processing helpers (`ProcessRequest`, `ProcessRequestAsync`)
- Authorisation abstractions: `NuciApiAuthorisation`, `NuciApiKeyAuthorisation`, `NuciApiNoneAuthorisation`
- HMAC token retrieval from request headers
- Built-in request validation and error responses

## 🚀 Usage

```csharp
using Microsoft.AspNetCore.Mvc;
using NuciAPI.Controllers;
using NuciAPI.Requests;
using NuciAPI.Responses;

public class MyController : NuciApiController
{
    [HttpPost("endpoint")]
    public ActionResult MyEndpoint(MyRequest request)
    {
        return ProcessRequest(request, () =>
        {
            // Your logic here
        }, NuciApiAuthorisation.ApiKey("your-api-key"));
    }
}
```

## 🧩 Compatibility

| Component | Supported Versions | Notes |
|-----------|--------------------|-------|
| .NET | 10.0 | Target framework |
| ASP.NET Core | 10.0 | Framework reference |
| NuciAPI | 3.5.1 | Package reference |

## 🔌 Integrations

| Integration | Compatibility | Purpose | Required |
|-------------|---------------|---------|----------|
| Microsoft.AspNetCore.App | 10.0 | ASP.NET Core framework | Yes |
| NuciAPI | 3.5.1 | Core API abstractions | Yes |

## 🔐 Authentication and Authorisation

NuciAPI.Controllers provides pluggable authorisation strategies via the `NuciApiAuthorisation` abstract base class. Consumers choose a strategy at runtime.

| Role or Scope | Access | Intended Audience |
|---------------|--------|-------------------|
| `ApiKey` | Validates `Authorization: Bearer <key>` header | API consumers with pre-shared keys |
| `None` | No authentication | Public endpoints, development |

## 🧱 Extensibility

Consumers can implement custom authorisation by deriving from `NuciApiAuthorisation` and overriding `PerformAuthorisation(string)`.

| Extension Point | Contract | Purpose |
|-----------------|----------|---------|
| `NuciApiAuthorisation` | Abstract base class | Custom authentication strategies |
| `ProcessRequest` overloads | Protected methods | Custom request processing pipelines |

## 🛡️ Privacy and Data

NuciAPI.Controllers does not collect, process, transmit, or persist any personal data. It is a library that provides base controller classes and authorisation abstractions for ASP.NET Core APIs. The library itself has no telemetry, no external integrations that transmit data, and no data storage. For the detailed description, see [PRIVACY.md](./PRIVACY.md).

## 🛠️ Development

### Requirements

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Setup

```bash
git clone https://github.com/hmlendea/nuciapi.controllers.git
cd nuciapi.controllers
dotnet restore
```

### Build

```bash
dotnet build NuciAPI.Controllers.slnx
```

### Test

```bash
dotnet test NuciAPI.Controllers.UnitTests/NuciAPI.Controllers.UnitTests.csproj
```

## 🏗️ Architecture

NuciAPI.Controllers is a .NET 10.0 library providing abstract base controllers and pluggable authorisation strategies for ASP.NET Core REST APIs. It uses the Strategy pattern for authorisation, has no runtime of its own, and executes entirely within the consumer's request pipeline. Key boundaries: public contract layer (`NuciApiController`, `NuciApiAuthorisation` factories) and implementation layer (concrete strategies, HMAC parsing). Dependencies: `Microsoft.AspNetCore.App` (framework reference), `NuciAPI 3.5.1` (package reference). For the detailed architecture, see [ARCHITECTURE.md](./ARCHITECTURE.md).

## 📚 Documentation

| Resource | Description |
|----------|-------------|
| [Architecture](./ARCHITECTURE.md) | System context, components, runtime flows, and extension points |
| [API Reference](./docs/api-reference/INDEX.md) | Auto-generated API documentation |
| [Quick Start](./docs/guides/quick-start.md) | Get started with NuciAPI.Controllers |
| [FAQ](./docs/faq.md) | Frequently asked questions |
| [Troubleshooting](./docs/troubleshooting.md) | Diagnostic procedures and resolutions |

## 🌐 API Reference

NuciAPI.Controllers is a **library**, not an HTTP API. It provides base classes and abstractions for consumers to build their own REST APIs. See [API Reference Index](./docs/api-reference/INDEX.md) for the public API surface.

## 🧾 Data Formats and Protocols

| Format or Protocol | Specification | Purpose |
|--------------------|---------------|---------|
| HMAC Token | `X-HMAC` header | Request integrity verification |
| API Key | `Authorization: Bearer <key>` | Authentication |

## 🏷️ Versioning

- **Scheme**: Semantic Versioning (MAJOR.MINOR.PATCH)
- **Library Version**: 2.3.1 (NuGet package `NuciAPI.Controllers`)
- **Target Framework**: net10.0
- **NuciAPI Dependency**: 3.5.1

## 🩺 Troubleshooting

See [Troubleshooting Guide](./docs/troubleshooting.md) for diagnostic procedures and resolutions to recurring problems.

## ❓ FAQ

See the [Frequently Asked Questions](./docs/faq.md) for responses to frequently raised inquiries.

## 🤝 Contributing

You are welcome to submit any suggestion, feedback, or modification to this project.

When doing so, please:
- Maintain cross-platform compatibility
- Preserve the existing public contract unless a breaking change is intentional
- Submit focused pull requests that conform to the existing code style
- Maintain your branch synchronised with `master`
- Revise the documentation when functionality changes
- Properly test all modifications, including edge cases and error conditions
- Include tests for additional or modified functionality
- Raise a new [issue](https://github.com/hmlendea/nuciapi.controllers/issues) for problems or suggestions

See the [contributing guidelines](./CONTRIBUTING.md) for details on how to report issues, suggest enhancements, and submit changes.

## 📜 Code of Conduct

See the [Code of Conduct](./CODE_OF_CONDUCT.md) for the standards of conduct expected in all project spaces.

## 🔒 Security

NuciAPI.Controllers follows a coordinated disclosure policy. Supported versions receive security maintenance via NuGet and GitHub Releases. For information on reporting security vulnerabilities, see [SECURITY.md](./SECURITY.md).

## 💝 Project Engagement

Discovered a problem or have a suggestion? [Open an issue](https://github.com/hmlendea/nuciapi.controllers/issues)!

If you find this project useful, consider [funding it](https://hmlendea.go.ro/funding) or starring ⭐️ it on GitHub!

[![Donate](https://raw.githubusercontent.com/hmlendea/readme-assets/master/donate_generic.png)](https://hmlendea.go.ro/funding)

## 📄 License

This project is being distributed under the `GPL-3.0-or-later`.
See [LICENSE](./LICENSE) for additional information.
