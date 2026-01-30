# DKNet.AspCore.Idempotency

A robust, production-ready idempotency middleware for ASP.NET Core minimal APIs and endpoints. This library prevents duplicate request processing by enforcing idempotent request semantics using distributed caching.

![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=.net)
![License](https://img.shields.io/badge/License-MIT-green)
[![NuGet](https://img.shields.io/nuget/v/DKNet.AspCore.Idempotency)](https://www.nuget.org/packages/DKNet.AspCore.Idempotency)

## Overview

Idempotency is a critical feature for API design, especially for operations that modify state (POST, PUT, PATCH, DELETE). This library provides an elegant way to implement idempotent endpoints in ASP.NET Core by:

- **Tracking Request Processing**: Uses idempotency keys to identify duplicate requests
- **Caching Results**: Stores successful responses in a distributed cache for re-delivery
- **Preventing Side Effects**: Eliminates accidental duplicate processing from network retries or timeouts
- **Minimal Configuration**: Simple setup with sensible defaults
- **Composable Design**: Integrates seamlessly with ASP.NET Core's minimal API ecosystem

## Key Features

### ✨ Core Features
- **Idempotency Key Header Support** - Standard HTTP header-based idempotency key tracking
- **Distributed Caching** - Uses ASP.NET Core's `IDistributedCache` for scalable, multi-instance support
- **Conflict Handling Strategies** - Choose between returning cached results or 409 Conflict responses
- **Automatic Status Code Filtering** - Only caches successful responses (2xx status codes)
- **Route-Scoped Keys** - Composite keys prevent the same key being used across different endpoints
- **Configurable Expiration** - TTL-based cache invalidation (default: 4 hours)
- **Security Sanitization** - Input sanitization to prevent cache key injection attacks

### 🔒 Production-Ready
- ✅ Zero warnings (`TreatWarningsAsErrors=true`)
- ✅ Nullable reference type support
- ✅ Comprehensive XML documentation on all public APIs
- ✅ Async/await throughout
- ✅ Thread-safe distributed cache operations
- ✅ Detailed structured logging

## Installation

### Via NuGet Package Manager
```bash
dotnet add package DKNet.AspCore.Idempotency
```

### Via .csproj
```xml
<ItemGroup>
  <PackageReference Include="DKNet.AspCore.Idempotency" Version="*" />
</ItemGroup>
```

## Quick Start

### 1. Register Idempotency Services

In your `Program.cs`, register the idempotency services with dependency injection:

```csharp
var builder = WebApplicationBuilder.CreateBuilder(args);

// Add idempotency services
builder.Services.AddIdempotency(options =>
{
    options.IdempotencyHeaderKey = "X-Idempotency-Key"; // default
    options.CachePrefix = "idem";                        // default
    options.Expiration = TimeSpan.FromHours(4);         // default
    options.ConflictHandling = IdempotentConflictHandling.ConflictResponse; // default
    options.JsonSerializerOptions = new JsonSerializerOptions 
    { 
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
    };
});

// Add distributed cache (required)
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});

var app = builder.Build();
```

### 2. Apply to Endpoints

Use the `RequiredIdempotentKey()` filter on endpoints that should be idempotent:

```csharp
// POST endpoint with idempotency
app.MapPost("/orders", CreateOrderAsync)
    .WithName("CreateOrder")
    .WithOpenApi()
    .RequiredIdempotentKey(); // <- Add idempotency filter

// PUT endpoint with idempotency
app.MapPut("/orders/{id}", UpdateOrderAsync)
    .WithName("UpdateOrder")
    .RequiredIdempotentKey();

app.Run();
```

### 3. Send Requests with Idempotency Key

Clients send requests with the idempotency key header:

```http
POST /orders HTTP/1.1
Host: api.example.com
X-Idempotency-Key: 550e8400-e29b-41d4-a716-446655440000
Content-Type: application/json

{
  "productId": 123,
  "quantity": 5,
  "customerId": 456
}
```

## Configuration

### IdempotencyOptions

Customize idempotency behavior through `IdempotencyOptions`:

```csharp
builder.Services.AddIdempotency(options =>
{
    // HTTP header name for idempotency keys
    // Default: "X-Idempotency-Key"
    options.IdempotencyHeaderKey = "Idempotency-Key";
    
    // Prefix for all cache keys to prevent collisions
    // Default: "idem"
    options.CachePrefix = "myapp-idempotency";
    
    // Cache entry expiration time
    // Default: 4 hours
    // Requests with expired keys are treated as new requests
    options.Expiration = TimeSpan.FromHours(24);
    
    // How to handle duplicate requests
    // Default: ConflictResponse (returns 409 Conflict)
    options.ConflictHandling = IdempotentConflictHandling.CachedResult; // Return cached response instead
    
    // JSON serialization options for response caching
    // Used when ConflictHandling is set to CachedResult
    options.JsonSerializerOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
});
```

### Conflict Handling Strategies

#### 1. ConflictResponse (Default)
Returns an HTTP 409 Conflict response when a duplicate request is detected:

```
HTTP/1.1 409 Conflict
Content-Type: application/problem+json

{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.8",
  "title": "Conflict",
  "status": 409,
  "detail": "The request with the same idempotent key `550e8400-e29b-41d4-a716-446655440000` has already been processed."
}
```

#### 2. CachedResult
Returns the cached response from the original request:

```
HTTP/1.1 200 OK
Content-Type: application/json

{
  "orderId": 789,
  "status": "created",
  "createdAt": "2025-01-30T10:30:00Z"
}
```

## Behavior and Flow

### Request Processing Flow

```
┌─────────────────────────────────────┐
│ Request arrives with or without     │
│ Idempotency-Key header              │
└────────────┬────────────────────────┘
             │
             ▼
┌─────────────────────────────────────┐
│ Is Idempotency-Key header present?  │
└────────┬───────────────────┬────────┘
         │ No                │ Yes
         ▼                   ▼
    ┌─────────────┐  ┌────────────────────┐
    │ Return 400  │  │ Check cache for    │
    │ Bad Request │  │ composite key      │
    └─────────────┘  └────────┬───────────┘
                               │
                    ┌──────────┴──────────┐
                    │ Found              │ Not Found
                    ▼                     ▼
            ┌─────────────────┐  ┌──────────────────┐
            │ Check conflict  │  │ Process request  │
            │ handling        │  │ normally         │
            └────┬────────────┘  └────┬─────────────┘
                 │                    │
         ┌───────┴────────┐           │
         │                │           │
    Conflict        Cached         ▼
    Response        Result   ┌──────────────────┐
         │                │  │ Is status code   │
         │                │  │ 2xx (success)?   │
         │                │  └────┬──────────┬──┘
         │                │       │          │
         │                │     Yes         No
         │                │       │          │
         │                │       ▼          ▼
         │                │  ┌────────┐  ┌──────┐
         │                │  │ Cache  │  │ Don't│
         │                │  │ result │  │cache │
         │                │  └────────┘  └──────┘
         │                │       │          │
         └────────────────┴───────┴──────────┘
                          │
                          ▼
                  ┌──────────────────┐
                  │ Return response  │
                  │ to client        │
                  └──────────────────┘
```

### Composite Key Format

The filter creates a composite key from the route template and idempotency key to support the same idempotency key being used across different endpoints:

```
CompositeKey = "{routeTemplate}_{idempotencyKey}"

Examples:
- Route: POST /orders, Key: abc-123 → "POST /orders_abc-123"
- Route: PUT /users/{id}, Key: abc-123 → "PUT /users/{id}_abc-123"
```

### Cache Key Sanitization

User-provided idempotency keys are sanitized to prevent cache key injection:

```csharp
// Input: "abc-123/../../malicious"
// Sanitized: "idem_abc-123__________malicious" (uppercase)

// Characters removed/replaced:
// "/" → "_"
// "\n" → removed
// "\r" → removed
// Result is uppercased for consistency
```

## Usage Examples

### Basic POST Endpoint

```csharp
app.MapPost("/orders", async (CreateOrderRequest request, IOrderService service) =>
{
    var order = await service.CreateOrderAsync(request);
    return Results.Created($"/orders/{order.Id}", order);
})
.Produces<OrderResponse>(StatusCodes.Status201Created)
.RequiredIdempotentKey();

public record CreateOrderRequest(string ProductId, int Quantity);
public record OrderResponse(string OrderId, string Status, DateTime CreatedAt);
```

### PUT Endpoint with Custom Configuration

```csharp
builder.Services.AddIdempotency(options =>
{
    options.IdempotencyHeaderKey = "Request-Id";
    options.ConflictHandling = IdempotentConflictHandling.CachedResult;
    options.Expiration = TimeSpan.FromHours(24);
});

app.MapPut("/users/{id}", async (string id, UpdateUserRequest request, IUserService service) =>
{
    var user = await service.UpdateUserAsync(id, request);
    return Results.Ok(user);
})
.Produces<UserResponse>()
.RequiredIdempotentKey();
```

### DELETE Endpoint (No Response Body)

```csharp
app.MapDelete("/orders/{id}", async (string id, IOrderService service) =>
{
    await service.DeleteOrderAsync(id);
    return Results.NoContent();
})
.RequiredIdempotentKey();
```

## Testing

### Unit Testing with TestContainers

The library includes integration tests using TestContainers for SQL Server and Redis:

```csharp
[Collection("Redis Collection")]
public class IdempotencyEndpointTests : IAsyncLifetime
{
    private readonly ApiFixture _fixture;
    
    public IdempotencyEndpointTests()
    {
        _fixture = new ApiFixture();
    }
    
    public async Task InitializeAsync() => await _fixture.InitializeAsync();
    public async Task DisposeAsync() => await _fixture.DisposeAsync();
    
    [Fact]
    public async Task CreateOrder_WithValidIdempotencyKey_Returns201Created()
    {
        // Arrange
        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new CreateOrderRequest("PROD-001", 5);
        
        // Act
        var response = await _fixture.HttpClient!.PostAsJsonAsync(
            "/orders",
            request,
            headers => headers.Add("X-Idempotency-Key", idempotencyKey)
        );
        
        // Assert
        response.StatusCode.Should().Be(StatusCodes.Status201Created);
    }
    
    [Fact]
    public async Task CreateOrder_WithDuplicateIdempotencyKey_Returns409Conflict()
    {
        // Arrange
        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new CreateOrderRequest("PROD-001", 5);
        
        // Act - First request
        var firstResponse = await _fixture.HttpClient!.PostAsJsonAsync(
            "/orders",
            request,
            headers => headers.Add("X-Idempotency-Key", idempotencyKey)
        );
        
        // Act - Duplicate request
        var secondResponse = await _fixture.HttpClient!.PostAsJsonAsync(
            "/orders",
            request,
            headers => headers.Add("X-Idempotency-Key", idempotencyKey)
        );
        
        // Assert
        firstResponse.StatusCode.Should().Be(StatusCodes.Status201Created);
        secondResponse.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }
}
```

## Requirements

- **.NET**: 9.0+
- **ASP.NET Core**: 9.0+
- **IDistributedCache Implementation**: Redis, SQL Server, MemoryCache, or other ASP.NET Core distributed cache provider

### Distributed Cache Setup

You **must** register an `IDistributedCache` implementation. Choose one:

```csharp
// Redis (Recommended for production)
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "localhost:6379";
});

// SQL Server
builder.Services.AddDistributedSqlServerCache(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.SchemaName = "dbo";
    options.TableName = "DistributedCache";
});

// In-Memory (Development only)
builder.Services.AddDistributedMemoryCache();
```

## Logging

The filter provides detailed structured logging at various levels:

```csharp
// Configure logging in appsettings.json
{
  "Logging": {
    "LogLevel": {
      "DKNet.AspCore.Idempotency": "Debug"
    }
  }
}
```

### Log Examples

```
[Debug] Checking idempotency header key: X-Idempotency-Key
[Debug] Trying to get existing result for cache key: IDEM_550E8400-E29B-41D4-A716-446655440000
[Debug] Existing result found: null
[Debug] Returning result to the client
[Info] Caching the response for idempotency key: 550e8400-e29b-41d4-a716-446655440000
```

## API Reference

### IdempotentSetup Extensions

#### AddIdempotency
```csharp
public static IServiceCollection AddIdempotency(
    this IServiceCollection services,
    Action<IdempotencyOptions>? config = null)
```
Registers idempotency services into the dependency injection container.

#### RequiredIdempotentKey
```csharp
public static RouteHandlerBuilder RequiredIdempotentKey(
    this RouteHandlerBuilder builder)
```
Adds the idempotency endpoint filter to a route handler.

### IIdempotencyKeyRepository

Core repository interface for managing idempotency keys:

```csharp
public interface IIdempotencyKeyRepository
{
    /// Checks if the key has been processed
    ValueTask<(bool processed, string? result)> IsKeyProcessedAsync(string idempotencyKey);
    
    /// Marks the key as processed with optional result
    ValueTask MarkKeyAsProcessedAsync(string idempotencyKey, string? result = null);
}
```

## Best Practices

### ✅ DO
- Use **idempotency keys on all state-modifying operations** (POST, PUT, PATCH, DELETE)
- Use **UUIDs or random identifiers** for idempotency keys (e.g., v4 UUIDs)
- **Configure appropriate expiration times** based on your use case
- **Log idempotency events** for audit trails
- **Test with duplicate requests** to verify idempotent behavior
- **Use distributed cache in production** for multi-instance deployments
- **Set descriptive cache key prefixes** to organize cached data

### ❌ DON'T
- Don't rely on **sequential or predictable idempotency keys**
- Don't use **extremely long expiration times** (increases memory usage)
- Don't **modify the idempotency key** after sending the first request
- Don't cache **error responses** (only 2xx status codes are cached)
- Don't use **in-memory cache in production** (won't work across instances)
- Don't forget to **configure a distributed cache provider**
- Don't expect **idempotency without the header** (returns 400 Bad Request)

## Performance Considerations

### Caching Strategy
- **Successful Responses (2xx)**: Cached with TTL from configuration
- **Error Responses (4xx, 5xx)**: Not cached, processed normally each time
- **Partial Content (206)**: Not cached (outside the 200-299 range in typical config)

### Memory Impact
With default configuration (4-hour expiration):
- Small response (< 1KB): ~1.5KB cached per entry
- Medium response (1-10KB): ~10-15KB cached per entry
- Large response (> 10KB): Recommend filtering these or using shorter TTL

## Troubleshooting

### Issue: "Idempotency header key is missing. Returning 400 Bad Request."
**Solution**: Ensure your client is sending the idempotency key header:
```http
X-Idempotency-Key: your-unique-key
```

### Issue: Cache entries not persisting across requests
**Solution**: Verify a distributed cache is properly configured:
```csharp
// Check appsettings.json has valid Redis/SQL connection
// Or register in-memory cache if testing locally
builder.Services.AddDistributedMemoryCache();
```

### Issue: Same key works on one endpoint but not another
**Solution**: This is expected behavior. Composite keys include the route, so the same key can be used across different endpoints:
```
POST /orders + key ABC = "POST /orders_ABC" (different from)
PUT /orders/{id} + key ABC = "PUT /orders/{id}_ABC"
```

## License

Licensed under the MIT License. See LICENSE file in the project root for details.

Copyright © 2025 Steven Hoang. All rights reserved.

## Contributing

Contributions are welcome! Please ensure:
- Code compiles with zero warnings
- All public APIs have XML documentation
- Tests cover new functionality
- Follow the DKNet framework patterns and standards

## See Also

- [DKNet Framework](https://github.com/drunkcoding/dknet)
- [ASP.NET Core Minimal APIs](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis)
- [Idempotency RFC Specification](https://tools.ietf.org/html/draft-idempotency-http-methods)
- [Distributed Caching in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed)

---

**Version**: 10.0+ | **Status**: Production Ready | **Last Updated**: January 30, 2026
