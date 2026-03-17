# Feature Specification: AI Library Skills for DKNet RESTful API Development

**Feature Branch**: `003-ai-library-skills`  
**Created**: 2026-03-16  
**Status**: Draft  
**Input**: User description: "for each project here is the library that will be used to implement the RESTful API. I would like to build a set of AI skills so that the AI agent can understand each library and use it correctly."

## Overview

This feature defines a set of structured AI knowledge documents ("skills") — one per DKNet library — that enable AI coding agents (GitHub Copilot, Cursor, Claude, etc.) to correctly understand, choose, and apply each library when helping developers build RESTful APIs on the DKNet Framework stack.

---

## User Scenarios & Testing *(mandatory)*

<!--
  User stories are ordered by priority (P1 = most critical).
  Each story is independently testable and delivers standalone value.
-->

### User Story 1 - AI Generates Correct CQRS REST Endpoint (Priority: P1)

A developer prompts their AI agent to scaffold a new REST endpoint (e.g., "Create a POST endpoint to create a Product"). The AI correctly generates a SlimMessageBus command handler, maps it to a Minimal API endpoint via `DKNet.AspCore.Extensions`, and wires `FluentResults` for uniform HTTP responses — without the developer having to specify which libraries to use.

**Why this priority**: The CQRS + Minimal API pattern (`DKNet.SlimBus.Extensions` + `DKNet.AspCore.Extensions`) is the primary architectural pattern for every REST endpoint in DKNet. If the AI gets this wrong, nothing else matters.

**Independent Test**: Verified by asking the AI to scaffold a single POST resource endpoint and checking that the output contains a `ICommandHandler<TCommand, Result<TResponse>>` handler, a command record, and a `MapPost` one-liner using `DKNet.AspCore.Extensions` — without manual result-to-HTTP translation code.

**Acceptance Scenarios**:

1. **Given** a developer asks for a POST endpoint to create a resource, **When** the AI has the DKNet skill files loaded, **Then** it generates a `ICommandHandler<TCommand, Result<TResponse>>` handler, a matching command record, and a `MapPost` Minimal API endpoint using the `DKNet.AspCore.Extensions` one-liner mapping.
2. **Given** the developer asks for a GET query endpoint, **When** the AI generates the code, **Then** it uses `IQueryHandler<TQuery, Result<TResponse>>` and maps it as a `MapGet` with a `null → 404` guard.
3. **Given** the developer asks for a paged list endpoint, **When** the AI generates the code, **Then** it uses `IPagedQueryHandler<TQuery, TItem>` and returns a `PagedResult<T>` JSON wrapper.

---

### User Story 2 - AI Applies Idempotency to State-Mutating Endpoints (Priority: P2)

A developer asks the AI to "make this POST endpoint idempotent." The AI correctly adds `DKNet.AspCore.Idempotency` middleware, registers the appropriate backing store (in-memory or SQL Server via `DKNet.AspCore.Idempotency.MsSqlStore`), and attaches the idempotency filter to the endpoint — without introducing duplicate-processing risk.

**Why this priority**: Idempotency is a correctness requirement for payment, order, and mutation endpoints. Incorrect application causes data corruption or silent duplicate processing.

**Independent Test**: Verified in isolation by prompting the AI to add idempotency to an existing endpoint and checking that the output includes `.WithIdempotency()` on the endpoint, `AddIdempotency(...)` in service registration, and — when SQL Server is requested — `AddIdempotencyMsSqlStore(...)` with a configuration reference (not a hardcoded connection string).

**Acceptance Scenarios**:

1. **Given** an existing POST endpoint, **When** the developer asks the AI to add idempotency, **Then** the AI adds `.WithIdempotency()` to the endpoint and `AddIdempotency(...)` to `Program.cs` service registration.
2. **Given** the developer specifies SQL Server persistence, **When** the AI applies idempotency, **Then** it also registers `AddIdempotencyMsSqlStore(...)` with the connection string read from configuration — not hardcoded.
3. **Given** a duplicate request arrives with the same idempotency key, **When** the endpoint is called, **Then** the generated architecture returns the cached response and does not re-execute the handler.

---

### User Story 3 - AI Uses Repositories and Specifications for Data Access (Priority: P3)

A developer asks the AI to "add data access for Products filtered by category and price range." The AI correctly uses `DKNet.EfCore.Repos` with a `Specification<Product>` from `DKNet.EfCore.Specifications`, avoiding raw `DbContext` calls, N+1 queries, and in-memory filtering.

**Why this priority**: Data access is involved in nearly every endpoint. Incorrect patterns (raw `DbContext`, in-memory filtering) cause performance and maintainability problems at scale.

**Independent Test**: Verified independently by asking the AI to generate a data access layer for a filtered list query and checking that the output contains a `Specification<T>` subclass (not a raw LINQ chain) with `WithFilter`, `AddInclude`, and `AddOrderBy`, and that the repository call is `_repository.ToListAsync(spec, cancellationToken)`.

**Acceptance Scenarios**:

1. **Given** a request for filtered data, **When** the AI generates the query code, **Then** it creates a `Specification<T>` subclass with `WithFilter`, `AddInclude`, and `AddOrderBy` — with no raw `_dbContext.Set<T>().ToList().Where(...)` pattern.
2. **Given** dynamic runtime filters (e.g., search term, price range), **When** the AI adds dynamic filtering, **Then** it uses `DynamicPredicateBuilder` with `.DynamicAnd(builder => builder.With(...))` and includes `.AsExpandable()` on the query.
3. **Given** a write operation, **When** the AI generates the data access code, **Then** it uses `IWriteRepository<T>` for commands and `IReadRepository<T>` for queries, respecting CQRS boundaries.

---

### User Story 4 - AI Adds Cross-Cutting Concerns Correctly (Priority: P4)

A developer asks the AI to "add audit logging to this entity," "encrypt this column," or "restrict this query to the current user's data." The AI correctly applies the appropriate DKNet library (`DKNet.EfCore.AuditLogs`, `DKNet.EfCore.Encryption`, or `DKNet.EfCore.DataAuthorization`) without mixing them up or misapplying registration.

**Why this priority**: Cross-cutting concerns are frequently added after initial scaffolding. Misapplication causes silent failures or security vulnerabilities.

**Independent Test**: Verified independently by prompting the AI to add each concern in isolation and confirming correct attribute decoration and service registration for each library, with no cross-contamination.

**Acceptance Scenarios**:

1. **Given** a request to add audit logging, **When** the AI generates the code, **Then** it applies the DKNet audit marker/attribute to the entity and registers `AddEfCoreAuditLogs()` in `Program.cs` — not a manual `SaveChanges` override.
2. **Given** a request to encrypt a column, **When** the AI generates the code, **Then** it applies the DKNet encryption attribute to the entity property and registers the encryption provider in `DbContext` configuration.
3. **Given** a request for row-level data authorization, **When** the AI generates the code, **Then** it uses `DKNet.EfCore.DataAuthorization` patterns with the correct filter injection, not a manual `.Where(x => x.OwnerId == userId)` in every query.

---

### Edge Cases

- What happens when a developer asks the AI about a library that has no DKNet equivalent? The skill files should acknowledge the gap and suggest the closest DKNet alternative.
- What happens when two libraries overlap in functionality (e.g., both hooks and events could react to entity changes)? The skill files must include a disambiguation decision guide.
- How does the AI handle requests requiring composition of multiple libraries (e.g., idempotent CQRS endpoint + audit log + encrypted column)? A composition pattern document must exist.
- What if the developer's prompt is ambiguous about query vs. command? The skill files must define the disambiguation rule (mutates state → command; returns data → query).

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: A skill file MUST exist for every DKNet library listed in the Libraries to Cover table, covering: purpose, when to use, when NOT to use, setup/registration pattern, and at least one complete code example.
- **FR-002**: Each skill file MUST include an "Anti-Patterns" section listing the top 3 incorrect usages with corrected DKNet alternatives.
- **FR-003**: Skill files MUST use consistent, predictable section headings so AI agents can parse and navigate them reliably.
- **FR-004**: Each skill file MUST document inter-library dependencies and how it composes with other DKNet libraries.
- **FR-005**: A master index document MUST map common REST API development scenarios to the correct set of DKNet libraries (e.g., "CRUD endpoint → SlimBus.Extensions + AspCore.Extensions + EfCore.Repos").
- **FR-006**: Skill files MUST be discoverable by AI agents through the existing `memory-bank/README.md` navigation and `AGENTS.md` loading instructions.
- **FR-007**: Each skill file MUST state the minimum .NET version and minimum package version for every API surface it documents.
- **FR-008**: Each skill file MUST include a "Quick Decision Guide" to help the AI choose between library variants (e.g., in-memory vs. SQL Server idempotency store, read vs. write repository).
- **FR-009**: Each skill file MUST include at least one test example following the DKNet test standard: xUnit, Shouldly, and TestContainers (no `UseInMemoryDatabase`).
- **FR-010**: A composition pattern document MUST exist for the most common multi-library scenarios: CRUD endpoint, paginated search, idempotent mutation, audited mutation, and encrypted data access.

### Libraries to Cover

| Layer | Library | Primary Role |
|-------|---------|-------------|
| API Endpoint Mapping | `DKNet.AspCore.Extensions` (SlimBus folder) | CQRS handler → Minimal API wiring |
| CQRS Handlers | `DKNet.SlimBus.Extensions` | Commands, queries, pagination, domain events |
| Idempotency | `DKNet.AspCore.Idempotency` | Duplicate request prevention |
| Idempotency Store | `DKNet.AspCore.Idempotency.MsSqlStore` | SQL Server persistence for idempotency |
| Repository (Write) | `DKNet.EfCore.Repos` | Concrete repository implementations |
| Repository (Abstractions) | `DKNet.EfCore.Repos.Abstractions` | IReadRepository, IWriteRepository contracts |
| Specifications | `DKNet.EfCore.Specifications` | Reusable, composable query specifications |
| Audit Logging | `DKNet.EfCore.AuditLogs` | Change tracking and audit trail |
| Data Authorization | `DKNet.EfCore.DataAuthorization` | Row-level access control |
| Encryption | `DKNet.EfCore.Encryption` | Column-level data encryption |
| Domain Events | `DKNet.EfCore.Events` | EF Core-integrated event publishing |
| EF Core Extensions | `DKNet.EfCore.Extensions` | EF Core utility and configuration helpers |
| DTO Generation | `DKNet.EfCore.DtoGenerator` | Source-generated DTOs from entity definitions |
| Background Tasks | `DKNet.AspCore.Tasks` | Hosted service task scheduling for APIs |
| Framework Extensions | `DKNet.Fw.Extensions` | Core utility extensions (validation, type helpers) |

### Constitution Alignment *(mandatory)*

- **CA-001 Runtime Baseline**: All skill file code examples MUST use .NET 10+ syntax and `net10.0` targets; no .NET 9 or earlier examples.
- **CA-002 Build Quality**: All code examples in skill files MUST be free of compiler warnings when `TreatWarningsAsErrors=true` is active.
- **CA-003 Nullability**: All code examples MUST use nullable reference types (`?`) and handle nulls explicitly; no `!` suppressors without justification.
- **CA-004 Documentation**: Skill files are the documentation; they MUST be complete, accurate, and reviewed when library APIs change.
- **CA-005 Testing**: Each skill file test example MUST use xUnit, Shouldly, and TestContainers; no `UseInMemoryDatabase` in any example.
- **CA-006 Patterns**: Skill files MUST reinforce Specification/Repository, dynamic predicate, and CQRS patterns throughout all examples.
- **CA-007 Security**: Libraries handling sensitive data (idempotency keys, encryption keys, authorization) MUST include a dedicated security notes section in their skill file.

### Key Entities

- **Skill File**: A structured markdown document for a single DKNet library, stored under `memory-bank/libraries/`, containing purpose, setup, usage patterns, anti-patterns, composition rules, and test examples.
- **Master Index**: A top-level scenario-to-library mapping document (`memory-bank/libraries/README.md`) serving as the AI agent's entry point for choosing which skill files to load.
- **Composition Pattern**: A multi-library recipe document showing how two or more DKNet libraries work together for a complete REST API scenario.
- **AI Agent Loading Point**: The existing `memory-bank/README.md` and `AGENTS.md` files, updated to reference the new `memory-bank/libraries/` directory so agents automatically discover skill files.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An AI agent with skill files loaded produces compilable, warning-free DKNet REST API code on the first attempt for at least 90% of the scenario types documented in the master index.
- **SC-002**: A developer using AI assistance can scaffold a complete CRUD REST feature (endpoint + handler + repository + specification) in under 5 minutes without correcting AI-generated library misuse.
- **SC-003**: AI-generated code uses the correct DKNet library (not an equivalent third-party alternative) for 100% of scenarios covered by skill files.
- **SC-004**: Documented anti-patterns do not appear in AI-generated code for any scenario explicitly covered by skill files (zero occurrences of documented anti-patterns).
- **SC-005**: All 15 libraries listed in the Libraries to Cover table have a completed skill file that passes the specification quality checklist before this feature is marked Done.
- **SC-006**: The master index and all composition pattern documents are reviewed and approved by the library maintainer before this feature is marked Done.

---

## Assumptions

- AI agents are GitHub Copilot, Cursor, or similar tools that load context from files referenced in `AGENTS.md`, `.github/copilot-instructions.md`, or `memory-bank/README.md`.
- Skill files will be stored under `memory-bank/libraries/` and referenced from the existing `memory-bank/README.md` navigation so the loading chain is consistent with current project conventions.
- The primary REST API style for DKNet projects is ASP.NET Core Minimal APIs; MVC Controller patterns are out of scope for this feature.
- `DKNet.AspCore.Extensions` in the `SlimBus/` folder is the Minimal API endpoint mapping library; the identically-named package in `AspNet/` folder covers unrelated concerns and is documented separately.
- Skill files are living documents; keeping them accurate is the responsibility of the library maintainer and is part of the definition of done for any future library change.
- Skill file quality is validated through the checklist template in `.specify/templates/checklist-template.md` before marking each skill file complete.

