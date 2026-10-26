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

## Documentation

| Guide | Read when |
|---|---|
| [Package overview](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.AspCore.Idempotency/index.md) | Start with architecture and the end-to-end flow. |
| [Protect endpoints and route groups](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.AspCore.Idempotency/protect-endpoints.md) | Choose which individual endpoints and group verbs require keys. |
| [Validate client keys](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.AspCore.Idempotency/validate-keys.md) | Set the header name, accepted length, and bounded regular expression. |
| [Isolate keys by caller](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.AspCore.Idempotency/scope-callers.md) | Choose caller isolation and understand route-template key identity. |
| [Choose duplicate responses and retention](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.AspCore.Idempotency/replay-responses.md) | Choose 409 versus replay and configure reservation and result lifetimes. |
| [Choosing a store](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.AspCore.Idempotency/choose-store.md) | Compare backends and understand first-explicit-store registration. |
| [Implement a custom reservation store](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.AspCore.Idempotency/implement-store.md) | Implement the atomic reservation and completion contract. |
