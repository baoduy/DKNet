# Research: Parallelize SQL Integration Tests

**Phase**: 0 - Research and decision consolidation  
**Date**: 2026-03-27

## 1. Migration Store Strategy Per Test Type

**Decision**: Use isolated SQLite for migrated integration tests by default, and restrict `UseInMemoryDatabase` to non-relational behavior tests only.

**Rationale**: SQLite preserves more relational behavior (constraints, transactions, SQL translation differences) than EF InMemory while remaining lightweight and parallel-friendly.

**Alternatives considered**:
- Full EF InMemory migration for all projects: rejected because it can mask SQL translation and relational constraint behaviors.
- Keep all tests on shared SQL Server container: rejected because shared infrastructure blocks safe parallelization and increases flakiness.

## 2. Constitution Compliance With Real-Provider Validation

**Decision**: Keep a focused SQL Server Testcontainers contract lane for provider-specific behaviors that SQLite cannot represent.

**Rationale**: Repository constitution requires real-provider validation for EF Core integration behavior that depends on provider semantics; preserving a smaller SQL lane satisfies this while enabling broad parallel migration.

**Alternatives considered**:
- Remove all SQL Server integration tests: rejected due to constitutional non-compliance and risk of provider drift.
- Keep all existing SQL tests unchanged: rejected due to inability to achieve requested parallel execution gains.

## 3. Test Isolation Model

**Decision**: Enforce per-test or per-test-class unique database identities and deterministic setup/teardown for all migrated projects.

**Rationale**: Parallel safety requires strict data isolation. Shared database names or long-lived contexts create non-deterministic cross-test contamination.

**Alternatives considered**:
- Single shared SQLite database per project: rejected due to data race and locking risk.
- Serialized test execution: rejected because it does not meet parallelization goal.

## 4. Initial Project Scope

**Decision**: Target these SQL-dependent integration test projects first:
- `src/AspNet/AspCore.Idempotency.MsSqlStore.Tests`
- `src/EfCore/EfCore.Specifications.Tests`
- `src/EfCore/EfCore.Relational.Helpers.Tests`

**Rationale**: These projects currently instantiate SQL Server via `Testcontainers.MsSql` and/or `UseSqlServer` in fixtures/tests and are the highest-impact candidates for parallel-safe migration.

**Alternatives considered**:
- Repo-wide test migration in one sweep: rejected due to larger blast radius and harder verification.
- Single-project pilot only: rejected as insufficient to satisfy "all SQL itest projects" scope.

## 5. Parallel Execution Enablement

**Decision**: Enable parallel execution at test runner level while retaining collection boundaries only where shared fixtures are unavoidable.

**Rationale**: Over-broad collection usage can inadvertently serialize tests. Parallel-friendly fixture design should replace shared-fixture serialization where practical.

**Alternatives considered**:
- Keep current collection design unchanged: rejected because it may continue to suppress parallelism.
- Remove all collection controls indiscriminately: rejected because a few contract-lane tests may still require controlled fixture scope.

## 6. CI Validation Strategy

**Decision**: Validate migration success using repeated parallel runs (10 consecutive unchanged-commit runs) and track contention/flakiness metrics.

**Rationale**: One successful run is not enough to prove isolation stability. Repetition validates determinism and contention removal.

**Alternatives considered**:
- Single run validation: rejected due to weak confidence in parallel stability.
- Time-only metric without reliability checks: rejected because speed gains without stability are not acceptable.

## 7. Migration Exception Governance (Implemented)

Approved exception criteria for retaining SQL contract-lane coverage:

- SQL-provider query translation differs from SQLite in a way that changes behavior under test.
- Transaction semantics (locking/isolation) are part of the behavior under test.
- Relational helper behavior directly depends on SQL Server metadata or SQL dialect.

Required exception metadata:

- Reason code (`ProviderSpecificSql`, `TransactionSemantics`, `UnsupportedTranslation`, `Other`).
- Mitigation and retained test coverage reference.
- Approver and review-by date (within 90 days).