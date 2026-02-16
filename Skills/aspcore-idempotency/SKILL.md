---
name: aspcore-idempotency
description: DKNet ASP.NET Core Idempotency - Make API operations idempotent to prevent duplicate requests and ensure exactly-once semantics
license: MIT
---

# ASP.NET Core Idempotency Skill

This skill helps GitHub Copilot generate code using DKNet's ASP.NET Core Idempotency package (`DKNet.AspCore.Idempotency`) for building idempotent APIs that safely handle duplicate requests.

## 🎯 Package Purpose

`DKNet.AspCore.Idempotency` provides:
- **Idempotency Keys** - Request deduplication using unique keys
- **Response Caching** - Cache successful responses for replay
- **Distributed Support** - Works with SQL Server and Redis stores
- **Automatic Integration** - Minimal API and Controller support
- **Conflict Detection** - Handles concurrent duplicate requests

**NuGet Packages**:
- `DKNet.AspCore.Idempotency` (Core)
- `DKNet.AspCore.Idempotency.MsSqlStore` (SQL Server storage)

## 📦 Installation

```bash
dotnet add package DKNet.AspCore.Idempotency
dotnet add package DKNet.AspCore.Idempotency.MsSqlStore
```

## 🏗️ Setup

### Configure Services
```csharp
using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.MsSqlStore;

var builder = WebApplication.CreateBuilder(args);

// Add idempotency with SQL Server store
builder.Services.AddIdempotency<SqlServerIdempotencyStore>(options =>
{
    options.IdempotencyKeyHeader = "X-Idempotency-Key";
    options.CacheTimeout = TimeSpan.FromMinutes(5);
    options.EnableDistributedLocking = true;
});

// Or use in-memory store for testing
builder.Services.AddIdempotency(options =>
{
    options.IdempotencyKeyHeader = "X-Idempotency-Key";
    options.CacheTimeout = TimeSpan.FromMinutes(5);
});

var app = builder.Build();

// Add idempotency middleware
app.UseIdempotency();

app.Run();
```

### Configure Database (SQL Server)
```csharp
// Apply migrations to create idempotency table
services.AddDbContext<IdempotencyDbContext>(options =>
    options.UseSqlServer(connectionString));

// Table: IdempotencyRecords (Key, Response, CreatedAt, ExpiresAt)
```

## 🎯 Usage Patterns

### Pattern 1: Minimal API with Idempotency
```csharp
using DKNet.AspCore.Idempotency;

app.MapPost("/orders", async (
    CreateOrderRequest request,
    IOrderService orderService,
    CancellationToken cancellationToken) =>
{
    var order = await orderService.CreateOrderAsync(request, cancellationToken);
    return Results.Created($"/orders/{order.Id}", order);
})
.WithIdempotency(); // Enable idempotency

// Client sends:
// POST /orders
// X-Idempotency-Key: 550e8400-e29b-41d4-a716-446655440000
// { "customerId": "...", "items": [...] }

// Duplicate requests with same key return cached response
```

### Pattern 2: Controller with Idempotency Attribute
```csharp
using DKNet.AspCore.Idempotency;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    
    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }
    
    /// <summary>
    ///     Creates a new order.
    /// </summary>
    [HttpPost]
    [Idempotent] // Enable idempotency
    [ProducesResponseType(typeof(OrderResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateOrder(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var order = await _orderService.CreateOrderAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
    }
    
    /// <summary>
    ///     Gets an order by ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(OrderResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrder(
        Guid id,
        CancellationToken cancellationToken)
    {
        var order = await _orderService.GetOrderAsync(id, cancellationToken);
        if (order == null)
            return NotFound();
        
        return Ok(order);
    }
}
```

### Pattern 3: Payment Processing with Idempotency
```csharp
[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    
    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }
    
    /// <summary>
    ///     Processes a payment (critical operation requiring idempotency).
    /// </summary>
    [HttpPost]
    [Idempotent(RequireIdempotencyKey = true)] // Require key!
    [ProducesResponseType(typeof(PaymentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ProcessPayment(
        [FromBody] ProcessPaymentRequest request,
        CancellationToken cancellationToken)
    {
        // This will only execute once per idempotency key
        // Subsequent calls with same key return cached result
        var result = await _paymentService.ProcessPaymentAsync(
            request,
            cancellationToken);
        
        return Ok(result);
    }
}

// Client usage:
// POST /api/payments
// X-Idempotency-Key: payment-12345-abc
// { "orderId": "...", "amount": 99.99, "method": "CreditCard" }
```

### Pattern 4: Custom Idempotency Key Generation
```csharp
using DKNet.AspCore.Idempotency;

// Custom key generator
public class OrderIdempotencyKeyGenerator : IIdempotencyKeyGenerator
{
    public string GenerateKey(HttpContext context)
    {
        // Generate key based on request content
        var requestBody = GetRequestBody(context);
        var hash = ComputeHash(requestBody);
        return $"order-{hash}";
    }
}

// Register in DI
builder.Services.AddSingleton<IIdempotencyKeyGenerator, OrderIdempotencyKeyGenerator>();
```

### Pattern 5: Handling Conflicts
```csharp
[HttpPost]
[Idempotent]
public async Task<IActionResult> CreateOrder(
    [FromBody] CreateOrderRequest request,
    CancellationToken cancellationToken)
{
    try
    {
        var order = await _orderService.CreateOrderAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
    }
    catch (IdempotencyConflictException ex)
    {
        // Concurrent requests with same key
        // Wait and retry or return conflict
        return Conflict(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Concurrent Request",
            Detail = "Another request with the same idempotency key is being processed. Please retry.",
            Instance = HttpContext.Request.Path
        });
    }
}
```

## 🚨 Critical Rules

### 1. Always Use Idempotency for Mutating Operations
```csharp
// ✅ Good - POST/PUT/PATCH operations are idempotent
[HttpPost]
[Idempotent]
public async Task<IActionResult> Create(...)

[HttpPut("{id}")]
[Idempotent]
public async Task<IActionResult> Update(...)

[HttpDelete("{id}")]
[Idempotent]
public async Task<IActionResult> Delete(...)

// ❌ Bad - GET operations don't need idempotency (already idempotent)
[HttpGet]
[Idempotent] // Unnecessary
public async Task<IActionResult> Get(...)
```

### 2. Require Keys for Critical Operations
```csharp
// ✅ Good - Payments must have key
[HttpPost]
[Idempotent(RequireIdempotencyKey = true)]
public async Task<IActionResult> ProcessPayment(...)

// ❌ Bad - Critical operation without required key
[HttpPost]
[Idempotent] // Optional key (bad for payments!)
public async Task<IActionResult> ProcessPayment(...)
```

### 3. Set Appropriate Cache Timeouts
```csharp
// ✅ Good - Reasonable timeout
builder.Services.AddIdempotency(options =>
{
    options.CacheTimeout = TimeSpan.FromMinutes(5); // 5 minutes
});

// ❌ Bad - Too short (increased risk of duplicates)
builder.Services.AddIdempotency(options =>
{
    options.CacheTimeout = TimeSpan.FromSeconds(10); // Too short!
});

// ❌ Bad - Too long (memory/storage waste)
builder.Services.AddIdempotency(options =>
{
    options.CacheTimeout = TimeSpan.FromHours(24); // Too long!
});
```

### 4. Use Distributed Store in Production
```csharp
// ✅ Good - SQL Server or Redis in production
builder.Services.AddIdempotency<SqlServerIdempotencyStore>(options =>
{
    options.EnableDistributedLocking = true;
});

// ❌ Bad - In-memory store in production (lost on restart!)
builder.Services.AddIdempotency(options => { }); // In-memory only!
```

### 5. Document Idempotency Requirements
```csharp
// ✅ Good - Clear documentation
/// <summary>
///     Creates a new order.
/// </summary>
/// <remarks>
///     This endpoint is idempotent. Include an X-Idempotency-Key header
///     to ensure duplicate requests return the same result.
/// </remarks>
[HttpPost]
[Idempotent]
public async Task<IActionResult> CreateOrder(...)

// ❌ Bad - No documentation
[HttpPost]
[Idempotent]
public async Task<IActionResult> CreateOrder(...) // What key to use?
```

## 🚫 Common Mistakes

### 1. Not Using Idempotency for State-Changing Operations
```csharp
// ❌ Bad - Payment without idempotency (dangerous!)
[HttpPost("payments")]
public async Task<IActionResult> ProcessPayment(...)
{
    // Could charge customer multiple times on retry!
}

// ✅ Good - Idempotent payment
[HttpPost("payments")]
[Idempotent(RequireIdempotencyKey = true)]
public async Task<IActionResult> ProcessPayment(...)
{
    // Safe to retry - only processes once per key
}
```

### 2. Using Short-Lived Keys
```csharp
// ❌ Bad - Timestamp-based key (not idempotent!)
X-Idempotency-Key: order-2024-01-15-10-30-45

// ✅ Good - Stable key
X-Idempotency-Key: 550e8400-e29b-41d4-a716-446655440000
```

### 3. Not Handling Concurrent Requests
```csharp
// ❌ Bad - No conflict handling
[HttpPost]
[Idempotent]
public async Task<IActionResult> CreateOrder(...)
{
    var order = await _service.CreateOrderAsync(...);
    return Created(...);
    // What if concurrent request is processing?
}

// ✅ Good - Handle conflicts
[HttpPost]
[Idempotent]
public async Task<IActionResult> CreateOrder(...)
{
    try
    {
        var order = await _service.CreateOrderAsync(...);
        return Created(...);
    }
    catch (IdempotencyConflictException)
    {
        return Conflict("Concurrent request in progress");
    }
}
```

### 4. Forgetting Middleware Registration
```csharp
// ❌ Bad - Missing middleware
var app = builder.Build();
app.UseRouting();
// Missing: app.UseIdempotency();
app.MapControllers();

// ✅ Good - Middleware registered
var app = builder.Build();
app.UseRouting();
app.UseIdempotency(); // Required!
app.MapControllers();
```

## 📝 Client Usage Example

```csharp
using System.Net.Http;

public class OrderClient
{
    private readonly HttpClient _httpClient;
    
    public async Task<Order> CreateOrderAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        // Generate idempotency key (e.g., GUID)
        var idempotencyKey = Guid.NewGuid().ToString();
        
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(request)
        };
        
        // Add idempotency key header
        httpRequest.Headers.Add("X-Idempotency-Key", idempotencyKey);
        
        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            // Retry after brief delay
            await Task.Delay(1000, cancellationToken);
            return await CreateOrderAsync(request, cancellationToken);
        }
        
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Order>(cancellationToken);
    }
}
```

## 🔗 Related Skills

- `dknet-overview` - Overall architecture
- `fw-extensions` - Core utilities

## 📚 Additional Resources

- [Idempotency in REST APIs](https://www.rfc-editor.org/rfc/rfc7232.html)
- [Stripe Idempotency Guide](https://stripe.com/docs/api/idempotent_requests)

---

**When to Use This Skill**: Reference this skill when building APIs that need to handle duplicate requests safely, especially for payments, order processing, or other critical operations.
