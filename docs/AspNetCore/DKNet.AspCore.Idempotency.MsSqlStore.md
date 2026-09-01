# DKNet.AspCore.Idempotency.MsSqlStore

SQL Server-backed `IIdempotencyKeyStore` for
[`DKNet.AspCore.Idempotency`](DKNet.AspCore.Idempotency.md), built on the shared
[relational base](DKNet.AspCore.Idempotency.Relational.md).

## ✨ Why use it?

- **Idempotency keys survive restarts and scale-out** without standing up Redis — they live in a
  SQL Server table alongside your business data, with the same backups and the same operational
  tooling.
- **Atomic, not best-effort.** The unique index `UX_CompositeKey` makes duplicate-request
  reservation race-free, closing the check-then-act window the core package's built-in
  distributed-cache store can only narrow.
- **Auditable.** Unlike a Redis store, the processed-key ledger is an ordinary table you can query:
  which key, which endpoint, which verb, what status code, when it expires.
- **One registration call.** `AddIdempotencyWithMsSqlStore(connectionString)` registers the
  `DbContext`, its factory, and the store; migrations apply themselves on first use.

Reach for this store when SQL Server is already part of your stack — see
[Choosing a store](DKNet.AspCore.Idempotency.md#-choosing-a-store) on the core page for the full
Redis-vs-relational comparison, which is not repeated here.

## 🚀 Quick Start

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

var app = builder.Build();

app.MapPost("/orders", CreateOrder)
    .RequiredIdempotentKey();

await app.RunAsync();
```

`AddIdempotencyWithMsSqlStore` (in `IdempotencyMsSqlSetup`) does two things: it calls
`AddIdempotencyMsSqlStore(connectionString)` to register `IdempotencyDbContext` — a scoped
`AddDbContext` with a singleton `DbContextOptions`, plus an
`IDbContextFactory<IdempotencyDbContext>` — and then
`AddIdempotentKey<IdempotencySqlServerStore>(config)` to wire the store in as the
`IIdempotencyKeyStore`. Call it once at start-up, then use `.RequiredIdempotentKey()` on endpoints
as described on the core page.

## 🧩 Features

### Registration entry points

`IdempotencyMsSqlSetup` is the package's only public type, and it declares exactly two extension methods on
`IServiceCollection`:

| Method | Registers | Does **not** register |
|---|---|---|
| `AddIdempotencyMsSqlStore(string connectionString)` | `IdempotencyDbContext` via `AddDbContext` (scoped context, singleton `DbContextOptions`) plus `AddDbContextFactory<IdempotencyDbContext>` | The key store. Called on its own, no `IIdempotencyKeyStore` exists and `RequiredIdempotentKey()` cannot resolve the filter's dependency. |
| `AddIdempotencyWithMsSqlStore(string connectionString, Action<IdempotencyOptions>? config = null)` | Everything the first method does, then `AddIdempotentKey<IdempotencySqlServerStore>(config)` | Nothing — this is the call an application makes. |

Both throw `ArgumentNullException` on a null `services` and `ArgumentException` on a null, empty or
whitespace connection string. Both are first-wins: `AddIdempotencyMsSqlStore` returns early once
`IdempotencyDbContext` is registered, and `AddIdempotentKey<TStore>` returns early once any
`IIdempotencyKeyStore` is registered, so a second call with a different connection string is silently a no-op.

`IdempotencySqlServerStore` is `internal`, so `AddIdempotentKey<IdempotencySqlServerStore>()` is not something
application code can write — `AddIdempotencyWithMsSqlStore(...)` is the supported way in. Reach for the
`AddIdempotencyMsSqlStore(...)` overload on its own only when you want the `DbContext` registered without
replacing the key store — for example to run `Database.MigrateAsync()` from a start-up job.

### What the package creates, and what you provide

| Thing | Who provides it |
|---|---|
| The `IdempotencyKeys` table, `UX_CompositeKey`, `IX_IdempotencyKeys_ExpiresAt` and `CK_StatusCode_Valid` | The package — created by the shipped `Initial` migration |
| Applying that migration | The package, automatically, on first use per connection string (or you, ahead of time — see [Gotchas & limits](#-gotchas--limits)) |
| The `migrate.IdempotencyDbContext` migrations-history table and the `migrate` schema | The package, through EF Core |
| A reachable SQL Server database and a login that can create tables in it | **You** |
| Row expiry / cleanup of keys nobody ever retries | **You** — `ExpiresAt` is indexed, but nothing sweeps it |
| Backup, retention and PII policy for cached response bodies | **You** — `Body` holds the serialized response verbatim |

### The `IdempotencyKeys` table

The `Initial` migration creates one table:

| Column | Type | Notes |
|---|---|---|
| `Id` | `uniqueidentifier` | Primary key `PK_IdempotencyKeys` |
| `CompositeKey` | `nvarchar(128)` | Unique — see below |
| `Endpoint` | `nvarchar(250)` | |
| `IdempotentKey`, `Method` | `varchar(150)` / `varchar(20)`, non-Unicode | |
| `Body` | `nvarchar(max)`, max 1,048,576 chars | SQL Server override of the shared entity mapping |
| `ContentType` | `varchar(256)`, non-Unicode | Nullable |
| `StatusCode` | `int` | `CK_StatusCode_Valid` check constraint, `100`–`599` |
| `CreatedAt`, `ExpiresAt` | `datetimeoffset` | `ExpiresAt` nullable |

### Atomic reservation via `UX_CompositeKey`

Two indexes back the store's behaviour. `UX_CompositeKey` (unique, on `CompositeKey`) is what makes
the reserve-then-insert flow in the relational base race-free — only one concurrent insert for the
same key succeeds; every other caller hits a unique violation and reads the winner's row back
instead of proceeding. `IX_IdempotencyKeys_ExpiresAt` speeds up the expired-reservation reclaim.

`IdempotencySqlServerStore` classifies that collision by `SqlException.Number` — `2601` (duplicate
key in a unique index) or `2627` (unique/primary key constraint violation) — never by message text,
which is localized by the server's login language:

```csharp
protected override bool IsProviderUniqueViolation(DbUpdateException ex) =>
    ex.InnerException is SqlException { Number: 2601 or 2627 };
```

See `IdempotencySqlServerStoreUniqueViolationTests` for the exact scenarios this guards against.

### Design-time tooling

`IdempotencyDbContext` here is the shared relational context closed over SQL Server's
`DbContextOptions`. `DbContextFactory` (an `IDesignTimeDbContextFactory<IdempotencyDbContext>`)
exists only so `dotnet ef` design-time tooling can build a context outside your app's DI container
— it reads its connection string from the `IDEMPOTENCY_MSSQL_CONNECTION` environment variable, not
from app configuration, and throws `InvalidOperationException` when that variable is unset.

## ⚙️ Configuration reference

There is no SQL-Server-specific options type — `AddIdempotencyWithMsSqlStore` configures the same
`IdempotencyOptions` the core package defines (`Expiration`, `ConflictHandling`,
`InFlightReservationTimeout`, …); see the
[core configuration reference](DKNet.AspCore.Idempotency.md#-configuration-reference).

What SQL Server itself gets is fixed by this package rather than exposed as options:

| Setting | Value | Effect |
|---|---|---|
| `EnableRetryOnFailure` | `3` retries, `5` seconds apart | Transient-fault resilience on idempotency queries. |
| `UseQuerySplittingBehavior` | `QuerySplittingBehavior.SplitQuery` | EF Core split-query mode for this context. |
| `MigrationsAssembly` | this package's assembly | Migrations ship with the store, not with your app. |
| `MigrationsHistoryTable` | `[migrate].[IdempotencyDbContext]` | Separate from your app's own `__EFMigrationsHistory`. |
| `optionsLifetime` | `ServiceLifetime.Singleton` | Options are shared; the `DbContext` itself stays scoped. |

## 🧱 Where it fits

![Architecture diagram: one AddIdempotencyWithMsSqlStore call wires the core package's endpoint filter to IdempotencySqlServerStore, which inherits every shared step from IdempotencyRelationalStore and reaches SQL Server's IdempotencyKeys table, applying this package's own migrations history on first use.](../diagrams/idempotency-mssql-composition.svg)

This package supplies only what is provider-specific: the closed `IdempotencyDbContext`, the SQL
Server column type and check-constraint SQL for `IdempotencyKeyConfiguration`, the `Initial`
migration, and `IdempotencySqlServerStore.IsProviderUniqueViolation`. Everything else — the
reserve/check/complete flow, the migration guard, the expired-reservation reclaim — lives in
[DKNet.AspCore.Idempotency.Relational](DKNet.AspCore.Idempotency.Relational.md); the HTTP-facing
pieces (endpoint filter, key scope resolution, `IdempotencyOptions`) live in
[DKNet.AspCore.Idempotency](DKNet.AspCore.Idempotency.md).

## ⚠️ Gotchas & limits

- **Migration ownership** — migrations ship inside this package's own assembly
  (`sqlOptions.MigrationsAssembly(typeof(IdempotencyMsSqlSetup).Assembly)`); don't add your own
  `IdempotencyDbContext` migrations to your application's assembly.
- **Migrations run automatically, with no opt-out** — the relational base checks for and applies
  pending migrations the first time the store is used against a given connection string, under a
  lock. Most apps never run migrations by hand, but the first request against a fresh database pays
  that cost. For a controlled production rollout instead, set `IDEMPOTENCY_MSSQL_CONNECTION` and
  run `dotnet ef database update --context IdempotencyDbContext` from a project that references
  this package before traffic arrives.
- **Registration is first-wins** — `AddIdempotencyMsSqlStore` returns early once
  `IdempotencyDbContext` is registered, and `AddIdempotentKey<TStore>` returns early once an
  `IIdempotencyKeyStore` is registered. A second call with a *different* connection string is
  silently a no-op.
- **Nothing purges expired rows.** `ExpiresAt` is indexed and expired rows are reclaimed
  opportunistically when a request collides with one, but the table grows until you add your own
  cleanup job for keys that are never retried.
- **Every protected request costs a database round-trip.** If your SQL Server is a bottleneck or
  the endpoint is very high-throughput, the
  [Redis store](DKNet.AspCore.Idempotency.RedisStore.md) avoids the query planner entirely.

## 🔗 Related packages

- [DKNet.AspCore.Idempotency](DKNet.AspCore.Idempotency.md) — the core package this store plugs
  into; start there to wire idempotency into an app at all.
- [DKNet.AspCore.Idempotency.NpgsqlStore](DKNet.AspCore.Idempotency.NpgsqlStore.md) — the same
  store shape for PostgreSQL; reach for it instead when Postgres, not SQL Server, is your database.
- [DKNet.AspCore.Idempotency.RedisStore](DKNet.AspCore.Idempotency.RedisStore.md) — reach for it
  when you want the lowest latency and no schema or migrations to own.
- [DKNet.AspCore.Idempotency.Relational](DKNet.AspCore.Idempotency.Relational.md) — read it only if
  you are adding a *new* relational provider to the family.
