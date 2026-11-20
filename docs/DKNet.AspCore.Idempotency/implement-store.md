# Implement a custom reservation store

Part of [DKNet.AspCore.Idempotency](index.md). Implement `IIdempotencyKeyStore` directly; the shared relational classes are internal. A minimal in-process store, atomic through `ConcurrentDictionary.TryAdd`:

```csharp
using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.Filtering;
using DKNet.AspCore.Idempotency.Store;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

var services = new ServiceCollection();
services.AddIdempotentKey<DemoKeyStore>();
Console.WriteLine(services.Count(d => d.ServiceType == typeof(IIdempotencyKeyStore))); // 1

public sealed class DemoKeyStore : IIdempotencyKeyStore
{
    private readonly ConcurrentDictionary<string, CachedResponse> _entries = new();

    public ValueTask<(bool processed, CachedResponse? response)> IsKeyProcessedAsync(IdempotentKeyInfo keyInfo)
    {
        var reservation = new CachedResponse
        {
            StatusCode = 102, Body = null, ContentType = "text/plain",
            CreatedAt = DateTimeOffset.UtcNow, ExpiresAt = null
        };

        // TryAdd is the atomic reserve: exactly one caller wins a brand-new key.
        if (_entries.TryAdd(keyInfo.CompositeKey, reservation))
            return ValueTask.FromResult<(bool, CachedResponse?)>((false, null));

        var existing = _entries[keyInfo.CompositeKey];
        return ValueTask.FromResult<(bool, CachedResponse?)>(
            existing.StatusCode == 102 ? (true, null) : (true, existing));
    }

    public ValueTask MarkKeyAsProcessedAsync(IdempotentKeyInfo keyInfo, CachedResponse cachedResponse)
    {
        _entries[keyInfo.CompositeKey] = cachedResponse;
        return ValueTask.CompletedTask;
    }
}
```

`IdempotentKeyInfo.CompositeKey` is the identity your store receives: `Scope:Method:Endpoint:IdempotentKey`. `CachedResponse` requires `StatusCode`, nullable `Body`, `ContentType`, `CreatedAt`, and nullable `ExpiresAt` at construction; its derived `IsExpired` compares the non-null expiry against UTC now. The filter validates and scopes the key before handing it to the store, so a store never sees an unvalidated or unscoped identity.

![A custom store must reserve atomically before reporting a new key, then replace that reservation with completion data.](diagrams/custom-reservation.svg)

## ⚙️ Configuration reference

| Method | Return type | Required behavior |
|---|---|---|
| `IsKeyProcessedAsync(IdempotentKeyInfo)` | `ValueTask<(bool processed, CachedResponse? response)>` | Atomically reserve before returning `(false, null)`. Return `(true, null)` while in flight, or `(true, response)` after completion. |
| `MarkKeyAsProcessedAsync(IdempotentKeyInfo, CachedResponse)` | `ValueTask` | Replace the reservation with completion data and honor the intended retention window. |

| Override point | How you customise it |
|---|---|
| `IIdempotencyKeyStore` | Implement both methods on your own class and register it with `services.AddIdempotentKey<TYourStore>()`. It replaces the in-memory default, or is a no-op if another explicit store already won. |

Use a bounded in-flight reservation, and make reclaim atomic. All shipped stores use status 102 as the reservation sentinel and SHA-256 over `CompositeKey`. A separate read followed by an unconditional write admits two winners.

## 🧱 Where it fits

| DKNet consumer | Package | What it does with this feature |
|---|---|---|
| Endpoint filter (internal; shown on [Protect endpoints and route groups](protect-endpoints.md)) | DKNet.AspCore.Idempotency | Resolves the registered `IIdempotencyKeyStore` and calls `IsKeyProcessedAsync` then `MarkKeyAsProcessedAsync` around each protected handler. |
| `IdempotencyRelationalStore<TContext>` | [Relational](../DKNet.AspCore.Idempotency.Relational/reserve-relational-keys.md) | Shared base implementation; MsSqlStore and NpgsqlStore extend it instead of implementing the interface directly. |
| `IdempotencySqlServerStore` | [MsSqlStore](../DKNet.AspCore.Idempotency.MsSqlStore/persist-responses.md) | Implements the contract via the relational base, backed by SQL Server. |
| `IdempotencyPostgresStore` | [NpgsqlStore](../DKNet.AspCore.Idempotency.NpgsqlStore/persist-responses.md) | Implements the contract via the relational base, backed by PostgreSQL. |
| `IdempotencyRedisStore` | [RedisStore](../DKNet.AspCore.Idempotency.RedisStore/reserve-redis-keys.md) | Implements the contract directly, backed by Redis `SET NX GET`. |

**Entry point in your app:** `Program.cs`, via `services.AddIdempotentKey<TYourStore>()` with your own public implementation.

## ⚠️ Gotchas & limits

- Register your implementation with `AddIdempotentKey<TStore>()`. It is singleton and must support concurrent calls.
- The interface supplies no cancellation token, transaction, release operation, or reservation ownership token.
- Hash the full composite identity rather than stripping or truncating it. Never use `SafeKey` as storage identity.
- Retention and duplicate behavior must be tested against the actual backing store; interface conformance alone proves no atomicity.
