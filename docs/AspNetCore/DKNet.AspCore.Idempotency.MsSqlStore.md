# DKNet.AspCore.Idempotency.MsSqlStore

SQL Server-backed store for [DKNet.AspCore.Idempotency](DKNet.AspCore.Idempotency.md), built on the shared
[relational base](DKNet.AspCore.Idempotency.Relational.md).

## 🧭 When to use it

Pick this store when SQL Server is already part of your stack and you want idempotency keys to survive
restarts without standing up Redis — see the "Choosing a store" section on the
[core idempotency page](DKNet.AspCore.Idempotency.md) for the full Redis-vs-relational comparison.

## 🚀 Install & register

```bash
dotnet add package DKNet.AspCore.Idempotency.MsSqlStore
```

```csharp
using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.MsSqlStore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddIdempotencyWithMsSqlStore(
    builder.Configuration.GetConnectionString("IdempotencyDb")!,
    options =>
    {
        options.Expiration = TimeSpan.FromHours(48);
        options.ConflictHandling = IdempotentConflictHandling.CachedResult;
    });
```

`AddIdempotencyWithMsSqlStore` (in `IdempotencyMsSqlSetup`) does two things: it calls
`AddIdempotencyMsSqlStore(connectionString)` to register `IdempotencyDbContext` — as a scoped `AddDbContext`
plus an `IDbContextFactory<IdempotencyDbContext>`, guarded so a second call with a different connection
string is a no-op — and then `AddIdempotentKey<IdempotencySqlServerStore>(config)` to wire the store into
the core `IIdempotencyKeyStore`. Call it once at startup, then use `.RequiredIdempotentKey()` on endpoints
as described on the core page.

## 🗄️ SQL Server specifics

The `Initial` migration creates one table, `IdempotencyKeys`:

| Column | Type | Notes |
|---|---|---|
| `Id` | `uniqueidentifier` | primary key |
| `CompositeKey` | `nvarchar(128)` | unique — see below |
| `Endpoint` | `nvarchar(250)` | |
| `IdempotentKey`, `Method` | `varchar(150)` / `varchar(20)`, non-unicode | |
| `Body` | `nvarchar(max)` | SQL-Server-specific override of the shared entity mapping |
| `ContentType` | `varchar(256)`, non-unicode | nullable |
| `StatusCode` | `int` | `CK_StatusCode_Valid` check constraint, `100`–`599` |
| `CreatedAt`, `ExpiresAt` | `datetimeoffset` | `ExpiresAt` nullable |

Two indexes back the store's behaviour: `UX_CompositeKey` (unique, on `CompositeKey`) is what makes the
reserve-then-insert flow in the relational base race-free — only one concurrent insert for the same key
succeeds, every other caller hits a unique violation and reads the winner's row back instead. `IX_IdempotencyKeys_ExpiresAt`
speeds up the expired-reservation reclaim.

`IdempotencyDbContext` here is just the shared relational context closed over SQL Server's
`DbContextOptions`. `DbContextFactory` (an `IDesignTimeDbContextFactory<IdempotencyDbContext>`) exists only
so `dotnet ef` design-time tooling can build a context outside of your app's DI container — it reads its
connection string from the `IDEMPOTENCY_MSSQL_CONNECTION` environment variable, not from app configuration.

## ⚙️ Configuration

There is no SQL-Server-specific options type — `AddIdempotencyWithMsSqlStore` configures the same
`IdempotencyOptions` the core package defines (`Expiration`, `ConflictHandling`, `InFlightReservationTimeout`,
etc.). What SQL Server itself gets, fixed rather than exposed as options:

- `EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null)` — 3 retries, 5 seconds apart.
- `QuerySplittingBehavior.SplitQuery`.
- Migrations history table `[migrate].[IdempotencyDbContext]`, separate from your app's own `__EFMigrationsHistory`.

## 🧱 How it composes

This package supplies only what's provider-specific: the closed `IdempotencyDbContext`, the SQL Server
column types and check-constraint SQL for `IdempotencyKeyConfiguration`, the `Initial` migration, and
`IdempotencySqlServerStore.IsProviderUniqueViolation`. Everything else — the reserve/check/complete flow,
the migration guard, the expired-reservation reclaim — lives in
[DKNet.AspCore.Idempotency.Relational](DKNet.AspCore.Idempotency.Relational.md); the HTTP-facing pieces
(endpoint filter, key scope resolution, `IdempotencyOptions`) live in
[DKNet.AspCore.Idempotency](DKNet.AspCore.Idempotency.md).

## ⚠️ Gotchas

- **Migration ownership** — migrations ship inside this package's own assembly
  (`sqlOptions.MigrationsAssembly(typeof(IdempotencyMsSqlSetup).Assembly)`); don't add your own
  `IdempotencyDbContext` migrations to your application's assembly.
- **Running migrations** — the relational base lazily calls `Database.MigrateAsync()` the first time the
  store is used against a given connection string, so most apps never run migrations by hand. For a
  controlled production rollout instead, set `IDEMPOTENCY_MSSQL_CONNECTION` and run
  `dotnet ef database update --context IdempotencyDbContext` from a project that references this package.
- **Unique-violation handling** — SQL Server reports the `UX_CompositeKey` collision as error number
  `2601` or `2627`, classified by `SqlException.Number`, never by message text (which is localized by the
  server's login language). See `IdempotencySqlServerStoreUniqueViolationTests` for the exact scenarios
  this guards against.
