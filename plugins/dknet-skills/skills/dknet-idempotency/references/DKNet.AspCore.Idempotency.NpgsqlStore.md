# DKNet.AspCore.Idempotency.NpgsqlStore

| Field | Value |
|---|---|
| Area | AspNetCore |
| NuGet package | `DKNet.AspCore.Idempotency.NpgsqlStore` — `dotnet add package DKNet.AspCore.Idempotency.NpgsqlStore` |
| NuGet page | https://www.nuget.org/packages/DKNet.AspCore.Idempotency.NpgsqlStore |
| Docs | https://github.com/baoduy/DKNet/blob/dev/docs/AspNetCore/DKNet.AspCore.Idempotency.NpgsqlStore.md |
| Source | https://github.com/baoduy/DKNet/tree/dev/src/AspNet/DKNet.AspCore.Idempotency.NpgsqlStore |
| Depends on (DKNet) | `DKNet.AspCore.Idempotency`, `DKNet.AspCore.Idempotency.Relational` |
| Depends on (3rd party) | `Microsoft.EntityFrameworkCore.Design` (build-time only), `Microsoft.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL` |
| Target framework | `net10.0` |

## Purpose

Persistent, PostgreSQL-backed implementation of `IIdempotencyKeyStore` for `DKNet.AspCore.Idempotency`. It registers an EF Core `DbContext` closed over Npgsql, ships an `Initial` migration that creates the `IdempotencyKeys` table with a unique index on `CompositeKey`, and supplies `IdempotencyPostgresStore` — whose only job is telling the shared `IdempotencyRelationalStore<TContext>` base how PostgreSQL reports a unique-key violation (`PostgresException.SqlState == 23505`). All reserve/check/complete logic, the expired-reservation reclaim, and the migration guard live in the Relational base package, not here.

**Not**: a low-latency store (every protected request costs a DB round-trip — use `DKNet.AspCore.Idempotency.RedisStore` for that), a store that purges its own expired rows (nothing sweeps `ExpiresAt`), or something you register standalone — it plugs into `DKNet.AspCore.Idempotency`'s endpoint filter and does nothing without it.

## Entry points

| Call | Exact signature | Called on | Notes |
|---|---|---|---|
| `AddIdempotencyNpgsqlStore` | `public static IServiceCollection AddIdempotencyNpgsqlStore(this IServiceCollection services, string connectionString)` | `IServiceCollection` | Registers `IdempotencyDbContext` (scoped, `optionsLifetime: ServiceLifetime.Singleton`), `IDbContextFactory<IdempotencyDbContext>`, and `IdempotencyMigrationHostedService<IdempotencyDbContext>`. Does **not** register `IIdempotencyKeyStore`. First-wins: no-ops once `IdempotencyDbContext` is registered. Throws `ArgumentNullException` (null `services`) / `ArgumentException` (null/empty/whitespace `connectionString`). |
| `AddIdempotencyWithNpgsqlStore` | `public static IServiceCollection AddIdempotencyWithNpgsqlStore(this IServiceCollection services, string connectionString, Action<IdempotencyOptions>? config = null)` | `IServiceCollection` | Calls `AddIdempotencyNpgsqlStore` then `AddIdempotentKey<IdempotencyPostgresStore>(config)`. This is the call an application makes; it wires the store into `IIdempotencyKeyStore` and validates `IdempotencyOptions` eagerly with `ValidateOnStart()`. Call before any `RequiredIdempotentKey()` mapping. |
| `DbContextFactory.CreateDbContext` | `internal sealed class : IDesignTimeDbContextFactory<IdempotencyDbContext>` | `dotnet ef` tooling only | Reads `IDEMPOTENCY_NPGSQL_CONNECTION` from the **environment**, not app config; throws `InvalidOperationException` if unset. Not something app code calls. |

## Public surface

### `DKNet.AspCore.Idempotency.NpgsqlStore`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `IdempotencyNpgsqlSetup` | static class | The package's only application-facing type. | `AddIdempotencyNpgsqlStore(IServiceCollection, string)`; `AddIdempotencyWithNpgsqlStore(IServiceCollection, string, Action<IdempotencyOptions>? = null)` |

Everything else in the assembly is `internal`, listed here because it explains observable behaviour:

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `IdempotencyPostgresStore` (`Store/`) | `internal sealed class : IdempotencyRelationalStore<IdempotencyDbContext>` | PostgreSQL's unique-violation detector; every other behaviour is inherited. | `protected override bool IsProviderUniqueViolation(DbUpdateException ex)` → `ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }` |
| `IdempotencyDbContext` (`Data/`) | `internal sealed class : Relational.Data.IdempotencyDbContext` | Gives PostgreSQL its own closed `DbContextOptions<IdempotencyDbContext>` and migrations assembly; no extra entity mapping. | Ctor `(DbContextOptions<IdempotencyDbContext> options)` |
| `DbContextFactory` (`Data/`) | `internal sealed class : IDesignTimeDbContextFactory<IdempotencyDbContext>` | `dotnet ef` design-time factory only. | `CreateDbContext(string[] args)` |
| `IdempotencyKeyConfiguration` (`Data/Configurations/`) | `internal sealed class : Relational.Data.Configurations.IdempotencyKeyConfiguration` | Supplies the two Postgres-specific mapping overrides; picked up automatically by the Relational base's `OnModelCreating`. | `BodyColumnType => "text"`; `StatusCodeCheckConstraintSql => "\"StatusCode\" BETWEEN 100 AND 599"` |
| `Migrations.Initial` | `public partial class : Migration` | EF Core-generated migration; technically public (codegen convention), never called directly by app code. | `Up(MigrationBuilder)`, `Down(MigrationBuilder)` |

No PostgreSQL-specific options type exists — the contract types this store accepts (`IdempotentKeyInfo`, `CachedResponse`, `IIdempotencyKeyStore`) all live in the core `DKNet.AspCore.Idempotency` package.

## Options & defaults

`AddIdempotencyWithNpgsqlStore`'s `config` parameter configures the shared `DKNet.AspCore.Idempotency.IdempotencyOptions` — see that package's reference for the full 14-property table.

What this package fixes internally, not exposed as options:

| Setting | Value | Effect |
|---|---|---|
| `EnableRetryOnFailure` | 3 retries, 5 seconds apart | Transient-fault resilience on idempotency queries |
| `UseQuerySplittingBehavior` | `QuerySplittingBehavior.SplitQuery` | EF Core split-query mode for this context |
| `MigrationsAssembly` | this package's own assembly | Migrations ship with the store, not the app |
| `MigrationsHistoryTable` | `"IdempotencyDbContext"` in schema `"migrate"` | Separate from the app's own `__EFMigrationsHistory` |
| `optionsLifetime` | `ServiceLifetime.Singleton` | Options are shared; the `DbContext` itself stays scoped |

## Usage patterns

### Wire up idempotency backed by PostgreSQL (the supported entry point)

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

app.MapPost("/orders", () => Results.Ok())
    .RequiredIdempotentKey();

await app.RunAsync();
```

**Notes**: `AddIdempotencyWithNpgsqlStore` must run before any `RequiredIdempotentKey()` mapping. Migrations apply automatically once, at startup, via the registered migration hosted service — no separate migration step.

### Register only the `DbContext` (e.g. to run migrations from a start-up job, without replacing the key store)

```csharp
using DKNet.AspCore.Idempotency.NpgsqlStore;
using Microsoft.Extensions.DependencyInjection;

var connectionString = "Host=localhost;Database=idempotency;Username=postgres;Password=postgres";
var services = new ServiceCollection();
services.AddIdempotencyNpgsqlStore(connectionString);
// No IIdempotencyKeyStore registered yet - RequiredIdempotentKey() cannot resolve its dependency
// until AddIdempotentKey<TStore>(...) (or AddIdempotencyWithNpgsqlStore) also runs.
```

**Notes**: use this only when a separate call will register the key store (or won't — e.g. a worker process that just needs `IDbContextFactory<IdempotencyDbContext>` for schema tooling). Calling it twice with different connection strings is silently a no-op after the first call.

### Multi-tenant: one store per Postgres database in the same process

```csharp
using DKNet.AspCore.Idempotency.NpgsqlStore;
using Microsoft.Extensions.DependencyInjection;

// Each tenant database gets its own DI container/ServiceProvider (not the same IServiceCollection,
// since first-wins registration would otherwise ignore the second connection string).
var servicesTenantA = new ServiceCollection();
servicesTenantA.AddLogging();
servicesTenantA.AddIdempotencyWithNpgsqlStore("Host=db;Database=tenant_a;Username=app;Password=secret");
var providerA = servicesTenantA.BuildServiceProvider(); // kept for tenant A's requests; disposed with the app

var servicesTenantB = new ServiceCollection();
servicesTenantB.AddLogging();
servicesTenantB.AddIdempotencyWithNpgsqlStore("Host=db;Database=tenant_b;Username=app;Password=secret");
var providerB = servicesTenantB.BuildServiceProvider(); // kept for tenant B's requests; disposed with the app
```

**Notes**: the "migrations ensured" guard in the Relational base is keyed per connection string, not a single process-wide flag, so each database is prepared independently. Within *one* `IServiceCollection`, a second `AddIdempotencyWithNpgsqlStore` call with a different connection string is a no-op — this pattern requires a separate container per tenant database.

### `dotnet ef` design-time migration commands

```bash
export IDEMPOTENCY_NPGSQL_CONNECTION="Host=localhost;Database=idempotency;Username=postgres;Password=postgres"
dotnet ef migrations add MyChange \
  --project <path-to-a-checkout-of-DKNet.AspCore.Idempotency.NpgsqlStore> \
  --context DKNet.AspCore.Idempotency.NpgsqlStore.Data.IdempotencyDbContext
```

**Notes**: `DbContextFactory` throws `InvalidOperationException` if `IDEMPOTENCY_NPGSQL_CONNECTION` is unset — it reads only the environment variable, never app configuration. Most applications never run this: the shipped `Initial` migration plus the automatic startup hosted service is enough.

## Runtime behaviour

1. At application startup, the migration hosted service creates a short-lived `IdempotencyDbContext`, checks for pending migrations, and applies them if any are pending — before the host starts serving requests.
2. On a protected request, the core package's endpoint filter resolves `IIdempotencyKeyStore` (here, `IdempotencyPostgresStore`) and calls `IsKeyProcessedAsync(keyInfo)`.
3. The Relational base hashes the composite key (SHA-256, uppercase hex), calls the defensive migration-guard fallback (a no-op once step 1 already migrated this connection string), then reserves.
4. **Reserve** inserts a placeholder row with `StatusCode = 102` and `ExpiresAt = now + InFlightReservationTimeout`. Insert succeeds → caller proceeds to run the protected handler. Insert throws and `PostgresException.SqlState == "23505"` → a concurrent/prior request already holds the key: an unexpired blocking row returns `(true, cachedResponseOrNull)`; an expired blocking row is reclaimed atomically, and only the caller whose update affects 1 row proceeds as a fresh reservation.
5. After the handler runs, `MarkKeyAsProcessedAsync` overwrites the reservation row with the real response. If no row matched (called without a prior reservation), it falls back to insert, and swallows a resulting unique-violation defensively without overwriting the racing winner's row.

## Diagnostics & exceptions

| Type | Severity | When | Fix |
|---|---|---|---|
| `ArgumentNullException` | Error | `AddIdempotencyNpgsqlStore`/`AddIdempotencyWithNpgsqlStore` called with `services == null` | Pass a real `IServiceCollection` |
| `ArgumentException` | Error | `connectionString` is null, empty, or whitespace | Supply a real Npgsql connection string |
| `InvalidOperationException` | Error | `DbContextFactory.CreateDbContext` (design-time `dotnet ef`) runs with `IDEMPOTENCY_NPGSQL_CONNECTION` unset | Export that environment variable before running EF Core tooling |
| `DbUpdateException` wrapping `PostgresException { SqlState: "23505" }` | Handled internally | Two callers race the same `CompositeKey` | Not caller-visible — the Relational base catches and resolves it; do not add your own catch around the store |
| `OptionsValidationException` | Error, at startup (`ValidateOnStart()`) | Invalid `IdempotencyOptions` passed to `config` (e.g. non-positive `Expiration`, empty `IdempotencyHeaderKey`) | Fix the `config` delegate; see the core package's reference for the full validator list |

## Gotchas

- **`AddIdempotencyNpgsqlStore` alone registers no `IIdempotencyKeyStore`.** `RequiredIdempotentKey()` filters have nothing to resolve. Use `AddIdempotencyWithNpgsqlStore` unless you deliberately want only the `DbContext`.
- **Both registration methods are first-wins per `IServiceCollection`** — a second call, even with a *different* connection string, is silently a no-op once `IdempotencyDbContext` (or any `IIdempotencyKeyStore`) is registered. Register once per container; use a separate `IServiceCollection`/provider per connection string for multi-database scenarios.
- **`IdempotencyPostgresStore` is `internal`.** `AddIdempotentKey<IdempotencyPostgresStore>()` does not compile from application code. Use `AddIdempotencyWithNpgsqlStore(...)`, the only supported entry point that wires this store in.
- **Migrations run automatically at startup with no opt-out**, via the migration hosted service. A host that skips or reorders hosted services falls back to the Relational base's per-request guard, so the *first request* against a fresh database pays the migration cost (and holds a lock while doing so).
- **`MarkKeyAsProcessedAsync` swallows a unique violation when called without a prior reservation** (e.g. a retried background job calling it directly). It will not throw, but also will not overwrite whichever caller's row won — silent data loss for *that* caller's response body, not a crash. Always route through `IsKeyProcessedAsync` first (the endpoint filter already does).
- **Nothing purges expired rows** — `ExpiresAt` is indexed but only reclaimed opportunistically when a *new* request collides with an expired row. `IdempotencyKeys` grows unboundedly for keys nobody ever retries. Add your own scheduled cleanup job.
- **`DbContextFactory` (the `dotnet ef` design-time factory) reads `IDEMPOTENCY_NPGSQL_CONNECTION` from the process environment, never from `appsettings.json`/`IConfiguration`.** Running `dotnet ef migrations add` without exporting that variable throws `InvalidOperationException` even if the app's own config has a perfectly good connection string.
- **`Body` is mapped as PostgreSQL `text` (max 1,048,576 chars) and holds the serialized response verbatim**, including anything sensitive in a cached 2xx body. Keep response bodies on protected endpoints clean of secrets, or shorten `Expiration`.

## Anti-patterns & hallucination traps

- `services.AddIdempotentKey<IdempotencyPostgresStore>(...)` written directly in application code — does not compile; `IdempotencyPostgresStore` is `internal`. Use `AddIdempotencyWithNpgsqlStore(...)` instead.
- `IdempotencyNpgsqlOptions` / any Postgres-specific options class — does **not exist**. All configuration goes through the shared `DKNet.AspCore.Idempotency.IdempotencyOptions` via the `config` delegate.
- `options.FailOpen = false` — **does not exist** on `IdempotencyOptions`. (It appears in this package's own shipped XML `<example>` doc comment for `AddIdempotencyWithNpgsqlStore` — a stale example baked into the source itself.)
- Calling `AddIdempotencyNpgsqlStore(...)` expecting it to also register the key store — it only registers the `DbContext` + factory + migration hosted service; use `AddIdempotencyWithNpgsqlStore` for the full wire-up.
- Hand-writing an `IdempotencyDbContext` migration or calling `dbContext.Database.EnsureCreated()` — migrations ship with the package and are applied automatically; don't bypass them with `EnsureCreated`, which does not use the migrations history table this package relies on.
- Assuming a `MigrationsHistoryTable` named `__EFMigrationsHistory` — it is `migrate.IdempotencyDbContext` (schema `migrate`), separate from the app's own migrations history, by design.
- Catching `PostgresException`/`DbUpdateException` around calls to `IIdempotencyKeyStore` methods expecting to need custom unique-violation handling — the store already resolves this internally; an app-level catch is unnecessary and risks swallowing a real error.
- Registering this store per-request or per-tenant inside a single shared `IServiceCollection` by calling `AddIdempotencyWithNpgsqlStore` multiple times with different connection strings — first-wins silently ignores every call after the first; use one `IServiceCollection`/provider per tenant database instead.

## Composes with

| Package | Rule |
|---|---|
| `DKNet.AspCore.Idempotency` | Always required — supplies `IdempotencyOptions`, `IIdempotencyKeyStore`, the endpoint filter, and `RequiredIdempotentKey()`. This package only supplies the storage implementation. |
| `DKNet.AspCore.Idempotency.Relational` | Always required — supplies the shared reserve/check/complete logic and migration hosted service this package derives from. |
| `DKNet.AspCore.Idempotency.MsSqlStore` | Reach for it instead when the target database is SQL Server, not PostgreSQL — same store shape, same Relational base. |
| `DKNet.AspCore.Idempotency.RedisStore` | Reach for it instead when you want the lowest latency and no schema/migrations to own; this store trades that for durability, auditability, and multi-instance atomicity without adding Redis to the stack. |

## Testing notes

- Use `Testcontainers.PostgreSql` (`postgres:16-alpine`) for every integration test against this store — never EF Core InMemory or a mock `DbContext`. Postgres containers run natively on ARM64, unlike SQL Server's x64-only image, so this store's tests need no ARM64 exclusion.
- A `WebApplicationFactory` + `IAsyncLifetime` fixture that starts a fresh container per fixture instance, builds a per-run isolated database name, and calls `AddIdempotencyWithNpgsqlStore` inside `ConfigureWebHost` is the pattern to copy.
- Assert DI registration shape (service descriptors — `ServiceType`, `ImplementationType`) rather than resolving the container when testing setup extensions, to keep those tests free of any live PostgreSQL dependency.
- Fire several concurrent HTTP requests with the same idempotency key and assert the handler ran exactly once (e.g. same generated `Id` across every `201` response) rather than asserting an exact status-code split, since a slow container can shift how many callers land in the true collision window versus the reservation-already-cleared window.
- Build two separate `ServiceProvider`s against two databases on the same Postgres instance to prove the per-connection-string migration guard, not a single process-wide flag.
