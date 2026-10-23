# Create a model schema behind one table gate

Part of [DKNet.EfCore.Relational.Helpers](index.md). Use `CreateTableAsync<TEntity>` only for one-shot provisioning against an empty relational schema.

Use the [Quick Start](index.md#-quick-start) console project. Set `DKNET_DOCS_DB` to a PostgreSQL connection string with permission to create tables in a dedicated empty database. Each example uses `EnsureCreatedAsync` or whole-model creation; run each against its own database.

```csharp
using Microsoft.EntityFrameworkCore;
using DKNet.EfCore.Relational.Helpers;

var connectionString = Environment.GetEnvironmentVariable("DKNET_DOCS_DB")
    ?? throw new InvalidOperationException("Set DKNET_DOCS_DB to an empty PostgreSQL database.");
var o = new DbContextOptionsBuilder<DemoDb>();
o.UseNpgsql(connectionString);
await using var db = new DemoDb(o.Options);
await db.CreateTableAsync<Item>();
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

![CreateTableAsync ensures the database exists, probes the selected entity table and either returns or invokes whole-model CreateTablesAsync.](diagrams/efcore-relational-helpers-create-table.svg)

`TEntity` chooses only the existence gate. If that table exists the method returns even when other model tables are absent. Otherwise EF Core creates the full model schema, not just this entity table.

## ⚙️ Configuration reference

| Argument | Type | Default | Effect |
|---|---|---|---|
| `TEntity` | class | required | Table whose presence skips the whole-model creation step. |
| `cancellationToken` | `CancellationToken` | `default` | Forwarded to database creation, table probe and schema creation. |

## 🧱 Where it fits

DbContextHelpers calls EF Core relational database-creator services and the configured database catalog. The package uses Extensions for the SQL Server provider-name check; schema creation is owned by the provider.

![The package component map shows the host, contracts and consumers involved in this capability.](diagrams/components.svg)

## ⚠️ Gotchas & limits

- This is not a migration or a repair operation. A partially populated schema can fail because whole-model creation attempts tables that already exist.
- No migration-history updates or model-diff application are performed. Do not mix this provisioning path with migrations for the same database.
- No concurrency lock surrounds the check/create steps. Coordinate callers externally.
- The method casts IDatabaseCreator to RelationalDatabaseCreator and uses the same INFORMATION_SCHEMA probe; use a compatible relational provider.
