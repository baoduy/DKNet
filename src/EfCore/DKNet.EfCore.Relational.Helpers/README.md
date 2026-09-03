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

## Customisation reference

There is no options class, no `IServiceCollection` extension, and no MSBuild switch — the surface is the four
methods' parameters and the provider-dependent behaviour they inherit.

| Method | Knob | Type | Default | Effect |
|---|---|---|---|---|
| `CreateTableAsync<TEntity>` | `TEntity` | `class` | required | Picks the table whose existence is probed. Creation itself is not scoped to it — `CreateTablesAsync` creates every table missing from the model. |
| `CreateTableAsync<TEntity>` | `cancellationToken` | `CancellationToken` | `default` | Flows into the exists check, `EnsureCreatedAsync`, the table probe and `CreateTablesAsync`. |
| `GetDbConnection` | `cancellationToken` | `CancellationToken` | `default` | Used only when the connection was closed and has to be opened. |
| `GetTableName<TEntity>` | default schema | — | `"dbo"` on SQL Server, otherwise `null` | Substituted only when the provider name is exactly `Microsoft.EntityFrameworkCore.SqlServer` and the model supplies no schema. |
| `GetTableName<TEntity>` | resolution order | — | `GetSchema()` → `GetDefaultSchema()` → default schema; `GetTableName()` → `GetDefaultTableName()` | Fixed. Returns `(null, null)` when `TEntity` is not in the model. |
| `TableExistsAsync<TEntity>` | swallowed exception | — | `DbException` only | Any `DbException` reads as "table absent"; everything else propagates. |
| `TableExistsAsync<TEntity>` | `cancellationToken` | `CancellationToken` | `default` | Flows into the `AnyAsync` probe. |

All four require a relational provider, and none of them record a migration — mixing `CreateTableAsync` with EF
Core Migrations on the same database leaves an inconsistent `__EFMigrationsHistory`.

## Documentation

Full method reference, gotchas, and provider notes: https://github.com/baoduy/DKNet/blob/main/docs/EfCore/DKNet.EfCore.Relational.Helpers.md

## License

MIT © drunkcoding

## Repository

https://github.com/baoduy/DKNet
