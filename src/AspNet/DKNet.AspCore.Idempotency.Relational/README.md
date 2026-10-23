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

## Documentation

| Guide | Read when |
|---|---|
| [Package overview](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.AspCore.Idempotency.Relational/index.md) | Start with architecture and the end-to-end flow. |
| [Reserve and complete relational keys](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.AspCore.Idempotency.Relational/reserve-relational-keys.md) | Trace unique-index insertion, collision handling, and expired-row reclaim. |
| [Understand schema and migrations](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.AspCore.Idempotency.Relational/understand-relational-schema.md) | Inspect shared columns, provider mappings, and migration ownership. |
