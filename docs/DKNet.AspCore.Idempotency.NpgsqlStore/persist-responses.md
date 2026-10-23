# Persist reservations and responses in PostgreSQL

Part of [DKNet.AspCore.Idempotency.NpgsqlStore](index.md). Protect an endpoint after registering this provider:

```csharp
using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.NpgsqlStore;
var builder = WebApplication.CreateBuilder(args);
var connection = builder.Configuration.GetConnectionString("Idempotency")
    ?? throw new InvalidOperationException("Set ConnectionStrings__Idempotency.");
builder.Services.AddIdempotencyWithNpgsqlStore(connection);
var app = builder.Build();
app.MapPost("/orders", () => Results.Ok()).RequiredIdempotentKey();
await app.RunAsync();
```

![The PostgreSQL provider uses the shared insert-or-reclaim protocol and replaces the reservation with response data.](diagrams/persist-responses.svg)

The provider supplies the concrete context, migrations, body column type `text`, and structured unique-violation classification: PostgresException SQL state 23505. The [shared reservation protocol](../DKNet.AspCore.Idempotency.Relational/reserve-relational-keys.md) owns insert, duplicate lookup, expired-row reclaim, and completion. The [shared schema reference](../DKNet.AspCore.Idempotency.Relational/understand-relational-schema.md) owns the field and index definitions.

Migrations run during host startup through `IdempotencyMigrationHostedService<TContext>`. A defensive per-connection guard also checks pending migrations on first store use. The migrations history table is `migrate.IdempotencyDbContext`; the ledger table is `IdempotencyKeys`.

## ⚙️ Configuration reference

| Provider setting | Value | Effect |
|---|---|---|
| Design-time environment variable | `IDEMPOTENCY_NPGSQL_CONNECTION` | Required by the internal EF design-time factory. |
| Migrations assembly | This provider assembly | Keeps migrations with the provider. |
| Migrations history | `migrate.IdempotencyDbContext` | Records applied provider migrations. |

## 🧱 Where it fits

The package overview shows the host registrations and dependencies behind this capability. The core filter and selected store have separate responsibilities.

![The consumer host selects this package’s components and supplies the runtime dependencies shown in the package architecture.](diagrams/idempotency-npgsql-composition.svg)

## ⚠️ Gotchas & limits

- The database is external infrastructure supplied by the host operator; this package provisions no server.
- Expired rows remain until reclaimed or removed by your own cleanup. No automatic purge job is registered.
- The lookup hash is uppercase SHA-256, but raw key, route and method columns also persist.
- Ledger writes are separate from business writes. Expiry, data loss, or an overlong handler can permit re-execution.
- Startup requires database availability and migration permissions. Duplicate-check failures fail requests; completion failures are logged by the core filter.
