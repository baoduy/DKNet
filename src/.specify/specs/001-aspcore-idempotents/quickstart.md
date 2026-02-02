# Quickstart: DKNet.AspCore.Idempotents

Get started with idempotency support for your ASP.NET Core APIs in under 5 minutes.

## Installation

```bash
dotnet add package DKNet.AspCore.Idempotents
```

## Basic Setup

### 1. Register Services

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Add idempotency services (uses in-memory store by default)
builder.Services.AddIdempotency();

var app = builder.Build();
```

### 2. Protect Endpoints

#### Minimal API

```csharp
app.MapPost("/api/orders", async (CreateOrderRequest request) =>
{
    // Your order creation logic
    var order = await orderService.CreateAsync(request);
    return Results.Created($"/api/orders/{order.Id}", order);
})
.RequireIdempotency();  // <-- Add this

app.Run();
```

#### MVC Controller

```csharp
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    [HttpPost]
    [Idempotent]  // <-- Add this
    public async Task<IActionResult> Create(CreateOrderRequest request)
    {
        var order = await _orderService.CreateAsync(request);
        return CreatedAtAction(nameof(Get), new { id = order.Id }, order);
    }
}
```

### 3. Call from Client

```bash
curl -X POST https://localhost:5001/api/orders \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: my-unique-key-123" \
  -d '{"customerId": "cust_456", "productId": "prod_789"}'
```

**First call**: Executes the endpoint, caches response  
**Second call with same key**: Returns cached response (no duplicate order created!)

---

## Common Configurations

### Custom TTL

```csharp
// Global default
builder.Services.AddIdempotency(options =>
{
    options.DefaultTtl = TimeSpan.FromHours(48);
});

// Per-endpoint override
app.MapPost("/api/payments", CreatePayment)
    .RequireIdempotency(TimeSpan.FromHours(72));
```

### Use Redis for Distributed Caching

```csharp
// Register Redis
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});

// Use distributed cache for idempotency
builder.Services.AddIdempotency(options =>
{
    options.UseDistributedCache();
});
```

### Enable Request Body Fingerprinting

Reject requests that reuse the same idempotency key with a different body:

```csharp
builder.Services.AddIdempotency(options =>
{
    options.EnableFingerprinting = true;
});
```

### Handle Concurrent Requests

```csharp
builder.Services.AddIdempotency(options =>
{
    // Option 1: Wait for first request to complete (default)
    options.ConcurrencyMode = ConcurrencyMode.Wait;
    options.LockTimeout = TimeSpan.FromSeconds(30);

    // Option 2: Immediately reject with 409 Conflict
    options.ConcurrencyMode = ConcurrencyMode.RejectWithConflict;
});
```

---

## Response Headers

The library automatically adds these headers to responses:

| Header | Value | Meaning |
|--------|-------|---------|
| `Idempotency-Key-Status` | `created` | Fresh execution |
| `Idempotency-Key-Status` | `cached` | Returned from cache |
| `Idempotency-Key-Expires` | ISO timestamp | When cache expires |

---

## Error Handling

### Missing Idempotency Key

```json
{
    "status": 400,
    "title": "Bad Request",
    "detail": "Idempotency key is required for this endpoint."
}
```

### Invalid Key Format

```json
{
    "status": 400,
    "title": "Bad Request",
    "detail": "Idempotency key format is invalid."
}
```

### Concurrent Request Conflict

```json
{
    "status": 409,
    "title": "Conflict",
    "detail": "A request with this idempotency key is already in progress."
}
```

---

## Best Practices

### 1. Generate Unique Keys

```csharp
// Client-side: Use UUID or similar
var idempotencyKey = Guid.NewGuid().ToString();

// Or: Use business-meaningful keys
var idempotencyKey = $"order_{customerId}_{DateTime.UtcNow:yyyyMMddHHmmss}";
```

### 2. Store Keys for Retry

```csharp
// Save the key before making the request
var key = Guid.NewGuid().ToString();
await SaveIdempotencyKeyAsync(operationId, key);

// Use the same key for retries
var savedKey = await GetIdempotencyKeyAsync(operationId);
await httpClient.PostAsync(url, content, headers: new { IdempotencyKey = savedKey });
```

### 3. Apply Only to Mutating Operations

```csharp
// ✅ POST, PUT, PATCH - apply idempotency
app.MapPost("/orders", CreateOrder).RequireIdempotency();
app.MapPut("/orders/{id}", UpdateOrder).RequireIdempotency();

// ❌ GET, DELETE - typically don't need idempotency
app.MapGet("/orders/{id}", GetOrder);  // Already idempotent by nature
app.MapDelete("/orders/{id}", DeleteOrder);  // Already idempotent
```

### 4. Choose Appropriate TTL

| Operation Type | Suggested TTL |
|---------------|---------------|
| Payment processing | 24-72 hours |
| Order creation | 24 hours |
| User registration | 1 hour |
| Bulk operations | 48+ hours |

---

## Next Steps

- [Full API Reference](./contracts/idempotency-api.md)
- [Data Model](./data-model.md)
- [Implementation Tasks](./tasks.md)
