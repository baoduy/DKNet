# DKNet.AspCore.Idempotency.RedisStore

A Redis-backed `IIdempotencyKeyStore` for [`DKNet.AspCore.Idempotency`](DKNet.AspCore.Idempotency.md). Unlike the
SQL Server / PostgreSQL stores, this package does **not** derive from a relational base package — `IdempotencyRedisStore`
implements `IIdempotencyKeyStore` directly against `StackExchange.Redis`, using `SET NX` for atomic reservation and
native Redis key expiry (`EX`) instead of SQL rows and scheduled cleanup.

Pick Redis over a relational store when you already run Redis and want the lowest-latency option with no schema or
migrations to manage — expiry falls out of Redis's own TTL instead of a cleanup job. See
[DKNet.AspCore.Idempotency → Choosing a store](DKNet.AspCore.Idempotency.md) for the full Redis-vs-relational comparison.

## 🚀 Install & Register

```bash
dotnet add package DKNet.AspCore.Idempotency.RedisStore
```

```csharp
builder.Services.AddIdempotencyWithRedisStore(
    builder.Configuration.GetConnectionString("Redis")!,
    options =>
    {
        options.Expiration = TimeSpan.FromHours(48);
    });
```

`AddIdempotencyWithRedisStore` registers `IDistributedCache` + `IConnectionMultiplexer` for the given connection
string (skipping either if your app already registered one) and then calls `AddIdempotentKey<IdempotencyRedisStore>()`,
which wires `IdempotencyRedisStore` as the `IIdempotencyKeyStore` used by the endpoint filter.

If your app already owns an `IConnectionMultiplexer`, reuse it instead of a connection string:

```csharp
builder.Services.AddIdempotencyRedisStore(existingMultiplexer);
builder.Services.AddIdempotentKey<IdempotencyRedisStore>(options =>
{
    options.Expiration = TimeSpan.FromHours(48);
});
```

## ✨ Features

### Key format and prefixing

Every composite key (`Scope:Method:Endpoint:IdempotentKey`) is hashed with SHA-256 before it becomes a Redis key, so
structurally different composite keys never collide and no raw request data ends up in a Redis key name. The
configured prefix (`IdempotencyOptions.CachePrefix`, default `"idem"`) is prepended unchanged:

```csharp
// Redis key = $"{options.CachePrefix}{sha256-hex(keyInfo.CompositeKey)}"
builder.Services.AddIdempotencyWithRedisStore(connectionString, options =>
{
    options.CachePrefix = "myapp:idem:";
});
```

### TTL / expiry handling

Two windows, both enforced as native Redis TTLs — there is no background sweep:

- **In-flight reservations** use `IdempotencyOptions.InFlightReservationTimeout` (default 30s) as the key's TTL on
  the reservation `SET NX`, so a crashed handler's reservation self-expires and a later caller isn't blocked forever.
- **Completed responses** use `IdempotencyOptions.Expiration` (default 4h) as the key's TTL on the unconditional
  `SET` performed by `MarkKeyAsProcessedAsync`.

The store additionally checks `CachedResponse.IsExpired` on read and calls `KeyDeleteAsync` if a logically expired
entry is still physically present (e.g. an explicit `ExpiresAt` shorter than the Redis TTL):

```csharp
var store = app.Services.GetRequiredService<IIdempotencyKeyStore>();
var keyInfo = new IdempotentKeyInfo { Endpoint = "/api/orders", Method = "POST", IdempotentKey = "abc-123" };

await store.MarkKeyAsProcessedAsync(keyInfo, new CachedResponse
{
    StatusCode = 201,
    Body = "{\"id\":1}",
    ContentType = "application/json",
    CreatedAt = DateTimeOffset.UtcNow,
    ExpiresAt = DateTimeOffset.UtcNow.AddHours(48)
});
```

### Concurrency: atomic reservation

`IsKeyProcessedAsync` reserves a key with `SET NX`, giving exactly one concurrent caller the right to proceed —
proven under ten genuinely concurrent callers for the same key in
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

When the reservation `SET NX` loses the race, the store re-reads the winner's entry and decides what a losing
caller sees (see `IdempotencyRedisStoreDecisionTests` for the full branch table):

| Winner's entry on re-read | Result to the losing caller |
|---|---|
| Still in-flight (HTTP 102 sentinel) | `(true, null)` — caller should wait/retry |
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

## ⚙️ Configuration

`IdempotencyRedisStore` reads these `IdempotencyOptions` (configured via the `config` delegate on
`AddIdempotencyWithRedisStore` / `AddIdempotentKey<IdempotencyRedisStore>`); the full option set lives in
[DKNet.AspCore.Idempotency](DKNet.AspCore.Idempotency.md):

| Option | Default | Used for |
|---|---|---|
| `CachePrefix` | `"idem"` | Prefix on every Redis key |
| `Expiration` | 4 hours | TTL on completed-response entries |
| `InFlightReservationTimeout` | 30 seconds | TTL on reservation entries |
| `JsonSerializerOptions` | camelCase | Serializing `CachedResponse` to/from the Redis string value |

## 🧱 Composing with the core package

This package only supplies the `IIdempotencyKeyStore` implementation. Key validation, the endpoint filter, conflict
handling (`ConflictHandling`), and scope resolution all come from `DKNet.AspCore.Idempotency` unchanged — see that
package's docs for the endpoint-level behavior this store plugs into.

## ⚠️ Gotchas

- No schema and no migrations, but also no relational query-ability: you cannot ad-hoc query cached responses by
  scope, endpoint, or date range as you could with a SQL table — Redis only supports lookup by the hashed key.
- Expiry is enforced by Redis's own TTL, not application code, so there's no cleanup job to host — but it also means
  you can't tweak retention for already-written entries without a manual `KeyDeleteAsync`/`redis-cli` operation.
- Redis availability is a hard dependency: every idempotency-protected endpoint call goes through
  `IIdempotencyKeyStore`, and this package has no in-memory or local fallback if Redis is unreachable.
