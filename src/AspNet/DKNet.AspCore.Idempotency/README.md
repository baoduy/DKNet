# DKNet.AspCore.Idempotents

Enterprise-grade idempotency support for ASP.NET Core Minimal APIs and MVC endpoints.

## Overview

`DKNet.AspCore.Idempotents` provides middleware and filters to ensure API operations are idempotent—repeated requests with the same idempotency key return the same response without re-executing the operation.

### Key Features

- ✅ **Attribute & Fluent API**: Use `[Idempotent]` attribute or `.RequireIdempotency()` extension
- ✅ **Distributed Cache Support**: Redis, SQL Server, or any `IDistributedCache` implementation
- ✅ **Concurrent Request Handling**: Prevents race conditions with distributed locking
- ✅ **Response Status Codes**: Tracks which responses are cached vs fresh
- ✅ **Request Fingerprinting**: Optional validation that request body matches original
- ✅ **Zero Dependencies**: Only requires ASP.NET Core abstractions

## Quick Start

### Installation

```bash
dotnet add package DKNet.AspCore.Idempotents
```

### 1. Register Services

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Add Redis cache
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});

// Add idempotency
builder.Services.AddIdempotency(options =>
{
    options.IdempotencyHeaderKey = "Idempotency-Key";
    options.Expiration = TimeSpan.FromHours(24);
});

var app = builder.Build();
```

### 2. Protect Endpoints

#### Minimal API

```csharp
app.MapPost("/api/orders", CreateOrder)
    .RequireIdempotency();

app.MapPost("/api/payments", CreatePayment)
    .RequireIdempotency(TimeSpan.FromHours(72));
```

#### MVC Controller

```csharp
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    [HttpPost]
    [Idempotent]
    public async Task<IActionResult> Create(CreateOrderRequest request)
    {
        // Your logic here
    }
}
```

### 3. Call Your API

```bash
# First request
curl -X POST https://localhost:5001/api/orders \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: order-123-abc" \
  -d '{"customerId": "cust_456", "amount": 99.99}'

# Response Headers
# Idempotency-Key-Status: created

# Second request with same key
curl -X POST https://localhost:5001/api/orders \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: order-123-abc" \
  -d '{"customerId": "cust_456", "amount": 99.99}'

# Response Headers
# Idempotency-Key-Status: cached
# Idempotency-Key-Expires: 2026-01-30T12:00:00Z
```

## Configuration

### Global Options

```csharp
builder.Services.AddIdempotency(options =>
{
    // Header name for idempotency key (default: "Idempotency-Key")
    options.IdempotencyHeaderKey = "X-Idempotency-Key";

    // Cache key prefix (default: "idem")
    options.CachePrefix = "idempotency";

    // Response TTL (default: 24 hours)
    options.Expiration = TimeSpan.FromHours(48);

    // Maximum key length (default: 256)
    options.MaxKeyLength = 512;

    // Cache error responses (4xx, 5xx)? (default: false)
    options.CacheErrorResponses = true;

    // Enable request body fingerprinting (default: false)
    options.EnableFingerprinting = true;

    // Handle concurrent requests: ReturnCachedResult or ConflictResponse (default: ReturnCachedResult)
    options.ConflictHandling = IdempotentConflictHandling.ConflictResponse;

    // Lock timeout for concurrent requests (default: 30 seconds)
    options.LockTimeout = TimeSpan.FromSeconds(60);

    // Max body size to cache (default: 1 MB)
    options.MaxBodySize = 5 * 1024 * 1024;
});
```

### Per-Endpoint Configuration

```csharp
// Custom TTL
app.MapPost("/api/payments", CreatePayment)
    .RequireIdempotency(TimeSpan.FromHours(72));

// Custom options
app.MapPost("/api/bulk-operations", BulkCreate)
    .RequireIdempotency(options =>
    {
        options.Expiration = TimeSpan.FromHours(48);
        options.EnableFingerprinting = true;
        options.CacheErrorResponses = true;
    });
```

## Response Headers

The library automatically adds these headers to responses:

| Header | Value | Meaning |
|--------|-------|---------|
| `Idempotency-Key-Status` | `created` | Fresh execution |
| `Idempotency-Key-Status` | `cached` | Returned from cache |
| `Idempotency-Key-Expires` | ISO 8601 timestamp | When cache expires |

## Error Handling

### 400 Bad Request - Missing Key

```json
{
    "status": 400,
    "title": "Bad Request",
    "detail": "Idempotency key is required for this endpoint."
}
```

### 400 Bad Request - Invalid Key Format

```json
{
    "status": 400,
    "title": "Bad Request",
    "detail": "Idempotency key format is invalid. Key must be 1-256 alphanumeric characters, hyphens, or underscores."
}
```

### 409 Conflict - Concurrent Request

```json
{
    "status": 409,
    "title": "Conflict",
    "detail": "A request with this idempotency key is already in progress."
}
```

## Best Practices

### 1. Generate Unique Keys

Use UUIDs or business-meaningful identifiers:

```csharp
// UUID approach
var key = Guid.NewGuid().ToString();

// Business approach
var key = $"payment_{customerId}_{operationId}";
```

### 2. Store Keys for Retries

Persist the idempotency key so you can retry with the same key:

```csharp
var key = Guid.NewGuid().ToString();
await db.SaveIdempotencyKey(operationId, key);

// Later, for retry
var savedKey = await db.GetIdempotencyKey(operationId);
var response = await client.PostAsync(url, content, 
    headers: { IdempotencyKey = savedKey });
```

### 3. Apply to Mutating Operations Only

```csharp
// ✅ Apply to POST, PUT, PATCH
app.MapPost("/orders", CreateOrder).RequireIdempotency();

// ❌ Don't apply to GET (already safe)
app.MapGet("/orders/{id}", GetOrder);
```

### 4. Choose Appropriate TTL

| Operation | TTL |
|-----------|-----|
| Payment processing | 24-72 hours |
| Order creation | 24 hours |
| User registration | 1 hour |
| Bulk operations | 48+ hours |

## Architecture

```
Request
  │
  ├─ Extract Idempotency-Key header
  │
  ├─ Validate key format
  │
  ├─ Check distributed cache
  │   │
  │   ├─ Hit: Return cached response
  │   │
  │   └─ Miss: Acquire distributed lock
  │
  ├─ Execute endpoint
  │
  ├─ Cache response
  │
  ├─ Release lock
  │
  └─ Add response headers
```

## Performance Considerations

- **In-memory cache**: ~1-2ms overhead
- **Redis cache**: ~10-20ms overhead (network dependent)
- **Lock acquisition**: ~100ms (distributed environments)
- **Response body size**: Limited to `MaxBodySize` (default: 1 MB)

## Storage

### Redis (Recommended for distributed)

```csharp
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "localhost:6379";
});
builder.Services.AddIdempotency();
```

### SQL Server

```csharp
builder.Services.AddSqlServerCache(options =>
{
    options.ConnectionString = "connection_string";
    options.SchemaName = "dbo";
    options.TableName = "DistributedCache";
});
builder.Services.AddIdempotency();
```

## License

MIT License. See LICENSE file for details.

## Support

For issues, questions, or contributions, visit the [DKNet GitHub repository](https://github.com/baoduy/DKNet).
