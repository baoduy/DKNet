# Testing & Coverage Strategy

DKNet's test suite uses **xUnit** with **Shouldly** assertions and **TestContainers.MsSql** for integration tests,
so persistence is exercised against a real SQL Server rather than an in-memory provider. Every package has a
sibling `*.Tests` project, and coverage gates are enforced in CI to keep the framework reliable.

## Conventions

- **Naming**: `MethodName_Scenario_ExpectedBehavior` (for example `DynamicAnd_WithMultipleConditions_CombinesCorrectly`).
- **Stack**: xUnit + Shouldly + TestContainers.MsSql; avoid mocking the database.
- **Isolation**: prefer `IAsyncLifetime` fixtures over shared `IClassFixture` state when isolation matters.
- **SQL verification**: assert on `query.ToQueryString()` output alongside the materialized rows — a recurring
  pattern in the [Specifications](./EfCore/DKNet.EfCore.Specifications.md) tests that confirms filtering is
  translated to SQL, not run in memory.

## Why real databases

Integration tests run against TestContainers.MsSql (Docker required) rather than EF Core InMemory, because the
in-memory provider masks SQL-specific behavior. This is what validates features such as
[Specifications](./EfCore/DKNet.EfCore.Specifications.md), its Dynamic Predicate Builder (including the mandatory
`.AsExpandable()` expansion), and [DataAuthorization](./EfCore/DKNet.EfCore.DataAuthorization.md)'s query-filter
translation. Never run these tests locally on an ARM device (no `mssql/server` ARM image) — verify via the
`remote-tests.yml` GitHub Actions workflow instead (see the root `CLAUDE.md`).

## Coverage targets

| Area | Target |
|---|---|
| Core libraries | 99% line |
| EfCore libraries | 95% line |
| Service libraries | 90% line |
| CI gate (overall) | 80% line |

The primary CI pipeline (`.github/workflows/build-test-coverage.yml`) restores, builds in Release, runs tests with
coverage collection, runs SonarCloud analysis, enforces the 80% gate, and comments coverage on PRs. Tests should
encode *why* behavior matters, exercising the domain rules described in [Architecture](./Architecture.md) and the
persistence behaviors built on Specifications.

---

## Current status

- **Codecov**: [![codecov](https://codecov.io/github/baoduy/DKNet/graph/badge.svg?token=xtNN7AtB1O)](https://codecov.io/github/baoduy/DKNet)
- **Coverage visualization**: ![Coverage](https://codecov.io/gh/baoduy/DKNet/graphs/sunburst.svg?token=xtNN7AtB1O)
