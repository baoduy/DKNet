# API Reference

Index into the per-package API documentation. Each package below has its own canonical reference page —
`docs/<Area>/<Package>.md` — kept in sync with `src/` on every change. This page does not restate their contents;
it points to them, sorted by area, and lists all 28 published packages plus the three source-only ones.

Looking for a package by the problem you have rather than by name? Use the
[**Which package do I need?**](README.md#which-package-do-i-need) table on the documentation hub.

## 🔧 Core Framework

- [DKNet.Fw.Extensions](Core/DKNet.Fw.Extensions.md) — framework-agnostic reflection, type, string, enum, `DateTime` and DI-inspection helpers, plus fluent assembly/type scanning
- [DKNet.RandomCreator](Core/DKNet.RandomCreator.md) — cryptographically secure random string and character generation for passwords, tokens, and other secrets

## 🗄️ Entity Framework Core

- [DKNet.EfCore.Abstractions](EfCore/DKNet.EfCore.Abstractions.md) — entity base classes, domain-event contracts, and the attributes that steer audit, sequence, and mapping behaviour
- [DKNet.EfCore.Extensions](EfCore/DKNet.EfCore.Extensions.md) — convention-based entity configuration, global query filters, data seeding, GUID v7 keys, SQL sequences, `SnapshotContext`
- [DKNet.EfCore.Specifications](EfCore/DKNet.EfCore.Specifications.md) — filter/includes/order-by as one reusable object, the non-generic `IRepositorySpec`, and the runtime Dynamic Predicate Builder. The current, supported way to query and persist
- [DKNet.EfCore.Hooks](EfCore/DKNet.EfCore.Hooks.md) — before/after-`SaveChanges` interceptor pipeline (`IBeforeSaveHookAsync`/`IAfterSaveHookAsync`)
- [DKNet.EfCore.Events](EfCore/DKNet.EfCore.Events.md) — dispatches domain events raised by entities during `SaveChanges`
- [DKNet.EfCore.AuditLogs](EfCore/DKNet.EfCore.AuditLogs.md) — structured, field-level change records handed to publishers you register
- [DKNet.EfCore.DataAuthorization](EfCore/DKNet.EfCore.DataAuthorization.md) — global query filter on reads plus `SaveChanges`-time owner stamping on writes
- [DKNet.EfCore.Encryption](EfCore/DKNet.EfCore.Encryption.md) — transparent column-level encryption via an EF Core `ValueConverter`
- [DKNet.EfCore.DtoGenerator](EfCore/DKNet.EfCore.DtoGenerator.md) — Roslyn incremental source generator that emits DTO properties from an entity type. See also the [Global Exclusions Guide](EfCore/GLOBAL_EXCLUSIONS_GUIDE.md)
- [DKNet.EfCore.Relational.Helpers](EfCore/DKNet.EfCore.Relational.Helpers.md) — table creation, connection access, table-name resolution, and table-existence checks
- [DKNet.EfCore.Repos](EfCore/DKNet.EfCore.Repos.md) — **retired**, source-only, superseded by Specifications
- [DKNet.EfCore.Repos.Abstractions](EfCore/DKNet.EfCore.Repos.Abstractions.md) — **retired**, source-only, superseded by Specifications

## 📨 Messaging & CQRS

- [DKNet.SlimBus.Extensions](Messaging/DKNet.SlimBus.Extensions.md) — `Fluents.Requests`/`Fluents.Queries`/`Fluents.EventsConsumers` contracts, auto-save after a successful write, and domain events forwarded onto the bus
- [DKNet.SlimBus.Generators](Messaging/DKNet.SlimBus.Generators.md) — emits request records, handlers, and endpoint registration from `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]`-attributed entity members

## 🗃️ Blob Storage Services

- [DKNet.Svc.BlobStorage.Abstractions](Services/DKNet.Svc.BlobStorage.Abstractions.md) — provider-agnostic `IBlobService` contract and shared model types
- [DKNet.Svc.BlobStorage.AwsS3](Services/DKNet.Svc.BlobStorage.AwsS3.md) — AWS S3 adapter, also usable against MinIO and Cloudflare R2
- [DKNet.Svc.BlobStorage.AzureStorage](Services/DKNet.Svc.BlobStorage.AzureStorage.md) — Azure Blob Storage adapter
- [DKNet.Svc.BlobStorage.Local](Services/DKNet.Svc.BlobStorage.Local.md) — local-filesystem adapter with path-traversal protection

## 🔐 Cryptography

- [DKNet.Svc.Encryption](Services/DKNet.Svc.Encryption.md) — AES-GCM and RSA encryption, RSA signing, HMAC and SHA hashing, Base64/Base64URL helpers

## 📝 Documents & Text

- [DKNet.Svc.PdfGenerators](Services/DKNet.Svc.PdfGenerators.md) — HTML or Markdown to PDF via headless Chromium (PuppeteerSharp) and Markdig
- [DKNet.Svc.Transformation](Services/DKNet.Svc.Transformation.md) — fills bracketed tokens in a template string from plain objects or string dictionaries

## 🌐 ASP.NET Core Utilities

- [DKNet.AspCore.Extensions](AspNetCore/DKNet.AspCore.Extensions.md) — host-populated request members, discovered and versioned endpoint groups, generic list/read/delete endpoints, `FluentResults`-to-`IResult` conversion
- [DKNet.AspCore.Tasks](AspNetCore/DKNet.AspCore.Tasks.md) — `IBackgroundTask` implementations run once at host start-up
- [DKNet.AspCore.Idempotency](AspNetCore/DKNet.AspCore.Idempotency.md) — endpoint filter that blocks a duplicate write and replays or rejects the retry
- [DKNet.AspCore.Idempotency.MsSqlStore](AspNetCore/DKNet.AspCore.Idempotency.MsSqlStore.md) — SQL Server key store
- [DKNet.AspCore.Idempotency.NpgsqlStore](AspNetCore/DKNet.AspCore.Idempotency.NpgsqlStore.md) — PostgreSQL key store
- [DKNet.AspCore.Idempotency.RedisStore](AspNetCore/DKNet.AspCore.Idempotency.RedisStore.md) — Redis key store, no schema or migrations
- [DKNet.AspCore.Idempotency.Relational](AspNetCore/DKNet.AspCore.Idempotency.Relational.md) — shared EF Core building blocks the relational stores derive from; not referenced directly by application code

## ☁️ Aspire Integrations

- [Aspire.Hosting.ServiceBus](Aspire/Aspire.Hosting.ServiceBus.md) — adds the Azure Service Bus emulator as a resource inside a .NET Aspire AppHost. **Source-only** (`<IsPackable>false</IsPackable>`) — reference the project, not a package

## ⚙️ Configuration

There is no `DKNetOptions` and no single aggregator to configure DKNet through — each package registers itself
independently and exposes its own strongly-typed options where configuration is needed. Only the three blob
adapters bind from an `IConfiguration` section; everything else is configured by a delegate, a constructor
argument, or a type parameter. See [Configuration & Setup](Configuration.md) for the full picture.

## 📖 Usage Examples

- **[Examples & Recipes](Examples/README.md)** — runnable usage examples
- **[SlimBus.ApiEndpoints template](https://github.com/baoduy/DKNet.Templates)** — complete reference implementation, in the DKNet.Templates repository
- **Unit tests** — API usage in the sibling `*.Tests` projects next to each package under `src/`
