# DKNet.EfCore.Relational.Helpers

Small set of `DbContext` extension methods for relational-provider bookkeeping: table creation without a migration, an already-open connection, resolved schema/table names, and table-existence checks.

## Install

```bash
dotnet add package DKNet.EfCore.Relational.Helpers
```

## Features

- `CreateTableAsync<TEntity>()` — ensures the database exists and creates the table for `TEntity` if it isn't already there (one-shot, not a migration)
- `GetDbConnection()` — returns the `DbContext`'s underlying `DbConnection`, opening it if closed
- `GetTableName<TEntity>()` — resolves the schema and table name EF Core mapped for `TEntity` (with the SQL Server `dbo` default applied)
- `TableExistsAsync<TEntity>()` — checks whether the table for `TEntity` exists in the database

## Quick start

```csharp
using DKNet.EfCore.Relational.Helpers;

if (!await dbContext.TableExistsAsync<Product>())
{
    await dbContext.CreateTableAsync<Product>();
}

var (schema, table) = dbContext.GetTableName<Product>();
```

## Documentation

Full method reference, gotchas, and provider notes: https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/DKNet.EfCore.Relational.Helpers.md

## License

MIT © drunkcoding

## Repository

https://github.com/baoduy/DKNet
