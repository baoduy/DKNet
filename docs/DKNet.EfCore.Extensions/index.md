# DKNet.EfCore.Extensions

Model configuration, seeding, change-tracker helpers and sensitive JSON filtering for .NET developers using EF Core.

## ✨ Why use it?

- Apply configuration classes and shared entity conventions without repeating assembly scans per context.
- Reuse named filters, primary-key-aware seeders and sequence metadata across a relational model.
- Inspect save snapshots and opt individual JSON serializers into sensitive-property filtering.

## 🚀 Quick Start

Use a .NET 10 console project. Install the package, then replace `Program.cs` with the example.

```bash
dotnet new console -f net10.0 -n EfCoreDemo
cd EfCoreDemo
dotnet add package DKNet.EfCore.Extensions
dotnet add package Microsoft.EntityFrameworkCore.Sqlite --version 10.0.12
```

```csharp
using Microsoft.EntityFrameworkCore;
using DKNet.EfCore.Extensions.Configurations;

using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
connection.Open();
var o = new DbContextOptionsBuilder<DemoDb>();
o.UseSqlite(connection).UseAutoConfigModel<DemoDb>();
await using var db = new DemoDb(o.Options);
await db.Database.EnsureCreatedAsync();
var item = new Item { Name = "Coffee" };
db.Add(item);
await db.SaveChangesAsync();
Console.WriteLine(item.Id != Guid.Empty); // True

public sealed class DemoDb(DbContextOptions<DemoDb> options) : DbContext(options)
{
    public DbSet<Item> Items => Set<Item>();
}
public sealed class Item : DKNet.EfCore.Abstractions.Entities.Entity
{
    public string Name { get; set; } = "";
}
public sealed class ItemConfiguration : DefaultEntityTypeConfiguration<Item>
{
    public override void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Item> b)
    {
        base.Configure(b);
        b.Property(x => x.Name).HasMaxLength(80);
    }
}
```

Run with `dotnet run`.

## 🏗️ Architecture

![The host configures AutoConfigModelCustomizer for EF model construction and calls independent snapshot, save and serialization helpers.](diagrams/components.svg)

Model customization runs in EF Core model building and delegates to the original provider customizer. Global builder registrations and filter-key metadata are process-wide; seeders, snapshots and JSON filtering are independent opt-ins. The project references Abstractions, Fw.Extensions and EF Core Relational.

## 🔄 How it works

![UseAutoConfigModel stores assemblies, applies entity configurations and global builders, then registers sequences on SQL Server or Npgsql.](diagrams/efcore-extensions-model-build.svg)

The default assembly is the context assembly. Configuration classes introduce their entities; the scan does not map arbitrary plain types. Activation, configuration and provider errors propagate; seeding is configured separately.

## 🧩 Features

- [Apply model configurations and entity conventions](configure-model.md) — Discover `IEntityTypeConfiguration<T>` implementations and apply DKNet identity, audit and concurrency conventions through a configuration base.
- [Apply named filters across matching entities](apply-global-filters.md) — Use `IGlobalModelBuilder` for model-wide changes and `GlobalQueryFilter` for per-entity named query filters.
- [Seed missing rows through EF Core callbacks](seed-data.md) — Discover `IDataSeedingConfiguration` implementations and run them through EF Core native seeding callbacks.
- [Read and format a database sequence value](read-sequences.md) — Use the enum sequence metadata to register a model sequence and fetch its next value on SQL Server or Npgsql.
- [Read keys and tracked property values](inspect-entries.md) — Inspect model keys and original/current entry values without repeating metadata lookups.
- [Stage new entities found in collection navigations](discover-navigation-entities.md) — Walk collection navigations from possible updating roots and add discovered new children in one batch.
- [Retry a save after a concurrency conflict](handle-concurrency.md) — Wrap an async save in a bounded retry loop using `IEfCoreExceptionHandler`.
- [Capture a save-time snapshot for hooks](capture-save-snapshot.md) — Keep the entries and their pre-save states available after EF Core has changed tracking state.
- [Withhold sensitive properties from unauthorised callers](filter-sensitive-responses.md) — Opt a `JsonSerializerOptions` instance into role-aware omission for properties declared `[SensitiveData]`.

## 🔗 Related packages

- [DKNet.EfCore.Abstractions](../DKNet.EfCore.Abstractions/index.md) — reach for it to define entities and policy metadata.
- [DKNet.EfCore.Hooks](../DKNet.EfCore.Hooks/index.md) — reach for it to run work around async saves.
- [DKNet.EfCore.Relational.Helpers](../DKNet.EfCore.Relational.Helpers/index.md) — reach for it to inspect or provision relational tables.
- [DKNet.EfCore.AuditLogs](../DKNet.EfCore.AuditLogs/index.md) — reach for it for field-level audit capture.
- [DKNet.EfCore.DataAuthorization](../DKNet.EfCore.DataAuthorization/index.md) — reach for it for the existing ownership filter and stamping hook.
