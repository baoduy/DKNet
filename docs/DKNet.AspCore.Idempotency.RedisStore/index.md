# DKNet.AspCore.Idempotency.RedisStore

Redis-backed reservations and TTL-based response retention for .NET developers using DKNet idempotency.

## ✨ Why use it?

- Share a key ledger across hosts connected to the same Redis database.
- Reserve keys atomically without relational schema migrations.
- Expire reservations and completed values through Redis TTLs.

## 🚀 Quick Start

Use a .NET 10 ASP.NET Core project.

```bash
dotnet new web -f net10.0 -n PackageDemo
cd PackageDemo
dotnet add package DKNet.AspCore.Idempotency.RedisStore
```

Replace `Program.cs` with the example, then run `dotnet run`.

Set `ConnectionStrings__Redis` to a reachable Redis connection string. The Redis server must support the atomic `SET NX GET` operation used by StackExchange.Redis.

```csharp
using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.RedisStore;
var builder = WebApplication.CreateBuilder(args);
var connection = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("Set ConnectionStrings__Redis.");
builder.Services.AddIdempotencyWithRedisStore(connection);
var app = builder.Build();
app.MapPost("/orders", () => Results.Ok(new { Accepted = true }))
    .RequiredIdempotentKey();
await app.RunAsync();
```

POST twice with `X-Idempotency-Key: order-1`. The first returns 200 and the retry 409. Instances sharing this Redis database and prefix share the ledger.

## 🏗️ Architecture

![The host selects a singleton Redis key store that uses an IConnectionMultiplexer and IDatabase directly.](diagrams/components.svg)

The app supplies reachable Redis infrastructure. Existing multiplexer and distributed-cache registrations are retained independently; the store uses the multiplexer directly. No relational base, context or migration service participates.

## 🔄 How it works

![A protected request validates its key, reserves in Redis, invokes the endpoint and writes eligible completion data; duplicates stop before the handler.](diagrams/request-flow.svg)

The core filter decides 409 versus replay. Reservations expire separately from completed entries. Redis loss or eviction and handlers exceeding the reservation lifetime can permit another execution.

## 🧩 Features

- [Register Redis connections and select the store](register-redis-store.md) — Reuse Redis connections and select the internal store.
- [Reserve Redis keys and retain response values](reserve-redis-keys.md) — Trace atomic set-and-get reservation, logical expiry, and TTL writes.

## 🔗 Related packages

- [Idempotency](../DKNet.AspCore.Idempotency/index.md) — configure declaration, validation and replay.
- [MsSqlStore](../DKNet.AspCore.Idempotency.MsSqlStore/index.md) — choose a SQL Server ledger.
- [NpgsqlStore](../DKNet.AspCore.Idempotency.NpgsqlStore/index.md) — choose PostgreSQL.
- [Relational](../DKNet.AspCore.Idempotency.Relational/index.md) — read the shared SQL implementation, which Redis does not use.
