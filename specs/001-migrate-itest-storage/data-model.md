# Data Model: SQL Integration Test Parallelization Migration

## Entity: IntegrationTestProject

Represents a test project assessed and migrated under this feature.

| Field | Type | Required | Description |
|---|---|---|---|
| `projectId` | string | Yes | Stable identifier (e.g., csproj-relative path key). |
| `projectPath` | string | Yes | Repository-relative project path. |
| `moduleArea` | enum | Yes | `AspNet` or `EfCore`. |
| `currentStoreType` | enum | Yes | `SqlServerContainer`, `SqlServerConnection`, `SQLite`, `InMemory`. |
| `targetStoreType` | enum | Yes | `SQLite` or `InMemory` for migrated lane. |
| `requiresSqlContractLane` | bool | Yes | Indicates whether provider-specific SQL behavior must be retained in separate lane. |
| `migrationState` | enum | Yes | `Discovered`, `Assessed`, `Migrating`, `Validated`, `Exceptioned`. |
| `owner` | string | No | Maintainer or module owner for the project. |

### Validation Rules

- `targetStoreType=InMemory` is only valid when tests do not depend on relational/provider semantics.
- `requiresSqlContractLane=true` requires at least one contract-lane test definition.
- `migrationState=Validated` requires successful parallel run evidence.

## Entity: TestStoreProfile

Defines how a project provisions and isolates its test database resources.

| Field | Type | Required | Description |
|---|---|---|---|
| `profileId` | string | Yes | Unique profile identifier per project. |
| `projectId` | string | Yes | Foreign key to `IntegrationTestProject`. |
| `provider` | enum | Yes | `SQLite`, `InMemory`, `SqlServerContainer`. |
| `isolationMode` | enum | Yes | `PerTest`, `PerClass`, `PerRun`. |
| `databaseNameStrategy` | enum | Yes | `GuidPerTest`, `TimestampedPerClass`, `Fixed`. |
| `setupStrategy` | enum | Yes | `Fixture`, `Factory`, `Inline`. |
| `teardownStrategy` | enum | Yes | `DisposeContext`, `DropDatabase`, `ContainerStop`. |
| `parallelSafe` | bool | Yes | Indicates whether this profile is validated for parallel execution. |

### Validation Rules

- `parallelSafe=true` requires `databaseNameStrategy != Fixed` unless provider is pure in-memory per-test instance.
- `provider=SqlServerContainer` in migrated lane must use isolated DB naming if parallelized.
- `isolationMode=PerRun` is disallowed for migrated parallel-safe profiles.

## Entity: ParallelExecutionRun

Captures evidence that migrated tests are stable under parallel execution.

| Field | Type | Required | Description |
|---|---|---|---|
| `runId` | string | Yes | Unique run identifier. |
| `commitSha` | string | Yes | Git commit tested. |
| `startedAtUtc` | datetime | Yes | Run start timestamp. |
| `endedAtUtc` | datetime | Yes | Run end timestamp. |
| `targetProjects` | string[] | Yes | Project IDs included in run. |
| `parallelEnabled` | bool | Yes | Must be true for this feature validation. |
| `passCount` | int | Yes | Passing tests count. |
| `failCount` | int | Yes | Failing tests count. |
| `contentionFailureCount` | int | Yes | Failures attributable to shared DB contention. |
| `durationSeconds` | int | Yes | End-to-end elapsed duration. |

### Validation Rules

- `parallelEnabled` must be true for runs used as migration acceptance evidence.
- `contentionFailureCount` must be 0 for qualifying acceptance runs.
- Exactly 10 consecutive qualifying runs are required for SC-003 validation.

## Entity: MigrationExceptionRecord

Tracks explicit exceptions where SQL-dependent tests remain on contract lane.

| Field | Type | Required | Description |
|---|---|---|---|
| `exceptionId` | string | Yes | Unique exception identifier. |
| `projectId` | string | Yes | Foreign key to `IntegrationTestProject`. |
| `reasonCode` | enum | Yes | `ProviderSpecificSql`, `TransactionSemantics`, `UnsupportedTranslation`, `Other`. |
| `description` | string | Yes | Human-readable explanation. |
| `approvedBy` | string | Yes | Maintainer approval identity. |
| `reviewByDate` | date | Yes | Date to re-evaluate exception. |
| `mitigation` | string | Yes | How risk is covered in retained SQL lane. |

### Validation Rules

- Exception records require explicit mitigation and owner approval.
- `reviewByDate` must be within 90 days of creation.

## Relationships

- `IntegrationTestProject` 1:N `TestStoreProfile`
- `IntegrationTestProject` 1:N `MigrationExceptionRecord`
- `ParallelExecutionRun` N:M `IntegrationTestProject`

## State Transitions

`IntegrationTestProject.migrationState` transition rules:

- `Discovered -> Assessed` after dependency classification is complete.
- `Assessed -> Migrating` after target store profile is approved.
- `Migrating -> Validated` after acceptance criteria (parallel stability + behavior equivalence) are met.
- `Assessed -> Exceptioned` when approved exception is recorded.
- `Migrating -> Exceptioned` if migration reveals non-portable SQL semantics requiring retained contract lane.