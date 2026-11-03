# NuciAPI.Controllers Documentation

## Repository Summary

NuciAPI.Controllers is a .NET 10.0 class library providing abstract base controller classes and pluggable authorisation strategies for building REST APIs with ASP.NET Core. It standardises request processing, HMAC token handling, and authentication via the Strategy pattern, executing entirely within the consumer's ASP.NET Core request pipeline.

## Root Document Map

- [ARCHITECTURE.md](../ARCHITECTURE.md) — High-level architecture, system context, components, dependencies, deployment
- [SECURITY.md](../SECURITY.md) — Supported versions, vulnerability reporting, coordinated disclosure policy
- [PRIVACY.md](../PRIVACY.md) — Data handling statement (no personal data collected)
- [CONTRIBUTING.md](../CONTRIBUTING.md) — Development setup, code style, pull request process
- [CODE_OF_CONDUCT.md](../CODE_OF_CONDUCT.md) — Community participation standards
- [LICENSE](../LICENSE) — GPL-3.0-or-later terms
- [README.md](../README.md) — Installation, usage, capabilities overview

## Documentation Catalogue

### Architecture & Design
- [architecture.md](./architecture.md) — Detailed architecture complementing root ARCHITECTURE.md
- [design-decisions.md](./design-decisions.md) — Key architectural choices and rationale
- [invariants.md](./invariants.md) — System-wide invariants and contracts
- [dependencies.md](./dependencies.md) — External and internal dependencies

### Components
- [components/presentation.md](./components/presentation.md) — NuciApiController base class, request processing, response wrapping
- [components/application-services.md](./components/application-services.md) — Authorisation strategies (NuciApiAuthorisation, NuciApiKeyAuthorisation, NuciApiNoneAuthorisation)
- [components/host-and-composition.md](./components/host-and-composition.md) — Consumer integration, ASP.NET Core pipeline, DI registration

### Flows
- [flows/request-processing.md](./flows/request-processing.md) — End-to-end request flow from HTTP request to response
- [flows/authorisation.md](./flows/authorisation.md) — Authorisation validation flow (Bearer token parsing, API key comparison)
- [flows/hmac-extraction.md](./flows/hmac-extraction.md) — HMAC header extraction and URL decoding

### API Reference
- [api-reference/INDEX.md](./api-reference/INDEX.md) — API reference index with endpoint summary table
- [api-reference/nuciapicontroller.md](./api-reference/nuciapicontroller.md) — NuciApiController methods (ProcessRequest overloads, helpers)

### Cross-Cutting Concerns
- [security.md](./security.md) — Security model, authentication, authorisation, threat mitigations (complements root SECURITY.md)
- [error-handling.md](./error-handling.md) — Error taxonomy, handling patterns, recovery
- [logging.md](./logging.md) — Logging framework (none built-in), consumer responsibilities
- [concurrency-and-scheduling.md](./concurrency-and-scheduling.md) — Stateless per-request execution, async support, no shared state
- [configuration.md](./configuration.md) — Configuration schema (hard-coded headers, consumer-managed secrets)

### Data & State
- [data-model.md](./data-model.md) — Request/response DTOs from NuciAPI package, HmacToken property
- [state-and-persistence.md](./state-and-persistence.md) — No persistent state; stateless per-request execution

### Operations
- [build-and-deployment.md](./build-and-deployment.md) — Build pipeline (dotnet restore/build/test), NuGet packaging, GitHub Actions
- [testing.md](./testing.md) — Test strategy (no dedicated test project; consumer integration verification)
- [troubleshooting.md](./troubleshooting.md) — Common issues and solutions

### Guides
- [guides/quick-start.md](./guides/quick-start.md) — Getting started for newcomers
- [guides/troubleshooting.md](./guides/troubleshooting.md) — Step-by-step troubleshooting guide
- [change-guide.md](./change-guide.md) — How to modify common areas safely
- [api-usage-examples.md](./api-usage-examples.md) — Example consumer controller implementations
- [faq.md](./faq.md) — Frequently asked questions
- [ambiguities-and-open-questions.md](./ambiguities-and-open-questions.md) — Unresolved items, known gaps
- [documentation-maintenance.md](./documentation-maintenance.md) — How to keep docs current, ownership
- [integrations.md](./integrations.md) — Common integration patterns

### Repository Structure
- [repository-overview.md](./repository-overview.md) — Repository purpose, scope, entry points
- [repository-structure.md](./repository-structure.md) — Source tree layout, module organisation

## Navigation Aids

- **Start here (newcomers):** [guides/quick-start.md](./guides/quick-start.md) → [api-usage-examples.md](./api-usage-examples.md) → [architecture.md](./architecture.md)
- **Deep dive (component owners):** [components/](./components/) → [flows/](./flows/) → [design-decisions.md](./design-decisions.md)
- **Flows (debuggers):** [flows/request-processing.md](./flows/request-processing.md) → [flows/authorisation.md](./flows/authorisation.md)
- **API consumers:** [api-reference/INDEX.md](./api-reference/INDEX.md) → [api-reference/nuciapicontroller.md](./api-reference/nuciapicontroller.md)
- **Troubleshooting:** [guides/troubleshooting.md](./guides/troubleshooting.md) → [troubleshooting.md](./troubleshooting.md)
- **Integrations:** [integrations.md](./integrations.md)

## Maintenance Metadata

- **Last reviewed:** 2026-11-03
- **Owner:** Horațiu Mlendea
- **Coverage status:** Complete for current library scope (no controllers in library itself; documents base classes and extension points)