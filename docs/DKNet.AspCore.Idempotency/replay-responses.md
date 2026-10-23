# Choose duplicate responses and retention

Part of [DKNet.AspCore.Idempotency](index.md). The default strategy returns 409 for any duplicate. Opt into replay to return the stored body, content type, and status when completion data exists:

```csharp
using DKNet.AspCore.Idempotency;
using Microsoft.Extensions.DependencyInjection;
var services = new ServiceCollection();
services.AddIdempotentKey(o =>
{
    o.ConflictHandling = IdempotentConflictHandling.CachedResult;
    o.Expiration = TimeSpan.FromHours(24);
});
```

![A duplicate either returns 409 or replays a completed response; a new request stores only a cacheable handler result.](diagrams/response-retention.svg)

An in-flight duplicate still gets 409 in replay mode because it has no completed response. After the handler returns, the filter reads its `IStatusCodeHttpResult` and `IValueHttpResult` metadata where available, serializes the value, and records a response only for eligible status codes. Replay uses `TypedResults.Text` with those stored bytes as UTF-8 text.

## ⚙️ Configuration reference

| Option | Type | Default | Effect |
|---|---|---|---|
| `ConflictHandling` | `IdempotentConflictHandling` | `ConflictResponse` | 409 or `CachedResult` replay. |
| `Expiration` | `TimeSpan` | 4 hours | Lifetime of completed entries; must be positive. |
| `InFlightReservationTimeout` | `TimeSpan` | 30 seconds | Reservation lifetime before a key can be reclaimed. |
| `MinStatusCodeForCaching` | `int` | 200 | Inclusive range start, at least 100. |
| `MaxStatusCodeForCaching` | `int` | 299 | Inclusive range end, at most 599 and at least the minimum. |
| `AdditionalCacheableStatusCodes` | `HashSet<int>` | empty | Additional accepted status codes. |
| `JsonSerializerOptions` | `JsonSerializerOptions` | camelCase naming | Value serialization; cannot be null. |

These options are configured through the core or selected provider registration. Startup validators are added once. The reservation timeout has no positive-value validator in this registration.

## ⚠️ Gotchas & limits

- Noncacheable results, thrown handlers, and failed serialization leave the reservation until it expires; immediate retries can still get 409.
- A handler running longer than the reservation window can overlap a later retry. There is no lease renewal or fencing token.
- Caching errors are caught and logged; the original result still returns. Lookup failures are not caught by this filter.
- This is value serialization before result execution, not a wire-response capture. Arbitrary headers, cookies, streams, and `Location` are not replayed; content type falls back to `application/json`.
- Idempotency is bounded by retention and storage availability. It is not an exactly-once transaction with business data.
