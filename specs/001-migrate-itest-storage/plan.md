# Implementation Plan: Parallelize SQL Integration Tests

**Branch**: `001-migrate-itest-storage` | **Date**: 2026-03-27 | **Spec**: [spec.md](./spec.md)  
**Input**: Feature specification from `/specs/001-migrate-itest-storage/spec.md`

## Summary

Migrate SQL-dependent integration test projects to parallel-safe execution by defaulting to isolated SQLite-based test stores and limiting InMemory usage to non-relational behavior tests only. Preserve a focused SQL Server Testcontainers contract lane for provider-specific semantics that cannot be represented faithfully in SQLite. Deliver deterministic isolation, stable parallel runs, and measurable CI time reduction.

## Technical Context

**Language/Version**: C# 13 / .NET 10+  
**Primary Dependencies**: xUnit, Shouldly, EF Core 10+, Microsoft.Data.Sqlite, Testcontainers.MsSql, ASP.NET Core test host (`WebApplicationFactory`)  
**Storage**: SQLite in-memory/file-isolated test stores for migrated projects; SQL Server Testcontainers retained only for provider-specific contract coverage  
**Testing**: `dotnet test` with xUnit collection and fixture isolation, integration verification via targeted project runs  
**Target Platform**: DKNet CI (Azure Pipelines) and local developer macOS/Linux/Windows test execution  
**Project Type**: .NET class-library/test-suite monorepo  
**Performance Goals**: >=30% reduction in CI elapsed time for targeted integration test projects; zero SQL-contention failures across 10 consecutive runs  
**Constraints**: Preserve constitution-mandated real-provider validation where SQL-specific behavior is required; avoid cross-test shared state; keep warning-free builds  
**Scale/Scope**: 3 identified SQL-dependent test projects (AspNet Idempotency MsSqlStore tests, EF Specifications tests, EF Relational Helpers tests) plus CI parallelization configuration updates

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] **Runtime baseline**: Targets remain .NET 10+ (`net10.0`); no older TFMs introduced.
- [x] **Zero warnings**: Plan includes warning-safe changes only; analyzers remain enforced.
- [x] **Nullability contracts**: Fixture and helper signatures remain nullable-safe and explicit.
- [x] **Documentation contract**: Any new public test helpers will include XML docs and headers when applicable.
- [x] **Test-first gate**: Plan sequences migration validation tests before implementation refactors.
- [x] **Real DB integration**: SQL Server Testcontainers lane is retained for provider-specific behaviors; InMemory is not used as a replacement for SQL semantics.
- [x] **Pattern integrity**: EF query/predicate conventions (including `.AsExpandable()` where relevant) are preserved in migrated tests.
- [x] **Security/secrets**: No secrets are committed; test configuration remains environment-driven.

## Project Structure

### Documentation (this feature)

```text
specs/001-migrate-itest-storage/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── itest-parallelization.openapi.yaml
└── tasks.md
```

### Source Code (repository root)

```text
src/
├── AspNet/
│   └── AspCore.Idempotency.MsSqlStore.Tests/
│       ├── Fixtures/
│       └── Integration/
├── EfCore/
│   ├── EfCore.Specifications.Tests/
│   │   ├── Fixtures/
│   │   └── *Tests.cs
│   └── EfCore.Relational.Helpers.Tests/
│       ├── Fixtures/
│       └── *Tests.cs
└── DKNet.FW.sln
```

**Structure Decision**: This feature is a test-infrastructure migration that touches existing integration-test projects in `src/AspNet` and `src/EfCore` and does not introduce new runtime modules.

## Complexity Tracking

No constitution violations requiring exception approval.

## Post-Design Constitution Re-Check

- [x] **Runtime baseline**: Design remains on .NET 10+ and does not alter TFM policy.
- [x] **Zero warnings**: Planned changes are confined to test fixtures/config and can remain analyzer-clean.
- [x] **Nullability contracts**: Data-store profiles and migration metadata include explicit required/optional fields.
- [x] **Documentation contract**: No production public API changes; test helper documentation requirements retained.
- [x] **Test-first + real infra**: Parallel migration validation includes retained SQL contract tests for provider-specific semantics.
- [x] **Pattern integrity**: No deviation from Specification/Repository/dynamic predicate rules.
- [x] **Security/secrets**: Contract model keeps credentials out of source and enforces config indirection.

## Performance Comparison Criteria

- Baseline window: capture at least 5 successful pre-migration CI runs for targeted projects.
- Post-migration window: capture at least 5 successful post-migration CI runs for targeted projects.
- Acceptance threshold: mean duration improvement must be >=30%.
- Stability gate: contention-related failures must remain 0 across 10 unchanged-commit runs.
