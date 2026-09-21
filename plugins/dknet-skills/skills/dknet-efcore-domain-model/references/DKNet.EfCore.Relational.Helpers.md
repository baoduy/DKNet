# DKNet.EfCore.Relational.Helpers

| Field | Value |
|---|---|
| Area | EfCore |
| Install | `dotnet add package DKNet.EfCore.Relational.Helpers` |
| NuGet | https://www.nuget.org/packages/DKNet.EfCore.Relational.Helpers |
| Docs | https://github.com/baoduy/DKNet/blob/main/docs/EfCore/DKNet.EfCore.Relational.Helpers.md |
| Source | https://github.com/baoduy/DKNet/tree/main/src/EfCore/DKNet.EfCore.Relational.Helpers |
| Depends on (DKNet) | `DKNet.EfCore.Extensions` (used only for the ambient `DbContext.IsSqlServer()` extension) |
| Depends on (3rd party) | `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Relational` |
| Target framework | `net10.0` |

## Purpose

A single static class, `DbContextHelpers`, adding four `DbContext` extension methods for relational-provider
schema bookkeeping that EF Core does not expose directly: one-shot table creation without a migration, access
to EF Core's own open `DbConnection`, schema/table-name resolution for an entity, and a catalog-view
table-existence check. Everything is a plain extension method called directly on a `DbContext` instance —
there is no DI registration, no options class, and no `IServiceCollection` extension anywhere in the package.

It is NOT a migration tool and NOT the application-level data-access layer — use `dbContext.Database.Migrate()`
for production schema changes and `DKNet.EfCore.Specifications` for querying/persisting entities; reach for
this package only for infrastructure code (seed routines, diagnostics, first-run/test provisioning, health
checks) that needs to touch the physical schema.

## Entry points

| Call | Exact signature | Called on | Notes (lifetime, ordering, prerequisites) |
|---|---|---|---|
| `CreateTableAsync<TEntity>` | `public static Task CreateTableAsync<TEntity>(this DbContext dbContext, CancellationToken cancellationToken = default) where TEntity : class` | `DbContext` instance | Requires a relational provider. Ensures the database exists (`RelationalDatabaseCreator.EnsureCreatedAsync`) then, only if `TEntity`'s table is missing, calls `CreateTablesAsync` — which creates **every** missing table in the model, not just `TEntity`'s. Call once against a database with none of the model's tables yet; incompatible with EF Core Migrations on the same database. |
| `GetDbConnection` | `public static Task<DbConnection> GetDbConnection(this DbContext dbContext, CancellationToken cancellationToken = default)` | `DbContext` instance | Returns `dbContext.Database.GetDbConnection()`, calling `OpenAsync(cancellationToken)` first if the connection's `State == ConnectionState.Closed`. Mutates connection state as a side effect — caller owns not leaving it open longer than intended. |
| `GetTableName<TEntity>` | `public static (string? Schema, string? TableName) GetTableName<TEntity>(this DbContext dbContext)` | `DbContext` instance | Synchronous, no I/O — reads `dbContext.Model`. Returns `(null, null)` if `TEntity` isn't part of the model. No prerequisites. |
| `TableExistsAsync<TEntity>` | `public static Task<bool> TableExistsAsync<TEntity>(this DbContext dbContext, CancellationToken cancellationToken = default) where TEntity : class` | `DbContext` instance | Calls `GetTableName<TEntity>()` first and throws `InvalidOperationException` if `TEntity` has no table name (not in the model). Otherwise queries `INFORMATION_SCHEMA.TABLES` via `dbContext.Database.SqlQuery<int>(...)`. Requires a relational provider whose catalog exposes `INFORMATION_SCHEMA.TABLES` (SQL Server, PostgreSQL). |

## Public surface

### `DKNet.EfCore.Relational.Helpers`

| Type | Kind | Purpose | Key members with exact signatures |
|---|---|---|---|
| `DbContextHelpers` | static class | Holds all four `DbContext` extension methods described above. | `static Task CreateTableAsync<TEntity>(this DbContext, CancellationToken = default) where TEntity : class`; `static Task<DbConnection> GetDbConnection(this DbContext, CancellationToken = default)`; `static (string? Schema, string? TableName) GetTableName<TEntity>(this DbContext)`; `static Task<bool> TableExistsAsync<TEntity>(this DbContext, CancellationToken = default) where TEntity : class` |

No other public types exist in this package — no options class, no attribute, no analyzer. The package also
relies on one method it does **not** define: `DbContext.IsSqlServer()`, a public instance method in
`DKNet.EfCore.Extensions`'s `EfCoreExtensions` static class, deliberately declared in the ambient
`Microsoft.EntityFrameworkCore` namespace so it resolves on any `DbContext` without an extra `using`.

## Options & defaults

No options class, no `IServiceCollection` extension, no MSBuild switch. The entire customisation surface is the
methods' own parameters plus provider-dependent behaviour:

| Option | Type | Default | Effect | Where set |
|---|---|---|---|---|
| `TEntity` (on `CreateTableAsync`/`TableExistsAsync`) | generic type arg, `class` | required | Picks the table whose existence gates whether `CreateTablesAsync` runs at all; does not scope *what* gets created. | call site |
| `cancellationToken` (all async methods) | `CancellationToken` | `default` | Flows into `ExistsAsync`/`EnsureCreatedAsync`/`TableExistsAsync`/`CreateTablesAsync` (`CreateTableAsync` awaits its own internal `TableExistsAsync<TEntity>(cancellationToken)` call before deciding whether to run `CreateTablesAsync`), `OpenAsync` (`GetDbConnection`, only when closed), and the `INFORMATION_SCHEMA.TABLES` `SingleAsync()` (`TableExistsAsync`). | call site |
| Default schema (inside `GetTableName<TEntity>`) | implicit `string?` | `"dbo"` when `dbContext.IsSqlServer()` is `true`, otherwise `null` | Substituted only when `entityType.GetSchema()` and `GetDefaultSchema()` both return `null` and the provider name is exactly `Microsoft.EntityFrameworkCore.SqlServer` (case-insensitive, via `IsSqlServer()`). | `DbContextHelpers.GetTableName<TEntity>` |

## Usage patterns

### Provision a table on first run (no migrations yet)

**When**: seeding a dev/test database or standing up a brand-new environment before any EF Core migration exists.

```csharp
using Microsoft.EntityFrameworkCore;
using DKNet.EfCore.Relational.Helpers;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
}

public static class ProvisioningApp
{
    public static async Task RunAsync()
    {
        await using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql("Host=localhost;Database=app;Username=app;Password=app")
                .Options);

        await db.CreateTableAsync<Product>();
    }
}
```

**Notes**: this is `EnsureCreatedAsync`/`CreateTablesAsync` under the hood — call it once, against a database
with none of the model's tables. If migrations later run against the same database, `__EFMigrationsHistory`
will be missing or inconsistent.

### Check before creating (idempotent-looking guard)

**When**: a health check or conditional-DDL guard that must not assume the table is already there.

```csharp
using Microsoft.EntityFrameworkCore;
using DKNet.EfCore.Relational.Helpers;

await using var db = new AppDbContext(
    new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql("Host=localhost;Database=app;Username=app;Password=app")
        .Options);

if (!await db.TableExistsAsync<Product>())
{
    await db.CreateTableAsync<Product>();
}
```

**Notes**: `TableExistsAsync<TEntity>` throws `InvalidOperationException` if `TEntity` is not part of the model
— it does not return `false` in that case. Any genuine infrastructure failure (permissions, timeout, dropped
connection) from the `INFORMATION_SCHEMA.TABLES` query also propagates; it is no longer swallowed as "table
absent."

### Run raw ADO.NET on EF Core's own connection

**When**: a raw SQL statement needs to share EF Core's connection (and any ambient transaction) instead of opening a second one.

```csharp
using Microsoft.EntityFrameworkCore;
using DKNet.EfCore.Relational.Helpers;

await using var db = new AppDbContext(
    new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql("Host=localhost;Database=app;Username=app;Password=app")
        .Options);

var conn = await db.GetDbConnection();
await using var cmd = conn.CreateCommand();
cmd.CommandText = "SELECT COUNT(*) FROM \"Products\"";
var count = (long)(await cmd.ExecuteScalarAsync())!;
```

**Notes**: opens the connection if it is closed; leaves it open otherwise. `GetDbConnection` never closes a
connection — manage that lifecycle yourself if you opened it just for this call.

### Resolve an entity's real schema and table name for diagnostics

**When**: writing a diagnostic message, seed script, or hand-written SQL that must not hard-code a name the model may remap.

```csharp
using Microsoft.EntityFrameworkCore;
using DKNet.EfCore.Relational.Helpers;

await using var db = new AppDbContext(
    new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlServer("Server=.;Database=app;Trusted_Connection=True;")
        .Options);

var (schema, table) = db.GetTableName<Product>();
Console.WriteLine($"{schema}.{table}"); // e.g. "dbo.Products"
```

**Notes**: returns `(null, null)` if `Product` is not part of the model — it never throws. The `"dbo"` fallback
only applies on `Microsoft.EntityFrameworkCore.SqlServer`; on PostgreSQL/SQLite/other providers an entity with
no explicit schema resolves to `Schema = null`.

### Multi-tenant table provisioning per tenant database

**When**: each tenant gets its own database and the app must guarantee the schema exists before first use, without shipping migrations to every tenant.

```csharp
using Microsoft.EntityFrameworkCore;
using DKNet.EfCore.Relational.Helpers;

async Task EnsureTenantSchemaAsync(string tenantConnectionString, CancellationToken ct)
{
    await using var db = new AppDbContext(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(tenantConnectionString)
            .Options);

    await db.CreateTableAsync<Product>(ct);
}
```

**Notes**: because `CreateTablesAsync` creates every missing table in the model (not just `Product`'s), this
call also provisions any other new entity type added since the tenant database was last touched — as long as
*some* table is still missing. If every table the model knows about already exists, the whole-model creation
step is skipped entirely, so a newly added entity type will silently not be backfilled into an
already-provisioned tenant.

## Runtime behaviour

`CreateTableAsync<TEntity>`, in order:
1. Casts `dbContext.Database.GetService<IDatabaseCreator>()` to `RelationalDatabaseCreator`.
2. Calls `databaseCreator.ExistsAsync(cancellationToken)`; if `false`, calls
   `databaseCreator.EnsureCreatedAsync(cancellationToken)` to create the database itself.
3. Calls `dbContext.TableExistsAsync<TEntity>(cancellationToken)`. If `true`, returns immediately — no further
   DDL runs.
4. Otherwise calls `databaseCreator.CreateTablesAsync(cancellationToken)`, which creates every table declared
   in the model that does not already exist (not scoped to `TEntity`).

`TableExistsAsync<TEntity>`, in order:
1. Calls `GetTableName<TEntity>()`; throws `InvalidOperationException` if the resolved `tableName` is `null`.
2. Builds `dbContext.Database.SqlQuery<int>($"SELECT COUNT(*) AS \"Value\" FROM INFORMATION_SCHEMA.TABLES WHERE
   TABLE_NAME = {tableName}")`, adding `AND TABLE_SCHEMA = {schema}` when `schema` is non-null. The
   `AS "Value"` alias is required because `SingleAsync()` composes a row-limiting wrapper over the query, and
   EF Core's `SqlQuery<T>` contract needs the projected column named `Value`.
3. Awaits `query.SingleAsync(cancellationToken)` and returns `count > 0`. Any exception (including a
   `DbException` from a genuine infrastructure problem) propagates — nothing is swallowed.

`GetTableName<TEntity>` is fully synchronous: computes `defaultSchema` from `dbContext.IsSqlServer()`, looks up
`dbContext.Model.FindEntityType(typeof(TEntity))`, and if found resolves
`schema = entityType.GetSchema() ?? entityType.GetDefaultSchema() ?? defaultSchema` and
`tableName = entityType.GetTableName() ?? entityType.GetDefaultTableName()`.

`GetDbConnection` checks `dbContext.Database.GetDbConnection().State`; if `ConnectionState.Closed`, awaits
`conn.OpenAsync(cancellationToken)`, then returns the (now open, or already-open) connection.

## Diagnostics & exceptions

| ID or exception type | Severity | When | Fix |
|---|---|---|---|
| `InvalidOperationException` | Error | `TableExistsAsync<TEntity>()` called for a `TEntity` that `GetTableName<TEntity>()` resolves to `(null, null)` — i.e. not part of the `DbContext`'s model. | Add `TEntity` to the model (via `DbSet<TEntity>` or `OnModelCreating`) before calling, or don't call this for types that are intentionally unmapped. |
| `InvalidCastException` (undocumented, from the runtime) | Error | `dbContext.Database.GetService<IDatabaseCreator>()` does not resolve to a `RelationalDatabaseCreator` — i.e. the provider is non-relational (e.g. Cosmos). | Only use this package's methods against a relational provider. |
| Any `Exception`/`DbException` from `SingleAsync()` | Error | A genuine infrastructure failure while querying `INFORMATION_SCHEMA.TABLES` (permissions, timeout, dropped connection) inside `TableExistsAsync`. | Propagates by design (no longer swallowed) — catch it explicitly at the call site if the old "treat every failure as absent" behaviour is required. |
| `OperationCanceledException` | — | `cancellationToken` is already cancelled or cancelled mid-flight, on any of the three async methods (`GetTableName<TEntity>` is synchronous and takes no `CancellationToken`). | Standard cancellation handling. |

No analyzer diagnostics — this package defines no `DiagnosticDescriptor`s.

## Gotchas (source-verified)

- **`CreateTableAsync<TEntity>` is not scoped to `TEntity` in either direction.** If `TEntity`'s table is
  missing, `CreateTablesAsync` creates *every* missing table in the model, not just `TEntity`'s. If `TEntity`'s
  table already exists, the method returns immediately and will **not** backfill some other entity type added
  to the model later, even though that other table is still missing.
- **Not a migration tool.** `EnsureCreatedAsync`/`CreateTablesAsync` never touch `__EFMigrationsHistory`.
  Mixing this with `dbContext.Database.Migrate()` on the same database produces tables with no (or an
  inconsistent) migrations history.
- **`TableExistsAsync` no longer swallows exceptions**, despite what an older README may say. It queries
  `INFORMATION_SCHEMA.TABLES` and only a genuine zero-row result reads as "absent"; everything else (including
  a `DbException`) propagates.
- **`GetDbConnection` mutates connection state as a side effect.** It opens a closed connection but never
  closes it — callers who only needed it for one command must close it themselves if they don't want it left
  open for EF Core's own subsequent use.
- **`"dbo"` default schema is SQL Server–only**, gated by an exact (case-insensitive) provider-name match to
  `Microsoft.EntityFrameworkCore.SqlServer` via the ambient `DbContext.IsSqlServer()` from
  `DKNet.EfCore.Extensions`. On PostgreSQL, SQLite, or any other provider, an entity with no explicit schema
  returns `Schema = null` — do not assume `dbo` cross-provider.
- **Requires a relational provider.** All four methods depend on `Microsoft.EntityFrameworkCore.Relational`
  types (`RelationalDatabaseCreator`, `DbConnection`, `SqlQuery<T>`, schema/table metadata) — none work against
  Cosmos or other non-relational providers.
- **`SingleAsync()` requires the projected column literally named `Value`.** The `AS "Value"` alias in the
  `INFORMATION_SCHEMA.TABLES` query is not decorative — `SqlQuery<int>` composes a row-limiting wrapper for
  `SingleAsync()` per EF Core's contract, and it looks for that column name. A hand-copied variant of this
  query without the alias will fail to project.
- **No DI registration exists.** There is nothing to add to `IServiceCollection` — every method is called
  directly on a `DbContext` instance after `using DKNet.EfCore.Relational.Helpers;`.

## Anti-patterns & hallucination traps

- There is **no** `AddRelationalHelpers()`, `AddEfCoreRelationalHelpers()`, or any other `IServiceCollection`
  extension in this package — do not invent a DI registration call for it.
- There is **no** options class (`RelationalHelpersOptions` or similar) — nothing to bind from configuration.
- Do not call `CreateTableAsync<TEntity>()` expecting it to create only `TEntity`'s table, and do not call it
  repeatedly expecting it to backfill newly added entity types once the probed `TEntity`'s own table already
  exists — see the Gotchas above.
- Do not assume `TableExistsAsync<TEntity>` returns `false` for an unmapped `TEntity` — it throws
  `InvalidOperationException` instead.
- Do not assume `TableExistsAsync` treats a `DbException` as "table absent" — that was the old behaviour;
  current source propagates every exception.
- Do not assume `GetTableName<TEntity>().Schema` is `"dbo"` on non–SQL Server providers — it is `null` there
  when no explicit schema is mapped.
- This package has no relationship to `DKNet.EfCore.Specifications` or `DKNet.EfCore.Repos` (the latter has
  been removed from the solution entirely) — do not reach for `DbContextHelpers` for application-level
  querying/persistence, and do not look for a `Repos`-based repository here.
- `IsSqlServer()` is not defined in this package — it is an ambient `Microsoft.EntityFrameworkCore` extension
  method from `DKNet.EfCore.Extensions`'s `EfCoreExtensions`. Do not hand-roll a private copy of the
  provider-name check inside consuming code that already references this package's dependency chain.

## Composes with

| Package | Rule |
|---|---|
| `DKNet.EfCore.Extensions` | Required reference — supplies the ambient `DbContext.IsSqlServer()` used by `GetTableName<TEntity>`'s SQL Server `dbo` default. Reach for `DKNet.EfCore.Extensions` directly when you want automatic entity-configuration discovery, seeding, or sequences rather than one-off schema pokes. |
| `DKNet.EfCore.Abstractions` | Not referenced by this package. Reach for it when modelling entities (base classes, attributes), not when inspecting schema. |
| `DKNet.EfCore.Specifications` | The supported way to query/persist through a `DbContext` at the application level. Reach for it instead of this package once you're past infrastructure-level schema work. |

## Testing notes

- The fixture wraps `Testcontainers.PostgreSql` — despite the docs page's SQL Server framing for the `dbo`
  default, **the test suite validates entirely against PostgreSQL** via `UseNpgsql(...)`, never SQL Server.
- Test entities: a mapped entity re-mapped in `OnModelCreating` to schema `"public"`, and a second entity
  declared with a `[Table]` attribute but never added to the model, so `GetTableName<T>()` on it still returns
  `(null, null)`.
- Canonical assertion pattern: Shouldly (`ShouldBeTrue()`, `ShouldBe(...)`, `ShouldThrowAsync<T>()`) rather than
  raw xUnit `Assert`. Cancellation tests use a pre-cancelled `CancellationTokenSource` and assert
  `Should.ThrowAsync<OperationCanceledException>(...)`.
- A cancellation test for `GetDbConnection` appends `;Pooling=false` to the connection string — a pooled,
  already-open Npgsql connection hands out synchronously and never observes an already-cancelled token, so the
  test forces a genuine new dial to make cancellation observable.
