# DKNet.AspCore.Idempotency.MsSqlStore

A SQL Server ledger for .NET developers using DKNet minimal-API idempotency.

## ✨ Why use it?

- Share reservations across hosts using the same SQL Server database.
- Keep completed response data in a queryable table.
- Use the provider’s migrations and structured duplicate-key classification.

## 🚀 Quick Start

Use a .NET 10 ASP.NET Core project.

```bash
dotnet new web -f net10.0 -n PackageDemo
cd PackageDemo
dotnet add package DKNet.AspCore.Idempotency.MsSqlStore
```

Replace `Program.cs` with the example, then run `dotnet run`.

Set `ConnectionStrings__Idempotency` to a reachable SQL Server database connection string. The account must be able to apply the packaged migrations.

```csharp
using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.MsSqlStore;
var builder = WebApplication.CreateBuilder(args);
var connection = builder.Configuration.GetConnectionString("Idempotency")
    ?? throw new InvalidOperationException("Set ConnectionStrings__Idempotency.");
builder.Services.AddIdempotencyWithMsSqlStore(connection);
var app = builder.Build();
app.MapPost("/orders", () => Results.Ok(new { Accepted = true }))
    .RequiredIdempotentKey();
await app.RunAsync();
```

POST twice with `X-Idempotency-Key: order-1`. The first returns 200 and the retry 409. The ledger is shared by hosts using this database.

## 🏗️ Architecture

![The host selects the SQL Server store, whose context factory serves ledger operations and startup migrations.](diagrams/idempotency-mssql-composition.svg)

The provider registers a singleton store and factory, with fresh contexts per operation. The database is supplied by the application operator; startup applies this provider’s migrations.

## 🔄 How it works

![The host registers the SQL provider, applies pending migrations, then validates and reserves each protected request before running and retaining its result.](diagrams/request-flow.svg)

A live collision is a duplicate; an expired collision can be reclaimed by a conditional update. The core filter decides whether a result is cacheable. Expired rows are not automatically purged.

## 🧩 Features

- [Register the SQL Server ledger](register-store.md) — Register provider infrastructure and select the active key store.
- [Persist reservations and responses in SQL Server](persist-responses.md) — Trace provider persistence and find migration/tooling settings.

## 🔗 Related packages

- [Idempotency](../DKNet.AspCore.Idempotency/index.md) — configure filters, keys and duplicate responses.
- [Relational](../DKNet.AspCore.Idempotency.Relational/index.md) — inspect shared schema and concurrency.
- [NpgsqlStore](../DKNet.AspCore.Idempotency.NpgsqlStore/index.md) — use the other SQL provider.
- [RedisStore](../DKNet.AspCore.Idempotency.RedisStore/index.md) — use Redis TTLs instead of SQL rows.
