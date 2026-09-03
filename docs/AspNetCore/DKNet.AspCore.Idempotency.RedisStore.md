# DKNet.AspCore.Idempotency.RedisStore

Redis-backed `IIdempotencyKeyStore` for
[`DKNet.AspCore.Idempotency`](DKNet.AspCore.Idempotency.md), implemented directly against
`StackExchange.Redis` with `SET NX` reservation and native key expiry.

## ✨ Why use it?

- **Atomic reservation with no schema.** `SET NX` gives exactly one concurrent caller the right to
  proceed, the same all-or-nothing guarantee a relational unique index gives — without a table, a
  migration, or a design-time factory to own.
- **Lowest latency of the shipped stores.** No database round-trip and no query planner on the hot
  path of every idempotency-protected request.
- **Expiry is free.** Both the in-flight reservation window and the completed-response lifetime are
  Redis TTLs, so there is no cleanup job to host — unlike the relational stores, which need one.
- **Built for horizontal scale.** Unlike the core package's built-in distributed-cache store, this
  one is safe for multi-instance production traffic.

Reach for Redis over a relational store when you already run Redis and want the lowest-latency
option with no schema to manage. See
[Choosing a store](DKNet.AspCore.Idempotency.md#-choosing-a-store) on the core page for the full
Redis-vs-relational comparison.

## 🚀 Quick Start

```bash
dotnet add package DKNet.AspCore.Idempotency.RedisStore
```

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

app.MapPost("/orders", CreateOrder)
    .RequiredIdempotentKey();

await app.RunAsync();
```

`AddIdempotencyWithRedisStore` registers `IDistributedCache` and `IConnectionMultiplexer` for the
given connection string — skipping either if your app already registered one — and then calls
`AddIdempotentKey<IdempotencyRedisStore>()`, wiring `IdempotencyRedisStore` as the
`IIdempotencyKeyStore` the endpoint filter uses.

If your app already owns an `IConnectionMultiplexer`, hand it over first; the registration above
then reuses it instead of connecting a second one:

```csharp
builder.Services.AddIdempotencyRedisStore(existingMultiplexer);

builder.Services.AddIdempotencyWithRedisStore(
    builder.Configuration.GetConnectionString("Redis")!,
    options =>
    {
        options.Expiration = TimeSpan.FromHours(48);
    });
```

## 🧩 Features

### Registration entry points

`IdempotencyRedisSetup` is the package's only public type. It declares three extension methods on
`IServiceCollection`, and only the third one wires up the key store:

| Method | Registers | Guard |
|---|---|---|
| `AddIdempotencyRedisStore(string connectionString)` | `IDistributedCache` via `AddStackExchangeRedisCache`, **and** a singleton `IConnectionMultiplexer` built with `ConnectionMultiplexer.Connect(connectionString)` | Each of the two is registered only when nothing is registered for it yet — they are guarded independently, so an app that already owns one still gets the other. |
| `AddIdempotencyRedisStore(IConnectionMultiplexer connectionMultiplexer)` | The supplied multiplexer as a singleton | Returns early if any `IConnectionMultiplexer` is already registered. Registers no `IDistributedCache`. |
| `AddIdempotencyWithRedisStore(string connectionString, Action<IdempotencyOptions>? config = null)` | Calls the connection-string overload, then `AddIdempotentKey<IdempotencyRedisStore>(config)` | First-wins on `IIdempotencyKeyStore`, as everywhere else. |

The first two throw `ArgumentNullException` on a null `services` (and on a null multiplexer); the
connection-string overloads also throw `ArgumentException` on an empty or whitespace connection string.

### What the package creates, and what you provide

| Thing | Who provides it |
|---|---|
| Key layout, reservation entries and TTLs | The package — nothing is provisioned ahead of time, and there is no schema, migration or design-time factory |
| A reachable Redis instance or cluster | **You** |
| Redis memory sizing and its eviction policy | **You** — an eviction under memory pressure silently makes a key look new again |
| Key-space isolation from unrelated data | **You**, via `IdempotencyOptions.CachePrefix` |

### Key format and prefixing

Every composite key (`Scope:Method:Endpoint:IdempotentKey`) is hashed with SHA-256 and rendered as
lowercase hex before it becomes a Redis key, so structurally different composite keys never collide
and no raw request data ends up in a Redis key name. The configured prefix
(`IdempotencyOptions.CachePrefix`, default `"idem"`) is prepended unchanged:

```csharp
// Redis key = $"{options.CachePrefix}{lowercase-sha256-hex(keyInfo.CompositeKey)}"
builder.Services.AddIdempotencyWithRedisStore(connectionString, options =>
{
    options.CachePrefix = "myapp:idem:";
});
```

### TTL and expiry handling

Two windows, both enforced as native Redis TTLs — there is no background sweep:

- **In-flight reservations** use `IdempotencyOptions.InFlightReservationTimeout` (default 30s) as
  the key's TTL on the reservation `SET NX`, so a crashed handler's reservation self-expires and a
  later caller is not blocked forever.
- **Completed responses** use `IdempotencyOptions.Expiration` (default 4h) as the key's TTL on the
  unconditional `SET` performed by `MarkKeyAsProcessedAsync`.

The store additionally checks `CachedResponse.IsExpired` on read and calls `KeyDeleteAsync` if a
logically expired entry is still physically present (e.g. an explicit `ExpiresAt` shorter than the
Redis TTL):

```csharp
var store = app.Services.GetRequiredService<IIdempotencyKeyStore>();
var keyInfo = new IdempotentKeyInfo { Endpoint = "/API/ORDERS", Method = "POST", IdempotentKey = "abc-123" };

await store.MarkKeyAsProcessedAsync(keyInfo, new CachedResponse
{
    StatusCode = 201,
    Body = "{\"id\":1}",
    ContentType = "application/json",
    CreatedAt = DateTimeOffset.UtcNow,
    ExpiresAt = DateTimeOffset.UtcNow.AddHours(48)
});
```

### Atomic reservation

`IsKeyProcessedAsync` reserves a key with `SET NX`, giving exactly one concurrent caller the right
to proceed — proven under ten genuinely concurrent callers for the same key in
`IdempotencyRedisStoreConcurrencyTests.IsKeyProcessedAsync_ConcurrentRequestsWithSameKey_OnlyOneReservationWins`
(1 winner, 9 collisions):

```csharp
var (processed, cachedResponse) = await store.IsKeyProcessedAsync(keyInfo);
if (!processed)
{
    // This call won the reservation and must now run the handler and call
    // MarkKeyAsProcessedAsync — every other concurrent caller for the same key gets processed = true.
}
```

### Cache-decision logic

When the reservation `SET NX` loses the race, the store re-reads the winner's entry and decides what
a losing caller sees (see `IdempotencyRedisStoreDecisionTests` for the full branch table):

| Winner's entry on re-read | Result to the losing caller |
|---|---|
| Still in-flight (HTTP `102` sentinel) | `(true, null)` — caller should wait/retry |
| Completed | `(true, cachedResponse)` — replay the original response |
| Logically expired | Entry is deleted; caller proceeds as new: `(false, null)` |
| Gone (evicted between the failed `SET` and the re-read) | Caller proceeds as new: `(false, null)` |

```csharp
var (processed, cachedResponse) = await store.IsKeyProcessedAsync(keyInfo);
if (processed && cachedResponse is not null)
{
    // Replay cachedResponse.StatusCode / Body / ContentType instead of re-running the handler.
}
```

## ⚙️ Configuration reference

There is no Redis-specific options type. `IdempotencyRedisStore` reads these `IdempotencyOptions`,
configured via the `config` delegate on `AddIdempotencyWithRedisStore`; the full option set lives in
the [core configuration reference](DKNet.AspCore.Idempotency.md#-configuration-reference):

| Option | Default | Used for |
|---|---|---|
| `CachePrefix` | `"idem"` | Prefix on every Redis key |
| `Expiration` | 4 hours | TTL on completed-response entries |
| `InFlightReservationTimeout` | 30 seconds | TTL on reservation entries |
| `JsonSerializerOptions` | camelCase | Serializing `CachedResponse` to/from the Redis string value |

## 🧱 Where it fits

![Sequence diagram of one protected request: the filter calls IsKeyProcessedAsync, the store GETs the prefixed SHA-256 key, misses, reserves it with SET NX at the InFlightReservationTimeout TTL, and answers (false, null); the handler then runs once and the filter calls MarkKeyAsProcessedAsync, which overwrites the reservation with an unconditional SET at the Expiration TTL.](../diagrams/idempotency-redis-setnx.svg)

This package supplies only the `IIdempotencyKeyStore` implementation. Key validation, the endpoint
filter, conflict handling (`ConflictHandling`), and scope resolution all come from
[DKNet.AspCore.Idempotency](DKNet.AspCore.Idempotency.md) unchanged — see that package's page for
the endpoint-level behaviour this store plugs into.

Unlike the SQL Server and PostgreSQL stores, it does **not** derive from
[DKNet.AspCore.Idempotency.Relational](DKNet.AspCore.Idempotency.Relational.md):
`IdempotencyRedisStore` implements `IIdempotencyKeyStore` directly against `StackExchange.Redis`,
using `SET NX` for reservation and Redis TTLs instead of SQL rows and scheduled cleanup.

## ⚠️ Gotchas & limits

- **`IdempotencyRedisStore` is `internal`.** You cannot name it, so
  `AddIdempotentKey<IdempotencyRedisStore>(...)` is not something app code can write —
  `AddIdempotencyWithRedisStore(...)` is the only way to wire this store in. The public
  `AddIdempotencyRedisStore(...)` overloads register the Redis infrastructure *only*; on their own
  they leave the core package's default store in place.
- **`AddIdempotencyRedisStore(connectionString)` also registers `IDistributedCache`** (via
  `AddStackExchangeRedisCache`) even though this store does not use it — it talks to
  `IConnectionMultiplexer` directly. Harmless, but it is a service registration you did not ask
  for; pass an existing multiplexer first if you want to control that.
- **Registration is first-wins.** Each of `IDistributedCache`, `IConnectionMultiplexer`, and
  `IIdempotencyKeyStore` is registered only when nothing is registered for it yet, so a second call
  with a different connection string is silently a no-op.
- **No relational query-ability.** You cannot ad-hoc query cached responses by scope, endpoint, or
  date range as you could with a SQL table — Redis only supports lookup by the hashed key, and the
  hash is one-way.
- **Retention is fixed at write time.** Expiry is enforced by Redis's own TTL, not application code,
  so there is no cleanup job to host — but you also cannot tweak retention for already-written
  entries without a manual `KeyDeleteAsync` / `redis-cli` operation.
- **Redis availability is a hard dependency.** Every idempotency-protected endpoint call goes
  through `IIdempotencyKeyStore`, and this package has no in-memory or local fallback if Redis is
  unreachable.

## 🔗 Related packages

- [DKNet.AspCore.Idempotency](DKNet.AspCore.Idempotency.md) — the core package this store plugs
  into; start there to wire idempotency into an app at all.
- [DKNet.AspCore.Idempotency.MsSqlStore](DKNet.AspCore.Idempotency.MsSqlStore.md) — reach for it
  instead when you want the key ledger in SQL Server, queryable and backed up with your business
  data.
- [DKNet.AspCore.Idempotency.NpgsqlStore](DKNet.AspCore.Idempotency.NpgsqlStore.md) — the same
  trade-off on PostgreSQL.
- [DKNet.AspCore.Idempotency.Relational](DKNet.AspCore.Idempotency.Relational.md) — the base the two
  SQL stores share; this package does not use it.
