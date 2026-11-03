# Repository Structure

## Root Directory

```
nuciapi.controllers/
├── .github/                    # GitHub configuration
│   └── workflows/
│       └── dotnet.yml          # CI/CD pipeline
├── NuciAPI.Controllers/        # Main library project
├── docs/                       # Technical documentation
├── NuciAPI.Controllers.slnx    # SDK-style solution file
├── README.md                   # Project overview
├── PRIVACY.md                  # Privacy statement
├── SECURITY.md                 # Security policy
├── CONTRIBUTING.md             # Contribution guide
├── CODE_OF_CONDUCT.md          # Community standards
├── ARCHITECTURE.md             # Architecture overview
├── LICENSE                     # GPL-3.0-or-later
└── .gitignore
```

## Library Project: NuciAPI.Controllers/

```
NuciAPI.Controllers/
├── NuciAPI.Controllers.csproj  # Project file
├── NuciApiController.cs        # Abstract base controller
├── NuciApiAuthorisation.cs     # Abstract authorisation base
├── NuciApiKeyAuthorisation.cs  # API key implementation
├── NuciApiNoneAuthorisation.cs # No-op implementation
└── obj/                        # Build artifacts (gitignored)
    ├── Debug/
    │   └── net10.0/
    └── project.assets.json
```

### Project File (NuciAPI.Controllers.csproj)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <PackageId>NuciAPI.Controllers</PackageId>
    <Version>2.3.1</Version>
    <Authors>Horațiu Mlendea</Authors>
    <Copyright>Copyright © Horațiu Mlendea</Copyright>
    <PackageLicenseExpression>GPL-3.0-or-later</PackageLicenseExpression>
    <RepositoryUrl>https://github.com/hmlendea/nuciapi.controllers</RepositoryUrl>
    <PackageReadmeFile>README.md</PackageReadmeFile>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="NuciAPI" Version="3.5.1" />
  </ItemGroup>
  <ItemGroup>
    <None Include="..\README.md" Pack="true" PackagePath="\" />
  </ItemGroup>
</Project>
```

## Documentation: docs/

```
docs/
├── INDEX.md                           # Master index
├── architecture.md                    # Detailed architecture
├── security.md                        # Security model
├── error-handling.md                  # Error taxonomy
├── logging.md                         # Logging framework
├── concurrency-and-scheduling.md      # Execution model
├── configuration.md                   # Configuration (none)
├── data-model.md                      # Data models
├── state-and-persistence.md           # State management
├── testing.md                         # Testing guide
├── troubleshooting.md                 # Troubleshooting
├── build-and-deployment.md            # Build & deploy
├── change-guide.md                    # Version upgrades
├── design-decisions.md                # ADRs
├── invariants.md                      # System invariants
├── dependencies.md                    # Dependency analysis
├── components/
│   ├── presentation.md                # NuciApiController deep dive
│   ├── application-services.md        # Authorisation strategies
│   ├── host-and-composition.md        # Consumer integration
│   ├── integration-models.md          # Request/response models
│   └── browser-state-and-localisation.md
├── flows/
│   ├── request-processing.md          # End-to-end flow
│   ├── authorisation.md               # Auth decision tree
│   └── hmac-extraction.md             # HMAC flow
├── api-reference/
│   ├── INDEX.md                       # API reference index
│   └── nuciapicontroller.md           # Controller API
├── behaviour/
│   ├── request-processing.md          # Behavior specs
│   └── authorisation.md               # Auth behavior specs
├── guides/
│   ├── quick-start.md                 # Getting started
│   └── troubleshooting.md             # Troubleshooting guide
├── repository-overview.md             # This file
├── repository-structure.md            # This file
├── documentation-maintenance.md       # Doc maintenance
├── faq.md                             # FAQ
└── integrations.md                    # Integration patterns
```

## Solution File: NuciAPI.Controllers.slnx

```xml
<?xml version="1.0" encoding="utf-8"?>
<Solution FileFormatVersion="1.0">
  <Project Name="NuciAPI.Controllers" Path="NuciAPI.Controllers/NuciAPI.Controllers.csproj" />
</Solution>
```

## GitHub Workflows: .github/workflows/dotnet.yml

```yaml
name: .NET

on:
  push:
    branches: [dev, main]
  pull_request:
    branches: [dev, main]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v4

    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '10.0.x'

    - name: Restore
      run: dotnet restore NuciAPI.Controllers.slnx

    - name: Build
      run: dotnet build NuciAPI.Controllers.slnx --configuration Release --no-restore

    - name: Test
      run: dotnet test NuciAPI.Controllers.slnx --configuration Release --no-build --verbosity normal
```

## File Purposes

### Root Files

| File | Purpose |
|------|---------|
| `README.md` | Project overview, installation, usage, links |
| `PRIVACY.md` | Data handling statement (no data collected) |
| `SECURITY.md` | Vulnerability reporting, supported versions |
| `CONTRIBUTING.md` | Development setup, code style, PR process |
| `CODE_OF_CONDUCT.md` | Community behavior standards |
| `ARCHITECTURE.md` | High-level system architecture |
| `LICENSE` | GPL-3.0-or-later license text |
| `.gitignore` | Excludes bin/, obj/, .vs/, *.user, etc. |
| `NuciAPI.Controllers.slnx` | SDK-style solution |

### Library Source Files

| File | Purpose |
|------|---------|
| `NuciApiController.cs` | Base controller with ProcessRequest overloads |
| `NuciApiAuthorisation.cs` | Abstract auth + None/ApiKey factories |
| `NuciApiKeyAuthorisation.cs` | Bearer token validation |
| `NuciApiNoneAuthorisation.cs` | No-op authorisation |

### Documentation Files

| File | Purpose |
|------|---------|
| `docs/INDEX.md` | Master navigation, repository summary |
| `docs/architecture.md` | Detailed architecture elaboration |
| `docs/components/*.md` | Component deep dives |
| `docs/flows/*.md` | Sequence diagrams, decision trees |
| `docs/api-reference/*.md` | Complete API documentation |
| `docs/behaviour/*.md` | Testable behavior specifications |
| `docs/guides/*.md` | User-facing guides |
| `docs/*.md` | Cross-cutting concerns |

## Naming Conventions

### Files

| Type | Convention | Example |
|------|------------|---------|
| C# source | PascalCase.cs | `NuciApiController.cs` |
| Project | PascalCase.csproj | `NuciAPI.Controllers.csproj` |
| Solution | PascalCase.slnx | `NuciAPI.Controllers.slnx` |
| Markdown | kebab-case.md | `repository-structure.md` |
| Workflow | kebab-case.yml | `dotnet.yml` |

### Directories

| Type | Convention | Example |
|------|------------|---------|
| Project | PascalCase | `NuciAPI.Controllers/` |
| Docs category | kebab-case | `api-reference/` |
| GitHub | lowercase | `.github/` |

## Git Ignore Patterns

```gitignore
# Build outputs
bin/
obj/
*.nupkg

# IDE
.vs/
*.user
*.suo

# OS
.DS_Store
Thumbs.db

# Test results
TestResults/
*.trx
coverage/

# Local config
appsettings.*.json
!appsettings.json
!appsettings.Development.json
```

## Build Outputs (Not Committed)

```
NuciAPI.Controllers/
├── bin/
│   └── Debug/
│       └── net10.0/
│           ├── NuciAPI.Controllers.dll
│           ├── NuciAPI.Controllers.pdb
│           ├── NuciAPI.Controllers.xml
│           └── NuciAPI.Controllers.deps.json
└── obj/
    └── Debug/
        └── net10.0/
            ├── NuciAPI.Controllers.AssemblyInfo.cs
            ├── NuciAPI.Controllers.GeneratedMSBuildEditorConfig.editorconfig
            ├── ref/
            └── refint/
```

## Package Output (NuGet)

```
NuciAPI.Controllers.2.3.1.nupkg
├── lib/
│   └── net10.0/
│       ├── NuciAPI.Controllers.dll
│       └── NuciAPI.Controllers.xml
├── NuciAPI.Controllers.nuspec
└── README.md
```

## Directory Map Summary

```
.
├── .github/workflows/dotnet.yml          # CI
├── NuciAPI.Controllers/                  # Library
│   ├── *.cs                              # Source (4 files)
│   └── NuciAPI.Controllers.csproj        # Project
├── docs/                                 # Documentation (30+ files)
│   ├── INDEX.md
│   ├── architecture.md
│   ├── components/ (5)
│   ├── flows/ (3)
│   ├── api-reference/ (2)
│   ├── behaviour/ (2)
│   ├── guides/ (2)
│   └── *.md (12 cross-cutting)
├── NuciAPI.Controllers.slnx              # Solution
├── README.md                             # Root readme
├── PRIVACY.md
├── SECURITY.md
├── CONTRIBUTING.md
├── CODE_OF_CONDUCT.md
├── ARCHITECTURE.md
├── LICENSE
└── .gitignore
```

**Total tracked files:** ~45 (source + docs + config)
**Source files:** 4 C# files
**Documentation files:** ~30 markdown files