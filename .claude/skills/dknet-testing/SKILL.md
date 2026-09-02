---
name: dknet-testing
description: Use when writing, running, or debugging tests in the DKNet repo — TestContainers fixtures, ARM64/Apple Silicon SQL Server image failures, Docker-unavailable errors, asserting on generated SQL, coverage targets, or dispatching the remote x64 test workflow.
---

# Testing DKNet

Run everything from `src/`.

```bash
dotnet test DKNet.FW.sln --settings coverage.runsettings --collect:"XPlat Code Coverage"
dotnet test EfCore/EfCore.Specifications.Tests                       # one project
dotnet test --filter "FullyQualifiedName~DynamicAnd_WithMultipleConditions"
```

## Stack

xUnit + **Shouldly** + **TestContainers** (real SQL Server) + **Bogus** for data.

- Test names are `MethodName_Scenario_ExpectedBehavior` — `DynamicAnd_WithMultipleConditions_CombinesCorrectly`.
- **Never** EF Core InMemory for integration tests. It silently masks SQL-specific behaviour, which is exactly what these packages are about. This is not negotiable in this repo.
- Don't mock the `DbContext`. Prefer real implementations; mock only at genuine external boundaries.
- Prefer `IAsyncLifetime` over a shared `IClassFixture` when tests could interfere.

## The ARM64 image switch — read before "fixing" a container failure

`mcr.microsoft.com/mssql/server` ships **x64-only**; there is no ARM64 build. Fixtures therefore pick the image from the running architecture:

```csharp
private static readonly string MssqlImage =
    RuntimeInformation.ProcessArchitecture == Architecture.Arm64
        ? "mcr.microsoft.com/azure-sql-edge:latest"
        : "mcr.microsoft.com/mssql/server:2022-latest";
```

(`AspNet/AspCore.Idempotency.MsSqlStore.Tests/Fixtures/ApiFixture.cs`.)

**Keep this switch when editing any fixture.** On Apple Silicon the whole suite runs locally through the `azure-sql-edge` fallback — just run `dotnet test`. A green run on `azure-sql-edge` is not proof the tests pass on real SQL Server; the x64 runner below is the authority for that.

If a container test fails, check in this order: Docker running → image pulled → the arch switch intact. Do not "fix" it by switching the test to InMemory.

## Asserting on generated SQL

The recurring pattern in `EfCore.Specifications.Tests`: assert the SQL *and* the rows. SQL alone can pass while returning nothing.

```csharp
var query = _db.Products.AsExpandable().Where(predicate);
var sql = query.ToQueryString();
var results = await query.ToListAsync();

sql.ShouldContain("[p].[Price] > ");
results.ShouldAllBe(p => p.IsActive && p.Price > 50m);
```

`.AsExpandable()` is mandatory whenever a LinqKit dynamic predicate is involved — without it the expression can't be expanded.

## Remote x64 verification (fallback only)

Run locally first; that covers the whole solution. Use the remote runner when something genuinely can't run here (Docker down, an image that won't pull, a restricted sandbox) or when you want a true x64 second opinion. It runs against the **pushed** branch — commit first.

```bash
gh workflow run remote-tests.yml --ref <branch>
gh workflow run remote-tests.yml --ref <branch> -f project=EfCore/EfCore.Extensions.Tests
gh workflow run remote-tests.yml --ref <branch> -f filter="FullyQualifiedName~DynamicAnd"
gh run watch <run-id> --exit-status
gh run view <run-id> --log-failed
gh run download <run-id> -n test-results      # trx + build.log + test.log
```

The workflow must exist on the branch you dispatch; it lives on `dev`, so feature branches need it merged in.

**Do not** read pass/fail off `.github/workflows/build-test-coverage.yml` — its test step is `continue-on-error`, so it goes green with failing tests.

`Services/Svc.PdfGenerators.Tests` is the other remote-runner case: it downloads Chromium, which can fail in a restricted ARM sandbox.

## Coverage

| Area | Target |
|---|---|
| Core libraries | 99% line |
| EfCore libraries | 95% line |
| Service libraries | 90% line |
| CI gate (overall) | 80% |

CI parses `line-rate` from `coverage-report/Cobertura.xml` against an 80 threshold. Never commit `TestResults/`, `coverage-report*/`, or `nupkgs/`.

## Testing generated code

`SlimBus.Generators.Tests` asserts on **exact emitted strings** (`RequestEmissionTests`, `HandlerEmissionTests`, `EndpointEmissionTests`, `DiagnosticTests`). Any change to generator output breaks them by design — update the expectations deliberately, don't loosen the assertions. `SlimBus.Generators.Tests.Api` exercises the generated endpoints over real HTTP.
