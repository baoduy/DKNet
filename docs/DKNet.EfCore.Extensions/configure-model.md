# Apply model configurations and entity conventions

Part of [DKNet.EfCore.Extensions](index.md). Discover `IEntityTypeConfiguration<T>` implementations and apply DKNet identity, audit and concurrency conventions through a configuration base.

Use the console-project setup in the [Quick Start](index.md#-quick-start), then replace `Program.cs` with this example.

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

![UseAutoConfigModel records assemblies, AutoConfigModelCustomizer applies their configurations, and ItemConfiguration assigns GuidV7ValueGenerator to the Id.](diagrams/configure-model.svg)

`AutoConfigModelCustomizer` applies configurations, global builders and supported-provider sequences before delegating to the original model customizer. `DefaultEntityTypeConfiguration<TEntity>` is opt-in: derive and call `base.Configure` before your overrides.

## ⚙️ Configuration reference

| Setting | Type | Default | Effect |
|---|---|---|---|
| `UseAutoConfigModel<TContext>(assemblies)` | `Assembly[]?` | Context assembly | Assemblies to scan. |
| Nongeneric `UseAutoConfigModel(assemblies)` | `Assembly[]` | explicit argument | Empty list falls back to the runtime context assembly. |

The options extension preserves the provider model customizer and wraps `IModelCacheKeyFactory` with a global-builder registration version. Replacing the cache-key factory later also replaces that wrapper; account for builder registration in your custom key.

## ⚠️ Gotchas & limits

- The scan applies configuration classes; it does not discover every plain entity automatically. A `DbSet<T>` or a configuration still needs to introduce the type.
- An `Id` property is sufficient for key conventions; numeric IDs are generated on add and Guid IDs use `Guid.CreateVersion7()`. `GuidV7ValueGenerator.GeneratesTemporaryValues` is false.
- Audited creation fields are required and ignored after save; both By fields map to 255 characters. These conventions do not stamp field values.
- Concurrency interfaces configure RowVersion as a generated concurrency token. `[IgnoreEntity]` has no effect in this scanner.
- Repeated `UseAutoConfigModel` calls reuse the existing extension; supply the complete assembly set on the first call.
