# DKNet.EfCore.Relational.Helpers

Relational table metadata, existence, connection and one-shot schema helpers for .NET developers writing infrastructure code.

## ✨ Why use it?

- Resolve mapped table and schema names without a database round trip.
- Ask the database catalog whether a mapped table exists without probing entity rows.
- Use the context connection for ADO.NET or provision an empty model schema through an explicit table gate.

## 🚀 Quick Start

Use a .NET 10 console project. Install the package, then replace `Program.cs` with the example.

```bash
dotnet new console -f net10.0 -n EfCoreDemo
cd EfCoreDemo
dotnet add package DKNet.EfCore.Relational.Helpers
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL --version 10.0.3
```

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

Run with `dotnet run`. This metadata-only example does not connect to the placeholder endpoint.

## Documentation

| Guide | Read when |
|---|---|
| [Package overview](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.Relational.Helpers/index.md) | Start with architecture and the end-to-end flow. |
| [Resolve an entity table and schema](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.Relational.Helpers/resolve-table-names.md) | Read the physical table name and schema from EF Core model metadata without opening a connection. |
| [Open the context connection for ADO.NET](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.Relational.Helpers/open-context-connection.md) | Get the connection already associated with the DbContext, opening it when its state is Closed. |
| [Check whether a mapped table exists](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.Relational.Helpers/check-table-existence.md) | Query `INFORMATION_SCHEMA.TABLES` using the entity mapped table and, when known, schema. |
| [Create a model schema behind one table gate](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.Relational.Helpers/create-model-tables.md) | Use `CreateTableAsync<TEntity>` only for one-shot provisioning against an empty relational schema. |
