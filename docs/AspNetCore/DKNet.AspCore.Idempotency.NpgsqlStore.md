# DKNet.AspCore.Idempotency.NpgsqlStore

PostgreSQL-backed `IIdempotencyKeyStore` for
[`DKNet.AspCore.Idempotency`](DKNet.AspCore.Idempotency.md), built on the shared
[relational base](DKNet.AspCore.Idempotency.Relational.md).

## ✨ Why use it?

- **Idempotency keys survive restarts and are shared safely across instances**, without adding
  Redis to a stack that already runs PostgreSQL.
- **Atomic, not best-effort.** The unique index `UX_CompositeKey` makes duplicate-request
  reservation race-free, closing the check-then-act window the core package's built-in
  distributed-cache store can only narrow.
- **Auditable.** The processed-key ledger is an ordinary table you can query by endpoint, verb,
  status code, or expiry — something a Redis keyspace cannot give you.
- **Multi-tenant friendly.** Migrations and the "database is ready" guard are tracked per
  connection string, so one process fronting several Postgres databases prepares each of them.

Reach for this store when Postgres is already part of your stack — see
[Choosing a store](DKNet.AspCore.Idempotency.md#️-choosing-a-store) on the core page for how it
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
`IDbContextFactory<IdempotencyDbContext>` — against the given Npgsql connection string, then calls
`AddIdempotentKey<IdempotencyPostgresStore>(config)` to wire it up as the `IIdempotencyKeyStore`.
Call `AddIdempotencyNpgsqlStore(connectionString)` on its own if you only need the `DbContext`
registered — e.g. to run migrations from a start-up job — without replacing the key store.

## 🧩 Features

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

Multiple Postgres databases behind the same process — e.g. one store per tenant database — are each
migrated and guarded independently: the "migrations ensured" state is keyed per connection string,
not by a single process-wide flag, so registering this store against two different connection
strings prepares both (see `IdempotencyMultiDatabaseTests`).

### Design-time tooling

`IdempotencyDbContext` here is the shared relational context closed over Npgsql's
`DbContextOptions`. Its `DbContextFactory` (an `IDesignTimeDbContextFactory<IdempotencyDbContext>`)
exists only for `dotnet ef` — it reads `IDEMPOTENCY_NPGSQL_CONNECTION` from the environment, not
from app configuration, and throws `InvalidOperationException` when that variable is unset.

## ⚙️ Configuration reference

There is no PostgreSQL-specific options type — expiration, conflict handling, header name, and key
scope resolution all come from the shared `IdempotencyOptions`; see the
[core configuration reference](DKNet.AspCore.Idempotency.md#️-configuration-reference).

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

`IdempotencyPostgresStore` supplies only the Postgres unique-violation check; the reserve → check →
complete flow, the expired-reservation reclaim, and the per-connection-string migration guard all
live in `IdempotencyRelationalStore<TContext>` — see
[DKNet.AspCore.Idempotency.Relational](DKNet.AspCore.Idempotency.Relational.md). Its
`IdempotencyDbContext` and `IdempotencyKeyConfiguration` override only the two Postgres-specific
bits (`text` body column, quoted-identifier check-constraint SQL); the rest of the entity mapping
is the relational base's. The HTTP-facing pieces — endpoint filter, key scope resolution,
`IdempotencyOptions` — live in [DKNet.AspCore.Idempotency](DKNet.AspCore.Idempotency.md).

## ⚠️ Gotchas & limits

- **Migrations run automatically**, not on demand: the first call against a given connection string
  checks for and applies pending migrations under a lock. There is no separate "apply migrations"
  step to remember, but also no opt-out — the first request against a fresh database pays that
  cost.
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
