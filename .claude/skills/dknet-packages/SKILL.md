---
name: dknet-packages
description: Use when picking which DKNet package solves a scenario, when asked "which DKNet library do I use for X", when wiring DKNet packages into a DbContext or DI container, or before writing code against any DKNet.* API. Routes a need to the right package and its reference doc.
---

# DKNet package routing

**Rule: route, then read.** Pick the package from the tables below, then read its `docs/<Area>/<Package>.md` page before writing code. Those pages carry the real API, DI call, and options — this skill deliberately does not duplicate them, because copies rot.

Paths are repo-relative. Solution lives in `src/DKNet.FW.sln`.

## Removed — do not build on these

| Package | Use instead |
|---|---|
| `DKNet.EfCore.Repos` | **deleted entirely** — `DKNet.EfCore.Specifications` + `AddSpecRepo<TDbContext>()` |
| `DKNet.EfCore.Repos.Abstractions` | **deleted entirely** — `DKNet.EfCore.Specifications` |
| `DKNet.EfCore.DtoEntities` | **deleted entirely** — `DKNet.EfCore.DtoGenerator` |

Migration guide: `docs/EfCore/Migrating-Repos-To-Specifications.md`. None of the three packages' project files exist any more — a reference to any of them will not restore.

## EF Core — `docs/EfCore/`

Each row's "attaches via" is the DI/model call that turns the package on.

| Need | Package | Attaches via |
|---|---|---|
| Aggregate root, entity base types, `[CrudAction]`/`[CrudCreate]`/`[CrudUpdate]`/`[GenerateDto]` attributes | `DKNet.EfCore.Abstractions` | base classes you derive from |
| Auto-discover `IEntityTypeConfiguration`, global exclusions, model conventions | `DKNet.EfCore.Extensions` | `UseAutoConfigModel<TContext>()` |
| Run code before/after `SaveChanges` | `DKNet.EfCore.Hooks` | `AddDbContextWithHook<TDbContext>()` |
| Query with composable, reusable criteria; dynamic runtime predicates | `DKNet.EfCore.Specifications` | `AddSpecRepo<TDbContext>()` |
| Dispatch domain events raised by aggregates | `DKNet.EfCore.Events` | a hook + `AddEventPublisher<TDbContext, TImpl>()` |
| Audit trail of entity changes | `DKNet.EfCore.AuditLogs` | a hook + `AddEfCoreAuditLogs<TDbContext, TPublisher>()` |
| Row-level filtering by data owner / tenant | `DKNet.EfCore.DataAuthorization` | global query filter + `AddDataOwnerProvider<TDbContext, TProvider>()` |
| Transparently encrypt a column | `DKNet.EfCore.Encryption` | `ValueConverter` via `AddEfCoreEncryption<TKeyProvider>()` |
| Generate DTOs from entities at compile time | `DKNet.EfCore.DtoGenerator` | compile time only — reference as an analyzer, `PrivateAssets="all"` |
| Raw relational helpers on a `DbContext` | `DKNet.EfCore.Relational.Helpers` | plain extension methods |

Two traps worth naming here:

- `DKNet.EfCore.Specifications` dynamic predicates require `.AsExpandable()` on the query. Without it LinqKit cannot expand the expression.
- `DKNet.EfCore.DataAuthorization` **fails closed**: registering it against a `DbContext` that does not implement `IDataOwnerDbContext` throws at model build. That is intentional — do not "fix" it by skipping the filter.

## ASP.NET Core — `docs/AspNetCore/`

| Need | Package |
|---|---|
| Background jobs that run once per host start-up | `DKNet.AspCore.Tasks` |
| Minimal-API glue: endpoint discovery/mapping, claim-based request population (`[FromClaim]`), generic list/read/delete endpoints, `Result`→`ProblemDetails` | `DKNet.AspCore.Extensions` |
| Make a minimal-API endpoint safe to retry | `DKNet.AspCore.Idempotency` + one store |
| …backed by SQL Server | `DKNet.AspCore.Idempotency.MsSqlStore` |
| …backed by PostgreSQL | `DKNet.AspCore.Idempotency.NpgsqlStore` |
| …backed by Redis (no schema/migrations) | `DKNet.AspCore.Idempotency.RedisStore` |

`DKNet.AspCore.Idempotency.Relational` is the shared internal base for the two SQL stores. It is entirely `internal` — an application never references or registers it directly.

Idempotency keys arrive from the client. They are sanitized before logging and before being echoed in 409 responses; preserve that when editing the cache path.

## Messaging / CQRS — `docs/Messaging/`

| Need | Package |
|---|---|
| Command/query/event interfaces, EF Core auto-save interceptor, domain-event→bus publisher (MediatR-free) | `DKNet.SlimBus.Extensions` |
| Generate the CRUD vertical slice (request record + handler + endpoint) from attributed entity members | `DKNet.SlimBus.Generators` |

For anything involving `[CrudCreate]` / `[CrudUpdate]` / `[CrudAction]`, use the **`dknet-codegen`** skill.

## Services — `docs/Services/`

| Need | Package |
|---|---|
| Store/retrieve files without coupling to a cloud | `DKNet.Svc.BlobStorage.Abstractions` + one provider |
| …on Azure Blob Storage | `DKNet.Svc.BlobStorage.AzureStorage` |
| …on AWS S3 | `DKNet.Svc.BlobStorage.AwsS3` |
| …on local disk | `DKNet.Svc.BlobStorage.Local` |
| Encrypt or hash a value at the call site | `DKNet.Svc.Encryption` |
| Turn Markdown/HTML into a PDF | `DKNet.Svc.PdfGenerators` |
| Fill a text template from an object | `DKNet.Svc.Transformation` |

Encrypting an **EF Core column** is `DKNet.EfCore.Encryption`, not `Svc.Encryption`.

## Core — `docs/Core/`

| Need | Package |
|---|---|
| String/type/enum/DateTime extensions, DI guards, assembly & type scanning (`TypeExtractors`) | `DKNet.Fw.Extensions` |
| Cryptographically secure random strings with digit/symbol quotas (passwords, tokens) | `DKNet.RandomCreator` |

## Aspire — `docs/Aspire/`

| Need | Package |
|---|---|
| Azure Service Bus emulator as an Aspire resource | `Aspire.Hosting.ServiceBus` |

## If nothing fits

Check `docs/<Area>/README.md` for the area index, then `docs/Architecture.md`. If the need genuinely has no package, say so rather than inventing a DKNet API — hallucinated `DKNet.*` calls are the most common failure here.
