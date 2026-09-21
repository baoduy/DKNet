# DKNet.AspCore.Idempotency.Relational

| Field | Value |
|---|---|
| Area | AspNetCore |
| NuGet package | `DKNet.AspCore.Idempotency.Relational` — packable, but see **Purpose**: no application should add this directly |
| NuGet page | https://www.nuget.org/packages/DKNet.AspCore.Idempotency.Relational |
| Docs | https://github.com/baoduy/DKNet/blob/dev/docs/AspNetCore/DKNet.AspCore.Idempotency.Relational.md |
| Source | https://github.com/baoduy/DKNet/tree/dev/src/AspNet/DKNet.AspCore.Idempotency.Relational |
| Depends on (DKNet) | `DKNet.AspCore.Idempotency` (`IIdempotencyKeyStore`, `IdempotencyOptions`, `IdempotentKeyInfo`, `CachedResponse`, `AddIdempotentKey<TStore>`) |
| Depends on (3rd party) | `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Relational`, `Microsoft.Extensions.Hosting.Abstractions` |
| Target framework | `net10.0` |

## Purpose

This package is the shared EF Core plumbing behind every relational `DKNet.AspCore.Idempotency` store: one entity (`IdempotencyKeyEntity`), one `IEntityTypeConfiguration` mapping (`IdempotencyKeyConfiguration`), one `DbContext` base (`IdempotencyDbContext`), and the race-safe insert-or-query reserve/check/complete algorithm (`IdempotencyRelationalStore<TContext>`), plus a startup migration runner (`IdempotencyMigrationHostedService<TContext>`). A new provider (MySQL, SQLite, ...) only supplies the two things that genuinely differ per database: the response-body column type / check-constraint SQL, and how to recognize that database's own unique-key-violation error.

**It is NOT for application authors and has no consumer-facing API at all** — every type it declares is `internal`, gated open only to the two in-repo provider packages (`MsSqlStore`, `NpgsqlStore`) and their test projects. An application wires idempotency through `DKNet.AspCore.Idempotency` (`AddIdempotentKey<TStore>`) plus a concrete provider package's `AddIdempotencyWithXxxStore(...)` extension — never this one. Reach for this package's source only when implementing support for a brand-new relational database inside the DKNet repository itself.

## Entry points

There is no public entry point. This package registers nothing itself and exposes no `Add...` extension — a provider project (e.g. `DKNet.AspCore.Idempotency.NpgsqlStore`) supplies the DI registration extension that consumes this base's `internal` types.

## Public surface

**No public API surface exists.** Every type below is declared `internal`. Grouped here because a new in-repo provider project — the only real audience — must derive from these exact members.

### `DKNet.AspCore.Idempotency.Relational.Data`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `IdempotencyDbContext` | `internal abstract class : DbContext` | Shared EF Core context; discovers the derived (provider) assembly's own `IdempotencyKeyConfiguration` automatically. | Ctor takes non-generic `DbContextOptions` (one base ctor works for every provider's closed `DbContextOptions<TContext>`); `DbSet<IdempotencyKeyEntity> IdempotencyKeys { get; init; }` (`required`); `OnModelCreating` calls `modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly)`. |
| `IdempotencyKeyEntity` | `internal sealed class` | The shared row shape for one idempotency reservation/result. | Ctor `(IdempotentKeyInfo info, CachedResponse item)` — throws `ArgumentException` if `info.IdempotentKey` is null/empty; private parameterless ctor for EF materialization. Properties (all `private set`): `Guid Id`, `string IdempotentKey` (max 150), `string Endpoint` (max 250), `string Method` (max 20), `string CompositeKey`, `int StatusCode`, `string? Body`, `string? ContentType`, `DateTimeOffset CreatedAt`, `DateTimeOffset? ExpiresAt`; `bool IsExpired` (not mapped). `internal static string SanitizeKey(string key)` — SHA-256, uppercase hex, 64 chars; throws `ArgumentException` on null/whitespace. |

### `DKNet.AspCore.Idempotency.Relational.Data.Configurations`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `IdempotencyKeyConfiguration` | `internal abstract class : IEntityTypeConfiguration<IdempotencyKeyEntity>` | Every mapping detail identical across relational providers. | `protected abstract string BodyColumnType { get; }`; `protected abstract string StatusCodeCheckConstraintSql { get; }`; `Configure(EntityTypeBuilder<IdempotencyKeyEntity>)` — fixed mapping (see **Options & defaults**). |

### `DKNet.AspCore.Idempotency.Relational.Store`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `IdempotencyRelationalStore<TContext>` | `internal abstract class : IIdempotencyKeyStore, IAsyncDisposable` where `TContext : IdempotencyDbContext` | The shared reserve/check/complete implementation every relational provider store derives from. | Ctor `(IServiceProvider, IOptions<IdempotencyOptions>, ILogger)`; `protected abstract bool IsProviderUniqueViolation(DbUpdateException ex)`; `IsKeyProcessedAsync(IdempotentKeyInfo)`; `MarkKeyAsProcessedAsync(IdempotentKeyInfo, CachedResponse)`; `DisposeAsync()`. |
| `IdempotencyMigrationHostedService<TContext>` | `internal sealed class : IHostedLifecycleService` where `TContext : DbContext` | Applies pending EF Core migrations for `TContext` once, in `StartAsync`, before the host serves requests. | Ctor `(IDbContextFactory<TContext>)`; implements all six `IHostedLifecycleService` moments — only `StartAsync` does work (`GetPendingMigrationsAsync` → `MigrateAsync` if any). |

## Options & defaults

No options object exists in this package. The one runtime knob it reads belongs to the core package:

| Option | Type | Default | Effect |
|---|---|---|---|
| `IdempotencyOptions.InFlightReservationTimeout` | `TimeSpan` | `30 seconds` | Lifetime of a `StatusCode == 102` reservation row, on both the fresh-insert path and the expired-row reclaim path. A handler running longer than this without completing leaves its reservation reclaimable by the next caller for the same key. |

Fixed mapping applied by `IdempotencyKeyConfiguration.Configure` (not overridable by a provider except the two named columns):

| Column | Type / constraint | Notes |
|---|---|---|
| `Id` | `Guid` (PK) | Time-ordered (`Guid.CreateVersion7()`). |
| `IdempotentKey` | max 150, required, non-Unicode | Raw caller-supplied key. |
| `Endpoint` | max 250, required, Unicode | Route template, upper-invariant. |
| `Method` | max 20, required, non-Unicode | HTTP method. |
| `CompositeKey` | max 128, required, Unicode, unique index `UX_CompositeKey` | Uppercase hex SHA-256 of the raw composite key. |
| `StatusCode` | `int`, required, check constraint `CK_StatusCode_Valid` | 100–599; provider supplies the exact SQL via `StatusCodeCheckConstraintSql`. |
| `Body` | provider's `BodyColumnType`, max length 1,048,576, Unicode | `null` while the row is a reservation. |
| `ContentType` | max 256, non-Unicode | MIME type, nullable. |
| `CreatedAt` | `DateTimeOffset` | No extra constraint. |
| `ExpiresAt` | `DateTimeOffset?`, indexed (`IX_IdempotencyKeys_ExpiresAt`) | Powers reclaim/cleanup queries. |

## Usage patterns

These patterns are for **implementing a new relational provider inside the DKNet repository**, not for an application consuming idempotency — an application stops at `DKNet.AspCore.Idempotency` plus a concrete provider package (`MsSqlStore`/`NpgsqlStore`). Every type touched below (`IdempotencyDbContext`, `IdempotencyKeyConfiguration`, `IdempotencyRelationalStore<TContext>`, `IdempotencyMigrationHostedService<TContext>`) is `internal` with an `InternalsVisibleTo` grant that names only `DKNet.AspCore.Idempotency.MsSqlStore`, `DKNet.AspCore.Idempotency.NpgsqlStore`, and their test projects. **None of the fences below compile from outside the DKNet repository**, so every one is marked `// no-compile` — they show the shape a sixth in-repo provider would take, not something a consumer application can do.

### Step 1 of 4: deriving the closed `DbContext`

```csharp
// no-compile: DKNet.AspCore.Idempotency.Relational.Data.IdempotencyDbContext is internal,
// visible only inside the DKNet repository via InternalsVisibleTo.
using Microsoft.EntityFrameworkCore;

namespace DKNet.AspCore.Idempotency.MySqlStore.Data;

internal sealed class IdempotencyDbContext(DbContextOptions<IdempotencyDbContext> options)
    : DKNet.AspCore.Idempotency.Relational.Data.IdempotencyDbContext(options);
```

**Notes**: must stay `internal sealed`; the base's non-generic `DbContextOptions` constructor is why this one-liner compiles (inside the repo) without repeating any options-forwarding logic.

### Step 2 of 4: supplying the two provider-specific mapping seams

```csharp
// no-compile: DKNet.AspCore.Idempotency.Relational.Data.Configurations.IdempotencyKeyConfiguration
// is internal, visible only inside the DKNet repository via InternalsVisibleTo.
namespace DKNet.AspCore.Idempotency.MySqlStore.Data.Configurations;

internal sealed class IdempotencyKeyConfiguration
    : DKNet.AspCore.Idempotency.Relational.Data.Configurations.IdempotencyKeyConfiguration
{
    protected override string BodyColumnType => "longtext";

    protected override string StatusCodeCheckConstraintSql => "`StatusCode` BETWEEN 100 AND 599";
}
```

**Notes**: no `Configure(...)` override needed or allowed to add columns — the base's `Configure` is not virtual-overridable for anything beyond these two properties. Table shape (lengths, unicode flags, index names) is fixed and shared.

### Step 3 of 4: supplying the provider-specific unique-violation detector and store

```csharp
// no-compile: IdempotencyRelationalStore<TContext> is internal, visible only inside the
// DKNet repository via InternalsVisibleTo.
using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.MySqlStore.Data;
using DKNet.AspCore.Idempotency.Relational.Store;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace DKNet.AspCore.Idempotency.MySqlStore.Store;

internal sealed class IdempotencyMySqlStore(
    IServiceProvider serviceProvider,
    IOptions<IdempotencyOptions> options,
    ILogger<IdempotencyMySqlStore> logger)
    : IdempotencyRelationalStore<IdempotencyDbContext>(serviceProvider, options, logger)
{
    protected override bool IsProviderUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is MySqlException { ErrorCode: MySqlErrorCode.DuplicateKeyEntry };
}
```

**Notes**: match the provider's own *structured* error (an error code/number/SQL state), never a message substring — see **Gotchas**.

### Step 4 of 4: wiring DI — `DbContext`, factory, and startup migration

```csharp
// no-compile: IdempotencyMigrationHostedService<TContext> is internal, visible only inside
// the DKNet repository via InternalsVisibleTo. This is the only step that becomes truly
// public in a real provider, and it lives in the provider project, not here.
using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.MySqlStore.Data;
using DKNet.AspCore.Idempotency.MySqlStore.Store;
using DKNet.AspCore.Idempotency.Relational.Store;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DKNet.AspCore.Idempotency.MySqlStore;

public static class IdempotencyMySqlSetup
{
    public static IServiceCollection AddIdempotencyMySqlStore(
        this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<IdempotencyDbContext>(options =>
                options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), mySqlOptions =>
                    mySqlOptions.MigrationsAssembly(typeof(IdempotencyMySqlSetup).Assembly)),
                optionsLifetime: ServiceLifetime.Singleton)
            .AddDbContextFactory<IdempotencyDbContext>();

        services.AddHostedService<IdempotencyMigrationHostedService<IdempotencyDbContext>>();

        return services;
    }

    public static IServiceCollection AddIdempotencyWithMySqlStore(
        this IServiceCollection services, string connectionString, Action<IdempotencyOptions>? config = null)
    {
        services.AddIdempotencyMySqlStore(connectionString);
        return services.AddIdempotentKey<IdempotencyMySqlStore>(config);
    }
}
```

**Notes**: `AddDbContextFactory<TContext>` is mandatory — the base store resolves `IDbContextFactory<TContext>` per call, not an injected context. `AddHostedService<IdempotencyMigrationHostedService<TContext>>()` is what moves migration off the request path. A real provider also needs its own `IDesignTimeDbContextFactory<TContext>` and a `Migrations/` folder — see the `MsSqlStore`/`NpgsqlStore` reference files for that shape.

### Testing the shared reservation semantics against a real database

**When**: verifying a new provider (or the shared base) behaves correctly under concurrency.

```csharp
// no-compile: IdempotencyDbContext and IdempotencyPostgresStore here are the NpgsqlStore
// provider's own internal, closed types — visible only inside the DKNet repository.
using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.Filtering;
using DKNet.AspCore.Idempotency.NpgsqlStore.Data;
using DKNet.AspCore.Idempotency.NpgsqlStore.Store;
using DKNet.AspCore.Idempotency.Store;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

var connectionString = "Host=localhost;Database=idempotency;Username=postgres;Password=postgres";
var services = new ServiceCollection();
services.AddDbContextFactory<IdempotencyDbContext>(o => o.UseNpgsql(connectionString));
using var provider = services.BuildServiceProvider();

var store = new IdempotencyPostgresStore(
    provider, NullLogger<IdempotencyPostgresStore>.Instance, Options.Create(new IdempotencyOptions()));

var keyInfo = new IdempotentKeyInfo { Endpoint = "/API/ORDERS", Method = "POST", IdempotentKey = "abc-123" };
var (processed, response) = await store.IsKeyProcessedAsync(keyInfo);
// processed == false, response == null  → this caller won the reservation
await store.MarkKeyAsProcessedAsync(keyInfo, new CachedResponse
{
    StatusCode = 201, Body = "{}", ContentType = "application/json",
    CreatedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
});
```

**Notes**: `IdempotentKeyInfo` (`DKNet.AspCore.Idempotency.Filtering`) and `CachedResponse` (`DKNet.AspCore.Idempotency.Store`) are public — only the store and its `DbContext` are provider-internal. `CompositeKey` is `$"{Scope}:{Method}:{Endpoint}:{IdempotentKey ?? string.Empty}"`, computed lazily and cached on the record.

## Runtime behaviour

`IsKeyProcessedAsync(keyInfo)`:

1. `IdempotencyKeyEntity.SanitizeKey(keyInfo.CompositeKey)` hashes the composite key to the 64-char uppercase hex digest actually stored/queried.
2. Resolves `IDbContextFactory<TContext>` from a per-store scope, creates a fresh `TContext`.
3. A defensive fallback: if this connection string has not yet been marked migrated in a process-wide guard, takes a lock, checks pending migrations, and migrates if any — normally already a no-op because the migration hosted service ran first.
4. **Reserve**: adds a new `IdempotencyKeyEntity` with `StatusCode = 102` (the reservation sentinel) and `ExpiresAt = now + InFlightReservationTimeout`, then saves.
   - **Insert succeeds** → returns `(false, null)`; caller proceeds to run the protected handler.
   - **Insert throws, matched by `IsProviderUniqueViolation`** → re-reads the blocking row.
     - If it exists and is not expired: still `StatusCode == 102` → `(true, null)` (in-flight elsewhere); otherwise `(true, ToCachedResponse(blocking))` (already completed).
     - If it is expired: a conditional update flips `StatusCode` back to 102 for exactly one racing caller (reclaim); everyone else re-reads and branches as above.

`MarkKeyAsProcessedAsync(keyInfo, cachedResponse)`:

1. Sanitizes the key the same way.
2. Issues a single update on the row matching `CompositeKey`, setting `StatusCode`, `Body`, `ContentType`, `ExpiresAt` from `cachedResponse`.
3. If zero rows were updated (defensive-only path), inserts a fresh `IdempotencyKeyEntity`; a unique-violation on that fallback insert is logged and swallowed, never thrown.

`IdempotencyMigrationHostedService<TContext>.StartAsync` (the primary migration path): creates one short-lived `TContext`, checks pending migrations, and migrates if any — runs once, before the host accepts requests. The other five `IHostedLifecycleService` moments are no-ops.

## Diagnostics & exceptions

| Type | Severity | When | Fix |
|---|---|---|---|
| `ArgumentException` | error | `IdempotencyKeyEntity` constructor when `info.IdempotentKey` is null/empty | Never construct the entity with an unvalidated `IdempotentKeyInfo`; the endpoint filter already validates before this path is reached |
| `ArgumentException` | error | `IdempotencyKeyEntity.SanitizeKey(key)` when `key` is null/whitespace | Always pass a real composite key (this method is `internal` — never called from app code) |
| `DbUpdateException` (caught, not surfaced) | handled | The reserve/complete fallback insert, when `IsProviderUniqueViolation(ex)` matches | Expected concurrency path — no action; if it propagates uncaught, the derived store's `IsProviderUniqueViolation` override is matching the wrong error shape for that provider |

This package has no Roslyn analyzer.

## Gotchas

- **Every type here is `internal`, gated to a closed `InternalsVisibleTo` list.** A new provider cannot derive from any of these types unless it is added to that list inside this repo. There is no external extension path.
- **`IsProviderUniqueViolation` must key off a structured error (SQL Server 2601/2627, Postgres SQL state `23505`), never a message substring.** Matching on message text breaks under any non-English server login language, silently defeating the single-winner guarantee (both racers "win"). Always pattern-match the driver's typed exception/error-code.
- **The per-request migration guard is a *defensive fallback only*, keyed by connection string.** If the migration hosted service never ran (a host that skips hosted services), the very first request against a never-migrated database pays a full schema migration under a process-wide lock, blocking every other concurrent request behind it. Ensure the host actually runs hosted services (`WebApplication.RunAsync()`/`Run()` does this by default).
- **Forgetting `.AddDbContextFactory<TContext>()` throws at first use.** `IdempotencyRelationalStore<TContext>` resolves `IDbContextFactory<TContext>`, not an injected context — always chain `.AddDbContextFactory<TContext>()` after `.AddDbContext<TContext>()`.
- **The `102` (HTTP Processing) status code is a deliberate sentinel**, legal under the `100..599` check constraint but never a real completed response. A provider migration that narrows the check constraint below 102, or code that treats "row exists" as "completed," breaks the reservation/completed distinction.
- **Migrations are not shipped by this base package** — its `IdempotencyDbContext` is `abstract`, so it cannot be an EF Core design-time migrations target. A new provider that forgets its own `Migrations/` folder, `MigrationsAssembly(...)`, and `IDesignTimeDbContextFactory<TContext>` gets no schema at all.
- **Nothing purges expired rows** — `IX_IdempotencyKeys_ExpiresAt` exists only so a provider or app can build its own sweep; the base ships none. The table grows unbounded in a store nobody sweeps.
- **`SanitizeKey` hashes with SHA-256 and uppercases the hex** — a case-sensitive comparison anywhere downstream that lowercases first will never match, causing silent false negatives (an already-reserved key looks new). Always go through `SanitizeKey`/EF's `CompositeKey` column comparison, never re-derive the hash independently.

## Anti-patterns & hallucination traps

- `AddIdempotencyRelational(...)` / `AddIdempotencyRelationalStore(...)` — **does not exist**. This package has zero `Add...` DI extensions; only concrete provider packages (`MsSqlStore`, `NpgsqlStore`) do.
- `services.AddDbContext<DKNet.AspCore.Idempotency.Relational.Data.IdempotencyDbContext>(...)` from application code — **impossible**: the base `IdempotencyDbContext` is `abstract` and `internal`; only a provider's own closed subclass (itself `internal`, same assembly) can be registered.
- `IdempotencyRelationalStore.IsProviderUniqueViolation` matching on `ex.Message.Contains("duplicate")` or similar — **wrong and explicitly warned against**; error messages are localized. Match the provider's structured error code/number/SQL state.
- Calling `IdempotencyKeyEntity.SanitizeKey(...)` or constructing `IdempotencyKeyEntity` directly from an app or a new-provider's DI extension — **not reachable**; both are `internal` to this assembly plus its `InternalsVisibleTo` list. An app never touches this entity type at all.
- Overriding `IdempotencyKeyConfiguration.Configure(...)` to add extra columns or indexes — **not the intended seam**; only `BodyColumnType` and `StatusCodeCheckConstraintSql` are `protected abstract`.
- Skipping `AddHostedService<IdempotencyMigrationHostedService<TContext>>()` in a new provider's setup extension and assuming the per-request fallback is an equivalent replacement — it self-heals eventually but at the cost of a lock-held migration on the request path; treat the hosted service as the primary mechanism, not optional.
- Assuming `Expiration` (the core `IdempotencyOptions` for the *cached response* TTL) governs the `102` reservation window — it does not. Only `InFlightReservationTimeout` governs reservation lifetime here.

## Composes with

| Package | Rule |
|---|---|
| `DKNet.AspCore.Idempotency` | Always the actual dependency for an app — provides `IIdempotencyKeyStore`, `IdempotencyOptions`, `IdempotentKeyInfo`, `CachedResponse`, `AddIdempotentKey<TStore>`. This package only implements the store contract on top of EF Core. |
| `DKNet.AspCore.Idempotency.MsSqlStore` | The SQL Server provider built on this base; the worked example when the new database quotes identifiers with brackets. |
| `DKNet.AspCore.Idempotency.NpgsqlStore` | The PostgreSQL provider built on this base; the worked example when the new database reports errors by SQL state. |
| `DKNet.AspCore.Idempotency.RedisStore` | Reach for this instead when the target store is not relational at all — it implements `IIdempotencyKeyStore` directly and does not use this base. |
| `DKNet.Fw.Extensions` | Supplies `IsRegistered<TService>()`, the first-wins DI guard every provider's `Add...Store` extension uses before registering its `DbContext`. |

## Testing notes

There is no test project for this base package on its own — its behaviour is exercised entirely through the two provider test suites that construct `IdempotencyRelationalStore<TContext>` subclasses. If you are testing a *new* provider built on this base, the same shape applies to your own tests:

- Use `Testcontainers.PostgreSql` or the equivalent for your database — never EF Core InMemory, since the whole point of this base is real unique-constraint concurrency behaviour that InMemory does not enforce.
- Register a `DbCommandInterceptor` to assert the reserve path issues exactly one `INSERT` (not select-then-insert) and the complete path issues exactly one `UPDATE` — a regression in that shape silently reintroduces a race.
- Fire several concurrent `MarkKeyAsProcessedAsync`/`IsKeyProcessedAsync` calls for the same key and assert exactly one row survives and no unhandled `DbUpdateException` escapes.
- Build two separate `ServiceProvider`s against two distinct, never-migrated databases to prove the per-connection-string migration guard actually migrates *both*, not just the first one it saw.
- Wrap the real `IdempotencyMigrationHostedService<TContext>` in a spy `IHostedLifecycleService` to assert all six lifecycle moments fire in order, and assert the hosted-service type stays non-public.
