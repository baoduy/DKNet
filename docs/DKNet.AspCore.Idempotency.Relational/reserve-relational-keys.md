# Reserve and complete relational keys

Part of [DKNet.AspCore.Idempotency.Relational](index.md). The internal `IdempotencyRelationalStore<TContext>` implements the public core contract. An application selects it through a provider; it cannot derive from the base itself.

```csharp
using DKNet.AspCore.Idempotency.NpgsqlStore;
using Microsoft.Extensions.DependencyInjection;
var services = new ServiceCollection();
services.AddLogging();
services.AddIdempotencyWithNpgsqlStore("Host=localhost;Database=idempotency;Username=app;Password=local-demo");
```

![The relational store inserts a reservation, reads a unique-index collision, and conditionally reclaims only an expired row.](diagrams/idempotency-relational-reserve.svg)

Each check creates a context from `IDbContextFactory<TContext>` and attempts to insert a status-102 reservation. A provider-classified unique violation triggers a lookup of the blocking row. A live row returns `(true, null)` while reserved, or the completed response. An expired row is reclaimed with an `ExecuteUpdateAsync` whose predicate still requires expiry; exactly one affected row grants the reservation. A losing reclaimer reads the current state and returns a duplicate.

Completion updates status, body, content type and expiry directly. It preserves the reservation's `CreatedAt`. If no row remains it attempts an insert; a classified duplicate on that fallback is logged and swallowed. Other database failures propagate to the caller.

The shared store owns an async DI scope for its lifetime and creates a fresh context per operation. It is disposed asynchronously with its scope.

## ⚙️ Configuration reference

The store reads `InFlightReservationTimeout` from the [core retention settings](../DKNet.AspCore.Idempotency/replay-responses.md). It does not read `CachePrefix`. The filter supplies completed-response expiry.

## 🧱 Where it fits

The package overview shows the host registrations and dependencies behind this capability. The core filter and selected store have separate responsibilities.

![The consumer host selects this package’s components and supplies the runtime dependencies shown in the package architecture.](diagrams/components.svg)

## ⚠️ Gotchas & limits

- `UX_CompositeKey` must exist in the database. It is the fresh-reservation concurrency guard.
- A handler can outlive its reservation. Completion is keyed by the composite hash and carries no owner token.
- A row may be reclaimed after expiry; it is not automatically deleted. There is no cleanup worker.
- The shared classes and provider contexts are internal. An external provider implements the core interface instead.
