# Ambiguities and Open Questions

## Overview

This document tracks known ambiguities, design tensions, and open questions in NuciAPI.Controllers. Each item is a candidate for future resolution via RFC, issue, or versioned change.

## Open Questions

### OQ-001: CancellationToken Support

**Question:** Should `ProcessRequestAsync` accept and propagate `CancellationToken`?

**Current State:** No `CancellationToken` parameter. Consumer must handle cancellation inside action delegate.

**Arguments For:**
- Standard ASP.NET Core pattern
- Allows request timeout propagation
- Enables downstream cancellation (DB, HTTP)

**Arguments Against:**
- Adds parameter to all overloads (8 methods)
- Consumer can capture `HttpContext.RequestAborted` inside action
- Library has no async work of its own to cancel

**Decision Needed:** Add `CancellationToken` parameter vs. document current pattern.

---

### OQ-002: ProblemDetails Standardization

**Question:** Should error responses use RFC 7807 `ProblemDetails` instead of `NuciApiErrorResponse`?

**Current State:** Uses `NuciApiErrorResponse` from NuciAPI package.

**Arguments For:**
- Industry standard (RFC 7807)
- Better tooling support (Swagger, clients)
- Extensible with `extensions` dictionary

**Arguments Against:**
- Breaking change for existing consumers
- NuciAPI ecosystem uses `NuciApiErrorResponse`
- `ProblemDetails` requires ASP.NET Core 7+ (we're on 10, so OK)

**Decision Needed:** Migrate in v3.0 (breaking) or keep dual support.

---

### OQ-003: Authorisation Result Type

**Question:** Should `Authorise` return a result object instead of throwing?

**Current State:** Throws `AuthenticationException` on failure.

**Arguments For:**
- Exceptions for control flow debated
- Could return `AuthorisationResult` with success/failure details
- Enables richer context (claims, scopes, metadata)

**Arguments Against:**
- Current pattern works, well-understood
- Exception = immediate pipeline stop (desired for auth)
- `AuthenticationException` is standard ASP.NET Core

**Decision Needed:** Keep exceptions or introduce `AuthorisationResult` in v3.0.

---

### OQ-004: Built-in Rate Limiting

**Question:** Should library provide rate limiting abstraction?

**Current State:** No rate limiting. Consumer implements.

**Arguments For:**
- Common cross-cutting concern
- Could integrate with `ProcessRequest` flow
- ASP.NET Core 7+ has built-in rate limiting

**Arguments Against:**
- Consumer needs vary widely (per-key, per-IP, per-user, distributed)
- ASP.NET Core middleware is better place
- Adds configuration complexity

**Decision Needed:** Document middleware approach or add optional abstraction.

---

### OQ-005: Request/Response Compression

**Question:** Should library handle request/response compression?

**Current State:** No. Handled by ASP.NET Core middleware.

**Arguments For:**
- Could auto-compress large responses
- Consistent with HMAC extraction pattern

**Arguments Against:**
- Middleware handles this better (`AddResponseCompression`)
- Streaming responses complicate compression
- Not a controller concern

**Decision Needed:** Document middleware usage, no library change.

---

### OQ-006: API Versioning Support

**Question:** Should library provide API versioning helpers?

**Current State:** No. Consumer uses `Asp.Versioning` or custom.

**Arguments For:**
- Common requirement
- Could integrate with route/authorisation

**Arguments Against:**
- `Asp.Versioning` is mature, feature-complete
- Versioning is routing concern, not controller base concern
- Would add dependency

**Decision Needed:** Document recommended approach, no library change.

---

### OQ-007: OpenAPI/Swagger Integration

**Question:** Should library provide OpenAPI annotations or conventions?

**Current State:** No. Consumer adds `[ProducesResponseType]`, etc.

**Arguments For:**
- Reduces boilerplate
- Could auto-document `ProcessRequest` patterns

**Arguments Against:**
- Swashbuckle/Asp.Versioning handle this well
- Annotations belong on consumer actions
- Library has no knowledge of consumer models

**Decision Needed:** Document conventions, no library change.

---

### OQ-008: Request Body Streaming

**Question:** Should `ProcessRequest` support streaming request bodies?

**Current State:** Requires full model binding (`TRequest` parameter).

**Arguments For:**
- Large file uploads without buffering
- Streaming JSON deserialization

**Arguments Against:**
- Model binding is standard ASP.NET Core
- Streaming requires different action signature
- Rarely needed for typical REST APIs

**Decision Needed:** Document `IFormFile`/`Stream` patterns, no library change.

---

### OQ-009: Response Caching Headers

**Question:** Should library help set cache headers?

**Current State:** No. Consumer sets via `Response.Headers`.

**Arguments For:**
- Common pattern for GET endpoints
- Could integrate with `ProcessRequest` result

**Arguments Against:**
- Caching strategy varies wildly
- `ResponseCache` attribute exists
- Middleware better for global policies

**Decision Needed:** Document `ResponseCache` attribute usage.

---

### OQ-010: Correlation ID Standardization

**Question:** Should library standardize correlation ID header name and propagation?

**Current State:** No. Consumer chooses header (`X-Correlation-ID`, `X-Request-ID`, `Traceparent`).

**Arguments For:**
- Consistent distributed tracing
- Could auto-propagate to downstream calls

**Arguments Against:**
- W3C `traceparent` is emerging standard
- Consumer may have existing convention
- Library has no HTTP client to propagate

**Decision Needed:** Document W3C `traceparent` recommendation.

---

## Design Tensions

### DT-001: Simplicity vs. Extensibility

**Tension:** Base class with many overloads vs. minimal core with composition.

**Current:** 8 `ProcessRequest` overloads (sync/async × void/response × generic/non-generic).

**Question:** Reduce overloads in v3.0? Use single generic with default type params?

---

### DT-002: Opinionated vs. Flexible

**Tension:** Enforce patterns (auth, validation, errors) vs. allow consumer freedom.

**Current:** Opinionated on auth (required), validation (null check), errors (NuciApiErrorResponse).

**Question:** Make authorisation optional with default? Allow custom error types?

---

### DT-003: Inheritance vs. Composition

**Tension:** `NuciApiController` base class vs. `IProcessRequest` service + filters.

**Current:** Inheritance (base controller).

**Question:** Provide both? Composition-based alternative in v3.0?

---

### DT-004: Sync vs. Async

**Tension:** Maintain sync overloads vs. async-only.

**Current:** Both sync and async for all variants.

**Question:** Deprecate sync in v3.0? Keep for legacy?

---

## Resolved Questions

### RQ-001: Target Framework

**Question:** Which .NET version to target?

**Resolved:** net10.0 (DD-011). Revisit at .NET 10 EOL.

### RQ-002: License

**Question:** Which license?

**Resolved:** GPL-3.0-or-later (DD-013).

### RQ-003: Package Structure

**Question:** Single package or split?

**Resolved:** Single package `NuciAPI.Controllers` (DD-014).

### RQ-004: Logging

**Question:** Built-in logging?

**Resolved:** No logging (DD-003).

### RQ-005: Configuration

**Question:** Configuration system?

**Resolved:** No configuration (DD-004).

---

## Tracking

| ID | Status | Priority | Target Version | Owner |
|----|--------|----------|----------------|-------|
| OQ-001 | Open | Medium | 3.0 | - |
| OQ-002 | Open | High | 3.0 | - |
| OQ-003 | Open | Medium | 3.0 | - |
| OQ-004 | Open | Low | - | - |
| OQ-005 | Open | Low | - | - |
| OQ-006 | Open | Low | - | - |
| OQ-007 | Open | Low | - | - |
| OQ-008 | Open | Low | - | - |
| OQ-009 | Open | Low | - | - |
| OQ-010 | Open | Medium | 2.4 | - |

## Contributing

To propose resolution for an open question:
1. Open GitHub issue with `design` label
2. Reference the OQ-ID
3. Provide rationale, alternatives, migration path
4. Community discussion → RFC → Decision

To add new ambiguity:
1. Open issue with `ambiguity` label
2. Describe the tension/question
3. Will be assigned next OQ-ID