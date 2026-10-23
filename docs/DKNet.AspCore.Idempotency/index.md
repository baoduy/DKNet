# DKNet.AspCore.Idempotency

Bounded idempotency reservations and optional response replay for .NET developers building minimal APIs.

## ✨ Why use it?

- Stop concurrent retries from reaching the same handler while a reservation is live.
- Scope client keys by caller, verb, and route template.
- Start locally, then select a shared SQL or Redis ledger without changing endpoint declarations.

## 🚀 Quick Start

Use a .NET 10 ASP.NET Core project.

```bash
dotnet new web -f net10.0 -n PackageDemo
cd PackageDemo
dotnet add package DKNet.AspCore.Idempotency
```

Replace `Program.cs` with the example, then run `dotnet run`.

```csharp
using DKNet.AspCore.Idempotency;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddIdempotentKey();
var app = builder.Build();
app.MapPost("/orders", () => Results.Ok(new { Accepted = true }))
    .RequiredIdempotentKey();
await app.RunAsync();
```

Send `POST /orders` with `X-Idempotency-Key: order-1`. The first call returns 200; an immediate retry returns 409. A missing header returns 400. This example keeps keys in one process; use a provider for shared storage.

## 🏗️ Architecture

![A protected route uses the endpoint filter, host options and singleton store before invoking its handler.](diagrams/components.svg)

The filter runs inside the host. The default store owns one process-local dictionary; provider registrations replace it with a singleton adapter to shared storage. Business data writes remain separate from the idempotency ledger.

## 🔄 How it works

![The filter validates a key, reserves it, invokes a new operation, and retains eligible results; duplicates return 409 or replay.](diagrams/idempotency-request-flow.svg)

Invalid keys return 400 before storage. A duplicate in flight always returns 409. The reservation is time-bounded, so a long-running handler can outlive it; successful completion does not create an exactly-once business transaction.

## 🧩 Features

- [Protect endpoints and route groups](protect-endpoints.md) — Choose which individual endpoints and group verbs require keys.
- [Validate client keys](validate-keys.md) — Set the header name, accepted length, and bounded regular expression.
- [Isolate keys by caller](scope-callers.md) — Choose caller isolation and understand route-template key identity.
- [Choose duplicate responses and retention](replay-responses.md) — Choose 409 versus replay and configure reservation and result lifetimes.
- [Choosing a store](choose-store.md) — Compare backends and understand first-explicit-store registration.
- [Implement a custom reservation store](implement-store.md) — Implement the atomic reservation and completion contract.

## 🔗 Related packages

- [MsSqlStore](../DKNet.AspCore.Idempotency.MsSqlStore/index.md) — choose it for a SQL Server ledger.
- [NpgsqlStore](../DKNet.AspCore.Idempotency.NpgsqlStore/index.md) — choose it for PostgreSQL.
- [RedisStore](../DKNet.AspCore.Idempotency.RedisStore/index.md) — choose it for Redis TTLs.
- [Relational](../DKNet.AspCore.Idempotency.Relational/index.md) — read the shared SQL protocol.
- [Tasks](../DKNet.AspCore.Tasks/index.md) — use it for work triggered at host start.
- [Extensions](../DKNet.AspCore.Extensions/index.md) — use it for endpoint discovery and request helpers.
