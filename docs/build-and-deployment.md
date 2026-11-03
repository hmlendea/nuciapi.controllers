# Build and Deployment

## Build Process

### Local Build

```bash
# Restore dependencies
dotnet restore NuciAPI.Controllers.slnx

# Build (Debug)
dotnet build NuciAPI.Controllers.slnx --configuration Debug

# Build (Release)
dotnet build NuciAPI.Controllers.slnx --configuration Release

# Build with specific framework
dotnet build NuciAPI.Controllers/NuciAPI.Controllers.csproj --framework net10.0 --configuration Release
```

### Build Output

```
NuciAPI.Controllers/bin/Release/net10.0/
├── NuciAPI.Controllers.dll
├── NuciAPI.Controllers.pdb
├── NuciAPI.Controllers.deps.json
├── NuciAPI.Controllers.xml (documentation)
└── NuciAPI.dll (transitive)
```

### Build Verification

```bash
# Run tests
dotnet test NuciAPI.Controllers.slnx --configuration Release --no-build

# Pack NuGet package
dotnet pack NuciAPI.Controllers/NuciAPI.Controllers.csproj --configuration Release --output ./nupkg

# Verify package
dotnet nuget verify ./nupkg/NuciAPI.Controllers.2.3.1.nupkg
```

## CI/CD Pipeline

### GitHub Actions Workflow (`.github/workflows/dotnet.yml`)

```yaml
name: .NET

on:
  push:
    branches: [dev, main]
  pull_request:
    branches: [dev, main]

jobs:
  build:
    name: Build
    runs-on: ubuntu-latest

    steps:
    - uses: actions/checkout@v4

    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '10.0.x'

    - name: Restore dependencies
      run: dotnet restore NuciAPI.Controllers.slnx

    - name: Build
      run: dotnet build NuciAPI.Controllers.slnx --configuration Release --no-restore

    - name: Test
      run: dotnet test NuciAPI.Controllers.slnx --configuration Release --no-build --verbosity normal

    - name: Pack
      if: github.event_name == 'push' && github.ref == 'refs/heads/main'
      run: dotnet pack NuciAPI.Controllers/NuciAPI.Controllers.csproj --configuration Release --output ./nupkg

    - name: Publish to NuGet
      if: github.event_name == 'push' && github.ref == 'refs/heads/main'
      run: dotnet nuget push ./nupkg/*.nupkg --api-key ${{ secrets.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json
```

### Required Secrets

| Secret | Description | Source |
|--------|-------------|--------|
| `NUGET_API_KEY` | NuGet.org API key for publishing | NuGet.org account settings |

## NuGet Package

### Package Metadata (from `.csproj`)

```xml
<PropertyGroup>
  <PackageId>NuciAPI.Controllers</PackageId>
  <Version>2.3.1</Version>
  <Authors>Horațiu Mlendea</Authors>
  <Copyright>Copyright © Horațiu Mlendea</Copyright>
  <PackageLicenseExpression>GPL-3.0-or-later</PackageLicenseExpression>
  <PackageProjectUrl>https://github.com/hmlendea/nuciapi.controllers</PackageProjectUrl>
  <RepositoryUrl>https://github.com/hmlendea/nuciapi.controllers</RepositoryUrl>
  <RepositoryType>git</RepositoryType>
  <PackageTags>aspnetcore;controller;api;authorisation;hmac</PackageTags>
  <Description>Base controller classes and authorisation abstractions for ASP.NET Core REST APIs.</Description>
  <PackageReadmeFile>README.md</PackageReadmeFile>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <IncludeSymbols>true</IncludeSymbols>
  <SymbolPackageFormat>snupkg</SymbolPackageFormat>
</PropertyGroup>
```

### Package Contents

```
NuciAPI.Controllers.2.3.1.nupkg
├── lib/net10.0/
│   ├── NuciAPI.Controllers.dll
│   └── NuciAPI.Controllers.xml
├── NuciAPI.Controllers.2.3.1.nuspec
└── README.md
```

### Versioning Strategy

| Version | When | Example |
|---------|------|---------|
| Major | Breaking changes | 2.3.1 → 3.0.0 |
| Minor | New features, backward compatible | 2.3.1 → 2.4.0 |
| Patch | Bug fixes | 2.3.1 → 2.3.2 |

**Pre-release:** `2.4.0-preview.1`, `2.4.0-rc.1`

## Deployment

### NuGet.org

```bash
# Manual publish
dotnet nuget push ./nupkg/NuciAPI.Controllers.2.3.1.nupkg \
  --api-key $NUGET_API_KEY \
  --source https://api.nuget.org/v3/index.json \
  --skip-duplicate
```

### GitHub Packages (Alternative)

```bash
# Publish to GitHub Packages
dotnet nuget push ./nupkg/NuciAPI.Controllers.2.3.1.nupkg \
  --api-key $GITHUB_TOKEN \
  --source https://nuget.pkg.github.com/hmlendea/index.json
```

### Consumer Installation

```bash
# NuGet CLI
dotnet add package NuciAPI.Controllers --version 2.3.1

# PackageReference
<PackageReference Include="NuciAPI.Controllers" Version="2.3.1" />

# Paket
paket add NuciAPI.Controllers --version 2.3.1
```

## Docker (Consumer Usage)

### Multi-stage Build Example

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["MyApp/MyApp.csproj", "MyApp/"]
RUN dotnet restore "MyApp/MyApp.csproj"
COPY . .
WORKDIR "/src/MyApp"
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MyApp.dll"]
```

### NuGet Package in Docker

```dockerfile
# If using local NuGet feed
COPY ./nupkg/*.nupkg /tmp/packages/
RUN dotnet nuget add source /tmp/packages -n LocalPackages
RUN dotnet add MyApp/MyApp.csproj package NuciAPI.Controllers --version 2.3.1 --source LocalPackages
```

## Release Process

### Automated (Main Branch)

1. Push to `main` branch
2. GitHub Actions builds, tests, packs
3. Publishes to NuGet.org
4. Creates GitHub Release with changelog

### Manual Release

```bash
# 1. Update version in csproj
# <Version>2.4.0</Version>

# 2. Update CHANGELOG.md

# 3. Commit and tag
git add .
git commit -m "chore: release v2.4.0"
git tag v2.4.0
git push origin main --tags

# 4. GitHub Actions handles rest
```

### Pre-release

```bash
# Version: 2.4.0-preview.1
# Tag: v2.4.0-preview.1
# Published with --prerelease flag
dotnet nuget push ./nupkg/NuciAPI.Controllers.2.4.0-preview.1.nupkg \
  --api-key $NUGET_API_KEY \
  --source https://api.nuget.org/v3/index.json
```

## Quality Gates

### Required Checks (CI)

- [ ] Restore succeeds
- [ ] Build succeeds (Release)
- [ ] All tests pass
- [ ] No compiler warnings (treat as errors)
- [ ] Package packs successfully
- [ ] Package verification passes

### Recommended Checks

- [ ] Code coverage > 80%
- [ ] No security vulnerabilities (`dotnet list package --vulnerable`)
- [ ] License compliance check
- [ ] API compatibility check (vs previous version)

## Environment Configuration

### Development

```bash
# User secrets for API keys
dotnet user-secrets set "ApiKey" "dev-key"
dotnet user-secrets set "NuciAPI:ApiKey" "dev-key"
```

### Staging/Production

```bash
# Environment variables
export ApiKey="prod-key-from-vault"
export NuciAPI__ApiKey="prod-key-from-vault"

# Or Azure Key Vault / AWS Secrets Manager integration
```

## Rollback Procedure

### NuGet Package

```bash
# Unlist problematic version (preferred over delete)
dotnet nuget delete NuciAPI.Controllers 2.3.1 \
  --api-key $NUGET_API_KEY \
  --source https://api.nuget.org/v3/index.json \
  --non-interactive
```

### Git Tag

```bash
# Delete remote tag
git push origin --delete v2.3.1

# Delete local tag
git tag -d v2.3.1
```

## Monitoring

### Build Metrics

| Metric | Target |
|--------|--------|
| Build time | < 2 minutes |
| Test time | < 1 minute |
| Package size | < 200 KB |
| Dependencies | 0 vulnerabilities |

### Deployment Metrics

| Metric | Target |
|--------|--------|
| Publish time | < 30 seconds |
| NuGet index time | < 5 minutes |
| Consumer install time | < 10 seconds |

## Troubleshooting Build

### Common Issues

| Issue | Cause | Solution |
|-------|-------|----------|
| `NETSDK1045` | .NET 10 not installed | Install .NET 10 SDK |
| `NU1101` | NuGet source unavailable | Check nuget.config, network |
| `CS0246` | Missing NuciAPI reference | `dotnet restore` |
| Test fails | Missing test dependencies | Check test project references |

### Debug Build

```bash
# Verbose build output
dotnet build -v detailed

# Verbose test output
dotnet test -v detailed --logger "console;verbosity=detailed"

# Inspect package
dotnet nuget verify ./nupkg/*.nupkg
```