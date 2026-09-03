# DKNet.AspCore.Idempotency.NpgsqlStore

PostgreSQL-backed `IIdempotencyKeyStore` for
[`DKNet.AspCore.Idempotency`](DKNet.AspCore.Idempotency.md), built on the shared
[relational base](DKNet.AspCore.Idempotency.Relational.md).

## ✨ Why use it?

- **Idempotency keys survive restarts and are shared safely across instances**, without adding
  Redis to a stack that already runs PostgreSQL.
- **Atomic across instances.** The unique index `UX_CompositeKey` makes duplicate-request
  reservation race-free for every instance sharing the database — the core package's built-in
  in-process store guarantees that within one process only.
- **Auditable.** The processed-key ledger is an ordinary table you can query by endpoint, verb,
  status code, or expiry — something a Redis keyspace cannot give you.
- **Multi-tenant friendly.** Migrations and the "database is ready" guard are tracked per
  connection string, so one process fronting several Postgres databases prepares each of them.

Reach for this store when Postgres is already part of your stack — see
[Choosing a store](DKNet.AspCore.Idempotency.md#-choosing-a-store) on the core page for how it
compares to Redis and to the SQL Server store; that comparison is not repeated here.

## 🚀 Quick Start

```bash
dotnet add package DKNet.AspCore.Idempotency.NpgsqlStore
```

```csharp
using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.NpgsqlStore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddIdempotencyWithNpgsqlStore(
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

`AddIdempotencyWithNpgsqlStore` (in `IdempotencyNpgsqlSetup`) registers `IdempotencyDbContext` — a
scoped `AddDbContext` with a singleton `DbContextOptions`, plus an
`IDbContextFactory<IdempotencyDbContext>` and a hosted service that migrates the schema once at
application startup (`IdempotencyMigrationHostedService<IdempotencyDbContext>`) — against the given
Npgsql connection string, then calls `AddIdempotentKey<IdempotencyPostgresStore>(config)` to wire it
up as the `IIdempotencyKeyStore`. Call `AddIdempotencyNpgsqlStore(connectionString)` on its own if
you only need the `DbContext` (and the startup migration) registered without replacing the key store.

## 🧩 Features

### Registration entry points

`IdempotencyNpgsqlSetup` is the package's only public type, and it declares exactly two extension methods on
`IServiceCollection`:

| Method | Registers | Does **not** register |
|---|---|---|
| `AddIdempotencyNpgsqlStore(string connectionString)` | `IdempotencyDbContext` via `AddDbContext` (scoped context, singleton `DbContextOptions`), `AddDbContextFactory<IdempotencyDbContext>`, and `IdempotencyMigrationHostedService<IdempotencyDbContext>` (migrates at startup) | The key store. Called on its own, no `IIdempotencyKeyStore` exists and `RequiredIdempotentKey()` cannot resolve the filter's dependency. |
| `AddIdempotencyWithNpgsqlStore(string connectionString, Action<IdempotencyOptions>? config = null)` | Everything the first method does, then `AddIdempotentKey<IdempotencyPostgresStore>(config)` | Nothing — this is the call an application makes. |

Both throw `ArgumentNullException` on a null `services` and `ArgumentException` on a null, empty or
whitespace connection string, and both are first-wins: a second call with a different connection string is
silently a no-op once `IdempotencyDbContext` (or any `IIdempotencyKeyStore`) is already registered.

`IdempotencyPostgresStore` is `internal`, so `AddIdempotentKey<IdempotencyPostgresStore>()` is not something
application code can write — `AddIdempotencyWithNpgsqlStore(...)` is the supported way in. Reach for
`AddIdempotencyNpgsqlStore(...)` on its own only when you want the `DbContext` registered without replacing
the key store, for example to run migrations from a start-up job.

### What the package creates, and what you provide

| Thing | Who provides it |
|---|---|
| The `IdempotencyKeys` table, `UX_CompositeKey`, `IX_IdempotencyKeys_ExpiresAt` and `CK_StatusCode_Valid` | The package — created by the shipped `Initial` migration |
| Applying that migration | The package, automatically, once at application startup via a hosted service (or you, ahead of time — see [Gotchas & limits](#-gotchas--limits)) |
| The `migrate.IdempotencyDbContext` migrations-history table and the `migrate` schema | The package, through EF Core |
| A reachable PostgreSQL database and a role that can create tables and schemas in it | **You** |
| Row expiry / cleanup of keys nobody ever retries | **You** — `ExpiresAt` is indexed, but nothing sweeps it |
| Backup, retention and PII policy for cached response bodies | **You** — `Body` holds the serialized response verbatim |

### The `IdempotencyKeys` table

The shipped `Initial` migration creates one table:

| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` | Primary key `PK_IdempotencyKeys` |
| `CompositeKey` | `character varying(128)` | Unique — see below |
| `Endpoint` | `character varying(250)` | |
| `IdempotentKey`, `Method` | `character varying(150)` / `character varying(20)`, non-Unicode | |
| `Body` | `text`, max 1,048,576 chars | PostgreSQL override of the shared entity mapping |
| `ContentType` | `character varying(256)`, non-Unicode | Nullable |
| `StatusCode` | `integer` | `CK_StatusCode_Valid` check constraint, `100`–`599` |
| `CreatedAt`, `ExpiresAt` | `timestamp with time zone` | `ExpiresAt` nullable |

### Atomic reservation via `UX_CompositeKey`

`UX_CompositeKey` (unique, on `CompositeKey`) is what makes reservation atomic under concurrency:
only one concurrent insert for a given key can succeed; every other one fails with Postgres SQL
state `23505`, which the store detects and the relational base turns into "already
reserved/processed" instead of a raw exception. `IX_IdempotencyKeys_ExpiresAt` backs expiry
lookups.

```csharp
protected override bool IsProviderUniqueViolation(DbUpdateException ex) =>
    ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
```

### Per-connection-string migration guard

For the common case, migration now happens once at application startup via
`IdempotencyMigrationHostedService<IdempotencyDbContext>`, which `AddIdempotencyNpgsqlStore` registers
automatically — see
[DKNet.AspCore.Idempotency.Relational](DKNet.AspCore.Idempotency.Relational.md#idempotencymigrationhostedservicetcontext--migrate-once-at-startup).
The relational base's older per-request guard still exists underneath as a defensive fallback (for a host that
skips or reorders hosted services), and multiple Postgres databases behind the same process — e.g. one store per
tenant database — are still each migrated and guarded independently there: the "migrations ensured" state is keyed
per connection string, not by a single process-wide flag, so a process working against more than one connection
string prepares each one rather than skipping every database after the first (see
`IdempotencyMultiDatabaseTests`).

### Design-time tooling

`IdempotencyDbContext` here is the shared relational context closed over Npgsql's
`DbContextOptions`. Its `DbContextFactory` (an `IDesignTimeDbContextFactory<IdempotencyDbContext>`)
exists only for `dotnet ef` — it reads `IDEMPOTENCY_NPGSQL_CONNECTION` from the environment, not
from app configuration, and throws `InvalidOperationException` when that variable is unset.

## ⚙️ Configuration reference

There is no PostgreSQL-specific options type — expiration, conflict handling, header name, and key
scope resolution all come from the shared `IdempotencyOptions`; see the
[core configuration reference](DKNet.AspCore.Idempotency.md#-configuration-reference).

Registration bakes in Npgsql-specific EF Core configuration rather than exposing it through
`IdempotencyOptions`:

| Setting | Value | Effect |
|---|---|---|
| `EnableRetryOnFailure` | `3` retries, `5` seconds apart | Transient-fault resilience on idempotency queries. |
| `UseQuerySplittingBehavior` | `QuerySplittingBehavior.SplitQuery` | EF Core split-query mode for this context. |
| `MigrationsAssembly` | this package's assembly | Migrations ship with the store, not with your app. |
| `MigrationsHistoryTable` | `migrate.IdempotencyDbContext` | Separate from your app's own `__EFMigrationsHistory`. |
| `optionsLifetime` | `ServiceLifetime.Singleton` | Options are shared; the `DbContext` itself stays scoped. |

## 🧱 Where it fits

![Architecture diagram: one AddIdempotencyWithNpgsqlStore call wires the core package's endpoint filter to IdempotencyPostgresStore, which inherits every shared step from IdempotencyRelationalStore and reaches PostgreSQL's IdempotencyKeys table, applying this package's own migrations history on first use.](../diagrams/idempotency-npgsql-composition.svg)

`IdempotencyPostgresStore` supplies only the Postgres unique-violation check; the reserve → check →
complete flow, the expired-reservation reclaim, and the per-connection-string migration guard all
live in `IdempotencyRelationalStore<TContext>` — see
[DKNet.AspCore.Idempotency.Relational](DKNet.AspCore.Idempotency.Relational.md). Its
`IdempotencyDbContext` and `IdempotencyKeyConfiguration` override only the two Postgres-specific
bits (`text` body column, quoted-identifier check-constraint SQL); the rest of the entity mapping
is the relational base's. The HTTP-facing pieces — endpoint filter, key scope resolution,
`IdempotencyOptions` — live in [DKNet.AspCore.Idempotency](DKNet.AspCore.Idempotency.md).

## ⚠️ Gotchas & limits

- **Migrations run automatically**, not on demand: `AddIdempotencyNpgsqlStore` registers a hosted service
  (`IdempotencyMigrationHostedService<IdempotencyDbContext>`) that applies pending migrations once at application
  startup. There is no separate "apply migrations" step to remember, but also no opt-out. A host that skips or
  reorders hosted services falls back to the relational base's per-request guard instead, which checks for and
  applies pending migrations under a lock the first time the store is used against a given connection string — so
  the first request against a fresh database pays that cost in that case.
- **Registration is first-wins.** `AddIdempotencyNpgsqlStore` / `AddIdempotencyWithNpgsqlStore`
  return early once `IdempotencyDbContext` is registered, and `AddIdempotentKey<TStore>` returns
  early once an `IIdempotencyKeyStore` is registered — calling either again, even with a different
  connection string, is silently a no-op.
- **`MarkKeyAsProcessedAsync` defensively swallows a unique violation** if it is ever called
  without a prior reservation (e.g. concurrent calls from a retried background job): it will not
  throw, but it also will not overwrite whichever caller's insert won the race — see
  `IdempotencyMarkAsProcessedConcurrencyTests`.
- **Nothing purges expired rows.** `ExpiresAt` is indexed and expired rows are reclaimed
  opportunistically when a request collides with one, but the table grows until you add your own
  cleanup job for keys that are never retried.
- **Every protected request costs a database round-trip.** For very high-throughput endpoints the
  [Redis store](DKNet.AspCore.Idempotency.RedisStore.md) avoids the query planner entirely.

## 🔗 Related packages

- [DKNet.AspCore.Idempotency](DKNet.AspCore.Idempotency.md) — the core package this store plugs
  into; start there to wire idempotency into an app at all.
- [DKNet.AspCore.Idempotency.MsSqlStore](DKNet.AspCore.Idempotency.MsSqlStore.md) — the same store
  shape for SQL Server; reach for it instead when SQL Server, not Postgres, is your database.
- [DKNet.AspCore.Idempotency.RedisStore](DKNet.AspCore.Idempotency.RedisStore.md) — reach for it
  when you want the lowest latency and no schema or migrations to own.
- [DKNet.AspCore.Idempotency.Relational](DKNet.AspCore.Idempotency.Relational.md) — read it only if
  you are adding a *new* relational provider to the family.
