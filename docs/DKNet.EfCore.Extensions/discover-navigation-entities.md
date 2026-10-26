# Stage new entities found in collection navigations

Part of [DKNet.EfCore.Extensions](index.md). Walk collection navigations from possible updating roots and add discovered new children in one batch.

Use the console-project setup in the [Quick Start](index.md#-quick-start), then replace `Program.cs` with this example.

```csharp
using Microsoft.EntityFrameworkCore;
using DKNet.EfCore.Extensions.Extensions;

using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
connection.Open();
var o = new DbContextOptionsBuilder<DemoDb>();
o.UseSqlite(connection);
await using var db = new DemoDb(o.Options);
await db.Database.EnsureCreatedAsync();
var parent = new Item { Name = "Coffee" };
db.Add(parent);
await db.SaveChangesAsync();
db.ChangeTracker.AutoDetectChangesEnabled = false;
parent.Name = "Coffee beans";
db.Entry(parent).Property(x => x.Name).IsModified = true;
parent.Children.Add(new Child());
Console.WriteLine(db.GetNewEntitiesFromNavigations(db.Entry(parent)).Count()); // 1
Console.WriteLine(await db.AddNewEntitiesFromNavigations()); // 1: the child is considered new
db.ChangeTracker.AutoDetectChangesEnabled = true;
await db.SaveChangesAsync();

public sealed class DemoDb(DbContextOptions<DemoDb> options) : DbContext(options)
{
    public DbSet<Item> Items => Set<Item>();
}
public sealed class Item
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<Child> Children { get; set; } = [];
}
public sealed class Child
{
    public int Id { get; set; }
    public int ItemId { get; set; }
}
```

![AddNewEntitiesFromNavigations selects Modified or Detached tracked roots, reads collection navigation values, checks each child with IsNewEntity and adds the resulting batch.](diagrams/discover-navigation-entities.svg)

`GetPossibleUpdatingEntities` calls DetectChanges and selects Detached/Modified entries. `GetNewEntitiesFromNavigations(entry)` scans one root; its parameterless form scans those possible roots. `GetCollectionNavigations(Type)` selects collection, non-shadow `INavigation` metadata.

## ⚠️ Gotchas & limits

- This is a single-level collection walk. Reference navigations and skip navigations are not selected; Unchanged and Added roots are excluded by the automatic root selector.
- EF Core detection can already stage children. The example disables automatic detection so the explicit-root overload sees a Detached child first. The batch helper calls DetectChanges, then stages the child returned by its scan. Added children also satisfy IsNewEntity, so the count can include already tracked additions.
- `GetNavigationValues(object, INavigation)` reads a property or backing field and returns an empty sequence when no enumerable is available. The collection walker itself uses EF entry collection access.
- The discovery sequence can yield the same child through multiple roots; do not interpret the returned count as a unique database insertion count.
