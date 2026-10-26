# Persist reservations and responses in SQL Server

Part of [DKNet.AspCore.Idempotency.MsSqlStore](index.md). Protect an endpoint after registering this provider:

```csharp
using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.MsSqlStore;
var builder = WebApplication.CreateBuilder(args);
var connection = builder.Configuration.GetConnectionString("Idempotency")
    ?? throw new InvalidOperationException("Set ConnectionStrings__Idempotency.");
builder.Services.AddIdempotencyWithMsSqlStore(connection);
var app = builder.Build();
app.MapPost("/orders", () => Results.Ok()).RequiredIdempotentKey();
await app.RunAsync();
```

![The SQL Server provider uses the shared insert-or-reclaim protocol and replaces the reservation with response data.](diagrams/persist-responses.svg)

The provider supplies the concrete context, migrations, body column type `nvarchar(max)`, and structured unique-violation classification: SqlException numbers 2601 or 2627. The [shared reservation protocol](../DKNet.AspCore.Idempotency.Relational/reserve-relational-keys.md) owns insert, duplicate lookup, expired-row reclaim, and completion. The [shared schema reference](../DKNet.AspCore.Idempotency.Relational/understand-relational-schema.md) owns the field and index definitions.

Migrations run during host startup through `IdempotencyMigrationHostedService<TContext>`. A defensive per-connection guard also checks pending migrations on first store use. The migrations history table is `migrate.IdempotencyDbContext`; the ledger table is `IdempotencyKeys`.

## ⚙️ Configuration reference

| Provider setting | Value | Effect |
|---|---|---|
| Design-time environment variable | `IDEMPOTENCY_MSSQL_CONNECTION` | Required by the internal EF design-time factory. |
| Migrations assembly | This provider assembly | Keeps migrations with the provider. |
| Migrations history | `migrate.IdempotencyDbContext` | Records applied provider migrations. |
| Retry policy | 3 retries, maximum 5-second delay | SQL Server transient retry configuration. |
| Query splitting | `SplitQuery` | SQL Server EF query option. |

## 🧱 Where it fits

The package overview shows the host registrations and dependencies behind this capability. The core filter and selected store have separate responsibilities.

![The consumer host selects this package’s components and supplies the runtime dependencies shown in the package architecture.](diagrams/idempotency-mssql-composition.svg)

## ⚠️ Gotchas & limits

- The database is external infrastructure supplied by the host operator; this package provisions no server.
- Expired rows remain until reclaimed or removed by your own cleanup. No automatic purge job is registered.
- The lookup hash is uppercase SHA-256, but raw key, route and method columns also persist.
- Ledger writes are separate from business writes. Expiry, data loss, or an overlong handler can permit re-execution.
- Startup requires database availability and migration permissions. Duplicate-check failures fail requests; completion failures are logged by the core filter.
