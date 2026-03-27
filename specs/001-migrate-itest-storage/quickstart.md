# Quickstart: Parallelize SQL Integration Tests

## Purpose

Validate the migration strategy for SQL-dependent integration test projects so they can run in parallel without shared-state failures.

## Prerequisites

- .NET 10 SDK installed (see `src/global.json`).
- Docker available for retained SQL Server contract-lane tests.
- Repository cloned and feature branch checked out.

## 1. Restore and Baseline Build

Run from `src/`:

```bash
dotnet restore DKNet.FW.sln
dotnet build DKNet.FW.sln --configuration Release
```

Expected outcome:
- Build succeeds with zero warnings.

## 2. Run Targeted Projects in Parallel-Safe Mode

Run targeted migrated projects:

```bash
dotnet test AspNet/AspCore.Idempotency.MsSqlStore.Tests/AspCore.Idempotency.MsSqlStore.Tests.csproj --configuration Release
dotnet test EfCore/EfCore.Specifications.Tests/EfCore.Specifications.Tests.csproj --configuration Release
dotnet test EfCore/EfCore.Relational.Helpers.Tests/EfCore.Relational.Helpers.Tests.csproj --configuration Release
```

Expected outcome:
- Tests pass without shared SQL contention failures.

## 3. Execute Full Solution Test Pass

```bash
dotnet test DKNet.FW.sln --configuration Release --settings coverage.runsettings --collect "XPlat Code Coverage"
```

Expected outcome:
- Test suite completes successfully.
- Retained SQL contract-lane tests pass where provider-specific behavior is required.

## 4. Stability Validation (10 Consecutive Runs)

Execute the targeted migration test set 10 times on unchanged code.

Example loop from `src/`:

```bash
for i in {1..10}; do
	echo "Run $i"
	dotnet test AspNet/AspCore.Idempotency.MsSqlStore.Tests/AspCore.Idempotency.MsSqlStore.Tests.csproj --configuration Release -- RunConfiguration.MaxCpuCount=0 || break
	dotnet test EfCore/EfCore.Specifications.Tests/EfCore.Specifications.Tests.csproj --configuration Release -- RunConfiguration.MaxCpuCount=0 || break
	dotnet test EfCore/EfCore.Relational.Helpers.Tests/EfCore.Relational.Helpers.Tests.csproj --configuration Release -- RunConfiguration.MaxCpuCount=0 || break
done
```

Expected outcome:
- 0 failures attributed to shared database contention.
- Deterministic pass/fail behavior across runs.

Evidence capture template:

| Run | Result | Contention Failures | Duration (s) |
|---|---|---|---|
| 1 | PASS/FAIL | 0 | n/a |
| 2 | PASS/FAIL | 0 | n/a |
| ... | ... | ... | ... |
| 10 | PASS/FAIL | 0 | n/a |

## 5. CI Throughput Comparison

Compare elapsed time for targeted integration test jobs before and after migration.

Expected outcome:
- >=30% reduction in end-to-end CI feedback time for targeted projects.

Comparison formula:

- Baseline average duration: mean of at least 5 pre-migration runs.
- Post-migration average duration: mean of at least 5 post-migration runs.
- Improvement % = `((Baseline - Post) / Baseline) * 100`.

## 6. Exception Recording

For any scenario retained on SQL contract lane:

- Record a migration exception with reason, mitigation, approver, and review date.
- Ensure retained coverage remains active in CI.