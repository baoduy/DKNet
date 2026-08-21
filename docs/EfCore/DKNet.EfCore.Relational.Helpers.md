# DKNet.EfCore.Relational.Helpers

Small set of `DbContext` extension methods for relational-provider bookkeeping that EF Core doesn't expose directly: ensuring a single table exists without a full migration, getting an already-open connection, resolving an entity's actual schema/table name, and checking whether a table exists before you query it. Reach for it when you're writing infrastructure code (seed scripts, diagnostics, multi-tenant table provisioning, health checks) that needs to know or touch the physical schema behind a `DbContext`, rather than going through `Database.Migrate()` or raw ADO.NET yourself.

## Install and minimum usage

```bash
dotnet add package DKNet.EfCore.Relational.Helpers
```

```csharp
using DKNet.EfCore.Relational.Helpers;

var (schema, table) = dbContext.GetTableName<Product>();
```

## Extension methods

All four methods live on the single static class `DbContextHelpers` and extend `DbContext`.

### `CreateTableAsync<TEntity>`

```csharp
public static Task CreateTableAsync<TEntity>(
    this DbContext dbContext,
    CancellationToken cancellationToken = default)
    where TEntity : class
```

Ensures the database exists, then — if the table for `TEntity` isn't already there — creates the full physical schema via `RelationalDatabaseCreator.CreateTablesAsync`. This is EF Core's "ensure created" mechanism, not a migration: it does not update `__EFMigrationsHistory` and does not apply subsequent model changes, so calling it against a database that already has some tables (e.g. one created by migrations) is intended for one-shot/first-run scenarios, per the XML doc: "ensure this method is called only once."

```csharp
await using var db = new AppDbContext(options);
await db.CreateTableAsync<AuditLogEntry>();
```

Saves you from hand-rolling the `IDatabaseCreator` → `RelationalDatabaseCreator` cast and the exists-check dance yourself.

### `GetDbConnection`

```csharp
public static Task<DbConnection> GetDbConnection(
    this DbContext dbContext,
    CancellationToken cancellationToken = default)
```

Returns `dbContext.Database.GetDbConnection()`, opening it first if it's currently closed. Saves the `if (conn.State == ConnectionState.Closed) await conn.OpenAsync();` boilerplate every caller otherwise repeats before running raw ADO.NET against the same connection EF Core is using.

```csharp
var conn = await dbContext.GetDbConnection();
using var cmd = conn.CreateCommand();
cmd.CommandText = "SELECT COUNT(*) FROM Products";
var count = (int)await cmd.ExecuteScalarAsync();
```

### `GetTableName<TEntity>`

```csharp
public static (string? Schema, string? TableName) GetTableName<TEntity>(this DbContext dbContext)
```

Looks up `TEntity` in the EF Core model and returns its resolved schema and table name, falling back through `GetSchema()` → `GetDefaultSchema()` → (on SQL Server only) `"dbo"`, and `GetTableName()` → `GetDefaultTableName()`. Returns `(null, null)` if `TEntity` isn't part of the model. Saves you from reaching into `dbContext.Model.FindEntityType(...)` and knowing the right fallback order and the SQL Server `dbo` default yourself.

```csharp
var (schema, table) = dbContext.GetTableName<Product>();
// schema == "dbo", table == "Products" on SQL Server with no explicit mapping
```

### `TableExistsAsync<TEntity>`

```csharp
public static Task<bool> TableExistsAsync<TEntity>(
    this DbContext dbContext,
    CancellationToken cancellationToken = default)
    where TEntity : class
```

Runs `dbContext.Set<TEntity>().AnyAsync()` and reports `true`/`false` instead of letting the underlying `DbException` (e.g. "invalid object name") propagate. Useful as a guard before conditional DDL or before `CreateTableAsync` (which itself calls this internally).

```csharp
if (!await dbContext.TableExistsAsync<Product>())
{
    await dbContext.CreateTableAsync<Product>();
}
```

Note this issues a real query against the table (`SELECT ... WHERE EXISTS/LIMIT 1`-style), not a metadata-only check — on a large or locked table that carries the same cost as any other query.

## Configuration options

None. There's no options class, no DI registration, and no `IServiceCollection` extension — these are plain static extension methods you call directly on a `DbContext` instance. Nothing to configure or wire up.

## Composition with other DKNet packages

Standalone. This package has no dependency beyond `Microsoft.EntityFrameworkCore` and `Microsoft.EntityFrameworkCore.Relational`, doesn't reference `DKNet.EfCore.Abstractions` or any other DKNet package, and none of the other EfCore packages (`Repos`, `Events`, `Hooks`, `Specifications`) call into it. It's a small relational-provider utility you can add to any EF Core project independent of the rest of the DKNet stack.

## Gotchas and limits

- **Not a migration tool.** `CreateTableAsync` uses `EnsureCreatedAsync`/`CreateTablesAsync`, which is incompatible with EF Core Migrations on the same database — mixing the two leads to a database with tables but no (or an inconsistent) `__EFMigrationsHistory`. Use it for first-run/dev/test provisioning, not as a substitute for `dbContext.Database.Migrate()` in production.
- **Requires a relational provider.** All four methods depend on `Microsoft.EntityFrameworkCore.Relational` types (`RelationalDatabaseCreator`, `DbConnection`, schema/table metadata) — they will not work with non-relational providers (e.g. Cosmos).
- **`dbo` default schema is SQL Server–only.** `GetTableName<TEntity>` only substitutes `"dbo"` when the provider name is exactly `Microsoft.EntityFrameworkCore.SqlServer` (case-insensitive). On PostgreSQL, SQLite, or any other provider, an entity with no explicit schema returns `Schema = null` — callers must not assume `dbo` cross-provider.
- **`TableExistsAsync` swallows only `DbException`.** Any other exception (e.g. a cancellation, or a provider throwing something outside `DbException`) still propagates.
- **`GetDbConnection` mutates connection state.** It opens the connection as a side effect if closed; callers are responsible for not leaving it open longer than intended when managing the connection outside of EF Core's own lifecycle.
