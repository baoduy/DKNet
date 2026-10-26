# Resolve an entity table and schema

Part of [DKNet.EfCore.Relational.Helpers](index.md). Read the physical table name and schema from EF Core model metadata without opening a connection.

Use a .NET 10 console project, install this package and `Npgsql.EntityFrameworkCore.PostgreSQL` 10.x as in the [Quick Start](index.md#-quick-start). No running database is needed for this metadata example.

```csharp
using DKNet.EfCore.Relational.Helpers;
using Microsoft.EntityFrameworkCore;

var options = new DbContextOptionsBuilder<DemoDb>()
    .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused").Options;
using var db = new DemoDb(options);
Console.WriteLine(db.GetTableName<Item>()); // (, Items)

public sealed class DemoDb(DbContextOptions<DemoDb> options) : DbContext(options)
{
    public DbSet<Item> Items => Set<Item>();
}
public sealed class Item { public int Id { get; set; } }
```

![GetTableName finds Item in the model, resolves configured or default schema and table names, and returns their tuple.](diagrams/resolve-table-names.svg)

Schema resolution uses `GetSchema()`, then `GetDefaultSchema()`, then `dbo` for SQL Server only. Table resolution uses `GetTableName()` then `GetDefaultTableName()`.

## ⚠️ Gotchas & limits

- A type absent from the model returns `(null, null)`; the existence checker instead throws for that case.
- An unspecified schema stays null on non-SQL-Server providers. A null schema is not proof of which database search-path schema will be used.
