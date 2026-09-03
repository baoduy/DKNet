# DKNet Framework Documentation

DKNet is a suite of independent .NET 10 NuGet packages for building enterprise applications around
**Domain-Driven Design** and **Onion Architecture**. There is no framework to adopt wholesale and no single
`AddDKNet()` call: each package registers itself and can be used on its own, so you pull in only what a given
`DbContext`, API, or worker actually needs.

28 packages are published to NuGet. One more, `Aspire.Hosting.ServiceBus`, is source-only in this repository
(`<IsPackable>false</IsPackable>`) and documented anyway — it is an Aspire shared project you reference from an
AppHost directly. `DKNet.EfCore.Repos` and `DKNet.EfCore.Repos.Abstractions`, the older generic-repository
packages, were removed outright; see [Migrating-Repos-To-Specifications](./EfCore/Migrating-Repos-To-Specifications.md)
if you are upgrading off them.

## Quick Navigation

### Getting Started
- **[Getting Started Guide](Getting-Started.md)** — prerequisites, installation, and a first working setup
- **[Configuration & Setup](Configuration.md)** — how configuration composes across packages
- **[Architecture Guide](Architecture.md)** — the rings, what depends on what, and the two end-to-end paths

### Core Documentation
- **[API Reference](API-Reference.md)** — index into the per-package API documentation
- **[Examples & Recipes](Examples/README.md)** — runnable implementation examples
- **[FAQ](FAQ.md)** — common questions and troubleshooting

### Project Information
- **[Changelog](CHANGELOG.md)** — version history and release notes
- **[Migration Guide](Migration-Guide.md)** — upgrading between versions and off retired packages
- **[Contributing Guide](Contributing.md)** — how to contribute to the project
- **[Testing Strategy](Testing-Strategy.md)** — test stack and coverage targets
- **[Security Policy](Security.md)** — supported versions and vulnerability reporting

## Which package do I need?

Sorted by the problem you have, not by namespace. Follow the link for the full API and gotchas.

| I need to… | Package |
|---|---|
| Give entities identity, audit fields, and a domain-event queue | [DKNet.EfCore.Abstractions](./EfCore/DKNet.EfCore.Abstractions.md) |
| Have EF Core discover my `IEntityTypeConfiguration<T>` classes, apply global filters, seed data, use GUID v7 keys or SQL sequences | [DKNet.EfCore.Extensions](./EfCore/DKNet.EfCore.Extensions.md) |
| Query and persist without hand-rolling a repository per entity | [DKNet.EfCore.Specifications](./EfCore/DKNet.EfCore.Specifications.md) |
| Build a search/filter predicate whose criteria are only known at runtime | [DKNet.EfCore.Specifications](./EfCore/DKNet.EfCore.Specifications.md) — Dynamic Predicate Builder |
| Run my own code before and after every `SaveChanges` | [DKNet.EfCore.Hooks](./EfCore/DKNet.EfCore.Hooks.md) |
| Let a domain method raise an event that other code reacts to after the write commits | [DKNet.EfCore.Events](./EfCore/DKNet.EfCore.Events.md) |
| Record who changed which field, with sensitive values redacted | [DKNet.EfCore.AuditLogs](./EfCore/DKNet.EfCore.AuditLogs.md) |
| Stop one tenant or owner from reading another's rows | [DKNet.EfCore.DataAuthorization](./EfCore/DKNet.EfCore.DataAuthorization.md) |
| Encrypt a column without changing the queries that read it | [DKNet.EfCore.Encryption](./EfCore/DKNet.EfCore.Encryption.md) |
| Stop hand-writing DTOs that mirror entities | [DKNet.EfCore.DtoGenerator](./EfCore/DKNet.EfCore.DtoGenerator.md) |
| Check for or create a table at runtime, whatever the relational provider | [DKNet.EfCore.Relational.Helpers](./EfCore/DKNet.EfCore.Relational.Helpers.md) |
| Separate commands from queries and stop calling `SaveChangesAsync` in handlers | [DKNet.SlimBus.Extensions](./Messaging/DKNet.SlimBus.Extensions.md) |
| Generate CRUD requests, handlers, and endpoints from an attributed entity member | [DKNet.SlimBus.Generators](./Messaging/DKNet.SlimBus.Generators.md) |
| Map minimal-API endpoint groups by convention and turn a `Result` into `ProblemDetails` | [DKNet.AspCore.Extensions](./AspNetCore/DKNet.AspCore.Extensions.md) |
| Make a `POST`/`PUT`/`PATCH` safe for a client to retry | [DKNet.AspCore.Idempotency](./AspNetCore/DKNet.AspCore.Idempotency.md) on its own for local development, plus one store package for deployed traffic |
| Run a job exactly once at start-up, before traffic arrives | [DKNet.AspCore.Tasks](./AspNetCore/DKNet.AspCore.Tasks.md) |
| Store files without binding the application to one cloud | [DKNet.Svc.BlobStorage.Abstractions](./Services/DKNet.Svc.BlobStorage.Abstractions.md) plus one adapter |
| Encrypt, sign, or hash a value in ordinary application code | [DKNet.Svc.Encryption](./Services/DKNet.Svc.Encryption.md) |
| Render Markdown or HTML to a PDF file | [DKNet.Svc.PdfGenerators](./Services/DKNet.Svc.PdfGenerators.md) |
| Fill bracketed tokens in a template string from an object or a dictionary | [DKNet.Svc.Transformation](./Services/DKNet.Svc.Transformation.md) |
| Generate a password, token, or other secret | [DKNet.RandomCreator](./Core/DKNet.RandomCreator.md) |
| Scan assemblies for types, or reach for a string/enum/`DateTime`/reflection helper | [DKNet.Fw.Extensions](./Core/DKNet.Fw.Extensions.md) |
| Give a local Aspire AppHost its own Azure Service Bus emulator | [Aspire.Hosting.ServiceBus](./Aspire/Aspire.Hosting.ServiceBus.md) (source-only) |

## Component Documentation

### [Core Framework](./Core/README.md)
Foundation utilities that sit at the bottom of the dependency graph and pull in nothing else from DKNet.

- [DKNet.Fw.Extensions](./Core/DKNet.Fw.Extensions.md) — framework-agnostic reflection, type, string, enum and DI-inspection helpers, plus fluent assembly/type scanning (`TypeExtractors`)
- [DKNet.RandomCreator](./Core/DKNet.RandomCreator.md) — cryptographically secure random string and character generation for passwords, tokens, and other secrets

### [Entity Framework Core Extensions](./EfCore/README.md)
Entity base classes, the specification pattern, and the `SaveChanges` interceptor pipeline everything else hangs off.

- [DKNet.EfCore.Abstractions](./EfCore/DKNet.EfCore.Abstractions.md) — the persistence-agnostic vocabulary every other `DKNet.EfCore.*` package builds on: entity base classes, domain-event contracts, and the attributes that steer audit, sequence, and mapping behaviour
- [DKNet.EfCore.Extensions](./EfCore/DKNet.EfCore.Extensions.md) — the wiring layer: convention-based entity configuration, global query filters, data seeding, GUID v7 keys, SQL sequences, and the `SnapshotContext` the save hooks are built on
- [DKNet.EfCore.Specifications](./EfCore/DKNet.EfCore.Specifications.md) — filter, includes, and order-by as one reusable object, executed through a single non-generic `IRepositorySpec`, plus a runtime dynamic predicate builder. The current, supported way to query and persist
- [DKNet.EfCore.Hooks](./EfCore/DKNet.EfCore.Hooks.md) — a pluggable before/after-`SaveChanges` interceptor pipeline: one shared interceptor per `DbContext` type plus a pair of interfaces you implement
- [DKNet.EfCore.Events](./EfCore/DKNet.EfCore.Events.md) — dispatches domain events raised by entities during `SaveChanges`, so a domain method never references a publisher or a bus
- [DKNet.EfCore.AuditLogs](./EfCore/DKNet.EfCore.AuditLogs.md) — captures a structured, field-level change record for every created, updated, or deleted entity and hands the batch to publishers you register
- [DKNet.EfCore.DataAuthorization](./EfCore/DKNet.EfCore.DataAuthorization.md) — row-level, ownership-based authorization: an automatic global query filter on reads plus `SaveChanges`-time owner stamping on writes
- [DKNet.EfCore.Encryption](./EfCore/DKNet.EfCore.Encryption.md) — transparent, column-level encryption for `string` properties, applied at the database boundary via a standard `ValueConverter`
- [DKNet.EfCore.DtoGenerator](./EfCore/DKNet.EfCore.DtoGenerator.md) — a Roslyn incremental source generator that emits DTO properties from an entity type at compile time
- [DKNet.EfCore.Relational.Helpers](./EfCore/DKNet.EfCore.Relational.Helpers.md) — four `DbContext` extension methods for relational bookkeeping EF Core does not expose: table creation, connection access, table-name resolution, and table-existence checks

### [Messaging & CQRS](./Messaging/README.md)
SlimMessageBus integration: CQRS contracts, automatic save, and the CRUD source generator.

- [DKNet.SlimBus.Extensions](./Messaging/DKNet.SlimBus.Extensions.md) — fluent command/query/event interfaces, automatic `SaveChanges` after a successful write, and domain events forwarded onto the bus
- [DKNet.SlimBus.Generators](./Messaging/DKNet.SlimBus.Generators.md) — emits a whole CRUD vertical slice (request records, handlers, endpoint registration) from `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]`-attributed entity members

### [Service Layer](./Services/README.md)
Application-level services with no dependency on the EF Core packages.

- [DKNet.Svc.BlobStorage.Abstractions](./Services/DKNet.Svc.BlobStorage.Abstractions.md) — provider-agnostic `IBlobService` contract and shared model types
- [DKNet.Svc.BlobStorage.AwsS3](./Services/DKNet.Svc.BlobStorage.AwsS3.md) — AWS S3 adapter that also works against S3-compatible services such as MinIO and Cloudflare R2
- [DKNet.Svc.BlobStorage.AzureStorage](./Services/DKNet.Svc.BlobStorage.AzureStorage.md) — Azure Blob Storage adapter backed by `Azure.Storage.Blobs`
- [DKNet.Svc.BlobStorage.Local](./Services/DKNet.Svc.BlobStorage.Local.md) — local-filesystem adapter that stores blobs under a configured root folder with path-traversal protection
- [DKNet.Svc.Encryption](./Services/DKNet.Svc.Encryption.md) — explicitly-invoked cryptography: AES-GCM and RSA encryption, RSA signing, HMAC and SHA hashing, and Base64/Base64URL helpers
- [DKNet.Svc.PdfGenerators](./Services/DKNet.Svc.PdfGenerators.md) — converts HTML or Markdown into a PDF using headless Chromium (PuppeteerSharp) and Markdig, with page layout, header/footer, and margin control
- [DKNet.Svc.Transformation](./Services/DKNet.Svc.Transformation.md) — fills bracketed tokens in a template string from plain objects or string dictionaries by reflection

### [ASP.NET Core Utilities](./AspNetCore/README.md)
Start-up orchestration, minimal-API glue, and idempotency for web and API workloads.

- [DKNet.AspCore.Extensions](./AspNetCore/DKNet.AspCore.Extensions.md) — host-populated request members, discovered and versioned endpoint groups, verb-to-command mappers, generic list/read/delete endpoints, and `FluentResults`-to-`IResult` conversion
- [DKNet.AspCore.Tasks](./AspNetCore/DKNet.AspCore.Tasks.md) — implement `IBackgroundTask`, register it, and one `BackgroundService` runs every registered task once when the host starts
- [DKNet.AspCore.Idempotency](./AspNetCore/DKNet.AspCore.Idempotency.md) — an `IEndpointFilter` that recognises a client-supplied idempotency key, blocks the same operation from running twice, and replays or rejects the retry
- [DKNet.AspCore.Idempotency.MsSqlStore](./AspNetCore/DKNet.AspCore.Idempotency.MsSqlStore.md) — SQL Server-backed key store
- [DKNet.AspCore.Idempotency.NpgsqlStore](./AspNetCore/DKNet.AspCore.Idempotency.NpgsqlStore.md) — PostgreSQL-backed key store
- [DKNet.AspCore.Idempotency.RedisStore](./AspNetCore/DKNet.AspCore.Idempotency.RedisStore.md) — Redis-backed key store using `SET NX` reservation and native key expiry, with no schema or migrations
- [DKNet.AspCore.Idempotency.Relational](./AspNetCore/DKNet.AspCore.Idempotency.Relational.md) — the shared EF Core building blocks the two relational stores derive from. Not referenced directly by application code

### [Aspire Integrations](./Aspire/README.md)
Infrastructure orchestration helpers for .NET Aspire AppHost projects.

- [Aspire.Hosting.ServiceBus](./Aspire/Aspire.Hosting.ServiceBus.md) — adds the Azure Service Bus emulator as a resource inside an Aspire AppHost, so local work needs no shared cloud namespace. **Source-only**: it sets `<IsPackable>false</IsPackable>`, so reference the project from your AppHost rather than a NuGet package

## Architecture Overview

Every ring of the onion is a separate package, and every dependency points inward. The
[Architecture Guide](Architecture.md) walks the same picture in detail, adds the package dependency graph,
and traces a request and a domain event end to end.

![The DKNet onion: presentation packages at the top, the application ring below them, the EF Core infrastructure ring in the middle, and DKNet.EfCore.Abstractions plus the dependency-free foundation packages at the bottom. Every arrow is a project reference pointing inward.](./diagrams/dknet-layers.svg)

### Key architectural principles

1. **Dependency inversion** — inner rings never reference outer ones; the arrows above are real `ProjectReference` entries in `src/`.
2. **Separation of concerns** — one package, one responsibility, and no package that only exists to aggregate others.
3. **Domain-centricity** — business rules live in entity methods; `DKNet.EfCore.Abstractions` is the only package a domain model has to see.
4. **Event-driven where it helps** — domain events are queued on the entity and dispatched after the write commits, so a domain method never talks to a bus.
5. **Specifications, not a repository per entity** — one injected `IRepositorySpec` serves every entity type; the entity comes from the specification passed to each call.

## Getting started

1. **Pick the packages you need** — start from the *Which package do I need?* table above.
2. **Read the ring it lives in** — the [Architecture Guide](Architecture.md) explains what a package can and cannot depend on.
3. **Wire it up** — [Configuration & Setup](Configuration.md) covers the four registration conventions the packages share.
4. **Copy a working example** — [Examples & Recipes](Examples/README.md), or the SlimBus.ApiEndpoints template in the [DKNet.Templates](https://github.com/baoduy/DKNet.Templates) repository.

## Contributing to documentation

Corrections and additions are welcome. Open an issue describing the problem, or send a pull request that
follows the structure and voice of the surrounding pages — the canonical page structure is the
[Package Documentation Template](Package-Doc-Template.md); see also the [Contributing Guide](Contributing.md).

Every claim on these pages should be traceable to `src/`, a test, or a config file. If you find one that
is not, that is a bug worth reporting in the [DKNet repository](https://github.com/baoduy/DKNet).
