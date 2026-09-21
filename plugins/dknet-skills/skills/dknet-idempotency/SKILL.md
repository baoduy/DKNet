---
name: dknet-idempotency
description: Covers DKNet.AspCore.Idempotency (the RequiredIdempotentKey endpoint filter on RouteHandlerBuilder/RouteGroupBuilder, AddIdempotentKey, IdempotencyOptions, IdempotentConflictHandling, key scope resolution) plus its store packages DKNet.AspCore.Idempotency.Relational (internal EF Core base, never referenced by apps), DKNet.AspCore.Idempotency.MsSqlStore (AddIdempotencyWithMsSqlStore), DKNet.AspCore.Idempotency.NpgsqlStore (AddIdempotencyWithNpgsqlStore), and DKNet.AspCore.Idempotency.RedisStore (AddIdempotencyWithRedisStore). Use for an ASP.NET Core minimal API POST/PUT/DELETE endpoint that must be retry-safe with an Idempotency-Key header, turning a duplicate request into a 409 or a cached replay, choosing a store, or debugging a 400 key-validation failure, a 409 conflict, an OptionsValidationException at startup, or the 102 in-flight sentinel.
license: MIT
metadata:
  author: baoduy
  version: "0.1.0"
  packages: "DKNet.AspCore.Idempotency, DKNet.AspCore.Idempotency.Relational, DKNet.AspCore.Idempotency.MsSqlStore, DKNet.AspCore.Idempotency.NpgsqlStore, DKNet.AspCore.Idempotency.RedisStore"
---

# DKNet idempotent endpoints and key stores

This skill teaches the `DKNet.AspCore.Idempotency` endpoint filter for ASP.NET Core minimal APIs and its four store packages. Read `references/DKNet.AspCore.Idempotency.md` first — it owns every cross-cutting concept (options, scope resolution, conflict handling); each store's own reference file covers only what differs for that database. `references/diagnostics.md` is a single lookup table for every exception/status code this feature can produce.

## Packages

| Package | Install | What it gives you | Depends on | Reference |
|---|---|---|---|---|
| `DKNet.AspCore.Idempotency` | `dotnet add package DKNet.AspCore.Idempotency` | `RequiredIdempotentKey()` endpoint filter, `AddIdempotentKey()`/`AddIdempotentKey<TStore>()`, `IdempotencyOptions`, `IdempotentConflictHandling`, the in-process dev-only store | `DKNet.Fw.Extensions` | [references/DKNet.AspCore.Idempotency.md](references/DKNet.AspCore.Idempotency.md) |
| `DKNet.AspCore.Idempotency.Relational` | *(never add directly — see Rules)* | Shared EF Core reserve/check/complete algorithm behind MsSqlStore and NpgsqlStore | `DKNet.AspCore.Idempotency`, EF Core | [references/DKNet.AspCore.Idempotency.Relational.md](references/DKNet.AspCore.Idempotency.Relational.md) |
| `DKNet.AspCore.Idempotency.MsSqlStore` | `dotnet add package DKNet.AspCore.Idempotency.MsSqlStore` | `AddIdempotencyWithMsSqlStore(connectionString, config?)` — SQL Server-backed store, auto-migrated at startup | `DKNet.AspCore.Idempotency`, `.Relational`, EF Core SqlServer | [references/DKNet.AspCore.Idempotency.MsSqlStore.md](references/DKNet.AspCore.Idempotency.MsSqlStore.md) |
| `DKNet.AspCore.Idempotency.NpgsqlStore` | `dotnet add package DKNet.AspCore.Idempotency.NpgsqlStore` | `AddIdempotencyWithNpgsqlStore(connectionString, config?)` — PostgreSQL-backed store, auto-migrated at startup | `DKNet.AspCore.Idempotency`, `.Relational`, Npgsql EF Core | [references/DKNet.AspCore.Idempotency.NpgsqlStore.md](references/DKNet.AspCore.Idempotency.NpgsqlStore.md) |
| `DKNet.AspCore.Idempotency.RedisStore` | `dotnet add package DKNet.AspCore.Idempotency.RedisStore` | `AddIdempotencyWithRedisStore(connectionString, config?)` — Redis-backed store, `SET NX` + native TTLs, no schema | `DKNet.AspCore.Idempotency`, StackExchange.Redis | [references/DKNet.AspCore.Idempotency.RedisStore.md](references/DKNet.AspCore.Idempotency.RedisStore.md) |

## Quick start

```csharp
using DKNet.AspCore.Idempotency;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddIdempotentKey();

var app = builder.Build();

app.MapPost("/orders", () => TypedResults.Created("/orders/1"))
    .RequiredIdempotentKey();

await app.RunAsync();
```

This is the whole wiring: register a store (`AddIdempotentKey()` here selects the process-local, dev-only default), then opt an endpoint in with `RequiredIdempotentKey()`. A client resends the same `X-Idempotency-Key` header on a retry and gets `409` instead of a second order. Swap `AddIdempotentKey()` for `AddIdempotencyWithMsSqlStore(...)`/`AddIdempotencyWithNpgsqlStore(...)`/`AddIdempotencyWithRedisStore(...)` for production — see **How to** below.

## Rules

1. Register idempotency (`AddIdempotentKey()` or a store's `AddIdempotencyWithXxxStore(...)`) before `app.Build()` and before mapping any `RequiredIdempotentKey()` endpoint — the filter resolves `IIdempotencyKeyStore`/`IOptions<IdempotencyOptions>` from DI at request time, but registration order still decides which store, and which call's `config`, wins.
2. Never run the in-process default store (`AddIdempotentKey()` with no type argument) outside local dev/tests. It is a `ConcurrentDictionary`, process-local, lost on restart, and logs a startup warning while active. Use a `MsSqlStore`/`NpgsqlStore`/`RedisStore` package in production.
3. `RequiredIdempotentKey()` extends `RouteHandlerBuilder`/`RouteGroupBuilder` only — minimal APIs, not MVC controllers. There is no `[Idempotent]` attribute anywhere in this package family.
4. A group's `RequiredIdempotentKey()` with no verb argument covers `POST` only; name every other verb explicitly (`RequiredIdempotentKey("POST", "DELETE")`). An endpoint mapped with `app.Map(...)` and no routed verb is never covered, in a group or alone, and there is no warning when a mutating verb falls outside the covered set.
5. Use the `With` overload — `AddIdempotencyWithMsSqlStore`/`AddIdempotencyWithNpgsqlStore`/`AddIdempotencyWithRedisStore` — to get a working store. The plain `AddIdempotencyMsSqlStore`/`AddIdempotencyNpgsqlStore`/`AddIdempotencyRedisStore` overload registers only infrastructure (DbContext/migration service, or the Redis connection); it never registers `IIdempotencyKeyStore` itself.
6. Registration is first-wins per `IServiceCollection`: a second call to the same or a different store's `Add...`, even with a different connection string, is a silent no-op — no exception, no log. Register idempotency exactly once per container.
7. Never construct a shipped store type directly (`new IdempotencyInMemoryStore()`, `new IdempotencySqlServerStore(...)`, `new IdempotencyPostgresStore(...)`, `new IdempotencyRedisStore(...)`) or pass one to `AddIdempotentKey<T>()` from application code — every shipped store is `internal`. Select a store only through its package's `Add...` extension.
8. Never add `DKNet.AspCore.Idempotency.Relational` to an application directly. It has no public API and no `Add...` method — it exists only so `MsSqlStore`/`NpgsqlStore` can share the reserve/check/complete algorithm.
9. Default conflict handling is `IdempotentConflictHandling.ConflictResponse` → `409`. Set `ConflictHandling = IdempotentConflictHandling.CachedResult` to replay the original cached status/body/content-type instead of a bare conflict.
10. Idempotency keys are attacker-controlled. Never log or persist `IdempotentKey` or `IdempotentKeyInfo.SafeKey` as a lookup value — only the hashed `CompositeKey` is ever stored, and `SafeKey` exists solely for safe display/logging.
11. `IsKeyProcessedAsync` is never wrapped in try/catch by the filter — a store outage on the duplicate check surfaces as an unhandled `500`, asymmetric with `MarkKeyAsProcessedAsync` failures (caught, logged, response still returned). Harden the store, not the filter.
12. No shipped store purges expired entries except Redis, whose TTLs are native. A relational store's `IdempotencyKeys` table grows unbounded unless you add your own cleanup job against the indexed `ExpiresAt` column.
13. A custom `IIdempotencyKeyStore.IsKeyProcessedAsync` must be a single atomic reserve (unique index insert, `SET NX`, or CAS) keyed on `IdempotentKeyInfo.CompositeKey` — never a Get-then-Set, and never keyed on `SafeKey`.

## How to protect a route group with mixed verbs

**When**: several endpoints under one prefix need the same protection, and some of them use verbs beyond the `POST`-only default.

```csharp
using DKNet.AspCore.Idempotency;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddIdempotentKey();

var app = builder.Build();

var orders = app.MapGroup("/api/orders").RequiredIdempotentKey();               // POST only (default)
var admin = app.MapGroup("/api/admin").RequiredIdempotentKey("POST", "DELETE"); // explicit verbs

orders.MapPost("/", () => TypedResults.Created("/api/orders/1"));     // covered
orders.MapGet("/{id}", (string id) => TypedResults.Ok(id));          // untouched: GET
admin.MapDelete("/tenants/{id}", (string id) => TypedResults.Ok(id)); // covered: named

await app.RunAsync();
```

**Notes**: coverage is decided once, at app start, from each endpoint's routed verb — an `X-HTTP-Method-Override` header can't move an endpoint into or out of coverage. `app.Map(...)` with no verb constraint is never covered.

## How to replay the original response instead of a 409

**When**: a duplicate request should transparently get the exact original response back instead of an explicit conflict.

```csharp
using DKNet.AspCore.Idempotency;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddIdempotentKey(options =>
{
    options.ConflictHandling = IdempotentConflictHandling.CachedResult;
    options.Expiration = TimeSpan.FromHours(24);
});
```

**Notes**: the replayed status/body/content-type come from the store's `CachedResponse`, not from re-running the handler. If nothing is cached yet (still in-flight), the caller still gets `409` even under `CachedResult`.

## How to isolate idempotency keys per caller

**When**: two different callers must not collide on the same key against the same endpoint.

```csharp
using DKNet.AspCore.Idempotency;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddIdempotentKey(options =>
{
    options.ScopeHmacSecret = builder.Configuration["Idempotency:HmacSecret"];
    options.IncludeClientIpInScope = true; // last-resort fallback
});
```

**Notes**: the default chain is user claim → HMAC'd `Authorization` header (only if `ScopeHmacSecret` is set) → client IP (only if `IncludeClientIpInScope`) → empty scope. Setting `options.KeyScopeResolver` to your own `Func<HttpContext, string?>` instead skips this whole chain, even if the other two are also set — see the core reference for that recipe.

## How to wire SQL Server for production

**When**: the app has a SQL Server instance and wants a queryable, durable idempotency ledger.

```csharp
using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.MsSqlStore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddIdempotencyWithMsSqlStore(builder.Configuration.GetConnectionString("IdempotencyDb")!);

var app = builder.Build();
app.MapPost("/orders", () => Results.Ok()).RequiredIdempotentKey();
await app.RunAsync();
```

**Notes**: the `IdempotencyKeys` schema is created automatically the first time `app.RunAsync()`/`Run()` actually starts hosted services. `dotnet ef` tooling against this context reads `IDEMPOTENCY_MSSQL_CONNECTION` from the environment, never from configuration.

## How to wire PostgreSQL for production

**When**: same need as SQL Server, on Postgres — and the store's tests need to run natively on ARM64.

```csharp
using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.NpgsqlStore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddIdempotencyWithNpgsqlStore(builder.Configuration.GetConnectionString("IdempotencyDb")!);

var app = builder.Build();
app.MapPost("/orders", () => Results.Ok()).RequiredIdempotentKey();
await app.RunAsync();
```

**Notes**: same shape as SQL Server — migrations apply automatically at startup. Unlike SQL Server's x64-only container image, Postgres containers run natively on ARM64.

## How to wire Redis for production

**When**: the lowest latency and no owned schema/migrations matter more than a queryable audit table.

```csharp
using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.RedisStore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddIdempotencyWithRedisStore(builder.Configuration.GetConnectionString("Redis")!);

var app = builder.Build();
app.MapPost("/orders", () => Results.Ok()).RequiredIdempotentKey();
await app.RunAsync();
```

**Notes**: no schema, no migration — both the in-flight reservation and the completed response are native Redis TTLs (`SET NX` for the reserve, plain `SET` for the complete). `AddIdempotencyWithRedisStore` also registers `IDistributedCache`, unused by this store but harmless.

## How to reuse an existing `IConnectionMultiplexer` with the Redis store

**When**: the app already owns a Redis connection for other purposes and must not open a second one.

```csharp
using DKNet.AspCore.Idempotency.RedisStore;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

IConnectionMultiplexer existingMultiplexer = ConnectionMultiplexer.Connect("localhost:6379");
builder.Services.AddIdempotencyRedisStore(existingMultiplexer);
builder.Services.AddIdempotencyWithRedisStore(builder.Configuration.GetConnectionString("Redis")!);

var app = builder.Build();
await app.RunAsync();
```

**Notes**: `AddIdempotencyRedisStore(multiplexer)` must run first — the connection-string overload inside `AddIdempotencyWithRedisStore` then sees `IConnectionMultiplexer` already registered and skips connecting a second one. It still registers `IDistributedCache` regardless; there is no flag to suppress that.

## How to write your own `IIdempotencyKeyStore`

**When**: none of the shipped stores fit (e.g. DynamoDB, a message broker's own dedup table).

```csharp
using DKNet.AspCore.Idempotency.Filtering;
using DKNet.AspCore.Idempotency.Store;

public sealed class MyIdempotencyKeyStore : IIdempotencyKeyStore
{
    public ValueTask<(bool processed, CachedResponse? response)> IsKeyProcessedAsync(IdempotentKeyInfo keyInfo)
    {
        // Atomic check-and-reserve: absent -> write a reservation, return (false, null).
        throw new NotImplementedException();
    }

    public ValueTask MarkKeyAsProcessedAsync(IdempotentKeyInfo keyInfo, CachedResponse cachedResponse)
    {
        // Overwrite this caller's reservation with cachedResponse.
        throw new NotImplementedException();
    }
}
```

That store type lives in its own file, like any other application class. Wire it up from `Program.cs`:

```csharp
using DKNet.AspCore.Idempotency;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddIdempotentKey<MyIdempotencyKeyStore>();
```

**Notes**: `IsKeyProcessedAsync` must be atomic (unique index / `SET NX` / CAS — never Get-then-Set), must use a distinguishable in-flight sentinel (every shipped store uses HTTP `102`), must expire that reservation after `InFlightReservationTimeout`, and must hash `IdempotentKeyInfo.CompositeKey` (never `SafeKey`).

## Runtime behaviour

Per protected request, in order:

1. The filter reads the header named by `IdempotencyHeaderKey` (default `X-Idempotency-Key`), builds an `IdempotentKeyInfo` (endpoint and method upper-invariant, scope from `KeyScopeResolver` or the default chain).
2. `IdempotentKeyInfo.IsValid(options)` checks presence, then length, then format (under a fixed 100ms match timeout). Any failure short-circuits with `400` — the handler never runs.
3. The filter calls `store.IsKeyProcessedAsync(keyInfo)`. Every store hashes `CompositeKey` (`$"{Scope}:{Method}:{Endpoint}:{IdempotentKey}"`, SHA-256) before using it as the actual lookup key:
   - **New key** → the store atomically writes a reservation with status `102` and returns `(false, null)` — the handler runs.
   - **In-flight elsewhere** → returns `(true, null)` — the filter answers `409` (or, under `CachedResult`, the same `409`, since nothing is cached yet).
   - **Already completed** → returns `(true, cachedResponse)` — the filter answers `409` (`ConflictResponse`) or replays `cachedResponse` verbatim (`CachedResult`).
   - **Reservation expired** (`InFlightReservationTimeout` elapsed) → the store reclaims it for exactly one racing caller; every other racer re-reads and branches as above.
4. If this caller won the reservation, the handler runs, then the filter calls `store.MarkKeyAsProcessedAsync` — only if the response's status code is cacheable (`Min`/`MaxStatusCodeForCaching` or `AdditionalCacheableStatusCodes`). A serialization/store failure here is caught, logged, and never fails the original response.
5. The client always gets the handler's real result on the winning call. Every store enforces `Expiration` as the completed entry's absolute lifetime — a relational store via the `ExpiresAt` column (reclaimed opportunistically), Redis via native TTL.

## Gotchas

- **First-wins is asymmetric between a default and a named store.** The first `AddIdempotentKey()`/`AddIdempotentKey<T>()` call registers the store either way, but a *named* store call always replaces an already-registered in-process default (and its `config` then decides shared options) — while a second *named* store call after the first is a complete no-op, config included. Reordering two `AddIdempotentKey<T>(...)` calls silently changes which options apply.
- **A group's verb omission is silent.** A `PUT` in a POST-only-default group is simply unprotected, with nothing flagging it anywhere. Audit the group's mutating routes against the verb list you actually passed.
- **The `102` status is a deliberate in-flight sentinel, not a bug.** Every store uses HTTP `102` to mark a reservation — legal under the relational stores' `100..599` check constraint but never a real completed response. Always branch on the status value, not just row/key existence.
- **`SafeKey` must never back storage or lookup.** It strips control characters for safe logging/display; only `CompositeKey` (the raw key, hashed) is ever stored or compared. Substituting `SafeKey` into custom store code would silently collapse distinct keys that differ only in stripped control characters.
- **The 100ms regex timeout is a hang backstop, not a pattern fixer.** A pathological custom `IdempotencyKeyPattern` (nested quantifiers) still burns CPU on a long key before it safely times out into a `400`. Write custom patterns to be linear; don't rely on the timeout for performance.
- **`IsKeyProcessedAsync` failures are not caught; `MarkKeyAsProcessedAsync` failures are.** A store outage on the duplicate check bubbles up as an unhandled `500`; a caching failure after the handler ran is swallowed (logged) and the real response still reaches the client. The two behave completely differently for the same underlying outage, depending only on *when* it happens.
- **Relational stores never purge expired rows themselves.** `ExpiresAt` is indexed on both MsSqlStore and NpgsqlStore so *you* can query it, but nothing sweeps it — expired rows are reclaimed only opportunistically, on the next collision for that exact key.
- **Redis eviction looks identical to "key never existed."** Under memory pressure Redis can evict a completed idempotency entry before its TTL, and the store cannot distinguish that from a genuinely new key — a completed idempotent request can silently replay as brand-new.
- **Cached response bodies are stored verbatim, with no separate retention policy.** Relational stores put the full serialized body in an `nvarchar(max)`/`text` column (max 1,048,576 chars); Redis stores it as the value string. PII or secrets in a cached 2xx body persist for the full `Expiration` window.
- **A store's own migration/connection tooling reads its own env var, never `IConfiguration`.** `dotnet ef` design-time factories read `IDEMPOTENCY_MSSQL_CONNECTION` / `IDEMPOTENCY_NPGSQL_CONNECTION` from the process environment — running EF tooling without exporting that specific variable throws `InvalidOperationException` even when the app's own connection string is fine.
- **`AddIdempotencyRedisStore(connectionString)` always registers `IDistributedCache`, even though `IdempotencyRedisStore` never uses it.** An unused Redis-backed distributed cache shows up in the container with no way to suppress it from this package — harmless, but unrequested.
- **Two identically-named internal `IdempotencyDbContext` types exist** — one in the Relational base, one closed per provider package. Only matters if you're extending this family (both are `internal`), but explains a confusing type-resolution error if you ever read two provider packages' source side by side.

## Do not

- `services.AddIdempotency(...)` / `services.AddDKNetIdempotency(...)` — do not exist. The real call is `AddIdempotentKey()` / `AddIdempotentKey<TStore>()`.
- `[Idempotent]` attribute on a controller action — does not exist; this family only extends `RouteHandlerBuilder`/`RouteGroupBuilder` for minimal APIs.
- `new IdempotencyInMemoryStore()`, `new IdempotencySqlServerStore(...)`, `new IdempotencyPostgresStore(...)`, `new IdempotencyRedisStore(...)` — every shipped store is `internal`; select one through `Add...` extensions only.
- `IdempotencyMsSqlOptions` / `IdempotencyNpgsqlOptions` / `RedisIdempotencyOptions` / `IdempotencyRelationalOptions` — none of these types exist. Every store shares one options type: `DKNet.AspCore.Idempotency.IdempotencyOptions`.
- `AddDbContext<DKNet.AspCore.Idempotency.Relational.Data.IdempotencyDbContext>(...)` from application code — impossible; that base type is `abstract` and `internal`.
- `app.Services.GetRequiredService<IdempotencyRedisStore>()` / `<IdempotencySqlServerStore>()` / `<IdempotencyPostgresStore>()` — none is resolvable by its concrete type; resolve `IIdempotencyKeyStore` instead.
- Assuming `RequiredIdempotentKey()` with no verb argument on a group covers `PUT`/`DELETE` too — the default is `POST` only.
- Assuming `ConflictHandling` defaults to replaying the cached response — the default is `ConflictResponse` (`409`), not `CachedResult`.
- Calling `MarkKeyAsProcessedAsync` yourself to "pre-warm" a key — it's meant to be called exactly once by the filter, from the caller that won the reservation.

`options.FailOpen = false` looks plausible enough that it is worth showing why it fails — it appears verbatim inside this repo's own shipped XML `<example>` doc comments for `AddIdempotencyWithMsSqlStore`/`AddIdempotencyWithNpgsqlStore`, but `IdempotencyOptions` has no such member:

```csharp
// no-compile
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddIdempotencyWithMsSqlStore(
    builder.Configuration.GetConnectionString("IdempotencyDb")!,
    options =>
    {
        options.Expiration = TimeSpan.FromHours(48);
        options.FailOpen = false; // IdempotencyOptions has no FailOpen member — CS1061
    });
```

The plain (non-`With`) overload has no `config` parameter — that belongs only to the `With` overload:

```csharp
// no-compile
var services = new ServiceCollection();
var connectionString = "Server=.;Database=IdempotencyDb;TrustServerCertificate=True;";
services.AddIdempotencyMsSqlStore(connectionString, options => options.Expiration = TimeSpan.FromHours(1));
// AddIdempotencyMsSqlStore(IServiceCollection, string) has no `config` parameter — CS1501.
// Use AddIdempotencyWithMsSqlStore(services, connectionString, config) instead.
```

## Related skills

- `dknet-packages` — start there to confirm this is the right package before wiring idempotency into a new project.
- `dknet-aspcore-api` — minimal-API endpoint conventions and start-up tasks (`DKNet.AspCore.Extensions`, `DKNet.AspCore.Tasks`) this filter sits alongside.
- `dknet-core-utilities` — `DKNet.Fw.Extensions` supplies `IsRegistered<T>()`, the first-wins DI guard every `Add...Store` extension in this family uses.
- `dknet-testing` — TestContainers fixtures (MsSql/Postgres/Redis), the ARM64 exclusion rule for MsSql-backed tests, and general testing patterns for code built on DKNet.

## References

- [references/DKNet.AspCore.Idempotency.md](references/DKNet.AspCore.Idempotency.md) — the endpoint filter, `IdempotencyOptions`, scope resolution, conflict handling, the in-process dev store.
- [references/DKNet.AspCore.Idempotency.Relational.md](references/DKNet.AspCore.Idempotency.Relational.md) — the shared EF Core reserve/check/complete base; read only when adding a new relational provider inside the DKNet repo.
- [references/DKNet.AspCore.Idempotency.MsSqlStore.md](references/DKNet.AspCore.Idempotency.MsSqlStore.md) — SQL Server store wiring, migrations, `dotnet ef` tooling.
- [references/DKNet.AspCore.Idempotency.NpgsqlStore.md](references/DKNet.AspCore.Idempotency.NpgsqlStore.md) — PostgreSQL store wiring, migrations, `dotnet ef` tooling.
- [references/DKNet.AspCore.Idempotency.RedisStore.md](references/DKNet.AspCore.Idempotency.RedisStore.md) — Redis store wiring, TTL model, multiplexer reuse.
- [references/diagnostics.md](references/diagnostics.md) — every exception/status code this feature can produce, across all five packages, in one table.

Docs: https://baoduy.github.io/DKNet/ · NuGet: https://www.nuget.org/packages/DKNet.AspCore.Idempotency (and the `.Relational`/`.MsSqlStore`/`.NpgsqlStore`/`.RedisStore` siblings) · each reference file's header table links that package's own docs page under `github.com/baoduy/DKNet/blob/dev/docs/AspNetCore/`.
