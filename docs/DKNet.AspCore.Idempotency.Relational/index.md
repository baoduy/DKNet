# DKNet.AspCore.Idempotency.Relational

Internal EF Core storage machinery shared by the SQL Server and PostgreSQL idempotency providers.

## ✨ Why use it?

- Understand why SQL retries reserve through a unique index before handlers run.
- Inspect the shared row shape and expiry behavior used by both providers.
- Find which setup and migrations belong to the concrete provider.

## 🚀 Quick Start

This package is a transitive dependency of the SQL providers. Every type is internal; it exposes no application registration method. Install a provider to exercise it:

```bash
dotnet new web -f net10.0 -n PackageDemo
cd PackageDemo
dotnet add package DKNet.AspCore.Idempotency.Relational
dotnet add package DKNet.AspCore.Idempotency.NpgsqlStore
```

Set `ConnectionStrings__Idempotency` to a PostgreSQL connection string for a database the app can migrate. Then use this `Program.cs` and run `dotnet run`:

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

POST twice with `X-Idempotency-Key: order-1`: the first returns 200 and the retry 409. The provider applies its migrations on host start.

## 🏗️ Architecture

![Concrete SQL providers select the shared relational store, which creates contexts and reads or writes IdempotencyKeys.](diagrams/components.svg)

The public surface is empty. Application code registers a concrete provider; its singleton store holds a private async scope and uses a new EF context per operation.

## 🔄 How it works

![The host registers the SQL provider, applies pending migrations, then validates and reserves each protected request before running and retaining its result.](diagrams/request-flow.svg)

A successful insert returns new immediately. The unique-violation branch alone reads the blocking row. Neither expiry nor completion is a transaction with the application’s business writes.

## 🧩 Features

- [Reserve and complete relational keys](reserve-relational-keys.md) — Trace unique-index insertion, collision handling, and expired-row reclaim.
- [Understand schema and migrations](understand-relational-schema.md) — Inspect shared columns, provider mappings, and migration ownership.

## 🔗 Related packages

- [Idempotency](../DKNet.AspCore.Idempotency/index.md) — begin here for endpoint behavior and public contracts.
- [MsSqlStore](../DKNet.AspCore.Idempotency.MsSqlStore/index.md) — select the SQL Server implementation.
- [NpgsqlStore](../DKNet.AspCore.Idempotency.NpgsqlStore/index.md) — select the PostgreSQL implementation.
- [RedisStore](../DKNet.AspCore.Idempotency.RedisStore/index.md) — select a backend independent of this base.
