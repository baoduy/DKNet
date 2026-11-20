# DKNet Framework

[![codecov](https://codecov.io/github/baoduy/DKNet/graph/badge.svg?token=xtNN7AtB1O)](https://codecov.io/github/baoduy/DKNet)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/baoduy/DKNet/blob/dev/LICENSE)

A suite of 28 independent .NET 10 NuGet packages for building enterprise applications around **Domain-Driven Design**
and **Onion Architecture**.

## ✨ Why use it?

- **Take only what your `DbContext` or API needs.** Each package is opt-in and registers itself: there is no
  `AddDKNet()` and no package that only aggregates others.
- **One repository for every entity.** `DKNet.EfCore.Specifications` gives you one injected `IRepositorySpec` that
  takes a reusable specification, instead of a hand-rolled repository per entity.
- **Cross-cutting rules stay out of domain code.** Domain events, audit logs, row-level data authorization and column
  encryption attach to the `DbContext` through the `SaveChanges` hook pipeline or a value converter.
- **CQRS without MediatR.** `DKNet.SlimBus.Extensions` builds commands, queries and event consumers on
  SlimMessageBus, saves the `DbContext` after a successful write and forwards domain events onto the bus.
- **ASP.NET Core and service glue included.** Idempotent endpoints, startup background tasks, minimal-API endpoint
  groups, blob storage adapters, PDF generation and .NET Aspire integrations ship as their own packages.

## 🚀 Quick Start

Use a .NET 10 console project. Install the packages, then replace `Program.cs` with the example.

```bash
dotnet new console -f net10.0 -n EfCoreDemo
cd EfCoreDemo
dotnet add package DKNet.EfCore.Specifications
dotnet add package Microsoft.EntityFrameworkCore.Sqlite --version 10.0.12
```

```csharp
using DKNet.EfCore.Specifications;
using DKNet.EfCore.Specifications.Definitions;
using DKNet.EfCore.Specifications.Extensions;
using DKNet.EfCore.Specifications.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
connection.Open();
var services = new ServiceCollection();
services.AddDbContext<DemoDb>(o => o.UseSqlite(connection));
services.AddSpecRepo<DemoDb>();
using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
await using var db = scope.ServiceProvider.GetRequiredService<DemoDb>();
await db.Database.EnsureCreatedAsync();
var repo = scope.ServiceProvider.GetRequiredService<IRepositorySpec>();
await repo.AddRangeAsync(new[]
{
    new Product { Name = "Coffee", Price = 12 },
    new Product { Name = "Tea", Price = 6 }
});
await repo.SaveChangesAsync();
var rows = await repo.ToListAsync(new MinimumPrice(10));
Console.WriteLine(rows.Single().Name); // Coffee

public sealed class DemoDb(DbContextOptions<DemoDb> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
}
public sealed class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Price { get; set; }
}
public sealed class MinimumPrice : Specification<Product>
{
    public MinimumPrice(int minimum)
    {
        WithFilter(p => p.Price >= minimum);
        AddOrderBy(p => p.Id);
        AsNoTracking();
    }
}
```

Run with `dotnet run`. Prints `Coffee`.

Not sure which packages you need? The
[**Which package do I need?**](#which-package-do-i-need) table maps problems to packages. For a complete
reference application, see the [SlimBus.ApiEndpoints template](https://github.com/baoduy/DKNet.Templates).

## Documentation

![The DKNet onion: presentation packages on top, the application ring below, the EF Core infrastructure ring in the middle, and DKNet.EfCore.Abstractions plus the dependency-free foundation packages at the centre. Every arrow is a project reference pointing inward.](diagrams/dknet-layers.svg)

| Guide | Read when |
|---|---|
| [Package index](#package-index) | Find a package by area or by problem: all 28 published packages plus the source-only `Aspire.Hosting.ServiceBus`, each linking its own docs folder. |
| [Getting Started](Getting-Started.md) | Install the first packages and wire up a first working setup. |
| [Architecture Guide](Architecture.md) | See the onion rings, the package dependency graph, and a request and a domain event traced end to end. |
| [Configuration](Configuration.md) | Learn the registration conventions the packages share and read a worked `Program.cs`. |
| [Examples](Examples/README.md) | Copy a CRUD slice, domain events, specifications or a multi-tenant setup. |
| [FAQ](FAQ.md) | Look up a common question or a troubleshooting step. |
| [Migration Guide](Migration-Guide.md) | Upgrade between versions or move off a retired package. |
| [Testing Strategy](Testing-Strategy.md) · [Security](Security.md) · [Changelog](CHANGELOG.md) | Check the test stack, the supported versions, or what changed in a release. |
| [`dknet-skills` plugin](https://github.com/baoduy/DKNet/blob/dev/plugins/dknet-skills/README.md) | Teach your coding agent (Claude Code, Cursor, Codex, Copilot and others) every DKNet package: `npx skills add baoduy/DKNet`, or `claude plugin marketplace add baoduy/DKNet && claude plugin install dknet-skills@dknet` in Claude Code. |
| [CONTRIBUTING.md](https://github.com/baoduy/DKNet/blob/dev/CONTRIBUTING.md) · [SECURITY.md](https://github.com/baoduy/DKNet/blob/dev/SECURITY.md) | Set up for development and open a pull request, or report a vulnerability. |
| [MIT License](https://github.com/baoduy/DKNet/blob/dev/LICENSE) | Check the licence terms. |

## Which package do I need?

Sorted by the problem you have, not by namespace. Follow the link for the full API and gotchas.

| I need to… | Package |
|---|---|
| Give entities identity, audit fields, and a domain-event queue | [DKNet.EfCore.Abstractions](DKNet.EfCore.Abstractions/index.md) |
| Have EF Core discover my `IEntityTypeConfiguration<T>` classes, apply global filters, seed data, use GUID v7 keys or SQL sequences | [DKNet.EfCore.Extensions](DKNet.EfCore.Extensions/index.md) |
| Query and persist without hand-rolling a repository per entity | [DKNet.EfCore.Specifications](DKNet.EfCore.Specifications/index.md) |
| Build a search/filter predicate whose criteria are only known at runtime | [DKNet.EfCore.Specifications](DKNet.EfCore.Specifications/build-dynamic-predicates.md) — Dynamic Predicate Builder |
| Run my own code before and after every `SaveChanges` | [DKNet.EfCore.Hooks](DKNet.EfCore.Hooks/index.md) |
| Let a domain method raise an event that other code reacts to after the write commits | [DKNet.EfCore.Events](DKNet.EfCore.Events/index.md) |
| Record who changed which field, with sensitive values redacted | [DKNet.EfCore.AuditLogs](DKNet.EfCore.AuditLogs/index.md) |
| Stop one tenant or owner from reading another's rows | [DKNet.EfCore.DataAuthorization](DKNet.EfCore.DataAuthorization/index.md) |
| Encrypt a column without changing the queries that read it | [DKNet.EfCore.Encryption](DKNet.EfCore.Encryption/index.md) |
| Stop hand-writing DTOs that mirror entities | [DKNet.EfCore.DtoGenerator](DKNet.EfCore.DtoGenerator/index.md) |
| Check for or create a table at runtime, whatever the relational provider | [DKNet.EfCore.Relational.Helpers](DKNet.EfCore.Relational.Helpers/index.md) |
| Replace MediatR with an in-process command/query bus | [DKNet.SlimBus.Extensions](./DKNet.SlimBus.Extensions/index.md) |
| Separate commands from queries and stop calling `SaveChangesAsync` in handlers | [DKNet.SlimBus.Extensions](./DKNet.SlimBus.Extensions/auto-save-dbcontext.md) — auto-save |
| Generate CRUD requests, handlers, and endpoints from an attributed entity member | [DKNet.SlimBus.Generators](./DKNet.SlimBus.Generators/index.md) |
| Map minimal-API endpoint groups by convention and turn a `Result` into `ProblemDetails` | [DKNet.AspCore.Extensions](DKNet.AspCore.Extensions/index.md) |
| Make a `POST`/`PUT`/`PATCH` safe for a client to retry | [DKNet.AspCore.Idempotency](DKNet.AspCore.Idempotency/index.md) on its own for local development, plus one store package for deployed traffic |
| Run a batch of jobs once per host start, in a hosted `BackgroundService` alongside traffic | [DKNet.AspCore.Tasks](DKNet.AspCore.Tasks/index.md) |
| Store files without binding the application to one cloud | [DKNet.Svc.BlobStorage.Abstractions](./DKNet.Svc.BlobStorage.Abstractions/index.md) plus one adapter |
| Encrypt, sign, or hash a value in ordinary application code | [DKNet.Svc.Encryption](./DKNet.Svc.Encryption/index.md) |
| Render Markdown or HTML to a PDF file | [DKNet.Svc.PdfGenerators](./DKNet.Svc.PdfGenerators/index.md) |
| Fill bracketed tokens in a template string from an object or a dictionary | [DKNet.Svc.Transformation](./DKNet.Svc.Transformation/index.md) |
| Generate a password, token, or other secret | [DKNet.RandomCreator](./DKNet.RandomCreator/index.md) |
| Scan assemblies for types, or reach for a string/enum/`DateTime`/reflection helper | [DKNet.Fw.Extensions](./DKNet.Fw.Extensions/index.md) |
| Give a local Aspire AppHost its own Azure Service Bus emulator | [Aspire.Hosting.ServiceBus](./Aspire.Hosting.ServiceBus/index.md) (source-only) |

## Package index

28 packages are published to NuGet. One more, `Aspire.Hosting.ServiceBus`, is source-only in this repository
(`<IsPackable>false</IsPackable>`) and documented anyway — it is an Aspire shared project you reference from an
AppHost directly. `DKNet.EfCore.Repos` and `DKNet.EfCore.Repos.Abstractions`, the older generic-repository
packages, were removed outright; see [Migrating-Repos-To-Specifications](./EfCore/Migrating-Repos-To-Specifications.md)
if you are upgrading off them.

### Core Framework
Foundation utilities that sit at the bottom of the dependency graph and pull in nothing else from DKNet.

- [DKNet.Fw.Extensions](./DKNet.Fw.Extensions/index.md) — framework-agnostic reflection, type, string, enum and DI-inspection helpers, plus fluent assembly/type scanning (`TypeExtractors`)
- [DKNet.RandomCreator](./DKNet.RandomCreator/index.md) — cryptographically secure random string and character generation for passwords, tokens, and other secrets

### Entity Framework Core Extensions
Entity base classes, the specification pattern, and the `SaveChanges` interceptor pipeline everything else hangs off.

- [DKNet.EfCore.Abstractions](DKNet.EfCore.Abstractions/index.md) — the persistence-agnostic vocabulary every other `DKNet.EfCore.*` package builds on: entity base classes, domain-event contracts, and the attributes that steer audit, sequence, and mapping behaviour
- [DKNet.EfCore.Extensions](DKNet.EfCore.Extensions/index.md) — the wiring layer: convention-based entity configuration, global query filters, data seeding, GUID v7 keys, SQL sequences, and the `SnapshotContext` the save hooks are built on
- [DKNet.EfCore.Specifications](DKNet.EfCore.Specifications/index.md) — filter, includes, and order-by as one reusable object, executed through a single non-generic `IRepositorySpec`, plus a runtime dynamic predicate builder. The current, supported way to query and persist
- [DKNet.EfCore.Hooks](DKNet.EfCore.Hooks/index.md) — a pluggable before/after-`SaveChanges` interceptor pipeline: one shared interceptor per `DbContext` type plus a pair of interfaces you implement
- [DKNet.EfCore.Events](DKNet.EfCore.Events/index.md) — dispatches domain events raised by entities during `SaveChanges`, so a domain method never references a publisher or a bus
- [DKNet.EfCore.AuditLogs](DKNet.EfCore.AuditLogs/index.md) — captures a structured, field-level change record for every created, updated, or deleted entity and hands the batch to publishers you register
- [DKNet.EfCore.DataAuthorization](DKNet.EfCore.DataAuthorization/index.md) — row-level, ownership-based authorization: an automatic global query filter on reads plus `SaveChanges`-time owner stamping on writes
- [DKNet.EfCore.Encryption](DKNet.EfCore.Encryption/index.md) — transparent, column-level encryption for `string` properties, applied at the database boundary via a standard `ValueConverter`
- [DKNet.EfCore.DtoGenerator](DKNet.EfCore.DtoGenerator/index.md) — a Roslyn incremental source generator that emits DTO properties from an entity type at compile time
- [DKNet.EfCore.Relational.Helpers](DKNet.EfCore.Relational.Helpers/index.md) — four `DbContext` extension methods for relational bookkeeping EF Core does not expose: table creation, connection access, table-name resolution, and table-existence checks

### Messaging & CQRS
SlimMessageBus integration: CQRS contracts, automatic save, domain events onto the bus, and the CRUD source generator.

- [DKNet.SlimBus.Extensions](./DKNet.SlimBus.Extensions/index.md) — fluent command/query/event interfaces on SlimMessageBus, automatic `SaveChanges` after a successful write, and domain events forwarded onto the bus
- [DKNet.SlimBus.Generators](./DKNet.SlimBus.Generators/index.md) — emits a whole CRUD vertical slice (request records, handlers, endpoint registration) from `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]`-attributed entity members

### Service Layer
Application-level services with no dependency on the EF Core packages.

- [DKNet.Svc.BlobStorage.Abstractions](./DKNet.Svc.BlobStorage.Abstractions/index.md) — provider-agnostic `IBlobService` contract and shared model types
- [DKNet.Svc.BlobStorage.AwsS3](./DKNet.Svc.BlobStorage.AwsS3/index.md) — AWS S3 adapter that also works against S3-compatible services such as MinIO and Cloudflare R2
- [DKNet.Svc.BlobStorage.AzureStorage](./DKNet.Svc.BlobStorage.AzureStorage/index.md) — Azure Blob Storage adapter backed by `Azure.Storage.Blobs`
- [DKNet.Svc.BlobStorage.Local](./DKNet.Svc.BlobStorage.Local/index.md) — local-filesystem adapter that stores blobs under a configured root folder with path-traversal protection
- [DKNet.Svc.Encryption](./DKNet.Svc.Encryption/index.md) — explicitly-invoked cryptography: AES-GCM and RSA encryption, RSA signing, HMAC and SHA hashing, and Base64/Base64URL helpers
- [DKNet.Svc.PdfGenerators](./DKNet.Svc.PdfGenerators/index.md) — converts HTML or Markdown into a PDF using headless Chromium (PuppeteerSharp) and Markdig, with page layout, header/footer, and margin control
- [DKNet.Svc.Transformation](./DKNet.Svc.Transformation/index.md) — fills bracketed tokens in a template string from plain objects or string dictionaries by reflection

### ASP.NET Core Utilities
Start-up orchestration, minimal-API glue, and idempotency for web and API workloads.

- [DKNet.AspCore.Extensions](DKNet.AspCore.Extensions/index.md) — host-populated request members, discovered and versioned endpoint groups, verb-to-command mappers, generic list/read/delete endpoints, and `FluentResults`-to-`IResult` conversion
- [DKNet.AspCore.Tasks](DKNet.AspCore.Tasks/index.md) — implement `IBackgroundTask`, register it, and one `BackgroundService` runs every registered task once when the host starts
- [DKNet.AspCore.Idempotency](DKNet.AspCore.Idempotency/index.md) — an `IEndpointFilter` that recognises a client-supplied idempotency key, blocks the same operation from running twice, and replays or rejects the retry
- [DKNet.AspCore.Idempotency.MsSqlStore](DKNet.AspCore.Idempotency.MsSqlStore/index.md) — SQL Server-backed key store
- [DKNet.AspCore.Idempotency.NpgsqlStore](DKNet.AspCore.Idempotency.NpgsqlStore/index.md) — PostgreSQL-backed key store
- [DKNet.AspCore.Idempotency.RedisStore](DKNet.AspCore.Idempotency.RedisStore/index.md) — Redis-backed key store using `SET NX` reservation and native key expiry, with no schema or migrations
- [DKNet.AspCore.Idempotency.Relational](DKNet.AspCore.Idempotency.Relational/index.md) — the shared EF Core building blocks the two relational stores derive from. Not referenced directly by application code

### Aspire Integrations
Infrastructure orchestration helpers for .NET Aspire AppHost projects.

- [Aspire.Hosting.ServiceBus](./Aspire.Hosting.ServiceBus/index.md) — adds the Azure Service Bus emulator as a resource inside an Aspire AppHost, so local work needs no shared cloud namespace. **Source-only**: it sets `<IsPackable>false</IsPackable>`, so reference the project from your AppHost rather than a NuGet package
