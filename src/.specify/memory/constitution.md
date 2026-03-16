<!--
Sync Impact Report
==================
Version change: 1.1.0 -> 1.2.0

Modified Principles:
- I. Zero Warnings Tolerance -> I. Runtime and SDK Baseline (.NET 10+)
- II. Test-First with Real Databases (NON-NEGOTIABLE) -> II. Zero-Warning and Analyzer Discipline (NON-NEGOTIABLE)
- III. Async-Everywhere -> III. Nullable Safety and Explicit Contracts (NON-NEGOTIABLE)
- IV. Documentation & API Contracts -> IV. Public API Documentation and Packaging Contracts
- V. Security & Null Safety -> V. Test-First and Real Infrastructure Validation (NON-NEGOTIABLE)
- VI. Pattern Compliance -> VI. Architecture and Data Access Pattern Integrity

Added Sections:
- Principle VII. Security, Secrets, and Operational Hardening
- Additional Constraints

Removed Sections:
- Solution Architecture
- Module-Specific Standards

Templates requiring updates:
- ✅ updated `.specify/templates/plan-template.md`
- ✅ updated `.specify/templates/spec-template.md`
- ✅ updated `.specify/templates/tasks-template.md`
- ✅ updated `.specify/templates/commands/constitution.md`
- ✅ updated `.specify/templates/commands/plan.md`
- ✅ updated `.specify/templates/commands/analyze.md`
- ✅ updated `AGENTS.md`

Follow-up TODOs:
- None
-->

# DKNet Framework Constitution

## Core Principles

### I. Runtime and SDK Baseline (.NET 10+)

All maintained projects MUST target the .NET 10 platform baseline and align with the
solution SDK policy.

**Rules**:
- `global.json` MUST pin a .NET 10 SDK and use controlled roll-forward behavior
- Shared project settings MUST keep `TargetFramework` aligned with `net10.0`
- Feature work MUST NOT introduce new .NET 9 or earlier targets unless approved as a
  documented exception
- Version-sensitive documentation MUST be updated in the same change when runtime targets
  move

**Rationale**: A single runtime baseline prevents incompatibility drift across libraries,
analyzers, CI behavior, and NuGet consumers.

### II. Zero-Warning and Analyzer Discipline (NON-NEGOTIABLE)

Build quality is enforced at compile time. Warnings are treated as defects.

**Rules**:
- `TreatWarningsAsErrors=true` MUST remain enabled for all buildable projects
- `dotnet build` and CI builds MUST pass with zero warnings
- Analyzer suppressions MUST be minimal, justified, and committed with rationale
- `dotnet format` compliance MUST be enforced before merge

**Rationale**: Strict analyzer enforcement prevents silent regressions and keeps libraries
safe to consume at scale.

### III. Nullable Safety and Explicit Contracts (NON-NEGOTIABLE)

Null handling and contracts MUST be explicit in all public and internal behavior.

**Rules**:
- `<Nullable>enable</Nullable>` MUST remain enabled solution-wide
- Public APIs MUST validate nullable inputs and throw precise exceptions when required
- New code MUST NOT introduce nullable warnings or latent null-reference hazards
- Contract assumptions MUST be expressed in types and guard clauses, not comments alone

**Rationale**: DKNet is a shared library platform. Explicit null contracts reduce runtime
failures and improve API predictability.

### IV. Public API Documentation and Packaging Contracts

DKNet ships NuGet libraries; documentation is part of the product surface.

**Rules**:
- All public APIs MUST include XML documentation (`summary`, and tags as applicable)
- New `.cs` files MUST include approved file header content
- Package metadata and repository links MUST remain consistent with central package settings
- Breaking changes MUST be called out in PR descriptions and release notes

**Rationale**: Consumers rely on generated docs and package metadata for safe adoption and
upgrade planning.

### V. Test-First and Real Infrastructure Validation (NON-NEGOTIABLE)

Behavior changes MUST be driven by tests, and data-layer integration MUST be verified on
real providers.

**Rules**:
- Development MUST follow red-green-refactor for behavior changes
- Unit tests MUST use xUnit with clear Arrange-Act-Assert structure
- EF Core integration tests MUST use Testcontainers (for example, SQL Server via
  `Testcontainers.MsSql`)
- EF Core integration tests MUST NOT rely on InMemory provider for SQL-behavior validation
- Test names MUST follow `MethodName_Scenario_ExpectedBehavior`

**Rationale**: Real infrastructure tests expose provider-specific translation and transaction
issues that fake providers cannot.

### VI. Architecture and Data Access Pattern Integrity

Core architectural patterns are mandatory for consistency across modules.

**Rules**:
- Domain-driven and onion layering boundaries MUST be preserved
- Query logic MUST use Specification and Repository patterns where those abstractions exist
- Dynamic predicates MUST use the project predicate builder conventions
  (`PredicateBuilder.New<T>()`, `.DynamicAnd()`, `.DynamicOr()`)
- LinqKit-backed dynamic predicate queries MUST call `.AsExpandable()` before `.Where(...)`
- Read-only EF Core queries MUST use `.AsNoTracking()` and keep filtering in database

**Rationale**: Pattern consistency keeps modules composable, testable, and maintainable.

### VII. Security, Secrets, and Operational Hardening

Security hygiene is mandatory in source, configuration, and runtime behavior.

**Rules**:
- Secrets (keys, credentials, raw connection strings) MUST NOT be committed
- Sensitive configuration MUST come from approved configuration providers and secret stores
- Exception handling MUST log or propagate actionable context; silent catch blocks are
  prohibited
- Input validation MUST be explicit at boundaries (API endpoints, handlers, data filters)

**Rationale**: Secret leakage and opaque failures create avoidable operational and compliance
risk.

## Additional Constraints

- Primary languages and tooling: C# 13, .NET 10 SDK, EF Core 10+, xUnit, Shouldly,
  Testcontainers, LinqKit, System.Linq.Dynamic.Core.
- Central package and build governance in `Directory.Packages.props` and
  `Directory.Build.props` is authoritative for shared defaults.
- Module placement MUST follow solution domains (`Core`, `EfCore`, `AspNet`, `Services`,
  `SlimBus`, `Aspire`) with minimal cross-domain coupling.

## Development Workflow

All work MUST pass explicit gates before merge.

**Build Gate**:
- `dotnet restore DKNet.FW.sln`
- `dotnet build DKNet.FW.sln -c Debug`
- Zero warnings in all changed projects

**Test Gate**:
- `dotnet test DKNet.FW.sln` (or justified scoped runs during iteration)
- New/changed behavior covered by tests
- Integration scenarios use required real infrastructure patterns

**Documentation Gate**:
- XML docs and file headers present for new public surface
- Relevant memory-bank and agent guidance updated when standards or focus shift

**Review Gate**:
- At least one maintainer/module-owner approval for affected area
- Constitution check in planning artifacts passes or documented exceptions are approved

## Governance

This constitution is the highest authority for development standards in this repository.
Where guidance conflicts, this document takes precedence.

**Amendment Procedure**:
1. Submit a PR that updates `.specify/memory/constitution.md` with clear rationale.
2. Include a Sync Impact Report describing dependent template and guidance updates.
3. Obtain approval from project maintainers.
4. Apply required updates to templates, command docs, and runtime guidance in the same PR.
5. Record the resulting semantic version change and amendment date.

**Versioning Policy**:
- MAJOR: Removal/redefinition of principles or governance incompatible with prior process
- MINOR: New principle or materially expanded mandatory constraints
- PATCH: Wording clarifications, typo fixes, non-semantic refinements

**Compliance Review Expectations**:
- Every feature plan MUST include a constitution check before design/implementation.
- Reviewers MUST block merges that violate NON-NEGOTIABLE principles.
- Exceptions MUST be explicit, time-bound, and approved by maintainers.

**Guidance References**:
- `memory-bank/README.md`
- `memory-bank/activeContext.md`
- `memory-bank/copilot-quick-reference.md`
- `memory-bank/systemPatterns.md`
- `memory-bank/copilot-rules.md`
- `AGENTS.md`

**Version**: 1.2.0 | **Ratified**: 2026-01-29 | **Last Amended**: 2026-03-16
