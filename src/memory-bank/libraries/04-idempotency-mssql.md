# DKNet.AspCore.Idempotency.MsSqlStore — AI Skill File

> **Package**: `DKNet.AspCore.Idempotency.MsSqlStore`  
> **Minimum Version**: `10.0.0`  
> **Minimum .NET**: `net10.0`  
> **Source**: `src/AspNet/DKNet.AspCore.Idempotency.MsSqlStore/`

---

## Purpose

Provides a SQL Server-backed persistent store for `DKNet.AspCore.Idempotency`, enabling durable idempotency keys that survive application restarts and work correctly in multi-instance (load-balanced) deployments.

---

## When To Use

- ✅ Any production deployment with more than one application instance
- ✅ When idempotency keys must survive application restarts
- ✅ When using Azure SQL, SQL Server on-premises, or Azure SQL Managed Instance

## When NOT To Use

- ❌ Local development / single-instance dev environments — use the default in-memory cache from `AddDistributedMemoryCache()`
- ❌ When your infrastructure already has Redis — prefer a Redis-backed distributed cache instead
- ❌ Without first registering `DKNet.AspCore.Idempotency` — this package is a storage plugin, not standalone

---

## Installation

```bash
dotnet add package DKNet.AspCore.Idempotency.MsSqlStore
```

---

## Setup / DI Registration

```csharp
// Program.cs
using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.MsSqlStore;

// Register SQL Server idempotency store (replaces AddDistributedMemoryCache)
services.AddIdempotencyMsSqlStore(
    configuration.GetConnectionString("Default")!);

// Register idempotency services (same as always)
services.AddIdempotency();
```

The store auto-creates the required table on first use (no migrations needed).

---

## Key API Surface

| Method | Role |
|---|---|
| `services.AddIdempotencyMsSqlStore(connectionString)` | Register SQL Server idempotency storage |

---

## Usage Pattern

```csharp
// appsettings.json
{
  "ConnectionStrings": {
    "Default": "Server=...;Database=MyApp;Integrated Security=true;"
  }
}

// Program.cs
builder.Services.AddIdempotencyMsSqlStore(
    builder.Configuration.GetConnectionString("Default")!);
builder.Services.AddIdempotency(options =>
{
    options.Expiration = TimeSpan.FromHours(24);  // longer TTL for durable store
});

// Endpoint — unchanged; store is transparent
app.MapPost<CreateOrderCommand, OrderDto>("/orders")
   .WithIdempotency();
```

---

## Anti-Patterns

```csharp
// ❌ WRONG — hardcoding connection string
services.AddIdempotencyMsSqlStore("Server=prod-sql;Password=secret;");

// ✅ CORRECT — read from configuration
services.AddIdempotencyMsSqlStore(
    configuration.GetConnectionString("Default")!);

// ❌ WRONG — registering both AddDistributedMemoryCache AND AddIdempotencyMsSqlStore
services.AddDistributedMemoryCache();          // ← conflicts with the SQL store
services.AddIdempotencyMsSqlStore(connStr);

// ✅ CORRECT — choose one store
services.AddIdempotencyMsSqlStore(connStr);    // SQL store IS the distributed cache backend
```

---

## Composes With

| Library | How |
|---|---|
| `DKNet.AspCore.Idempotency` | **Required** — this package only provides the storage backend |

---

## Security Notes

- Connection string MUST come from `IConfiguration` / environment variables / Azure Key Vault — never hardcoded.
- The SQL table stores idempotency keys and serialized response bodies. Ensure your database access credentials follow least-privilege (read+write on the idempotency table only).
- Idempotency response data may contain PII — apply the same data retention policy as your main database.

---

## Test Example

```csharp
// Integration test — uses TestContainers SQL Server
public class MsSqlIdempotencyStoreTests : IAsyncLifetime
{
    private MsSqlContainer? _container;
    private WebApplicationFactory<Program>? _factory;

    public async Task InitializeAsync()
    {
        _container = new MsSqlBuilder().WithPassword("YourStrong@Passw0rd1").Build();
        await _container.StartAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
            host.ConfigureServices(services =>
            {
                services.AddIdempotencyMsSqlStore(_container.GetConnectionString());
                services.AddIdempotency();
            }));
    }

    [Fact]
    public async Task DuplicatePost_WithSqlStore_ReturnsCachedResponse()
    {
        var client = _factory!.CreateClient();
        var key = Guid.NewGuid().ToString();
        var req = () => new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders")
        {
            Content = JsonContent.Create(new CreateOrderCommand(Guid.NewGuid(), 1)),
            Headers = { { "Idempotency-Key", key } }
        };

        var r1 = await client.SendAsync(req());
        var r2 = await client.SendAsync(req());

        r1.StatusCode.ShouldBe(HttpStatusCode.Created);
        r2.StatusCode.ShouldBe(HttpStatusCode.Created);
        var d1 = await r1.Content.ReadFromJsonAsync<OrderDto>();
        var d2 = await r2.Content.ReadFromJsonAsync<OrderDto>();
        d1!.Id.ShouldBe(d2!.Id);
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null) await _factory.DisposeAsync();
        if (_container is not null) await _container.DisposeAsync();
    }
}
```

---

## Quick Decision Guide

- Use this package when idempotency state must survive restarts.
- Prefer this package for horizontal scale (multi-instance APIs).
- Keep in-memory store only for local development or ephemeral test runs.

---

## Version

| Version | Notes |
|---|---|
| `10.0.0` | Initial documentation |
