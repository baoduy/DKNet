# Register the PostgreSQL ledger

Part of [DKNet.AspCore.Idempotency.NpgsqlStore](index.md). `IdempotencyNpgsqlSetup` provides two application entry points:

```csharp
using DKNet.AspCore.Idempotency.NpgsqlStore;
using Microsoft.Extensions.DependencyInjection;
var services = new ServiceCollection();
services.AddLogging();
var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Idempotency")
    ?? throw new InvalidOperationException("Set ConnectionStrings__Idempotency.");
services.AddIdempotencyWithNpgsqlStore(connection);
```

![The host registers PostgreSQL infrastructure, then selects the internal provider store through the core registration.](diagrams/provider-registration.svg)

| Method | Registers |
|---|---|
| `AddIdempotencyNpgsqlStore(string)` | Provider context, context factory, and startup migration service only. |
| `AddIdempotencyWithNpgsqlStore(string, Action<IdempotencyOptions>?)` | The infrastructure above, plus the internal `IdempotencyPostgresStore` as singleton `IIdempotencyKeyStore`. |

The context is scoped, its options and factory are singleton, and each store operation creates its own context. The lower-level registration returns unchanged if its provider context is already registered; the first connection string therefore remains selected. Core store selection separately keeps the first explicit store, while allowing replacement of the in-memory default.

## ⚙️ Configuration reference

| Input | Type | Default | Effect |
|---|---|---|---|
| `connectionString` | `string` | required | Database used for provider-owned migrations and ledger rows. |
| `config` | `Action<IdempotencyOptions>?` | null | Configures [core validation](../DKNet.AspCore.Idempotency/validate-keys.md), [scope](../DKNet.AspCore.Idempotency/scope-callers.md), and [retention](../DKNet.AspCore.Idempotency/replay-responses.md). |

There is no provider-specific options class. `CachePrefix` does not isolate relational rows.

## 🧱 Where it fits

The package overview shows the host registrations and dependencies behind this capability. The core filter and selected store have separate responsibilities.

![The consumer host selects this package’s components and supplies the runtime dependencies shown in the package architecture.](diagrams/idempotency-npgsql-composition.svg)

## ⚠️ Gotchas & limits

- Null services and blank connection strings are rejected. The infrastructure method alone does not change the active key store.
- `IdempotencyPostgresStore` and its context are internal; use the public extension methods.
- A second explicit store registration does not replace the first store or run its configuration delegate.
- Sharing a database shares the ledger. Use distinct caller scope or endpoint identities where isolation is needed.
