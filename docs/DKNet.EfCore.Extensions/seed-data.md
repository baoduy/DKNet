# Seed missing rows through EF Core callbacks

Part of [DKNet.EfCore.Extensions](index.md). Discover `IDataSeedingConfiguration` implementations and run them through EF Core native seeding callbacks.

Use the console-project setup in the [Quick Start](index.md#-quick-start), then replace `Program.cs` with this example.

```csharp
using Microsoft.EntityFrameworkCore;
using DKNet.EfCore.Extensions.Configurations;
using DKNet.EfCore.Extensions.Extensions;

using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
connection.Open();
var o = new DbContextOptionsBuilder<DemoDb>();
o.UseSqlite(connection).UseAutoDataSeeding([typeof(DemoDb).Assembly]);
await using var db = new DemoDb(o.Options);
await db.Database.EnsureCreatedAsync();
await db.Database.EnsureCreatedAsync();
Console.WriteLine(await db.Items.CountAsync()); // 1

public sealed class DemoDb(DbContextOptions<DemoDb> options) : DbContext(options)
{
    public DbSet<Item> Items => Set<Item>();
}
public sealed class ItemSeed : DataSeedingConfiguration<Item>
{
    protected override ValueTask<ICollection<Item>> GetDataAsync(CancellationToken cancellation = default)
        => ValueTask.FromResult<ICollection<Item>>([new Item { Id = 7, Name = "Coffee" }]);
}
public sealed class Item
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}
```

![UseAutoDataSeeding discovers ItemSeed, hooks EF Core seeding, compares candidate primary keys and adds only missing rows before SaveChangesAsync.](diagrams/seed-data.svg)

`DataSeedingConfiguration<TEntity>` loads existing primary-key projections, including composite and shadow keys. Explicit non-default keys are compared by values, independent of entity equality. Empty candidates or no missing rows cause no save.

## ⚙️ Configuration reference

| Option | Type | Default | Effect |
|---|---|---|---|
| `assemblies` | `Assembly[]` | required | Scan seed configurations; an empty array finds none. Null throws. |
| `Order` | `int` | `0` | Metadata only; discovery does not sort it. |
| `EntityType` | `Type` | `typeof(TEntity)` | Exposes the targeted CLR type. |
| `SeedAsync` | nullable callback on interface | base implementation | Base loads GetDataAsync and inserts missing keys. Null callbacks are skipped. |

## ⚠️ Gotchas & limits

- Seeding is a separate opt-in from model configuration. Implementations are instantiated once with parameterless constructors when options are configured.
- `Order` is declared but not sorted or otherwise read by the discovery runner. Combine dependent operations in one seeder.
- All-default keys count as unset and always new. Use stable explicit keys for repeatable seeding. Candidate duplicates are not removed within one seed batch.
- This inserts missing rows; it does not update existing rows or delete obsolete data. Query filters on the DbSet affect which existing keys are seen.
- The synchronous callback blocks on SeedAsync with `CancellationToken.None`; the async callback forwards its cancellation token.
