# Check whether a mapped table exists

Part of [DKNet.EfCore.Relational.Helpers](index.md). Query `INFORMATION_SCHEMA.TABLES` using the entity mapped table and, when known, schema.

Use the [Quick Start](index.md#-quick-start) console project. Set `DKNET_DOCS_DB` to a PostgreSQL connection string with permission to create tables in a dedicated empty database. Each example uses `EnsureCreatedAsync` or whole-model creation; run each against its own database.

```csharp
using Microsoft.EntityFrameworkCore;
using DKNet.EfCore.Relational.Helpers;

var connectionString = Environment.GetEnvironmentVariable("DKNET_DOCS_DB")
    ?? throw new InvalidOperationException("Set DKNET_DOCS_DB to an empty PostgreSQL database.");
var o = new DbContextOptionsBuilder<DemoDb>();
o.UseNpgsql(connectionString);
await using var db = new DemoDb(o.Options);
await db.Database.EnsureCreatedAsync();
Console.WriteLine(await db.TableExistsAsync<Item>()); // True

public sealed class DemoDb(DbContextOptions<DemoDb> options) : DbContext(options)
{
    public DbSet<Item> Items => Set<Item>();
}
public sealed class Item
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}
```

![TableExistsAsync resolves schema and table metadata, executes a parameterized catalog count and returns true when count is positive.](diagrams/check-table-existence.svg)

The scalar query names its projected column `Value` for EF composition and uses SingleAsync with the caller cancellation token. It does not query entity rows.

## ⚠️ Gotchas & limits

- A type absent from the model throws InvalidOperationException before querying. A valid table with zero entity rows still exists.
- Permissions failures, timeouts, connection failures and cancellation propagate; they are not converted to false. This differs from the earlier target-table probe behavior.
- The catalog query is for SQL Server and PostgreSQL; SQLite does not supply INFORMATION_SCHEMA.TABLES. A null schema causes a name-only lookup across catalog schemas.
