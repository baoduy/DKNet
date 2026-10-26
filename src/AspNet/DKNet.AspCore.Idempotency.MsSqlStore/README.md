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

## Documentation

| Guide | Read when |
|---|---|
| [Package overview](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.AspCore.Idempotency.MsSqlStore/index.md) | Start with architecture and the end-to-end flow. |
| [Register the SQL Server ledger](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.AspCore.Idempotency.MsSqlStore/register-store.md) | Register provider infrastructure and select the active key store. |
| [Persist reservations and responses in SQL Server](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.AspCore.Idempotency.MsSqlStore/persist-responses.md) | Trace provider persistence and find migration/tooling settings. |
