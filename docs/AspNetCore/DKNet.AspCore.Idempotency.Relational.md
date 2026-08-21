# DKNet.AspCore.Idempotency.Relational

Shared EF Core building blocks for **implementing a relational idempotency store** —
the entity, the entity mapping, the shared `DbContext`, and the concurrency-safe
insert-or-query pattern that [`DKNet.AspCore.Idempotency.MsSqlStore`](./DKNet.AspCore.Idempotency.MsSqlStore.md)
and [`DKNet.AspCore.Idempotency.NpgsqlStore`](./DKNet.AspCore.Idempotency.NpgsqlStore.md) both derive from.

> **Not for app authors.** This package has no `AddIdempotency...` extension of its
> own and nothing in it is `public`. If you are wiring idempotency into an app, use
> [`DKNet.AspCore.Idempotency`](./DKNet.AspCore.Idempotency.md) — its "Choosing a
> store" section covers when a relational store is the right call — plus a concrete
> provider package. Read on only if you are adding support for a **new** relational
> database (MySQL, SQLite, …) to the DKNet idempotency family.

## 🧩 What problem does it solve?

Every relational idempotency store needs the same four things: an entity shaped
around `IdempotentKeyInfo`/`CachedResponse`, a mapping for that entity, a
`DbContext` that discovers the mapping, and a race-safe "has this key been seen
before, and if not, claim it" operation backed by a database unique constraint.
None of that is provider-specific — only the SQL error a duplicate-key violation
raises, and a couple of column-type/constraint-syntax details, differ between SQL
Server, PostgreSQL, and whatever comes next.

`DKNet.AspCore.Idempotency.Relational` factors the provider-agnostic 95% into one
place so a new provider package only has to supply the provider-specific 5%. It is
relevant exactly once: when you are about to write a new
`DKNet.AspCore.Idempotency.<Provider>Store` package.

Because every type here is `internal`, a new provider project must be granted
`InternalsVisibleTo` from this assembly (see `DKNet.AspCore.Idempotency.Relational.csproj`,
which currently lists `DKNet.AspCore.Idempotency.MsSqlStore` and
`DKNet.AspCore.Idempotency.NpgsqlStore` plus their test projects) — this is not a
publicly extensible base for out-of-repo consumers.

## 🔌 Minimum wiring a derived store needs to provide

A new provider package supplies exactly four pieces, all following the pattern
`IdempotencySqlServerStore`/`IdempotencyPostgresStore` already establish:

1. **A closed `DbContext`** — a one-line `internal sealed` subclass of
   `IdempotencyDbContext` that pins the provider's own closed
   `DbContextOptions<TContext>`:

   ```csharp
   internal sealed class IdempotencyDbContext(DbContextOptions<IdempotencyDbContext> options)
       : DKNet.AspCore.Idempotency.Relational.Data.IdempotencyDbContext(options);
   ```

2. **A concrete entity configuration** — subclass
   `IdempotencyKeyConfiguration` and override the two provider-specific members:

   ```csharp
   internal sealed class IdempotencyKeyConfiguration
       : DKNet.AspCore.Idempotency.Relational.Data.Configurations.IdempotencyKeyConfiguration
   {
       protected override string BodyColumnType => "nvarchar(max)";               // "text" for Npgsql
       protected override string StatusCodeCheckConstraintSql => "[StatusCode] BETWEEN 100 AND 599";
   }
   ```

3. **A concrete store** — subclass `IdempotencyRelationalStore<TContext>` and
   override `IsProviderUniqueViolation`:

   ```csharp
   internal sealed class IdempotencySqlServerStore(
       IServiceProvider serviceProvider,
       IOptions<IdempotencyOptions> options,
       ILogger<IdempotencySqlServerStore> logger)
       : IdempotencyRelationalStore<IdempotencyDbContext>(serviceProvider, options, logger)
   {
       protected override bool IsProviderUniqueViolation(DbUpdateException ex) =>
           ex.InnerException is SqlException { Number: 2601 or 2627 };
   }
   ```

4. **A DI registration extension** — register the closed `DbContext` (and its
   factory) plus `AddIdempotentKey<TStore>` from the core package:

   ```csharp
   public static IServiceCollection AddIdempotencyMsSqlStore(this IServiceCollection services, string connectionString)
   {
       services.AddDbContext<IdempotencyDbContext>(o => o.UseSqlServer(connectionString, sql => sql
               .MigrationsAssembly(typeof(IdempotencyMsSqlSetup).Assembly)
               .MigrationsHistoryTable(nameof(IdempotencyDbContext), "migrate")),
           optionsLifetime: ServiceLifetime.Singleton)
           .AddDbContextFactory<IdempotencyDbContext>();
       return services;
   }

   public static IServiceCollection AddIdempotencyWithMsSqlStore(
       this IServiceCollection services, string connectionString, Action<IdempotencyOptions>? config = null) =>
       services.AddIdempotencyMsSqlStore(connectionString)
           .AddIdempotentKey<IdempotencySqlServerStore>(config);
   ```

   `AddDbContextFactory<TContext>` is required — `IdempotencyRelationalStore<TContext>`
   resolves `IDbContextFactory<TContext>` per operation rather than injecting the
   context directly, so each reserve/check/complete call gets its own short-lived
   context instance instead of sharing one across a longer-lived scope.

Own migrations too: EF Core migrations live in the derived provider project
(`Migrations/` folder, its own `MigrationsAssembly`), not in this base package —
see [Gotchas](#⚠️-gotchas).

## ⚙️ Building blocks

### `IdempotencyKeyEntity`

The row shape shared by every provider. Constructed from `IdempotentKeyInfo` +
`CachedResponse`, with private setters so state only changes through its own
methods:

- `SanitizeKey(string key)` — hashes the raw composite key to a fixed 64-character
  uppercase hex SHA-256 digest before it is ever stored or queried, so structurally
  distinct keys never collapse onto the same database value and no caller-supplied
  content lands in a column verbatim.
- `Complete(CachedResponse response)` — overwrites an in-flight reservation row
  with the finished response's status code, body, content type, and expiry.
- `IsExpired` — `true` once `ExpiresAt` has passed.

### `IdempotencyKeyConfiguration`

The `IEntityTypeConfiguration<IdempotencyKeyEntity>` base every provider's own
configuration derives from. Owns every mapping detail that is identical across
providers — key, lengths, unicode flags, the `ExpiresAt` index, the unique
`UX_CompositeKey` index — and defers only the response-body column type and the
status-code check-constraint SQL to two `protected abstract` members the derived
type overrides (see [Configuration defaults](#🗄️-configuration--entity-mapping-defaults)).

### `IdempotencyDbContext`

The abstract `DbContext` base. Its `OnModelCreating` calls
`modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly)` — because it
scans the *derived* type's assembly, each provider only has to add its own
`IdempotencyKeyConfiguration` next to its `DbContext` subclass; nothing needs to
register it explicitly.

### `IdempotencyRelationalStore<TContext>`

The `IIdempotencyKeyStore` implementation itself:

- **`IsKeyProcessedAsync`** — looks up the sanitized composite key; if absent or
  expired, delegates to the reservation path below; if present with the sentinel
  `102` (Processing) status code, reports the key as still in-flight; otherwise
  returns the cached `CachedResponse`.
- **Reservation (insert-or-query) pattern** — `ReserveKeyAsync` inserts a
  placeholder row with `StatusCode == 102` and relies on the `UX_CompositeKey`
  unique index to make exactly one concurrent insert succeed. The loser's
  `DbUpdateException` is caught via the abstract `IsProviderUniqueViolation` hook,
  and the loser then either reports the winner's in-flight/completed state, or —
  if the row it collided with is itself expired — atomically reclaims it with a
  conditional `ExecuteUpdateAsync` (only rows still matching the expired
  predicate are updated), giving the same single-winner guarantee the unique
  index gives the fresh-insert path.
- **`MarkKeyAsProcessedAsync`** — turns the reservation row into the durable
  cached result via `entity.Complete(...)`.
- **Migration guard** — `EnsureDatabaseCreatedAsync` applies any pending EF Core
  migrations once per distinct connection string (tracked in a static
  `ConcurrentDictionary<string, bool>`, guarded by a `SemaphoreSlim`), so a process
  that targets more than one database (per-tenant databases, shared test hosts)
  migrates each one rather than skipping every database after the first.
- **Per-operation scoping** — each public method resolves
  `IDbContextFactory<TContext>` from an `AsyncServiceScope` created once per store
  instance and creates a fresh, short-lived `DbContext` per call via
  `CreateDbContextAsync()`.

## 🗄️ Configuration & entity mapping defaults

| Column | Type / constraint | Notes |
|---|---|---|
| `Id` | `Guid` (PK) | `Guid.CreateVersion7()`, time-ordered |
| `IdempotentKey` | `varchar(150)`, required | Non-Unicode, the raw caller-supplied key |
| `Endpoint` | `nvarchar(250)`, required | Unicode |
| `Method` | `varchar(20)`, required | Non-Unicode HTTP method |
| `CompositeKey` | `nvarchar(128)`, required, **unique** (`UX_CompositeKey`) | SHA-256 hash of the raw composite key |
| `StatusCode` | `int`, required, check `CK_StatusCode_Valid` | 100–599; provider supplies the constraint SQL |
| `Body` | provider column type (`nvarchar(max)` / `text`), max 1,048,576 chars | Unicode; `null` while reserved |
| `ContentType` | `varchar(256)` | Non-Unicode MIME type |
| `CreatedAt` / `ExpiresAt` | `DateTimeOffset` | `ExpiresAt` is indexed for cleanup queries |

## 🔗 How it composes

```
DKNet.AspCore.Idempotency            core: IIdempotencyKeyStore, IdempotencyOptions, AddIdempotentKey<TStore>
        ▲
        │ implements
DKNet.AspCore.Idempotency.Relational shared base: entity, mapping, DbContext, reserve/check/complete
        ▲                    ▲
        │ derives            │ derives
DKNet.AspCore.Idempotency     DKNet.AspCore.Idempotency
   .MsSqlStore                   .NpgsqlStore
```

App code never references this package directly — it depends on
`DKNet.AspCore.Idempotency` for `IIdempotencyKeyStore`/`AddIdempotentKey<TStore>`
and on a concrete provider package (`MsSqlStore`/`NpgsqlStore`) for the
`AddIdempotencyWithXxxStore(...)` registration that wires a store built on this
base into DI.

## ⚠️ Gotchas

- **The unique index is load-bearing, not incidental.** The entire insert-or-query
  concurrency guarantee rests on `UX_CompositeKey` being a real, enforced unique
  index in the database. A new provider's `IdempotencyKeyConfiguration` inherits
  the index declaration for free, but only a database that actually creates it —
  via a migration that has been applied — makes the guarantee hold; without it,
  concurrent duplicate requests can both "win" the reservation.
- **Migrations belong to the derived provider, not this base.** This package
  intentionally ships no `Migrations/` folder — `IdempotencyDbContext` here is
  `abstract`, so it cannot be a design-time migrations target. Each provider
  project owns its own migrations, its own `MigrationsAssembly(...)`, and its own
  `IDesignTimeDbContextFactory<TContext>` (see `MsSqlStore`/`NpgsqlStore`'s
  `Data/DbContextFactory.cs`).
- **`IsProviderUniqueViolation` must never match on message text.** Error
  messages are localized by the server's login language; match the provider's own
  structured error (SQL Server error numbers 2601/2627, Postgres SQL state
  `23505`), never a substring.
- **The `102` sentinel is deliberate, not a placeholder for "add real values
  later."** It is a legal value under `CK_StatusCode_Valid` (100–599) that no real
  completed HTTP response will ever use, so a reservation row and a completed row
  are always distinguishable by `StatusCode` alone.
- **Reservation lifetime tracks `IdempotencyOptions.InFlightReservationTimeout`**
  (default 30 seconds), not `Expiration`. A handler that runs longer than this
  timeout without completing leaves its reservation reclaimable by the next
  caller for the same key.
- **These types are `internal`.** A new provider project must be added to this
  package's `InternalsVisibleTo` list before it can derive from any of the base
  types described here.

## See also

- [`DKNet.AspCore.Idempotency`](./DKNet.AspCore.Idempotency.md) — the core
  package; see "Choosing a store" for when a relational store is the right
  choice at all.
- [`DKNet.AspCore.Idempotency.MsSqlStore`](./DKNet.AspCore.Idempotency.MsSqlStore.md)
- [`DKNet.AspCore.Idempotency.NpgsqlStore`](./DKNet.AspCore.Idempotency.NpgsqlStore.md)
