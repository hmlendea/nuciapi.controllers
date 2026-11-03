# Privacy and Personal Data

This document describes how NuciAPI.Controllers handles personal data. It covers the library behaviour and verified integrations described below. Where the software is self-hosted, the instance operator may have separate responsibilities described below.

## 📑 Table of Contents

- [What This Document Covers](#-what-this-document-covers)
- [Self-Hosted Deployments](#-self-hosted-deployments)
- [Data We Handle](#-data-we-handle)
  - [Data Provided to the Application](#data-provided-to-the-application)
  - [Data Generated or Collected by the Application](#data-generated-or-collected-by-the-application)
  - [Data Received from Integrations](#data-received-from-integrations)
- [Processing and Use](#-processing-and-use)
- [Storage, Retention, and Deletion](#-storage-retention-and-deletion)
- [External Processing and Integrations](#-external-processing-and-integrations)
- [Document Changes](#-document-changes)
- [Contact](#-contact)

## 🔎 What This Document Covers

This document describes how NuciAPI.Controllers at https://github.com/hmlendea/nuciapi.controllers handles personal data. It covers the library behaviour and verified integrations described below. Where the software is self-hosted, the instance operator may have separate responsibilities described below.

## 🏠 Self-Hosted Deployments

NuciAPI.Controllers is a .NET library distributed as a NuGet package. It is incorporated into applications built and operated by developers. This document covers the library itself; instance operators control their application's configuration, local storage, logs, backups, access controls, retention, and request handling. The library does not send any data to project maintainers or external services. No telemetry, update checks, crash reports, email, authentication, reverse-proxy, object-storage, or monitoring integrations are built into the library.

## 📥 Data We Handle

### Data Provided to the Application

The library does not request or collect any personal data from users, administrators, or connected systems. It provides base controller classes and authorisation abstractions for ASP.NET Core APIs.

### Data Generated or Collected by the Application

The library does not generate or collect any personal data automatically. No server, application, access, audit, telemetry, crash, or update-check data is produced by the library itself.

### Data Received from Integrations

The library does not receive personal data from integrations or third parties. It has no built-in external data sources.

## 🧭 Processing and Use

The library processes no personal data. It provides authorisation abstractions (`NuciApiAuthorisation`, `NuciApiKeyAuthorisation`, `NuciApiNoneAuthorisation`) and request-processing helpers (`ProcessRequest`, `ProcessRequestAsync`) that operate on data supplied by the consuming application. The library itself performs no processing of personal data.

## 🗄️ Storage, Retention, and Deletion

The library does not store any data. It has no local databases, files, logs, caches, backups, or configured storage services. For self-hosted deployments, the instance operator controls all storage, deletion, and backups for their application.

## 🔗 External Processing and Integrations

The library has no built-in external data transfer. It depends on `Microsoft.AspNetCore.App` (framework reference) and `NuciAPI` (NuGet package) for core functionality, but does not transmit data to any external service or recipient.

| Service or integration | Purpose | Data involved | Configuration or documentation |
|-----------------------|---------|---------------|--------------------------------|
| Microsoft.AspNetCore.App | ASP.NET Core framework | None | https://learn.microsoft.com/aspnet/core |
| NuciAPI | Core API abstractions | None | https://github.com/hmlendea/nuciapi |

## 🛡️ Data Protection and Security

The library itself implements no data protection mechanisms because it processes no personal data. Security of any personal data handled by the consuming application is the responsibility of the instance operator, including updates, secrets management, access controls, backups, network exposure, and log protection. The original maintainers are not responsible for changes, modifications, or privacy issues introduced by downstream forks.

## 🔄 Document Changes

Update this document when library data flows, storage, integrations, or deployment responsibilities change. The current version is published at https://github.com/hmlendea/nuciapi.controllers/blob/dev/PRIVACY.md.

## 📬 Contact

For questions about library data handling, contact the project maintainers at https://github.com/hmlendea/nuciapi.controllers. For a self-hosted application using this library, contact the instance operator. Include the application name and deployment context if applicable; do not send passwords, access tokens, or other secrets.