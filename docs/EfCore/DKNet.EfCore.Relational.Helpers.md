# DKNet.EfCore.Relational.Helpers

Four `DbContext` extension methods for relational-provider bookkeeping that EF Core does not expose directly — table
creation, connection access, table-name resolution, and table-existence checks.

## ✨ Why use it?

- **Provision one table without a migration** — `CreateTableAsync<TEntity>()` does the
  `IDatabaseCreator` → `RelationalDatabaseCreator` cast, the database-exists check, and the table-exists check for you,
  so first-run/test provisioning is a single call instead of a dozen lines of EF Core internals.
- **Reach the connection EF Core is already using** — `GetDbConnection()` returns the context's own `DbConnection`,
  opened if closed, so raw ADO.NET runs on the same connection (and the same transaction) rather than a second one.
- **Know an entity's real schema and table** — `GetTableName<TEntity>()` walks EF Core's fallback chain
  (`GetSchema()` → `GetDefaultSchema()` → SQL Server's `dbo`) so diagnostics, seed scripts, and hand-written SQL do not
  hard-code names the model may have remapped.
- **Ask whether a table exists with a cheap, ANSI-standard catalog lookup** — `TableExistsAsync<TEntity>()` queries
  `INFORMATION_SCHEMA.TABLES` (supported by both SQL Server and PostgreSQL, no provider branching) instead of
  querying the table itself and catching a failure, which is what a health check or a conditional-DDL guard
  actually wants — and a genuine infrastructure error (permissions, timeout, a dropped connection) now propagates
  instead of silently reading as "table absent."

Reach for this package when writing infrastructure code — seed routines, diagnostics, multi-tenant table
provisioning, health checks — that needs to know or touch the physical schema behind a `DbContext`. It is standalone:
no other DKNet package is required.

## 🚀 Quick Start

```bash
dotnet add package DKNet.EfCore.Relational.Helpers
```

```csharp
using DKNet.EfCore.Relational.Helpers;

var (schema, table) = dbContext.GetTableName<Product>();
```

## 🧩 Features

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

Resolves `TEntity`'s schema and table name via `GetTableName<TEntity>()` (throwing `InvalidOperationException` if
`TEntity` isn't part of the model), then queries the ANSI-standard `INFORMATION_SCHEMA.TABLES` catalog view —
`SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = ... [AND TABLE_SCHEMA = ...]` — and reports
`true` when the count is greater than zero. The same query works on SQL Server and PostgreSQL without any
provider-specific branching.

```csharp
if (!await dbContext.TableExistsAsync<Product>())
{
    await dbContext.CreateTableAsync<Product>();
}
```

This is a metadata-only lookup against the catalog view, not a query against `Product` itself — it doesn't touch or
lock the target table the way an earlier revision's `Set<TEntity>().AnyAsync()` probe did. It also no longer
swallows exceptions: a genuine infrastructure failure (a permissions error, a timeout, a dropped connection) now
propagates from `SingleAsync()` instead of being read as "table absent" — see
[Gotchas & limits](#-gotchas--limits) for what this means for callers written against the old behaviour.

## ⚙️ Configuration reference

There is no options class, no `IServiceCollection` extension, and no MSBuild switch — the entire customisation
surface is the four methods' own parameters and the provider-dependent behaviour they inherit.

| Method | Knob | Type | Default | Effect |
|---|---|---|---|---|
| `CreateTableAsync<TEntity>` | `TEntity` | `class` | required | Chooses the table whose existence is probed. The creation step itself is **not** scoped to it — `CreateTablesAsync` creates every table missing from the model. |
| `CreateTableAsync<TEntity>` | `cancellationToken` | `CancellationToken` | `default` | Passed to `ExistsAsync`, `EnsureCreatedAsync`, `TableExistsAsync` and `CreateTablesAsync`. |
| `GetDbConnection` | `cancellationToken` | `CancellationToken` | `default` | Passed to `OpenAsync`, and only used when the connection was closed. |
| `GetTableName<TEntity>` | default schema | — | `"dbo"` on SQL Server, otherwise `null` | Substituted only when the provider name is exactly `Microsoft.EntityFrameworkCore.SqlServer` (case-insensitive) and the model supplies no schema. |
| `GetTableName<TEntity>` | resolution order | — | `GetSchema()` → `GetDefaultSchema()` → default schema; `GetTableName()` → `GetDefaultTableName()` | Fixed; returns `(null, null)` when `TEntity` is not in the model. |
| `TableExistsAsync<TEntity>` | swallowed exception | — | none | A zero-row `INFORMATION_SCHEMA.TABLES` result reads as "table absent"; every exception, `DbException` included, propagates. |
| `TableExistsAsync<TEntity>` | `cancellationToken` | `CancellationToken` | `default` | Passed to the `INFORMATION_SCHEMA.TABLES` query's `SingleAsync()`. |

## 🧱 Where it fits

`CreateTableAsync<TEntity>` is the only method here with branching worth a picture — and the branch that surprises
people is the last one, where a single missing table triggers creation of every missing table:

![Workflow diagram of CreateTableAsync: it first asks the relational database creator whether the database exists and calls EnsureCreatedAsync when it does not, then probes TableExistsAsync for the entity. If the table is already there it returns without touching the database; if it is missing, CreateTablesAsync runs and creates every missing table in the model, not only the requested one.](../diagrams/efcore-relational-helpers-create-table.svg)

Otherwise standalone. Beyond `Microsoft.EntityFrameworkCore` and `Microsoft.EntityFrameworkCore.Relational`, this
package has a `ProjectReference` on [`DKNet.EfCore.Extensions`](./DKNet.EfCore.Extensions.md) — `GetTableName<TEntity>`'s
SQL Server `dbo` default is resolved via that package's `DbContext.IsSqlServer()` (an ambient `Microsoft.EntityFrameworkCore`
extension) rather than a private copy of the same check. It doesn't reference `DKNet.EfCore.Abstractions`, and none
of the other EfCore packages (`Repos`, `Events`, `Hooks`, `Specifications`) call into it. It's a small
relational-provider utility you can add to any EF Core project independent of the rest of the DKNet stack.

## ⚠️ Gotchas & limits

- **There is nothing to configure and nothing to register.** No options class, no `IServiceCollection`
  extension — these are plain static extension methods called directly on a `DbContext` instance.
- **`CreateTableAsync<TEntity>` is not scoped to `TEntity`, in either direction.** The type argument only picks
  which table is probed; once that probe comes back negative, `CreateTablesAsync` creates *every* table the model
  is missing. The other direction matters just as much and is easy to miss: if `TEntity`'s own table already
  exists, the probe comes back positive and the method returns immediately — it will **not** create or backfill
  any *other* table added to the model later, even though those tables are still missing. `CreateTableAsync<TEntity>`
  is a one-shot "does anything exist yet" gate, not a per-call "make sure everything is there" check.
- **Not a migration tool.** `CreateTableAsync` uses `EnsureCreatedAsync`/`CreateTablesAsync`, which is incompatible with EF Core Migrations on the same database — mixing the two leads to a database with tables but no (or an inconsistent) `__EFMigrationsHistory`. Use it for first-run/dev/test provisioning, not as a substitute for `dbContext.Database.Migrate()` in production.
- **Requires a relational provider.** All four methods depend on `Microsoft.EntityFrameworkCore.Relational` types (`RelationalDatabaseCreator`, `DbConnection`, schema/table metadata) — they will not work with non-relational providers (e.g. Cosmos).
- **`dbo` default schema is SQL Server–only.** `GetTableName<TEntity>` only substitutes `"dbo"` when the provider name is exactly `Microsoft.EntityFrameworkCore.SqlServer` (case-insensitive). On PostgreSQL, SQLite, or any other provider, an entity with no explicit schema returns `Schema = null` — callers must not assume `dbo` cross-provider.
- **`TableExistsAsync` no longer swallows exceptions at all — this is a behaviour change, not just an implementation
  detail.** It used to run a query against the target table and read *any* `DbException` as "table absent," which
  meant a permissions failure, a timeout, or a dropped connection all silently reported `false` too. It now queries
  `INFORMATION_SCHEMA.TABLES` and only reads a genuine zero-row result as "table absent" — every exception,
  including a `DbException` from a real infrastructure problem, now propagates to the caller instead. Code written
  against the old behaviour that relied on `TableExistsAsync` never throwing must add its own `try`/`catch` if it
  still wants to treat every failure as "absent."
- **`GetDbConnection` mutates connection state.** It opens the connection as a side effect if closed; callers are responsible for not leaving it open longer than intended when managing the connection outside of EF Core's own lifecycle.

## 🔗 Related packages

- [DKNet.EfCore.Extensions](./DKNet.EfCore.Extensions.md) – the DI/wiring layer. Reach for it instead when you want
  automatic entity configuration discovery, seeding, or sequences rather than one-off schema pokes.
- [DKNet.EfCore.Abstractions](./DKNet.EfCore.Abstractions.md) – the entity base classes and attributes the rest of
  the EfCore stack reads. Reach for it when you are modelling entities, not inspecting schema.
- [DKNet.EfCore.Specifications](./DKNet.EfCore.Specifications.md) – the supported way to query and persist through a
  `DbContext`. Reach for it for application-level data access; this package is for infrastructure-level schema work.
