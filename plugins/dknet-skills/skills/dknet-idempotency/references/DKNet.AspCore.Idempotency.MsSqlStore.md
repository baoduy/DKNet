# DKNet.AspCore.Idempotency.MsSqlStore

| Field | Value |
|---|---|
| Area | AspNetCore |
| NuGet package | `DKNet.AspCore.Idempotency.MsSqlStore` — `dotnet add package DKNet.AspCore.Idempotency.MsSqlStore` |
| NuGet page | https://www.nuget.org/packages/DKNet.AspCore.Idempotency.MsSqlStore |
| Docs | https://github.com/baoduy/DKNet/blob/dev/docs/AspNetCore/DKNet.AspCore.Idempotency.MsSqlStore.md |
| Source | https://github.com/baoduy/DKNet/tree/dev/src/AspNet/DKNet.AspCore.Idempotency.MsSqlStore |
| Depends on (DKNet) | `DKNet.AspCore.Idempotency` (core: options, endpoint filter, `AddIdempotentKey<T>`), `DKNet.AspCore.Idempotency.Relational` (shared `IdempotencyRelationalStore<TContext>`, shared entity/config, migration hosted service) |
| Depends on (3rd party) | `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Design` (design-time only) |
| Target framework | `net10.0` |

## Purpose

A SQL-Server-backed, persistent implementation of `IIdempotencyKeyStore` for `DKNet.AspCore.Idempotency`. It stores idempotency keys and cached HTTP responses in a SQL Server `IdempotencyKeys` table via EF Core, using a unique index (`UX_CompositeKey`) to make the reserve-then-check flow race-free across every application instance sharing the database — not just within one process, which is the in-process default store's limit. One call, `AddIdempotencyWithMsSqlStore(connectionString, ...)`, registers the `DbContext`, an `IDbContextFactory<IdempotencyDbContext>`, a startup migration hosted service, and the store itself.

**Not** for: low-latency/high-throughput endpoints where a DB round trip per request is a bottleneck (reach for `DKNet.AspCore.Idempotency.RedisStore` instead); PostgreSQL deployments (use `DKNet.AspCore.Idempotency.NpgsqlStore`); and it never purges expired rows itself — that is left to the consumer.

## Entry points

| Call | Exact signature | Called on | Notes |
|---|---|---|---|
| `AddIdempotencyMsSqlStore` | `public static IServiceCollection AddIdempotencyMsSqlStore(this IServiceCollection services, string connectionString)` | `IServiceCollection` | Registers `IdempotencyDbContext` (scoped, `optionsLifetime: ServiceLifetime.Singleton`), `AddDbContextFactory<IdempotencyDbContext>`, and `AddHostedService<IdempotencyMigrationHostedService<IdempotencyDbContext>>`. Throws `ArgumentNullException` on a null `services` or a null `connectionString`; `ArgumentException` on an empty/whitespace-only `connectionString`. First-wins: no-op once an `IdempotencyDbContext` registration already exists. Registers **no** `IIdempotencyKeyStore`. |
| `AddIdempotencyWithMsSqlStore` | `public static IServiceCollection AddIdempotencyWithMsSqlStore(this IServiceCollection services, string connectionString, Action<IdempotencyOptions>? config = null)` | `IServiceCollection` | Calls `AddIdempotencyMsSqlStore(connectionString)` then `services.AddIdempotentKey<IdempotencySqlServerStore>(config)`. This is the supported, application-facing call. `IdempotencySqlServerStore` is `internal`, so it cannot be passed to `AddIdempotentKey<T>()` directly from application code. |
| `DbContextFactory.CreateDbContext` | `IDesignTimeDbContextFactory<IdempotencyDbContext>` | `dotnet ef` design-time tooling only | Reads `IDEMPOTENCY_MSSQL_CONNECTION` from the environment; throws `InvalidOperationException` if unset. The class itself is `internal` — only reachable via reflection-based `dotnet ef` discovery, never from app code. |

`IdempotencyMsSqlSetup` (the static class holding both extension methods) is the package's only type meant for application use.

## Public surface

### `DKNet.AspCore.Idempotency.MsSqlStore`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `IdempotencyMsSqlSetup` | static class | DI registration entry point | `AddIdempotencyMsSqlStore(IServiceCollection, string)`; `AddIdempotencyWithMsSqlStore(IServiceCollection, string, Action<IdempotencyOptions>?)` |

Everything else in the assembly is `internal`, listed here because it explains observable behaviour:

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `IdempotencySqlServerStore` (`Store/`) | `internal sealed class : IdempotencyRelationalStore<IdempotencyDbContext>` | SQL Server store; supplies only the provider-specific unique-violation check | `protected override bool IsProviderUniqueViolation(DbUpdateException ex)` → true for `SqlException.Number` 2601 or 2627 |
| `IdempotencyDbContext` (`Data/`) | `internal sealed class` | SQL-Server-closed `DbContext`, distinct from the shared base of the same short name (fully qualify to disambiguate) | Ctor `(DbContextOptions<IdempotencyDbContext> options)` — no members of its own |
| `DbContextFactory` (`Data/`) | `internal sealed class` | `IDesignTimeDbContextFactory<IdempotencyDbContext>` for `dotnet ef` | `CreateDbContext(string[] args)` |
| `IdempotencyKeyConfiguration` (`Data/Configurations/`) | `internal sealed class` | SQL Server overrides of the two provider-specific mapping points | `BodyColumnType => "nvarchar(max)"`; `StatusCodeCheckConstraintSql => "[StatusCode] BETWEEN 100 AND 599"` |
| `Initial` (`Migrations/`) | `public partial class : Migration` | The single shipped migration that creates `IdempotencyKeys` | Public only because EF Core's migration-scaffolding convention requires it; never called directly |

Consumer takeaway: the **only** type an application ever references directly is `IdempotencyMsSqlSetup`, via its two extension methods. Everything else is `internal` and reached only indirectly through DI.

## Options & defaults

There is no SQL-Server-specific options type. `AddIdempotencyWithMsSqlStore`'s `config` parameter configures the shared `IdempotencyOptions` from `DKNet.AspCore.Idempotency` — see that package's reference for the full 14-property table. Validated eagerly via `ValidateOnStart()`.

What SQL Server itself gets is fixed by this package, not exposed as options:

| Setting | Value |
|---|---|
| `EnableRetryOnFailure` | 3 retries, 5 seconds apart |
| `UseQuerySplittingBehavior` | `QuerySplittingBehavior.SplitQuery` |
| `MigrationsAssembly` | this package's own assembly |
| `MigrationsHistoryTable` | `[migrate].[IdempotencyDbContext]` |
| `optionsLifetime` | `ServiceLifetime.Singleton` (the `DbContext` itself stays scoped) |

## Usage patterns

### Register and protect an endpoint (minimal API)

**When**: standard app startup, protecting a POST endpoint with SQL-Server-backed idempotency.

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

app.MapPost("/orders", () => Results.Ok())
    .RequiredIdempotentKey();

await app.RunAsync();
```

**Notes**: `RequiredIdempotentKey()` requires the `IIdempotencyKeyStore` to already be registered; call `AddIdempotencyWithMsSqlStore` before `app.Build()`. `app.RunAsync()` (or `Run()`) must actually execute for the migration hosted service to fire at startup.

### Protect a route group instead of one endpoint

**When**: several endpoints under a prefix should all require the key for a given set of verbs.

```csharp
using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.MsSqlStore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddIdempotencyWithMsSqlStore(
    builder.Configuration.GetConnectionString("IdempotencyDb")!);

var app = builder.Build();

var orders = app.MapGroup("/api/orders").RequiredIdempotentKey();          // POST only (default)
orders.MapPost("/", () => Results.Created("/api/orders/1", new { id = 1 }));

var admin = app.MapGroup("/api/admin").RequiredIdempotentKey("POST", "DELETE");
admin.MapDelete("/{id}", (int id) => Results.NoContent());

await app.RunAsync();
```

**Notes**: coverage is decided at endpoint-build time from the routed verb, not from the live request; an endpoint mapped with `app.Map(...)` and no explicit verb is never covered.

### Register the `DbContext` only, without replacing the key store

**When**: you want `Database.MigrateAsync()` available from a startup job but want to keep the default in-memory store (e.g. local dev) or defer store selection.

```csharp
using DKNet.AspCore.Idempotency.MsSqlStore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddIdempotencyMsSqlStore(
    builder.Configuration.GetConnectionString("IdempotencyDb")!);
// No IIdempotencyKeyStore registered yet — RequiredIdempotentKey() has nothing to resolve
// until AddIdempotentKey<T>(...) (or AddIdempotencyWithMsSqlStore) is also called.
```

**Notes**: the migration hosted service is still registered by this call alone, so the schema is still created at startup even though the store is not wired in.

### Run migrations ahead of time via `dotnet ef`

**When**: a controlled production rollout, applying the schema before traffic arrives instead of relying on the automatic startup migration.

```bash
export IDEMPOTENCY_MSSQL_CONNECTION="Server=.;Database=IdempotencyDb;TrustServerCertificate=True;"
dotnet ef database update --context IdempotencyDbContext \
    --project <path-to-a-checkout-of-DKNet.AspCore.Idempotency.MsSqlStore>
```

**Notes**: `DbContextFactory` (the `IDesignTimeDbContextFactory<IdempotencyDbContext>`) reads `IDEMPOTENCY_MSSQL_CONNECTION` — not app configuration — and throws `InvalidOperationException` if the variable is unset. Running `dotnet ef` against a package you only reference via NuGet requires the package's source; most applications instead let the startup migration hosted service apply the schema and skip this step entirely.

### Query the ledger for auditing

**When**: inspecting which keys were processed, e.g. from an admin tool or a cleanup job — the one advantage a SQL store has over Redis.

```csharp
using Microsoft.EntityFrameworkCore;
// IdempotencyDbContext and IdempotencyKeyEntity are internal to this package, so this
// pattern is only reachable from inside the package's own assembly (e.g. its tests via
// InternalsVisibleTo). An external application queries the IdempotencyKeys table
// directly with its own SQL client or a minimal separate EF model instead.
```

**Notes**: `IdempotencyDbContext`, `IdempotencyKeyEntity` and the store type are all `internal`. An application cannot query the entity through this package's own DbContext type; query the `IdempotencyKeys` table directly (raw SQL / Dapper / a minimal separate EF model) if programmatic access is needed.

## Runtime behaviour

1. **Startup**: `AddIdempotencyMsSqlStore` registers the `DbContext`, factory, and migration hosted service. When the host actually runs hosted services (normal `WebApplication.RunAsync()`/`Run()`), the migration hosted service creates a short-lived context, checks for pending migrations, and applies them once — before the host begins serving requests, and before any endpoint filter can execute.
2. **A request hits a `RequiredIdempotentKey()`-protected endpoint**: the core package's endpoint filter validates the header presence/pattern/length, computes the `IdempotentKeyInfo`, then calls `IIdempotencyKeyStore.IsKeyProcessedAsync`, resolved here to `IdempotencySqlServerStore`.
3. **`IsKeyProcessedAsync`**: hashes the composite key (SHA-256 hex), gets a `TContext` from the factory, runs the defensive migration-guard fallback (a no-op once the startup migration already ran), then reserves.
4. **Reserve**: inserts a placeholder row with `StatusCode = 102` and `ExpiresAt = now + InFlightReservationTimeout`. Insert succeeds → `(false, null)`, caller proceeds. Insert throws and `SqlException.Number` is 2601/2627 → another caller already holds `UX_CompositeKey`: re-reads the blocking row; unexpired → `(true, cachedResponse-or-null)`; expired → reclaims atomically and either proceeds as new or re-reads and branches the same way.
5. **Handler runs** (only for the winning caller), then `MarkKeyAsProcessedAsync` flips `StatusCode`/`Body`/`ContentType`/`ExpiresAt` on the reserved row — no read-before-write. If zero rows matched (defensive-only path), it falls back to inserting a fresh row.
6. **A concurrent duplicate** during the in-flight window gets `409` (`ConflictResponse`, default) or the replayed cached response (`CachedResult`) — this package supplies only the storage, not that branching logic.

## Diagnostics & exceptions

| Type | Severity | When | Fix |
|---|---|---|---|
| `ArgumentNullException` | Error | `AddIdempotencyMsSqlStore(null, ...)` — null `services`; also thrown (not `ArgumentException`) for a null `connectionString` | Pass a real `IServiceCollection` and a non-null connection string |
| `ArgumentException` | Error | `connectionString` is empty or whitespace-only (not null — see row above) | Supply a real SQL Server connection string |
| `InvalidOperationException` | Error | `DbContextFactory.CreateDbContext` runs (via `dotnet ef`) with `IDEMPOTENCY_MSSQL_CONNECTION` unset | `export IDEMPOTENCY_MSSQL_CONNECTION=...` before running `dotnet ef` |
| `OptionsValidationException` | Error, at startup (`ValidateOnStart()`, on the first `IOptions<IdempotencyOptions>.Value` resolve) | Any `IdempotencyOptions` invariant fails | Fix the offending option in the `config` delegate passed to `AddIdempotencyWithMsSqlStore` |
| `DbUpdateException` (`CK_StatusCode_Valid` violation) | Runtime | A cached response's `StatusCode` falls outside `100`–`599` | Should not occur through normal use; indicates a caller bypassing the filter's own validation |
| `DbUpdateException` + `SqlException.Number` 2601/2627 | Handled internally, not surfaced | Concurrent reservation collision on `UX_CompositeKey` | Expected control flow, not a bug |

No analyzer `DiagnosticDescriptor`s exist in this package.

## Gotchas

- **`IdempotencySqlServerStore` is `internal`.** `services.AddIdempotentKey<IdempotencySqlServerStore>()` does not compile in application code. Always call `AddIdempotencyWithMsSqlStore(...)` instead.
- **`AddIdempotencyMsSqlStore` alone registers no `IIdempotencyKeyStore`.** `RequiredIdempotentKey()` has nothing useful to resolve. Use `AddIdempotencyWithMsSqlStore` unless you deliberately want the context without the store.
- **Both registration methods are first-wins.** Calling either twice with two different connection strings silently keeps the *first* one — no error, no warning. Register idempotency exactly once per `IServiceCollection`.
- **Migrations run automatically at startup with no opt-out flag.** A host that skips or reorders hosted services falls back to the relational base's per-request migration guard, which takes a process-wide lock on first use — every concurrent request behind that lock. Ensure normal `WebApplication.RunAsync()`/`Run()` semantics, or pre-migrate via `dotnet ef database update`.
- **`SanitizeKey` hashes the key with SHA-256; it does not strip characters.** The `CompositeKey` column never contains the raw key — don't expect to `LIKE`-query it for the original value. Query by `IdempotentKey` (the raw column, still stored) when auditing.
- **Nothing purges expired rows.** `IdempotencyKeys` grows unbounded for keys that are only ever tried once. Add your own scheduled cleanup querying the indexed `ExpiresAt` column.
- **`Body` stores the serialized response verbatim, up to `nvarchar(max)` / 1,048,576 chars.** PII or secrets in a cached response body land in the database with no separate retention policy. Treat `IdempotencyKeys.Body` like any other data-at-rest containing response payloads.
- **`DbContextFactory` reads its connection string from `IDEMPOTENCY_MSSQL_CONNECTION`, not from `IConfiguration`/`appsettings.json`.** `dotnet ef` commands fail with `InvalidOperationException` unless that specific env var is exported first, even if the app's own connection string is configured correctly elsewhere.
- **The reservation sentinel status code is `102`** — legal under `CK_StatusCode_Valid` (100–599) but outside any real completed response's range. A query that filters `IdempotencyKeys` by "processed" should exclude `StatusCode = 102`, not just check row existence.
- **Two same-named `IdempotencyDbContext` classes exist** — one in this package's `Data` namespace (SQL-Server-closed, `internal sealed`), one in the shared `Relational.Data` namespace (abstract base). Only relevant inside the repo, but explains an ambiguous-reference error if both namespaces are `using`-imported in the same file.

## Anti-patterns & hallucination traps

- **Does not exist**: an `IdempotencyMsSqlOptions` / `SqlServerIdempotencyOptions` type. Everything is configured through the core `IdempotencyOptions` via the `config` delegate on `AddIdempotencyWithMsSqlStore`.
- **Does not exist**: `services.AddIdempotencyMsSqlStore(connectionString, config)` — that overload takes only `(services, connectionString)`; the `config` parameter belongs to `AddIdempotencyWithMsSqlStore`.
- `options.FailOpen = false` — **does not exist** on `IdempotencyOptions`. (It appears in this package's own shipped XML `<example>` doc comment for `AddIdempotencyWithMsSqlStore` — a stale example baked into the source itself. Copying it verbatim is a compile error.)
- Do not call `new IdempotencySqlServerStore(...)` or reference `DKNet.AspCore.Idempotency.MsSqlStore.Store.IdempotencySqlServerStore` from application code — it is `internal`.
- Do not hand-write a `DbContext` migration for `IdempotencyDbContext` in your own application assembly — migrations ship inside this package's own assembly; adding your own would target the wrong migrations-history table (`[migrate].[IdempotencyDbContext]` is provider-owned).
- Do not call `Database.MigrateAsync()` on the request path yourself expecting to skip the startup hosted service — migration already runs automatically; a manual call would either be redundant or race with the hosted service on first boot.
- `IdempotencyMsSqlSetup` lives directly under `DKNet.AspCore.Idempotency.MsSqlStore` (the package's project-root namespace), not under a `.Extensions` or `.Setup` sub-namespace.
- Assuming `AddIdempotencyMsSqlStore` throws or logs a warning on a second call with a different connection string — it does neither, it is a silent no-op.

## Composes with

| Package | Rule |
|---|---|
| `DKNet.AspCore.Idempotency` | Always required — provides `IdempotencyOptions`, the endpoint filter, and `AddIdempotentKey<T>`/`RequiredIdempotentKey()`. Read its reference for the request-side (header/scope/conflict) behaviour this store does not implement. |
| `DKNet.AspCore.Idempotency.Relational` | The shared base this package derives from. Read it only when adding a *new* relational provider, not when consuming this one. |
| `DKNet.AspCore.Idempotency.NpgsqlStore` | Same store shape for PostgreSQL — reach for it instead of this package when the database is Postgres, not SQL Server. |
| `DKNet.AspCore.Idempotency.RedisStore` | Reach for it instead when the lowest latency and no owned schema/migrations matter more than a queryable audit table. |

## Testing notes

- SQL Server only runs on x64 and Apple Silicon: `mssql/server` ships x64-only images. On any other ARM64 host, exclude tests against this store locally (`dotnet test --filter "FullyQualifiedName!~MsSqlStore"`) and re-validate through CI on an x64 runner — see the `dknet-testing` skill for the exact commands.
- Use `Testcontainers.MsSql` (falling back to `azure-sql-edge` on ARM64/Apple Silicon) behind a `WebApplicationFactory`, provisioning a fresh per-test database, rather than mocking the `DbContext` or the store.
- Fetch the `IdempotencyDbContext` from the fixture's DI container and query `IdempotencyKeys` directly (`CountAsync`/`FirstOrDefaultAsync`) to assert on persisted rows, not only on HTTP responses — that catches a reservation left at `StatusCode = 102` that an HTTP-only assertion would miss.
- To pin the concurrency guarantee, fire several parallel requests with the same idempotency key against a real container and assert exactly one success plus the rest as conflicts (or identical cached replays under `CachedResult`) — SQLite cannot substitute here once the unique-violation check classifies by `SqlException.Number`, so this path needs a real SQL Server/`azure-sql-edge` container.
