# DKNet Library Skills — Master Index

> **AI Agent Instructions**: Load this file first to identify which skill files you need for the current task, then load only the relevant skill files.

---

## Scenario → Library Mapping

| Task / Scenario | Libraries to load |
|---|---|
| Create ANY REST endpoint (GET / POST / PUT / PATCH / DELETE) | [01-slimbus-extensions](./01-slimbus-extensions.md), [02-aspcore-extensions](./02-aspcore-extensions.md) |
| Read data / query with filters | [05-efcore-repos-abstractions](./05-efcore-repos-abstractions.md), [06-efcore-repos](./06-efcore-repos.md), [07-efcore-specifications](./07-efcore-specifications.md) |
| Write / mutate data (create, update, delete) | [05-efcore-repos-abstractions](./05-efcore-repos-abstractions.md), [06-efcore-repos](./06-efcore-repos.md) |
| Paged / paginated list endpoint | [01-slimbus-extensions](./01-slimbus-extensions.md), [02-aspcore-extensions](./02-aspcore-extensions.md), [07-efcore-specifications](./07-efcore-specifications.md) |
| Prevent duplicate POST/PUT requests (idempotency) | [03-idempotency](./03-idempotency.md), [04-idempotency-mssql](./04-idempotency-mssql.md) |
| Track who changed what (audit trail) | [08-efcore-auditlogs](./08-efcore-auditlogs.md) |
| Restrict data by user/tenant (row-level security) | [09-efcore-dataauthorization](./09-efcore-dataauthorization.md) |
| Encrypt a sensitive column | [10-efcore-encryption](./10-efcore-encryption.md) |
| Publish domain events on save | [11-efcore-events](./11-efcore-events.md) |
| Auto-configure entities / DbContext setup | [12-efcore-extensions](./12-efcore-extensions.md) |
| Generate DTOs from entities without boilerplate | [13-efcore-dtogenerator](./13-efcore-dtogenerator.md) |
| Run a job on app startup (data seeding, cache warm-up) | [14-aspcore-tasks](./14-aspcore-tasks.md) |
| String/DateTime/Enum/Type utilities | [15-fw-extensions](./15-fw-extensions.md) |
| EF Core lifecycle callback (timestamps, cache, dispatch) | See [composition-patterns](./composition-patterns.md) — Hooks section |
| Full CRUD feature (all layers) | [composition-patterns](./composition-patterns.md) — CRUD recipe |

---

## Quick Disambiguation

### Commands vs. Queries
- **Command** = mutates state (POST, PUT, PATCH, DELETE) → `Fluents.Requests.*`
- **Query** = returns data without mutation (GET) → `Fluents.Queries.*`

### Which Repository Interface?
- **Reading data only** → inject `IReadRepository<T>`
- **Writing data** → inject `IWriteRepository<T>`
- **Both** → inject `IRepository<T>` (extends both)

### Idempotency Store
- **Development / stateless single-instance** → default in-memory (built into `AddIdempotency`)
- **Production / multi-instance / durable** → `AddIdempotencyMsSqlStore` from [04-idempotency-mssql](./04-idempotency-mssql.md)

### Hooks vs. AuditLogs vs. Events
- **Structured change records** (who changed field X from A to B) → [08-efcore-auditlogs](./08-efcore-auditlogs.md)
- **General lifecycle callbacks** (set timestamp, invalidate cache) → EfCore.Hooks (no dedicated skill file — general usage)
- **Domain event publishing to SlimBus** → [11-efcore-events](./11-efcore-events.md)

---

## Full Library List

| # | Package | Skill File | Purpose |
|---|---|---|---|
| 1 | `DKNet.SlimBus.Extensions` | [01](./01-slimbus-extensions.md) | CQRS handler interfaces + EF Core auto-save |
| 2 | `DKNet.AspCore.Extensions` | [02](./02-aspcore-extensions.md) | Minimal API endpoint wiring for CQRS handlers |
| 3 | `DKNet.AspCore.Idempotency` | [03](./03-idempotency.md) | Duplicate-safe POST/PUT endpoints |
| 4 | `DKNet.AspCore.Idempotency.MsSqlStore` | [04](./04-idempotency-mssql.md) | SQL Server persistence for idempotency |
| 5 | `DKNet.EfCore.Repos.Abstractions` | [05](./05-efcore-repos-abstractions.md) | IReadRepository / IWriteRepository contracts |
| 6 | `DKNet.EfCore.Repos` | [06](./06-efcore-repos.md) | Concrete repo implementations + DI registration |
| 7 | `DKNet.EfCore.Specifications` | [07](./07-efcore-specifications.md) | Specification pattern + dynamic predicates |
| 8 | `DKNet.EfCore.AuditLogs` | [08](./08-efcore-auditlogs.md) | Field-level audit trail |
| 9 | `DKNet.EfCore.DataAuthorization` | [09](./09-efcore-dataauthorization.md) | Row-level data ownership filters |
| 10 | `DKNet.EfCore.Encryption` | [10](./10-efcore-encryption.md) | AES-GCM column encryption |
| 11 | `DKNet.EfCore.Events` | [11](./11-efcore-events.md) | Domain event publishing from EF Core |
| 12 | `DKNet.EfCore.Extensions` | [12](./12-efcore-extensions.md) | Auto entity configuration + DbContext utilities |
| 13 | `DKNet.EfCore.DtoGenerator` | [13](./13-efcore-dtogenerator.md) | Source-generated DTOs from entities |
| 14 | `DKNet.AspCore.Tasks` | [14](./14-aspcore-tasks.md) | Startup background jobs |
| 15 | `DKNet.Fw.Extensions` | [15](./15-fw-extensions.md) | Core utility extensions |
| — | Composition Patterns | [composition-patterns](./composition-patterns.md) | Multi-library recipes |

