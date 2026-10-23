# Register Redis connections and select the store

Part of [DKNet.AspCore.Idempotency.RedisStore](index.md). Use the combined registration to select the store, not just its infrastructure:

```csharp
using DKNet.AspCore.Idempotency.RedisStore;
using Microsoft.Extensions.DependencyInjection;
var services = new ServiceCollection();
services.AddLogging();
services.AddIdempotencyWithRedisStore("localhost:6379", o => o.CachePrefix = "orders:idem:");
Console.WriteLine("Redis services registered; connection opens when resolved.");
```

![The string registration independently keeps existing distributed-cache and multiplexer services before selecting the core store.](diagrams/redis-registration.svg)

`IdempotencyRedisSetup` is the sole public type in this package:

| Method | Registers |
|---|---|
| `AddIdempotencyRedisStore(string)` | `IDistributedCache` through `AddStackExchangeRedisCache` and singleton `IConnectionMultiplexer` through a `ConnectionMultiplexer.Connect` factory, each only if absent. |
| `AddIdempotencyRedisStore(IConnectionMultiplexer)` | Supplied multiplexer as singleton only if absent; no distributed cache. |
| `AddIdempotencyWithRedisStore(string, Action<IdempotencyOptions>?)` | The string infrastructure overload, then the internal store through core `AddIdempotentKey<TStore>`. |

Register an existing multiplexer first if it should win; the combined call then reuses it, although it still registers `IDistributedCache` if that contract is absent. The store talks directly to `multiplexer.GetDatabase()`, not the distributed-cache abstraction.

## ⚙️ Configuration reference

The combined callback configures [core key validation](../DKNet.AspCore.Idempotency/validate-keys.md), [scope](../DKNet.AspCore.Idempotency/scope-callers.md), and [retention](../DKNet.AspCore.Idempotency/replay-responses.md). There is no Redis-specific options class.

## 🧱 Where it fits

The package overview shows the host registrations and dependencies behind this capability. The core filter and selected store have separate responsibilities.

![The consumer host selects this package’s components and supplies the runtime dependencies shown in the package architecture.](diagrams/components.svg)

## ⚠️ Gotchas & limits

- Null services/multiplexer and blank connection strings are rejected.
- Existing infrastructure registrations remain first-wins independently. The first explicit core store also wins over later explicit store choices.
- Supplying infrastructure alone does not select the internal Redis key store.
- Redis availability, memory capacity and eviction policy are external dependencies. An evicted key can be treated as new.
- The string registration does not open the store’s multiplexer until its singleton factory is resolved; connection errors can surface then.
