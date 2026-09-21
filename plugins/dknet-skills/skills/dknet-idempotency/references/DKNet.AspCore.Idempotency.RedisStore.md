# DKNet.AspCore.Idempotency.RedisStore

| Field | Value |
|---|---|
| Area | AspNetCore |
| NuGet package | `DKNet.AspCore.Idempotency.RedisStore` — `dotnet add package DKNet.AspCore.Idempotency.RedisStore` |
| NuGet page | https://www.nuget.org/packages/DKNet.AspCore.Idempotency.RedisStore |
| Docs | https://github.com/baoduy/DKNet/blob/dev/docs/AspNetCore/DKNet.AspCore.Idempotency.RedisStore.md |
| Source | https://github.com/baoduy/DKNet/tree/dev/src/AspNet/DKNet.AspCore.Idempotency.RedisStore |
| Depends on (DKNet) | `DKNet.AspCore.Idempotency` (transitively brings `DKNet.Fw.Extensions` for the `IsRegistered<T>()` first-wins guard) |
| Depends on (3rd party) | `Microsoft.Extensions.Caching.StackExchangeRedis` (brings in `StackExchange.Redis` transitively) |
| Target framework | `net10.0` |

## Purpose

A Redis-backed `IIdempotencyKeyStore` for `DKNet.AspCore.Idempotency`. It implements the store contract directly against `StackExchange.Redis` — atomic reservation via a single `SET key value NX GET` round trip (`IDatabase.StringSetAndGetAsync` with `When.NotExists`), and expiry as native Redis TTLs on both the in-flight reservation and the completed response, so there is no schema, migration or cleanup job. It is NOT a general-purpose Redis cache wrapper and does NOT derive from `DKNet.AspCore.Idempotency.Relational` — use it only as the idempotency key store behind `DKNet.AspCore.Idempotency`'s endpoint filter, and reach for `MsSqlStore`/`NpgsqlStore` instead when you need the ledger queryable in SQL alongside your business data.

## Entry points

| Call | Exact signature | Called on | Notes (lifetime, ordering, prerequisites) |
|---|---|---|---|
| `AddIdempotencyRedisStore` (connection string) | `public static IServiceCollection AddIdempotencyRedisStore(this IServiceCollection services, string connectionString)` | `IServiceCollection` | Registers `IDistributedCache` (via `AddStackExchangeRedisCache`) and a singleton `IConnectionMultiplexer` built with `ConnectionMultiplexer.Connect(connectionString)` — each guarded independently, so an app with either already registered still gets the other. Throws `ArgumentNullException` if `services` is null or if `connectionString` is null; `ArgumentException` if `connectionString` is empty/whitespace-only. Does **not** register the key store. |
| `AddIdempotencyRedisStore` (multiplexer) | `public static IServiceCollection AddIdempotencyRedisStore(this IServiceCollection services, IConnectionMultiplexer connectionMultiplexer)` | `IServiceCollection` | Registers the supplied multiplexer as a singleton. Returns early (no-op) if an `IConnectionMultiplexer` is already registered — first-wins. Throws `ArgumentNullException` on a null `services` or `connectionMultiplexer`. Registers no `IDistributedCache`. Call this **before** `AddIdempotencyWithRedisStore` if you want your own multiplexer used instead of a new connection. |
| `AddIdempotencyWithRedisStore` | `public static IServiceCollection AddIdempotencyWithRedisStore(this IServiceCollection services, string connectionString, Action<IdempotencyOptions>? config = null)` | `IServiceCollection` | Calls the connection-string overload above, then `services.AddIdempotentKey<IdempotencyRedisStore>(config)`. This is the call an application actually makes — the only public path that wires `IdempotencyRedisStore` in as `IIdempotencyKeyStore`, since that type is `internal`. First-wins on `IIdempotencyKeyStore`. Must run during service registration, before any endpoint calls `.RequiredIdempotentKey()`. |

## Public surface

### `DKNet.AspCore.Idempotency.RedisStore`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `IdempotencyRedisSetup` | static class | The package's only public type — the three DI extension methods above. | See **Entry points** for exact signatures. |

### `DKNet.AspCore.Idempotency.RedisStore.Store` (internal — explains observable behaviour)

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `IdempotencyRedisStore` | `internal sealed class : IIdempotencyKeyStore` | The Redis implementation of the store contract. Not nameable from application code — only reachable via `AddIdempotencyWithRedisStore`. | Ctor `(IConnectionMultiplexer, IOptions<IdempotencyOptions>, ILogger<IdempotencyRedisStore>)` — all three args null-checked. `IsKeyProcessedAsync(IdempotentKeyInfo)`. `MarkKeyAsProcessedAsync(IdempotentKeyInfo, CachedResponse)`. `private string SanitizeKey(string key)` — hashes the composite key. |

No options type of its own exists — the contract types this store accepts (`IdempotentKeyInfo` from `DKNet.AspCore.Idempotency.Filtering`, `CachedResponse` and `IIdempotencyKeyStore` from `DKNet.AspCore.Idempotency.Store`) all live in the core package.

## Options & defaults

There is no Redis-specific options type. `IdempotencyRedisStore` reads a subset of the shared `IdempotencyOptions` (configured via the `config` delegate on `AddIdempotencyWithRedisStore`):

| Option | Type | Default | Effect |
|---|---|---|---|
| `CachePrefix` | `string` | `"idem"` | Prepended, unchanged, to every Redis key before the hashed suffix |
| `Expiration` | `TimeSpan` | 4 hours | TTL on the completed-response entry written by `MarkKeyAsProcessedAsync`'s unconditional `SET` |
| `InFlightReservationTimeout` | `TimeSpan` | 30 seconds | TTL on the reservation entry written by `IsKeyProcessedAsync`'s `SET NX` |
| `JsonSerializerOptions` | `JsonSerializerOptions` | camelCase naming policy | Serializes/deserializes `CachedResponse` to/from the Redis string value |

Every other `IdempotencyOptions` member (`IdempotencyHeaderKey`, `IdempotencyKeyPattern`, `MaxIdempotencyKeyLength`, `ConflictHandling`, `MinStatusCodeForCaching`, `MaxStatusCodeForCaching`, `AdditionalCacheableStatusCodes`, `KeyScopeResolver`, `ScopeHmacSecret`, `IncludeClientIpInScope`) is read by the core package's endpoint filter, not by this store. All `IdempotencyOptions` values are validated eagerly at registration (`ValidateOnStart()`), not at request time.

## Usage patterns

### Quick start — connection string only

**When**: standing up idempotency against a fresh Redis connection with no existing DI registrations.

```csharp
using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.RedisStore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddIdempotencyWithRedisStore(
    builder.Configuration.GetConnectionString("Redis")!,
    options =>
    {
        options.Expiration = TimeSpan.FromHours(48);
    });

var app = builder.Build();

app.MapPost("/orders", () => Results.Ok())
    .RequiredIdempotentKey();

await app.RunAsync();
```

**Notes**: `RequiredIdempotentKey()` comes from the core package (`using DKNet.AspCore.Idempotency;`) — it is an extension on `RouteHandlerBuilder` (plus a `RouteGroupBuilder` overload) declared directly on `IdempotencySetup`. Clients send the key on `X-Idempotency-Key` (default header) on the `POST`; a retry with the same key either gets `409` or a cached replay depending on `ConflictHandling`.

### Reusing an existing `IConnectionMultiplexer`

**When**: the app already owns a multiplexer for other Redis usage and must not open a second connection.

```csharp
using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.RedisStore;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

IConnectionMultiplexer existingMultiplexer = ConnectionMultiplexer.Connect("localhost:6379");
builder.Services.AddIdempotencyRedisStore(existingMultiplexer);

builder.Services.AddIdempotencyWithRedisStore(
    builder.Configuration.GetConnectionString("Redis")!,
    options => options.Expiration = TimeSpan.FromHours(48));

var app = builder.Build();
await app.RunAsync();
```

**Notes**: `AddIdempotencyRedisStore(multiplexer)` must run first — `AddIdempotencyWithRedisStore`'s internal call to the connection-string overload sees `IConnectionMultiplexer` already registered and skips connecting a second one, but it still registers `IDistributedCache` (harmless, unused by this store).

### Custom cache prefix for key-space isolation

**When**: sharing a Redis instance/cluster with unrelated data and needing a distinct namespace.

```csharp
using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.RedisStore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddIdempotencyWithRedisStore(
    builder.Configuration.GetConnectionString("Redis")!,
    options => options.CachePrefix = "myapp:idem:");

var app = builder.Build();
await app.RunAsync();
```

**Notes**: the prefix is prepended unchanged; the remainder of the key is always a lowercase hex SHA-256 hash of the composite key, so the prefix is the only human-legible part of the Redis key name.

### Calling the store directly (advanced / diagnostics)

**When**: writing a diagnostic tool or a test that exercises the store contract without going through the endpoint filter.

```csharp
using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.Filtering;
using DKNet.AspCore.Idempotency.RedisStore;
using DKNet.AspCore.Idempotency.Store;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddIdempotencyWithRedisStore(builder.Configuration.GetConnectionString("Redis")!);
var app = builder.Build();

var store = app.Services.GetRequiredService<IIdempotencyKeyStore>();
var keyInfo = new IdempotentKeyInfo { Endpoint = "/API/ORDERS", Method = "POST", IdempotentKey = "abc-123" };

var (processed, cached) = await store.IsKeyProcessedAsync(keyInfo);
if (!processed)
{
    // Won the reservation; run the handler, then:
    await store.MarkKeyAsProcessedAsync(keyInfo, new CachedResponse
    {
        StatusCode = 201,
        Body = "{\"id\":1}",
        ContentType = "application/json",
        CreatedAt = DateTimeOffset.UtcNow,
        ExpiresAt = DateTimeOffset.UtcNow.AddHours(48)
    });
}
```

**Notes**: `IIdempotencyKeyStore` and `IdempotentKeyInfo`/`CachedResponse` are all core-package types, not this package's — the DI container resolves whatever concrete store was registered (here, the Redis one). You cannot write `app.Services.GetRequiredService<IdempotencyRedisStore>()` — the concrete type is `internal`.

## Runtime behaviour

For one idempotency-protected request, in order:

1. The core package's endpoint filter extracts an `IdempotentKeyInfo` from the request and validates it against `IdempotencyOptions` (header presence, length, regex).
2. The filter calls `IIdempotencyKeyStore.IsKeyProcessedAsync(keyInfo)`, which resolves to `IdempotencyRedisStore.IsKeyProcessedAsync`.
3. `SanitizeKey` computes the Redis key: `CachePrefix + lowercase-hex(SHA256(CompositeKey))`.
4. It builds a reservation `CachedResponse` with `StatusCode = 102` and calls `IDatabase.StringSetAndGetAsync(cacheKey, reservationJson, InFlightReservationTimeout, when: When.NotExists)` — an atomic `SET key value NX GET`: sets the key only if absent, and returns whatever value was already there (or nothing) in the same round trip.
5. If the returned previous value is null/whitespace, this caller won the reservation: returns `(false, null)`.
6. Otherwise it deserializes the previous value into a `CachedResponse` and branches:
   - Expired → deletes the key and recurses into `IsKeyProcessedAsync` once more, so the retry's own `SET NX GET` reserves fresh.
   - Still `StatusCode == 102` (a reservation) → returns `(true, null)` — caller should wait/retry.
   - Otherwise (a real completed response) → returns `(true, existing)` — the filter replays it.
7. If this caller won the reservation, the handler runs, then the filter calls `MarkKeyAsProcessedAsync(keyInfo, cachedResponse)`, which does an unconditional `SET` at the longer `Expiration` TTL, overwriting the reservation with the real result.

No background sweep exists anywhere in this path — both TTLs are enforced by Redis itself.

## Diagnostics & exceptions

| Type | Severity | When | Fix |
|---|---|---|---|
| `ArgumentNullException` | Error | `AddIdempotencyRedisStore(services, ...)` called with a null `services` or a null `connectionString`; the multiplexer overload called with a null `connectionMultiplexer` | Pass non-null arguments |
| `ArgumentException` | Error | `AddIdempotencyRedisStore(services, connectionString)` called with an empty or whitespace-only `connectionString` (not null — see row above) | Supply a real Redis connection string |
| `OptionsValidationException` (from the core package's option validators, via `ValidateOnStart()`) | Error, at startup | An empty header key/cache prefix, non-positive `Expiration`, an invalid status-code window, a null `JsonSerializerOptions`, `MaxIdempotencyKeyLength < 1`, an empty key pattern, or a whitespace `ScopeHmacSecret`, configured via the `config` delegate | Fix the offending `IdempotencyOptions` value before deploying; this fails fast at app startup, not per-request |
| `RedisConnectionException` / other `StackExchange.Redis` exceptions | Runtime, unhandled by this package | Redis is unreachable when `IsKeyProcessedAsync`/`MarkKeyAsProcessedAsync` runs | Ensure Redis availability; this package has no in-memory or degraded mode |

This package defines no analyzer `DiagnosticDescriptor`s.

## Gotchas

- **`IdempotencyRedisStore` is `internal sealed`.** Application code cannot write `AddIdempotentKey<IdempotencyRedisStore>()` — it won't compile outside this assembly and its test assembly. Always call `AddIdempotencyWithRedisStore(...)`, which wires it in for you.
- **`AddIdempotencyRedisStore(connectionString)` always registers `IDistributedCache` via `AddStackExchangeRedisCache`, even though `IdempotencyRedisStore` never uses it** — it talks to `IConnectionMultiplexer` directly. An unused Redis-backed distributed cache registration appears in the container regardless of which overload order you call. There is no flag to suppress it from this package; register your own `IDistributedCache` first if you need a different one, or ignore it as harmless.
- **Every DI registration in this package is first-wins, silently.** Calling `AddIdempotencyRedisStore` a second time with a *different* connection string is a complete no-op — no error, no warning. Call it exactly once per connection.
- **`IsKeyProcessedAsync` recurses into itself when it finds a logically-expired entry**, without any recursion-depth guard. In practice this is bounded by the atomic delete+re-reserve, but there is no explicit cap — a design note, not a bug seen in tests.
- **Redis eviction under memory pressure looks identical to "key never existed."** A completed idempotency entry can be evicted before its TTL, and the store has no way to distinguish that from a genuinely new key. A completed idempotent request could silently replay as if it were new. Size Redis memory/eviction policy so idempotency keys are never evicted before `Expiration`.
- **This store's TTL model has no notion of "cacheable status range" from `IdempotencyOptions`** (`MinStatusCodeForCaching`/`MaxStatusCodeForCaching`/`AdditionalCacheableStatusCodes`) — those are enforced entirely by the core package's endpoint filter, not by this store. Reading only this package's source will not explain why some status codes are never cached.
- **There is no way to ad-hoc query cached idempotency responses by scope, endpoint, or date range** — Redis only supports lookup by the one-way hashed key. Debugging "why did this request get replayed" requires reconstructing the exact composite key and hashing it yourself; use a relational store instead if ad-hoc querying matters.

## Anti-patterns & hallucination traps

- `services.AddIdempotentKey<IdempotencyRedisStore>()` written directly in application code — does not compile; `IdempotencyRedisStore` is `internal`. Use `AddIdempotencyWithRedisStore` instead.
- `new IdempotencyRedisStore(...)` constructed by hand in app code — same problem; the type is not reachable outside this assembly and its test assembly.
- Assuming a `RedisIdempotencyOptions` or `IdempotencyRedisOptions` type exists — it does not. All configuration goes through the shared `IdempotencyOptions` from `DKNet.AspCore.Idempotency`.
- Assuming `IdempotencyRedisStore` derives from or extends `DKNet.AspCore.Idempotency.Relational` — it does not; that base class is shared only by `MsSqlStore`/`NpgsqlStore`. This store implements `IIdempotencyKeyStore` directly.
- Calling `AddIdempotencyRedisStore(connectionString)` expecting it to also register the key store — it only wires Redis infrastructure (`IDistributedCache` + `IConnectionMultiplexer`); you still need `AddIdempotentKey<...>()` or, more simply, `AddIdempotencyWithRedisStore(...)`.
- Expecting a cleanup/background job type (e.g. `IdempotencyRedisCleanupService`) — none exists, and none is needed; expiry is entirely Redis TTLs. This is not a gap relative to the relational stores: `MsSqlStore`/`NpgsqlStore` ship no cleanup job either (only an automatic schema-migration hosted service) and explicitly leave sweeping expired rows to the operator.
- Reading the Redis key back with the raw idempotency key string — the actual Redis key is `CachePrefix + SHA256(CompositeKey)` in lowercase hex; the raw key is never stored as a key name.

## Composes with

| Package | Rule |
|---|---|
| `DKNet.AspCore.Idempotency` | Required base — supplies `IIdempotencyKeyStore`, `IdempotencyOptions`, `IdempotentKeyInfo`, `CachedResponse`, the endpoint filter and `RequiredIdempotentKey()`. Always read its reference for anything not store-specific. |
| `DKNet.AspCore.Idempotency.MsSqlStore` | Reach for it instead when you want the key ledger queryable in SQL Server alongside your business data, with backups. |
| `DKNet.AspCore.Idempotency.NpgsqlStore` | Same trade-off as MsSqlStore, on PostgreSQL. |
| `DKNet.AspCore.Idempotency.Relational` | The shared base for the two SQL stores — this package deliberately does **not** use it. |
| `Microsoft.Extensions.Caching.StackExchangeRedis` | Brought in as a package dependency purely for `AddStackExchangeRedisCache`/`IDistributedCache` registration; the store itself talks to `IConnectionMultiplexer`, not `IDistributedCache`. |

## Testing notes

- No live Redis or TestContainers dependency is required to test this store's DI wiring — mock `IConnectionMultiplexer`/`IDatabase` (e.g. with Moq) and assert on service *descriptors* (`ServiceType`, `ImplementationType`) rather than resolving the container when a real connection string like `"localhost:6379"` would otherwise open an actual socket.
- To prove the concurrency guarantee without a live Redis, back a mocked `IDatabase.StringSetAndGetAsync` with a real `ConcurrentDictionary<string,string>` and its `TryAdd`, then fire several genuinely concurrent `IsKeyProcessedAsync` calls for the same key and assert exactly one winner.
- Exercise the full branch table a losing caller can see: still in-flight, completed, logically expired (deleted + retried), and evicted-between-calls — each needs its own scripted mock response, since a real Redis instance won't reliably reproduce "evicted" on demand.
- Because `IdempotencyRedisStore` is `internal`, a test that constructs it directly needs `InternalsVisibleTo`; an application-level integration test without that visibility must go through `IIdempotencyKeyStore` resolved from DI instead.
