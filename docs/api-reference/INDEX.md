# API Reference Index

## Overview

NuciAPI.Controllers is a **library**, not an HTTP API. It provides base classes and abstractions for consumers to build their own REST APIs. This reference documents the public API surface that consumers use.

## Authentication Schemes

| Scheme | Description | Header |
|--------|-------------|--------|
| `ApiKey` | API key validation via `NuciApiAuthorisation.ApiKey(key)` | `Authorization: Bearer <key>` |
| `None` | No authentication via `NuciApiAuthorisation.None` | Not required |
| Custom | Consumer-defined strategies deriving from `NuciApiAuthorisation` | Consumer-defined |

## Base Classes

| Class | Namespace | Purpose |
|-------|-----------|---------|
| `NuciApiController` | `NuciAPI.Controllers` | Abstract base controller with request processing |
| `NuciApiAuthorisation` | `NuciAPI.Controllers` | Abstract authorisation strategy base |
| `NuciApiKeyAuthorisation` | `NuciAPI.Controllers` | API key validation strategy |
| `NuciApiNoneAuthorisation` | `NuciAPI.Controllers` | No-op authorisation strategy |

## NuciApiController Methods

### ProcessRequest Overloads

| Method | Returns | Description |
|--------|---------|-------------|
| `ProcessRequest<TRequest>(TRequest, Action, NuciApiAuthorisation)` | `ActionResult` | Sync, no response body |
| `ProcessRequestAsync<TRequest>(TRequest, Func<Task>, NuciApiAuthorisation)` | `Task<ActionResult>` | Async, no response body |
| `ProcessRequest<TRequest, TResponse>(TRequest, Func<TResponse>, NuciApiAuthorisation)` | `ActionResult` | Sync, typed response |
| `ProcessRequestAsync<TRequest, TResponse>(TRequest, Func<Task<TResponse>>, NuciApiAuthorisation)` | `Task<ActionResult>` | Async, typed response |

### Helper Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `GetHeaderValue(string)` | `string` | Get first header value |

## NuciApiAuthorisation Members

| Member | Type | Description |
|--------|------|-------------|
| `Name` | `string` | Strategy name ("ApiKey", "None") |
| `None` | `static NuciApiNoneAuthorisation` | No-op factory |
| `ApiKey(string)` | `static NuciApiKeyAuthorisation` | API key factory |
| `Authorise(string)` | `void` | Validate authorisation header |
| `PerformAuthorisation(string)` | `protected abstract void` | Strategy-specific validation |
| `ToString()` | `string` | Returns `"{Name}Authorisation"` |

## NuciApiKeyAuthorisation Members

| Member | Type | Description |
|--------|------|-------------|
| `Key` | `string` | Configured API key |
| `PerformAuthorisation(string)` | `protected override void` | Validates key match |

## NuciApiNoneAuthorisation Members

| Member | Type | Description |
|--------|------|-------------|
| Constructor | `internal` | Creates "None" strategy |
| `PerformAuthorisation(string)` | `protected override void` | No-op |

## Response Types (from NuciAPI Package)

| Type | Namespace | Purpose |
|------|-----------|---------|
| `NuciApiRequest` | `NuciAPI.Requests` | Base request DTO (has `HmacToken`) |
| `NuciApiResponse` | `NuciAPI.Responses` | Base response DTO |
| `NuciApiErrorResponse` | `NuciAPI.Responses` | Error responses (`InvalidRequest`, `NotFound`) |
| `NuciApiSuccessResponse` | `NuciAPI.Responses` | Success response (`Default`) |

## Error Responses

| Status | Condition | Response Body |
|--------|-----------|---------------|
| 400 | Null request | `NuciApiErrorResponse.InvalidRequest` |
| 401 | Missing/invalid auth | `AuthenticationException` message |
| 404 | Null response on GET | `NuciApiErrorResponse.NotFound` |
| 500 | No authorisation specified | `NotImplementedException` |
| 500 | Unhandled exception | Consumer's exception handler |

## HMAC Header

| Header | Direction | Processing |
|--------|-----------|------------|
| `X-HMAC` | Request → DTO | URL-decoded → `request.HmacToken` |

## Versioning

- **Library Version**: 2.3.1 (NuGet package `NuciAPI.Controllers`)
- **Target Framework**: net10.0
- **NuciAPI Dependency**: 3.5.1
- **ASP.NET Core**: Framework reference (version from consumer runtime)

## Related Documentation

- [NuciApiController Detail](./nuciapicontroller.md)
- [Architecture](../architecture.md)
- [Request Processing Flow](../flows/request-processing.md)
- [Authorisation Flow](../flows/authorisation.md)
- [HMAC Extraction Flow](../flows/hmac-extraction.md)