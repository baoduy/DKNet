# DKNet.EfCore.Extensions

[![NuGet](https://img.shields.io/nuget/v/DKNet.EfCore.Extensions)](https://www.nuget.org/packages/DKNet.EfCore.Extensions/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DKNet.EfCore.Extensions)](https://www.nuget.org/packages/DKNet.EfCore.Extensions/)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](https://github.com/baoduy/DKNet/blob/main/LICENSE)

The registration/glue package for the DKNet EF Core stack: automatic entity configuration discovery, global
query filters, data seeding, GUID v7 keys, SQL sequences and the `SnapshotContext` type shared by
`DKNet.EfCore.Hooks`, `DKNet.EfCore.Events`, `DKNet.EfCore.AuditLogs` and `DKNet.EfCore.DataAuthorization`.

## Features

- **Auto entity configuration** — `UseAutoConfigModel<TContext>()` applies every `IEntityTypeConfiguration<T>`
  found in one or more assemblies without manual `ApplyConfigurationsFromAssembly` calls.
- **`DefaultEntityTypeConfiguration<T>`** — base configuration that wires primary keys (GUID v7 for `Guid`
  ids), audit columns, and row-version concurrency by convention.
- **Global query filters** — `IGlobalModelBuilder` / `GlobalQueryFilter` for cross-cutting filters (soft
  delete, tenant/ownership isolation) applied automatically at model build time.
- **Data seeding** — `IDataSeedingConfiguration` / `DataSeedingConfiguration<T>`, wired into EF Core's native
  seeding pipeline via `UseAutoDataSeeding`.
- **GUID v7 keys** — `GuidV7ValueGenerator` produces time-ordered GUIDs instead of random ones.
- **SQL sequences** — declare sequences on an enum with `[SqlSequence]`/`[Sequence]`, read them with
  `DbContext.NextSeqValue(...)` (SQL Server and Npgsql).
- **Navigation & change-tracking helpers** — `IsNewEntity()`, `AddNewEntitiesFromNavigations()`, and related
  `EntityEntry`/`DbContext` extensions.
- **Concurrency-aware `SaveChanges`** — `IEfCoreExceptionHandler` + `SaveChangesWithConcurrencyHandlingAsync`.

## Installation

```bash
dotnet add package DKNet.EfCore.Extensions
```

## Quick Start

```csharp
using Microsoft.EntityFrameworkCore;
using DKNet.EfCore.Extensions.Configurations;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
}

public class ProductConfiguration : DefaultEntityTypeConfiguration<Product>
{
    public override void Configure(EntityTypeBuilder<Product> builder)
    {
        base.Configure(builder); // Id (GuidV7 if Guid), audit columns, concurrency token
        builder.Property(p => p.Name).HasMaxLength(255).IsRequired();
    }
}

// Registration
services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString)
           .UseAutoConfigModel<AppDbContext>()          // scan + apply configurations
           .UseAutoDataSeeding([typeof(AppDbContext).Assembly])); // optional: run seed data
```

## Customisation reference

There is no options class — everything is a registration argument, an attribute, or a virtual member you override.

| Knob | Where | Default | Effect |
|---|---|---|---|
| `UseAutoConfigModel<TContext>(params Assembly[]?)` | `DbContextOptionsBuilder` | `[typeof(TContext).Assembly]` when no assemblies are passed | Assemblies scanned for `IEntityTypeConfiguration<T>`, `IGlobalModelBuilder`, and `[SqlSequence]` enums. |
| `UseAutoDataSeeding(Assembly[])` | `DbContextOptionsBuilder` | not wired | Separate opt-in; attaches discovered `IDataSeedingConfiguration` types to EF Core's `UseSeeding`/`UseAsyncSeeding`. |
| `AddGlobalModelBuilder<TImplementation>()` | `IServiceCollection` | none | Adds a model builder that assembly scanning would not find. Merged and de-duplicated with the scanned ones. |
| `AddEfCoreExceptionHandler<TDbContext, THandler>()` | `IServiceCollection` | none | Registers a keyed `IEfCoreExceptionHandler` for that `DbContext`. Idempotent. |
| `IEfCoreExceptionHandler.MaxRetryCount` | interface default | `3` | Retry cap for `SaveChangesWithConcurrencyHandlingAsync`; past it the method returns `0` instead of throwing. |
| `EfConcurrencyResolution` | handler return value | — | `IgnoreChanges`, `RetrySaveChanges`, or `RethrowException`. |
| `GlobalQueryFilter.FilterKey` | your override | required | Named EF Core query-filter key. |
| `GlobalQueryFilter.IsIgnorable` | your override | `true` | Whether a specification's `IgnoreQueryFilters()` may bypass this filter. Override to `false` for isolation filters. |
| `IDataSeedingConfiguration.Order` | your override | `0` | **Declared but never read** — seeders run in assembly-scan order. |
| `DefaultEntityTypeConfiguration<T>.Configure` | your override | applies key, audit and concurrency conventions | Call `base.Configure(builder)` first, then add your own mapping. |
| `[Sequence]` / `[SqlSequence]` | `DKNet.EfCore.Abstractions` | see that package | Registered only when the provider is SQL Server or Npgsql. |

`DefaultEntityTypeConfiguration<T>` applies, by reflection: `Id` as the key (`ValueGeneratedOnAdd`, plus
`GuidV7ValueGenerator` for `Guid`), `IAuditedProperties` columns (`CreatedBy`/`CreatedOn` required, max length
255, insert-only; `UpdatedBy` max length 255), and `IConcurrencyEntity<T>.RowVersion` as a row-version
concurrency token.

Full feature guide, composition with Hooks/Events/AuditLogs/DataAuthorization, and gotchas:
https://github.com/baoduy/DKNet/blob/main/docs/EfCore/DKNet.EfCore.Extensions.md
