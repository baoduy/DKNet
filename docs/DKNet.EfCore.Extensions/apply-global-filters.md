# Apply named filters across matching entities

Part of [DKNet.EfCore.Extensions](index.md). Use `IGlobalModelBuilder` for model-wide changes and `GlobalQueryFilter` for per-entity named query filters.

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
db.Items.AddRange(new Item { Name = "visible" }, new Item { Name = "hidden" });
await db.SaveChangesAsync();
Console.WriteLine(await db.Items.CountAsync()); // 1

public sealed class DemoDb(DbContextOptions<DemoDb> options) : DbContext(options)
{
    public DbSet<Item> Items => Set<Item>();
}
public sealed class Item
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}
public sealed class VisibleItems : GlobalQueryFilter
{
    public override string FilterKey => nameof(VisibleItems);
    protected override IEnumerable<Microsoft.EntityFrameworkCore.Metadata.IMutableEntityType>
        GetEntityTypes(ModelBuilder model) => model.Model.GetEntityTypes().Where(t => t.ClrType == typeof(Item));
    protected override System.Linq.Expressions.Expression<Func<T, bool>>? HasQueryFilter<T>(DbContext context)
        => x => EF.Property<string>(x, nameof(Item.Name)) != "hidden";
}
```

![Model building discovers VisibleItems, selects Item metadata and installs the named filter so queries exclude hidden items.](diagrams/apply-global-filters.svg)

`GlobalQueryFilter.Apply` selects entity types, asks `HasQueryFilter<TEntity>` for each expression and installs only non-null results. Exceptions from that reflected call are unwrapped.

## ⚙️ Configuration reference

| Setting | Type | Default | Effect |
|---|---|---|---|
| `FilterKey` | `string` | abstract | EF Core named filter key. |
| `IsIgnorable` | `bool` | `true` | Includes the key in static `IgnorableFilterKeys` for specification bypass. |
| `AddGlobalModelBuilder<T>()` | type registration | none | Merge an extra type with scanned types, distinct by type. |

| Override point | How you customise it |
|---|---|
| `IGlobalModelBuilder.Apply(ModelBuilder, DbContext)` | Implement it directly for a model-wide change that is not a named query filter (such as adding extra entities to the model). Derive from `GlobalQueryFilter` instead when the change is a per-entity named filter. |
| `GlobalQueryFilter.GetEntityTypes(ModelBuilder)` / `HasQueryFilter<TEntity>(DbContext)` | Override both in your `GlobalQueryFilter` subclass to select entity types and build each one's filter expression. |
| `GlobalQueryFilter.IsIgnorable` | `virtual`, defaults to `true`. Override to `false` for a filter that must never be bypassed by `ISpecification.IsIgnoreQueryFilters`. |

Use [DataAuthorization](../DKNet.EfCore.DataAuthorization/index.md) for the existing ownership filter; it sets `IsIgnorable = false`.

## 🧱 Where it fits

| DKNet consumer | Package | What it does with this feature |
|---|---|---|
| `DataOwnerAuthQuery` | [DKNet.EfCore.DataAuthorization](../DKNet.EfCore.DataAuthorization/filter-owned-rows.md) | Derives from `GlobalQueryFilter` for its row-ownership filter and overrides `IsIgnorable` to `false` so specifications cannot bypass it. |
| `SpecificationExtensions.ApplySpecs` | [DKNet.EfCore.Specifications](../DKNet.EfCore.Specifications/compose-specifications.md) | Reads `GlobalQueryFilter.IgnorableFilterKeys` to decide which named filters `IgnoreQueryFilters()` is allowed to bypass. |

**Entry point in your app:** derive from `GlobalQueryFilter` (or implement `IGlobalModelBuilder` directly), then either let assembly scanning during `UseAutoConfigModel` (see [Apply model configurations and entity conventions](configure-model.md)) discover it, or call `AddGlobalModelBuilder<T>()` explicitly.

## ⚠️ Gotchas & limits

- Both assembly-discovered and explicitly registered builders use `Activator.CreateInstance`; provide a usable parameterless constructor. `AddGlobalModelBuilder<T>()` does not resolve constructor dependencies from DI.
- Explicit builder types live in a process-wide append-only collection; they are not scoped to a service provider or DbContext.
- `IsIgnorable` governs DKNet specification bypass policy. Direct EF Core filter bypass APIs remain available; this is not a database security boundary.
