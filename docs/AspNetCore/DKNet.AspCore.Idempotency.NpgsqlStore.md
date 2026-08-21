# DKNet.AspCore.Idempotency.NpgsqlStore

PostgreSQL-backed persistent store for `DKNet.AspCore.Idempotency`, built on the shared EF Core relational
store in `DKNet.AspCore.Idempotency.Relational`.

## ✨ When to use it

Pick this store when Postgres is already part of your stack and idempotency keys need to survive restarts
and be shared safely across instances — see
[Choosing a store](DKNet.AspCore.Idempotency.md#choosing-a-store) for how it compares to Redis and to the
SQL Server relational store; that comparison isn't repeated here.

## 🚀 Install & Register

```bash
dotnet add package DKNet.AspCore.Idempotency.NpgsqlStore
```

```csharp
builder.Services.AddIdempotencyWithNpgsqlStore(
    builder.Configuration.GetConnectionString("IdempotencyDb")!,
    options =>
    {
        options.Expiration = TimeSpan.FromHours(48);
        options.ConflictHandling = IdempotentConflictHandling.CachedResult;
    });
```

`AddIdempotencyWithNpgsqlStore` registers `IdempotencyDbContext` (a singleton `DbContextOptions<IdempotencyDbContext>`
plus an `IDbContextFactory<IdempotencyDbContext>`) against the given Npgsql connection string, then calls
`AddIdempotentKey<IdempotencyPostgresStore>(config)` to wire it up as the `IIdempotencyKeyStore`. Call
`AddIdempotencyNpgsqlStore(connectionString)` on its own if you only need the `DbContext` registered — e.g. to
run migrations from a startup job — without replacing the key store.

## 🐘 Postgres specifics

The shipped `Initial` migration creates one `IdempotencyKeys` table:

- `Id uuid` — primary key.
- `CompositeKey character varying(128)` with a **unique index `UX_CompositeKey`** — this is what makes
  reservation atomic under concurrency: only one concurrent insert for a given key can succeed; every other
  one fails with Postgres SqlState `23505`, which `IdempotencyPostgresStore.IsProviderUniqueViolation` detects
  and the relational base turns into "already reserved/processed" instead of a raw exception.
- `Endpoint`, `Method`, `IdempotentKey`, `ContentType` as bounded `varchar` columns; `Body text` (up to 1 MB);
  `StatusCode integer` guarded by check constraint `CK_StatusCode_Valid` (`100`–`599`).
- `IX_IdempotencyKeys_ExpiresAt` index for expiry lookups.

Multiple Postgres databases behind the same process — e.g. one `IdempotencyPostgresStore` per tenant database —
are each migrated and guarded independently: the "migrations ensured" state is keyed per connection string, not
a single process-wide flag, so registering this store against two different connection strings prepares both
(see `IdempotencyMultiDatabaseTests`).

## ⚙️ Defaults specific to this store

Registration bakes in Npgsql-specific EF Core configuration rather than exposing it through `IdempotencyOptions`:

- Migrations load from this assembly, recorded in a `migrate.IdempotencyDbContext` history table.
- `QuerySplittingBehavior.SplitQuery`.
- `EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null)`.

Everything else — expiration, conflict handling, header name, key scope resolution, etc. — comes from the
shared `IdempotencyOptions` described in the core doc.

## 🧩 How it composes

`IdempotencyPostgresStore` only supplies the Postgres unique-violation check; the reserve → check → complete
flow, expired-reservation reclaim, and per-connection-string migration guard all live in
`IdempotencyRelationalStore<TContext>` — see
[DKNet.AspCore.Idempotency.Relational](DKNet.AspCore.Idempotency.Relational.md). `IdempotencyDbContext` and its
`IdempotencyKeyConfiguration` override only the two Postgres-specific bits (`text` body column, quoted-identifier
check-constraint SQL); the rest of the entity mapping is the relational base's.

## ⚠️ Gotchas

- **Migrations run automatically**, not on demand: the first call against a given connection string checks for
  and applies pending migrations under a lock. There's no separate "apply migrations" step to remember, but
  also no opt-out — the first request against a fresh database pays that cost.
- Design-time EF tooling (`dotnet ef migrations add ...`) needs the `IDEMPOTENCY_NPGSQL_CONNECTION` environment
  variable set — see `DbContextFactory`.
- `MarkKeyAsProcessedAsync` defensively swallows a unique-violation if it's ever called without a prior
  reservation (e.g. concurrent calls from a retried background job): it won't throw, but it also won't overwrite
  whichever caller's insert won the race — see `IdempotencyMarkAsProcessedConcurrencyTests`.
- `AddIdempotencyNpgsqlStore` / `AddIdempotencyWithNpgsqlStore` are first-wins: calling either again, even with a
  different connection string, is a no-op once `IdempotencyDbContext` is already registered.
