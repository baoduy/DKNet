# Implement a custom reservation store

Part of [DKNet.AspCore.Idempotency](index.md). Implement `IIdempotencyKeyStore` directly; the shared relational classes are internal. These are the two methods your implementation must supply:

```csharp
using DKNet.AspCore.Idempotency.Filtering;
using DKNet.AspCore.Idempotency.Store;
Console.WriteLine(typeof(IIdempotencyKeyStore).GetMethods().Length); // 2
var info = new IdempotentKeyInfo
{
    Endpoint = "/ORDERS", Method = "POST", IdempotentKey = "order-1"
};
Console.WriteLine(info.CompositeKey); // :POST:/ORDERS:order-1
```

![A custom store must reserve atomically before reporting a new key, then replace that reservation with completion data.](diagrams/custom-reservation.svg)

| Method | Required behavior |
|---|---|
| `IsKeyProcessedAsync(IdempotentKeyInfo)` | Atomically reserve before returning `(false, null)`. Return `(true, null)` while in flight, or `(true, response)` after completion. |
| `MarkKeyAsProcessedAsync(IdempotentKeyInfo, CachedResponse)` | Replace the reservation with completion data and honor the intended retention window. |

Use a bounded in-flight reservation, and make reclaim atomic. All shipped stores use status 102 as the reservation sentinel and SHA-256 over `CompositeKey`. A separate read followed by an unconditional write admits two winners.

`CachedResponse` requires `StatusCode`, nullable `Body`, `ContentType`, `CreatedAt`, and nullable `ExpiresAt` at construction. Its derived `IsExpired` compares the non-null expiry against UTC now. `IdempotentKeyInfo` requires `Endpoint` and `Method`; `Scope` defaults empty and `IdempotentKey` is nullable. The filter validates the key before handing it to the store.

## ⚠️ Gotchas & limits

- Register your implementation with `AddIdempotentKey<TStore>()`. It is singleton and must support concurrent calls.
- The interface supplies no cancellation token, transaction, release operation, or reservation ownership token.
- Hash the full composite identity rather than stripping or truncating it. Never use `SafeKey` as storage identity.
- Retention and duplicate behavior must be tested against the actual backing store; interface conformance alone proves no atomicity.
