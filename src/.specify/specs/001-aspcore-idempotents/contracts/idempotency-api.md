# API Contracts: DKNet.AspCore.Idempotents

## Service Registration API

### AddIdempotency Extension Methods

```csharp
// Namespace: Microsoft.Extensions.DependencyInjection

/// <summary>
///     Adds idempotency services to the service collection with default options.
/// </summary>
public static IServiceCollection AddIdempotency(this IServiceCollection services);

/// <summary>
///     Adds idempotency services with custom configuration.
/// </summary>
public static IServiceCollection AddIdempotency(
    this IServiceCollection services,
    Action<IdempotencyOptions> configure);
```

**Usage Examples**:

```csharp
// Basic setup (in-memory store)
builder.Services.AddIdempotency();

// Custom options
builder.Services.AddIdempotency(options =>
{
    options.HeaderName = "X-Idempotency-Key";
    options.DefaultTtl = TimeSpan.FromHours(12);
    options.EnableFingerprinting = true;
    options.ConcurrencyMode = ConcurrencyMode.RejectWithConflict;
});

// With distributed cache (requires IDistributedCache registration)
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "localhost:6379";
});
builder.Services.AddIdempotency(options =>
{
    options.UseDistributedCache();
    options.DefaultTtl = TimeSpan.FromHours(24);
});

// With custom store
builder.Services.AddIdempotency(options =>
{
    options.UseStore<MyCustomIdempotencyStore>();
});
```

---

## Endpoint Configuration API

### Minimal API Extensions

```csharp
// Namespace: Microsoft.AspNetCore.Builder

/// <summary>
///     Requires idempotency key for this endpoint with default options.
/// </summary>
public static RouteHandlerBuilder RequireIdempotency(this RouteHandlerBuilder builder);

/// <summary>
///     Requires idempotency key with custom TTL.
/// </summary>
public static RouteHandlerBuilder RequireIdempotency(
    this RouteHandlerBuilder builder,
    TimeSpan ttl);

/// <summary>
///     Requires idempotency key with custom configuration.
/// </summary>
public static RouteHandlerBuilder RequireIdempotency(
    this RouteHandlerBuilder builder,
    Action<EndpointIdempotencyOptions> configure);

/// <summary>
///     Requires idempotency key for all endpoints in this group.
/// </summary>
public static RouteGroupBuilder RequireIdempotency(this RouteGroupBuilder builder);
```

**Usage Examples**:

```csharp
// Single endpoint with default settings
app.MapPost("/orders", CreateOrder)
    .RequireIdempotency();

// Custom TTL
app.MapPost("/payments", CreatePayment)
    .RequireIdempotency(TimeSpan.FromHours(48));

// Custom options per endpoint
app.MapPost("/transfers", CreateTransfer)
    .RequireIdempotency(options =>
    {
        options.Ttl = TimeSpan.FromHours(72);
        options.EnableFingerprinting = true;
    });

// Apply to entire group
var api = app.MapGroup("/api/v1")
    .RequireIdempotency();

api.MapPost("/orders", CreateOrder);      // Idempotency enforced
api.MapPost("/products", CreateProduct);  // Idempotency enforced
api.MapGet("/orders/{id}", GetOrder);     // GET - idempotency ignored (safe method)
```

---

### MVC Attribute

```csharp
// Namespace: DKNet.AspCore.Idempotents

/// <summary>
///     Marks an action as requiring an idempotency key.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class IdempotentAttribute : Attribute, IFilterFactory
{
    /// <summary>
    ///     The time-to-live for cached responses. Format: "hh:mm:ss" or "d.hh:mm:ss".
    ///     If not specified, uses the global default.
    /// </summary>
    public string? Ttl { get; set; }

    /// <summary>
    ///     When true, validates request body hash matches the original request.
    /// </summary>
    public bool EnableFingerprinting { get; set; }
}
```

**Usage Examples**:

```csharp
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    // Basic usage
    [HttpPost]
    [Idempotent]
    public async Task<IActionResult> Create(CreateOrderRequest request)
    {
        // ...
    }

    // Custom TTL (48 hours)
    [HttpPost("bulk")]
    [Idempotent(Ttl = "48:00:00")]
    public async Task<IActionResult> CreateBulk(CreateBulkRequest request)
    {
        // ...
    }

    // With fingerprinting
    [HttpPost("transfer")]
    [Idempotent(Ttl = "24:00:00", EnableFingerprinting = true)]
    public async Task<IActionResult> Transfer(TransferRequest request)
    {
        // ...
    }
}

// Class-level attribute (applies to all mutating methods)
[ApiController]
[Route("api/[controller]")]
[Idempotent]
public class PaymentsController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreatePaymentRequest request) { }

    [HttpPost("{id}/refund")]
    public async Task<IActionResult> Refund(Guid id) { }
}
```

---

## Storage Interface

### IIdempotencyStore

```csharp
// Namespace: DKNet.AspCore.Idempotents.Stores

/// <summary>
///     Defines the contract for idempotency response storage.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    ///     Retrieves a cached response for the given idempotency key.
    /// </summary>
    /// <param name="key">The idempotency key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cached response, or null if not found or expired.</returns>
    Task<CachedResponse?> GetAsync(IdempotencyKey key, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Stores a response for the given idempotency key.
    /// </summary>
    /// <param name="key">The idempotency key.</param>
    /// <param name="response">The response to cache.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetAsync(IdempotencyKey key, CachedResponse response, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Attempts to acquire a lock for the given idempotency key.
    /// </summary>
    /// <param name="key">The idempotency key.</param>
    /// <param name="timeout">Maximum time to wait for the lock.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the lock was acquired; otherwise, false.</returns>
    Task<bool> TryAcquireLockAsync(IdempotencyKey key, TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Releases a previously acquired lock.
    /// </summary>
    /// <param name="key">The idempotency key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReleaseLockAsync(IdempotencyKey key, CancellationToken cancellationToken = default);
}
```

---

## HTTP Headers

### Request Headers

| Header | Required | Description | Example |
|--------|----------|-------------|---------|
| `Idempotency-Key` | Yes* | Unique key for idempotent request | `Idempotency-Key: ord_123abc` |

*Required only for endpoints marked with `[Idempotent]` or `.RequireIdempotency()`.

### Response Headers

| Header | Condition | Description | Example |
|--------|-----------|-------------|---------|
| `Idempotency-Key-Status` | Always | `created` or `cached` | `Idempotency-Key-Status: cached` |
| `Idempotency-Key-Expires` | When cached | ISO 8601 expiry timestamp | `Idempotency-Key-Expires: 2026-01-30T12:00:00Z` |
| `Retry-After` | On 409 Conflict | Seconds to wait before retry | `Retry-After: 5` |

---

## Error Responses

### 400 Bad Request - Missing Key

```http
HTTP/1.1 400 Bad Request
Content-Type: application/problem+json

{
    "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
    "title": "Bad Request",
    "status": 400,
    "detail": "Idempotency key is required for this endpoint.",
    "instance": "/api/orders"
}
```

### 400 Bad Request - Invalid Key Format

```http
HTTP/1.1 400 Bad Request
Content-Type: application/problem+json

{
    "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
    "title": "Bad Request",
    "status": 400,
    "detail": "Idempotency key format is invalid. Key must be 1-256 alphanumeric characters, hyphens, or underscores.",
    "instance": "/api/orders"
}
```

### 409 Conflict - Concurrent Request (RejectWithConflict mode)

```http
HTTP/1.1 409 Conflict
Content-Type: application/problem+json
Retry-After: 5

{
    "type": "https://tools.ietf.org/html/rfc7231#section-6.5.8",
    "title": "Conflict",
    "status": 409,
    "detail": "A request with this idempotency key is already in progress.",
    "instance": "/api/orders"
}
```

### 422 Unprocessable Entity - Fingerprint Mismatch

```http
HTTP/1.1 422 Unprocessable Entity
Content-Type: application/problem+json

{
    "type": "https://tools.ietf.org/html/rfc4918#section-11.2",
    "title": "Unprocessable Entity",
    "status": 422,
    "detail": "Request body does not match original request for this idempotency key.",
    "instance": "/api/orders"
}
```

---

## Complete Request/Response Examples

### First Request (Cache Miss)

**Request**:
```http
POST /api/orders HTTP/1.1
Host: api.example.com
Content-Type: application/json
Idempotency-Key: ord_abc123xyz

{
    "customerId": "cust_456",
    "items": [
        { "productId": "prod_789", "quantity": 2 }
    ]
}
```

**Response**:
```http
HTTP/1.1 201 Created
Content-Type: application/json
Location: /api/orders/ord_001
Idempotency-Key-Status: created

{
    "id": "ord_001",
    "customerId": "cust_456",
    "status": "pending",
    "total": 99.99
}
```

### Duplicate Request (Cache Hit)

**Request** (identical to first):
```http
POST /api/orders HTTP/1.1
Host: api.example.com
Content-Type: application/json
Idempotency-Key: ord_abc123xyz

{
    "customerId": "cust_456",
    "items": [
        { "productId": "prod_789", "quantity": 2 }
    ]
}
```

**Response** (cached):
```http
HTTP/1.1 201 Created
Content-Type: application/json
Location: /api/orders/ord_001
Idempotency-Key-Status: cached
Idempotency-Key-Expires: 2026-01-30T12:00:00Z

{
    "id": "ord_001",
    "customerId": "cust_456",
    "status": "pending",
    "total": 99.99
}
```
