# Choosing a store

Part of [DKNet.AspCore.Idempotency](index.md). Use the built-in store for a local demonstration, or select a provider that shares the ledger across application instances. No shipped implementation type is public.

```csharp
using DKNet.AspCore.Idempotency;
using Microsoft.Extensions.DependencyInjection;
var services = new ServiceCollection();
services.AddIdempotentKey(); // singleton, process-local ledger
```

![Registration uses the default only while no explicit store has been selected, and keeps the first explicitly selected store.](diagrams/store-selection.svg)

| Selection | Use when | Persistence and reservation |
|---|---|---|
| `AddIdempotentKey()` | Local development or unit tests | Concurrent dictionary; atomic within one process, lost on restart. |
| [MsSqlStore](../DKNet.AspCore.Idempotency.MsSqlStore/index.md) | The app uses SQL Server | Unique-index reservation and conditional expired-row reclaim. |
| [NpgsqlStore](../DKNet.AspCore.Idempotency.NpgsqlStore/index.md) | The app uses PostgreSQL | Same relational protocol with PostgreSQL error classification. |
| [RedisStore](../DKNet.AspCore.Idempotency.RedisStore/index.md) | The app uses Redis | Atomic `SET NX GET`, with TTLs. |
| [Relational](../DKNet.AspCore.Idempotency.Relational/index.md) | Reading the SQL implementation | Internal shared machinery; cannot be selected directly. |
| `AddIdempotentKey<TStore>()` | A custom backend is required | Your public implementation of `IIdempotencyKeyStore`. |

An explicit store replaces the built-in default in either registration order. Between explicit stores, the first wins, including its configuration delegate. A later default registration is a complete no-op. Replacing the default runs the explicit registration's configuration after any earlier default configuration.

The in-memory store logs one startup warning while it is the resolved store. It sweeps expired entries every 256 writes, and can reclaim an expired entry on access; this is not an immediate per-entry disposal timer.

## ⚙️ Configuration reference

| Option | Type | Default | Effect |
|---|---|---|---|
| `CachePrefix` | `string` | `idem` | Prepended to the SHA-256 key by memory and Redis stores; relational stores do not read it. Must be nonblank. |

All other settings are owned by [validation](validate-keys.md), [scope](scope-callers.md), and [retention](replay-responses.md).

## ⚠️ Gotchas & limits

- An in-memory ledger cannot coordinate replicas or survive restart. Use shared storage for those requirements.
- Provider infrastructure registration alone does not select a store; use `AddIdempotencyWith…Store`.
- SQL tables contain raw key, endpoint, and method columns alongside the hashed composite key. Hashing the lookup key does not make the whole ledger anonymous.
