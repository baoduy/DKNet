# DKNet.AspCore.Idempotency.NpgsqlStore

[![NuGet](https://img.shields.io/nuget/v/DKNet.AspCore.Idempotency.NpgsqlStore.svg)](https://www.nuget.org/packages/DKNet.AspCore.Idempotency.NpgsqlStore/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

PostgreSQL-backed persistent store for `DKNet.AspCore.Idempotency`, built on the shared EF Core relational
store (`DKNet.AspCore.Idempotency.Relational`).

## ✨ Why use it?

- **Persistent** – idempotency keys live in a Postgres `IdempotencyKeys` table, surviving app restarts.
- **Atomic under concurrency** – a unique index on the composite key serializes duplicate requests in the
  database itself; no application-level locking needed.
- **Migrations included** – ships with its own EF Core migration and applies it automatically on first use,
  independently per connection string.
- **Multi-database ready** – register the store against several Postgres databases (e.g. per tenant) from the
  same process; each one is prepared on its own.
- **Npgsql-tuned** – retry-on-failure and split-query behaviour are configured out of the box.

## 🚀 Quick Start

```bash
dotnet add package DKNet.AspCore.Idempotency.NpgsqlStore
```

```csharp
using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.NpgsqlStore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddIdempotencyWithNpgsqlStore(
    builder.Configuration.GetConnectionString("IdempotencyDb")!,
    options =>
    {
        options.Expiration = TimeSpan.FromHours(48);
        options.ConflictHandling = IdempotentConflictHandling.CachedResult;
    });

var app = builder.Build();

app.MapPost("/orders", CreateOrderAsync)
    .RequiredIdempotentKey();

app.Run();
```

## 📖 Documentation

Full guide — Postgres schema, concurrency behaviour, multi-database support, and how this store composes with
the core and relational packages:
[DKNet.AspCore.Idempotency.NpgsqlStore.md](https://github.com/baoduy/DKNet/blob/main/docs/AspNetCore/DKNet.AspCore.Idempotency.NpgsqlStore.md)

## License

MIT — see [LICENSE](https://opensource.org/licenses/MIT).
