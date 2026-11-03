# Security Policy

This document describes the security policy for NuciAPI.Controllers, including supported versions, vulnerability reporting, and disclosure expectations.

## 📑 Table of Contents

- [Supported Versions](#-supported-versions)
- [Reporting a Vulnerability](#-reporting-a-vulnerability)
- [Scope](#-scope)
- [Disclosure Policy](#-disclosure-policy)

## 🛡️ Supported Versions

Use this table to indicate which project versions currently receive security maintenance.

| Version | Distribution Channel | Supported |
|---------|--------------------|-----------|
| Latest version | NuGet | ✅ |
| Latest version | GitHub Releases | ✅ |
| Preceding versions | Any distribution channel | ❌ |

## 🚨 Reporting a Vulnerability

Please do not disclose suspected vulnerabilities publicly before maintainers have had an opportunity to validate and remediate them.

To report a vulnerability:
- [GitHub Security Advisories](https://github.com/hmlendea/nuciapi.controllers/security/advisories)
- Contact the maintainers directly

## 📌 Scope

The original maintainers are not responsible for changes, modifications, or security issues introduced by downstream forks.

The subsequent report categories are in scope for this repository:
- Vulnerabilities in the NuciAPI.Controllers library code
- Authorisation logic flaws (NuciApiAuthorisation, NuciApiKeyAuthorisation, NuciApiNoneAuthorisation)
- Request processing vulnerabilities (ProcessRequest, ProcessRequestAsync)
- HMAC token handling issues

The subsequent categories are out of scope unless explicitly stated to the contrary:
- Vulnerabilities in downstream applications using this library
- Vulnerabilities in ASP.NET Core framework (Microsoft.AspNetCore.App)
- Vulnerabilities in NuciAPI dependency
- Issues in unofficial forks or modified distributions

## 📢 Disclosure Policy

This project follows coordinated disclosure:
1. Vulnerabilities are investigated privately.
2. A remediation plan is prepared and validated.
3. Public disclosure is published after a fix, mitigation, or agreed risk decision is available.
4. Credit is attributed in accordance with reporter preference and project policy.