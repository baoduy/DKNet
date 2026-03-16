# DKNet.AspCore.Idempotency — AI Skill File

> **Package**: `DKNet.AspCore.Idempotency`  
> **Minimum Version**: `10.0.0`  
> **Minimum .NET**: `net10.0`  
> **Source**: `src/AspNet/DKNet.AspCore.Idempotency/`

---

## Purpose

Prevents duplicate processing of state-mutating HTTP requests (POST, PUT, PATCH, DELETE) by caching the first successful response keyed on an idempotency header and returning it verbatim for subsequent requests with the same key.

---

## When To Use

- ✅ Any POST endpoint that creates a resource (payment, order, user registration)
- ✅ Any PUT / PATCH that modifies financial or audit-sensitive state
- ✅ Any endpoint that is called from a client with automatic retry logic

## When NOT To Use

- ❌ GET endpoints — they are naturally idempotent; do not add this filter
- ❌ Endpoints where replaying the last response would be semantically wrong (e.g., endpoints that intentionally allow duplicate inserts)
- ❌ Replacing distributed locking for concurrent-write scenarios — use `AsyncKeyedLock` or a database-level lock for that

---

## Installation

```bash
dotnet add package DKNet.AspCore.Idempotency
```

For SQL Server persistence (production):

```bash
dotnet add package DKNet.AspCore.Idempotency.MsSqlStore
```

---

## Setup / DI Registration

```csharp
// Program.cs
using DKNet.AspCore.Idempotency;

// Required: distributed cache (in-memory for dev, Redis/SQL for prod)
services.AddDistributedMemoryCache();   // dev only
// OR: services.AddStackExchangeRedisCache(...);

// Register idempotency services
services.AddIdempotency(options =>
{
    options.HeaderName = "Idempotency-Key";   // default
    options.Expiration = TimeSpan.FromHours(4); // default
    // options.OnConflict = ConflictStrategy.ReturnCached; // default
});
```

---

## Key API Surface

| Type / Method | Role |
|---|---|
| `services.AddIdempotency(options)` | Register idempotency services |
| `IdempotencyOptions.HeaderName` | HTTP header the client sends (default: `Idempotency-Key`) |
| `IdempotencyOptions.Expiration` | How long the cached response is kept |
| `IdempotencyOptions.OnConflict` | `ReturnCached` (default) or `ReturnConflict` (409) |
| `routeBuilder.WithIdempotency()` | Apply idempotency filter to an endpoint |

---

## Usage Pattern

```csharp
// Program.cs — service registration
services.AddDistributedMemoryCache();
services.AddIdempotency();   // uses defaults

// Endpoint — apply .WithIdempotency() after the mapper
app.MapPost<CreateOrderCommand, OrderDto>("/orders")
   .WithIdempotency()
   .WithTags("Orders");

app.MapPut<UpdateOrderCommand, OrderDto>("/orders/{Id}")
   .WithIdempotency()
   .WithTags("Orders");
```

---

## Anti-Patterns

```csharp
// ❌ WRONG — applying idempotency to a GET endpoint
app.MapGet<GetOrderQuery, OrderDto>("/orders/{Id}")
   .WithIdempotency();   // ← pointless; GET is already safe + idempotent

// ✅ CORRECT — only mutating verbs need it
app.MapPost<CreateOrderCommand, OrderDto>("/orders")
   .WithIdempotency();

// ❌ WRONG — using AddDistributedMemoryCache in production (not shared across instances)
services.AddDistributedMemoryCache();  // ← single-node only!

// ✅ CORRECT for production — use SQL Server store or Redis
services.AddIdempotencyMsSqlStore(configuration.GetConnectionString("Default")!);

// ❌ WRONG — hardcoding the idempotency key in the request body
// Idempotency keys belong in HTTP headers, not request bodies
```

---

## Composes With

| Library | How |
|---|---|
| `DKNet.AspCore.Idempotency.MsSqlStore` | Production-grade durable store — see [04-idempotency-mssql](./04-idempotency-mssql.md) |
| `DKNet.AspCore.Extensions` | Chain `.WithIdempotency()` after `MapPost<>` / `MapPut<>` |

---

## Security Notes

- **Key sanitization**: The library sanitizes idempotency key values to prevent cache-key injection. Do not bypass this.
- **Scope**: The cache key is composite (`route + idempotency-key-header-value`) so the same key cannot be replayed against a different endpoint.
- **TTL**: Default expiration is 4 hours. Adjust via `IdempotencyOptions.Expiration` based on your retry window.
- **Secrets**: Never log or expose idempotency keys in responses or logs — treat them like short-lived tokens.

---

## Test Example

```csharp
// Uses TestContainers.MsSql-backed integration test environment when validating durable flows.
[Fact]
public async Task Post_DuplicateIdempotencyKey_ReturnsCachedResponse()
{
    // Arrange
    var client = factory.CreateClient();
    var command = new CreateOrderCommand(ProductId: Guid.NewGuid(), Qty: 1);
    var idempotencyKey = Guid.NewGuid().ToString();
    var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders")
    {
        Content = JsonContent.Create(command),
        Headers = { { "Idempotency-Key", idempotencyKey } }
    };

    // Act — first call
    var first = await client.SendAsync(request);
    first.StatusCode.ShouldBe(HttpStatusCode.Created);
    var firstBody = await first.Content.ReadFromJsonAsync<OrderDto>();

    // Act — duplicate call with same key
    var duplicate = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders")
    {
        Content = JsonContent.Create(command),
        Headers = { { "Idempotency-Key", idempotencyKey } }
    };
    var second = await client.SendAsync(duplicate);

    // Assert — same response, no second insert
    second.StatusCode.ShouldBe(HttpStatusCode.Created);
    var secondBody = await second.Content.ReadFromJsonAsync<OrderDto>();
    secondBody!.Id.ShouldBe(firstBody!.Id);
}
```

---

## Quick Decision Guide

- Use idempotency for POST/PUT/PATCH/DELETE endpoints with retry risk.
- Keep GET endpoints without idempotency filters.
- Choose SQL/Redis store for multi-instance production deployments.

---

## Version

| Version | Notes |
|---|---|
| `10.0.0` | Initial documentation |

