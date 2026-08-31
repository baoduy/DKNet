# DKNet.AspCore.Idempotency.Relational

Shared EF Core building blocks — entity, mapping, `DbContext`, and the concurrency-safe
reserve/check/complete flow — that every relational idempotency store for
[`DKNet.AspCore.Idempotency`](DKNet.AspCore.Idempotency.md) derives from.

> **Not for app authors.** This package has no `AddIdempotency...` extension of its own and
> nothing in it is `public`. If you are wiring idempotency into an app, use
> [`DKNet.AspCore.Idempotency`](DKNet.AspCore.Idempotency.md) — its
> [Choosing a store](DKNet.AspCore.Idempotency.md#️-choosing-a-store) section covers when a
> relational store is the right call — plus a concrete provider package
> ([MsSqlStore](DKNet.AspCore.Idempotency.MsSqlStore.md) /
> [NpgsqlStore](DKNet.AspCore.Idempotency.NpgsqlStore.md)). Read on only if you are adding
> support for a **new** relational database (MySQL, SQLite, …) to the DKNet idempotency family.

## ✨ Why use it?

- **You are writing the fifth idempotency store and only 5% of it is new.** Every relational
  provider needs the same entity, the same mapping, the same `DbContext`, and the same
  "has this key been seen, and if not, claim it" operation. Only the duplicate-key error the
  provider raises, the response-body column type, and the check-constraint quoting differ.
- **Concurrency correctness is already solved and already tested.** The insert-or-query
  reservation, the expired-row reclaim, and the single-winner guarantee live here — you inherit
  them instead of re-deriving a race-free protocol per provider.
- **Multi-database processes work out of the box.** The migration guard is keyed per connection
  string, so a per-tenant or shared-test-host process migrates every database it targets rather
  than skipping all but the first.
- **The two SQL stores in this repo are the worked examples.**
  [`MsSqlStore`](DKNet.AspCore.Idempotency.MsSqlStore.md) and
  [`NpgsqlStore`](DKNet.AspCore.Idempotency.NpgsqlStore.md) are each about four small types on
  top of this base — copy either one.

## 🚀 Quick Start

```bash
dotnet add package DKNet.AspCore.Idempotency.Relational
```

A new provider package supplies exactly four pieces, following the pattern
`IdempotencySqlServerStore` / `IdempotencyPostgresStore` already establish.

**1. A closed `DbContext`** — a one-line `internal sealed` subclass of `IdempotencyDbContext`
that pins the provider's own closed `DbContextOptions<TContext>`:

```csharp
internal sealed class IdempotencyDbContext(DbContextOptions<IdempotencyDbContext> options)
    : DKNet.AspCore.Idempotency.Relational.Data.IdempotencyDbContext(options);
```

**2. A concrete entity configuration** — subclass `IdempotencyKeyConfiguration` and override the
two provider-specific members:

```csharp
internal sealed class IdempotencyKeyConfiguration
    : DKNet.AspCore.Idempotency.Relational.Data.Configurations.IdempotencyKeyConfiguration
{
    protected override string BodyColumnType => "nvarchar(max)";                 // "text" for Npgsql
    protected override string StatusCodeCheckConstraintSql => "[StatusCode] BETWEEN 100 AND 599";
}
```

**3. A concrete store** — subclass `IdempotencyRelationalStore<TContext>` and override
`IsProviderUniqueViolation`:

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

**4. A DI registration extension** — register the closed `DbContext` *and its factory*, then hand
the store to `AddIdempotentKey<TStore>` from the core package:

```csharp
public static IServiceCollection AddIdempotencyMsSqlStore(this IServiceCollection services, string connectionString)
{
    ArgumentNullException.ThrowIfNull(services);
    ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

    if (services.IsRegistered<IdempotencyDbContext>())
        return services;

    services.AddDbContext<IdempotencyDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sqlOptions => sqlOptions
                .MigrationsAssembly(typeof(IdempotencyMsSqlSetup).Assembly)
                .MigrationsHistoryTable(nameof(IdempotencyDbContext), "migrate"));
        }, optionsLifetime: ServiceLifetime.Singleton)
        .AddDbContextFactory<IdempotencyDbContext>();

    return services;
}

public static IServiceCollection AddIdempotencyWithMsSqlStore(
    this IServiceCollection services, string connectionString, Action<IdempotencyOptions>? config = null)
{
    services.AddIdempotencyMsSqlStore(connectionString);
    return services.AddIdempotentKey<IdempotencySqlServerStore>(config);
}
```

`IsRegistered<TService>()` is the first-wins guard from `DKNet.Fw.Extensions` (namespace
`Microsoft.Extensions.DependencyInjection`). `AddDbContextFactory<TContext>` is required — `IdempotencyRelationalStore<TContext>` resolves
`IDbContextFactory<TContext>` per operation rather than injecting the context directly, so each
reserve/check/complete call gets its own short-lived context instance instead of sharing one
across a longer-lived scope.

Own the migrations too: they live in the derived provider project (a `Migrations/` folder, its own
`MigrationsAssembly`), never in this base package — see
[Gotchas & limits](#️-gotchas--limits).

## 🧩 Features

### `IdempotencyKeyEntity` — the shared row shape

Constructed from an `IdempotentKeyInfo` plus a `CachedResponse`, with private setters so state only
changes through its own methods:

- `SanitizeKey(string key)` — hashes the raw composite key to a fixed 64-character **uppercase**
  hex SHA-256 digest before it is ever stored or queried, so structurally distinct keys never
  collapse onto the same database value and no caller-supplied content lands in a column verbatim.
  Throws `ArgumentException` on a null/whitespace key.
- `Complete(CachedResponse response)` — overwrites an in-flight reservation row with the finished
  response's status code, body, content type, and expiry.
- `IsExpired` — `true` once a non-null `ExpiresAt` has passed. Not mapped to a column.

### `IdempotencyKeyConfiguration` — the shared mapping

The `IEntityTypeConfiguration<IdempotencyKeyEntity>` base every provider's own configuration
derives from. It owns every mapping detail that is identical across providers — key, lengths,
unicode flags, the `ExpiresAt` index, the unique `UX_CompositeKey` index — and defers exactly two
`protected abstract` members to the derived type (see
[Configuration reference](#️-configuration-reference)).

### `IdempotencyDbContext` — mapping discovery from the derived assembly

Its `OnModelCreating` calls `modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly)`.
Because that scans the **derived** type's assembly, each provider only has to place its own
`IdempotencyKeyConfiguration` next to its `DbContext` subclass; nothing needs registering
explicitly. The base constructor accepts the non-generic `DbContextOptions`, so a single base
works regardless of which closed `DbContextOptions<TContext>` the derived context declares.

### `IdempotencyRelationalStore<TContext>` — reserve, check, complete

The `IIdempotencyKeyStore` implementation itself:

- **`IsKeyProcessedAsync`** — looks up the sanitized composite key among unexpired rows; if absent
  or expired, delegates to the reservation path below; if present with the sentinel `102`
  (Processing) status code, reports the key as still in-flight (`(true, null)`); otherwise returns
  the winner's cached `CachedResponse`.
- **Reservation (insert-or-query) pattern** — `ReserveKeyAsync` inserts a placeholder row with
  `StatusCode == 102` and relies on the `UX_CompositeKey` unique index to make exactly one
  concurrent insert succeed. The loser's `DbUpdateException` is caught via the abstract
  `IsProviderUniqueViolation` hook, and the loser then either reports the winner's
  in-flight/completed state, or — if the row it collided with is itself expired — atomically
  reclaims it with a conditional `ExecuteUpdateAsync` (only rows still matching the expired
  predicate are updated), giving the same single-winner guarantee the unique index gives the
  fresh-insert path.
- **`MarkKeyAsProcessedAsync`** — turns the reservation row into the durable cached result via
  `entity.Complete(...)`. If no reservation row is found it defensively inserts one, and a
  unique-violation on that fallback insert is logged and swallowed rather than thrown.
- **Migration guard** — `EnsureDatabaseCreatedAsync` applies any pending EF Core migrations once
  per distinct connection string (tracked in a `static ConcurrentDictionary<string, bool>`, guarded
  by a `SemaphoreSlim`), so a process that targets more than one database (per-tenant databases,
  shared test hosts) migrates each one rather than skipping every database after the first.
- **Per-operation scoping** — each public method resolves `IDbContextFactory<TContext>` from an
  `AsyncServiceScope` created once per store instance, and creates a fresh, short-lived
  `DbContext` per call via `CreateDbContextAsync()`. The store is `IAsyncDisposable` and disposes
  that scope.

## ⚙️ Configuration reference

The base exposes no options type. What a derived configuration must supply:

| Member | Type | Purpose |
|---|---|---|
| `BodyColumnType` | `protected abstract string` | Provider column type for the response body — `nvarchar(max)` on SQL Server, `text` on PostgreSQL. |
| `StatusCodeCheckConstraintSql` | `protected abstract string` | `CK_StatusCode_Valid` SQL, differing only in identifier quoting — `[StatusCode] BETWEEN 100 AND 599` vs `"StatusCode" BETWEEN 100 AND 599`. |

Everything else is fixed by the shared mapping:

| Column | Type / constraint | Notes |
|---|---|---|
| `Id` | `Guid` (PK) | `Guid.CreateVersion7()`, time-ordered |
| `IdempotentKey` | max 150, required, non-Unicode | The raw caller-supplied key |
| `Endpoint` | max 250, required, Unicode | Route template, upper-invariant |
| `Method` | max 20, required, non-Unicode | HTTP method |
| `CompositeKey` | max 128, required, Unicode, **unique** (`UX_CompositeKey`) | Uppercase hex SHA-256 of the raw composite key |
| `StatusCode` | `int`, required, check `CK_StatusCode_Valid` | 100–599; provider supplies the constraint SQL |
| `Body` | provider column type, max 1,048,576 chars | Unicode; `null` while reserved |
| `ContentType` | max 256, non-Unicode | MIME type, nullable |
| `CreatedAt` / `ExpiresAt` | `DateTimeOffset` / `DateTimeOffset?` | `ExpiresAt` is indexed (`IX_IdempotencyKeys_ExpiresAt`) for cleanup queries |

Runtime behaviour is driven by the core package's `IdempotencyOptions` — this store reads
`InFlightReservationTimeout` for the reservation window; see the
[core configuration reference](DKNet.AspCore.Idempotency.md#️-configuration-reference).

## 🧱 Where it fits

```text
DKNet.AspCore.Idempotency            core: IIdempotencyKeyStore, IdempotencyOptions, AddIdempotentKey<TStore>
        ▲
        │ implements
DKNet.AspCore.Idempotency.Relational shared base: entity, mapping, DbContext, reserve/check/complete
        ▲                    ▲
        │ derives            │ derives
DKNet.AspCore.Idempotency     DKNet.AspCore.Idempotency
   .MsSqlStore                   .NpgsqlStore
```

App code never references this package directly — it depends on `DKNet.AspCore.Idempotency` for
`IIdempotencyKeyStore`/`AddIdempotentKey<TStore>` and on a concrete provider package
(`MsSqlStore`/`NpgsqlStore`) for the `AddIdempotencyWithXxxStore(...)` registration that wires a
store built on this base into DI.

## ⚠️ Gotchas & limits

- **These types are `internal`.** A new provider project must be added to this package's
  `InternalsVisibleTo` list before it can derive from any of the base types described here — the
  `.csproj` currently lists `DKNet.AspCore.Idempotency.MsSqlStore`,
  `DKNet.AspCore.Idempotency.NpgsqlStore`, and their two test projects. This is not a publicly
  extensible base for out-of-repo consumers.
- **The unique index is load-bearing, not incidental.** The entire insert-or-query concurrency
  guarantee rests on `UX_CompositeKey` being a real, enforced unique index in the database. A new
  provider's `IdempotencyKeyConfiguration` inherits the index declaration for free, but only a
  database that actually creates it — via a migration that has been applied — makes the guarantee
  hold; without it, concurrent duplicate requests can both "win" the reservation.
- **Migrations belong to the derived provider, not this base.** This package intentionally ships no
  `Migrations/` folder — `IdempotencyDbContext` here is `abstract`, so it cannot be a design-time
  migrations target. Each provider project owns its own migrations, its own
  `MigrationsAssembly(...)`, and its own `IDesignTimeDbContextFactory<TContext>` (see
  `MsSqlStore`/`NpgsqlStore`'s `Data/DbContextFactory.cs`).
- **`IsProviderUniqueViolation` must never match on message text.** Error messages are localized by
  the server's login language; match the provider's own structured error (SQL Server error numbers
  2601/2627, Postgres SQL state `23505`), never a substring.
- **The `102` sentinel is deliberate, not a placeholder.** It is a legal value under
  `CK_StatusCode_Valid` (100–599) that no real completed HTTP response will ever use, so a
  reservation row and a completed row are always distinguishable by `StatusCode` alone.
- **Reservation lifetime tracks `IdempotencyOptions.InFlightReservationTimeout`** (default 30
  seconds), not `Expiration`. A handler that runs longer than this timeout without completing
  leaves its reservation reclaimable by the next caller for the same key.
- **Nothing purges expired rows.** Rows outlive their `ExpiresAt` until a colliding request
  reclaims them; `IX_IdempotencyKeys_ExpiresAt` exists so a provider or an application can add its
  own sweep, but the base ships none.

## 🔗 Related packages

- [DKNet.AspCore.Idempotency](DKNet.AspCore.Idempotency.md) — the core package; reach for it (not
  this one) to wire idempotency into an app, and see its "Choosing a store" section for whether a
  relational store is the right choice at all.
- [DKNet.AspCore.Idempotency.MsSqlStore](DKNet.AspCore.Idempotency.MsSqlStore.md) — the SQL Server
  provider built on this base; the closest worked example when the new database quotes identifiers
  with brackets.
- [DKNet.AspCore.Idempotency.NpgsqlStore](DKNet.AspCore.Idempotency.NpgsqlStore.md) — the
  PostgreSQL provider built on this base; the closest worked example when the new database reports
  errors by SQL state.
- [DKNet.AspCore.Idempotency.RedisStore](DKNet.AspCore.Idempotency.RedisStore.md) — reach for this
  instead when the target store is not relational at all; it implements `IIdempotencyKeyStore`
  directly and does not use this base.
