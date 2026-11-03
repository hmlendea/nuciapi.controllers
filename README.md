[![Donate](https://img.shields.io/badge/-%E2%99%A5%20Donate-%23ff69b4)](https://hmlendea.go.ro/funding) [![Latest GitHub release](https://img.shields.io/github/v/release/hmlendea/nuciapi.controllers)](https://github.com/hmlendea/nuciapi.controllers/releases/latest) [![Build Status](https://github.com/hmlendea/nuciapi.controllers/actions/workflows/dotnet.yml/badge.svg)](https://github.com/hmlendea/nuciapi.controllers/actions/workflows/dotnet.yml)

# NuciAPI.Controllers

NuciAPI.Controllers provides base controller classes and authorisation abstractions for building REST APIs with ASP.NET Core.

## 📑 Table of Contents

- [Installation](#-installation)
- [Capabilities](#-capabilities)
- [Usage](#-usage)
- [Privacy and Data](#-privacy-and-data)
- [Contributing](#-contributing)
- [License](#-license)

## 📦 Installation

[![Get it from NuGet](https://raw.githubusercontent.com/hmlendea/readme-assets/master/badges/stores/nuget.png)](https://nuget.org/packages/NuciAPI.Controllers)

**.NET CLI**:
```bash
dotnet add package NuciAPI.Controllers
```

**Package Manager**:
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

## 🛡️ Privacy and Data

NuciAPI.Controllers does not collect, process, transmit, or persist any personal data. It is a library that provides base controller classes and authorisation abstractions for ASP.NET Core APIs. The library itself has no telemetry, no external integrations that transmit data, and no data storage. For the detailed description, see [PRIVACY.md](./PRIVACY.md).

## 🤝 Contributing

You are welcome to submit any suggestion, feedback, or modification to this project.

When doing so, please:
- Maintain cross-platform compatibility
- Preserve the existing public contract unless a breaking change is intentional
- Submit focused pull requests that conform to the existing code style
- Maintain your branch synchronised with `dev`
- Revise the documentation when functionality changes
- Properly test all modifications, including edge cases and error conditions
- Include tests for additional or modified functionality
- Raise a new [issue](https://github.com/hmlendea/nuciapi.controllers/issues) for problems or suggestions

## 📄 License

This project is being distributed under the `GPL-3.0-or-later`.
See [LICENSE](./LICENSE) for additional information.
