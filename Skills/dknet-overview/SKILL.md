---
name: dknet-overview
description: DKNet Framework overview, architecture patterns, and getting started guide for building enterprise .NET applications
license: MIT
---

# DKNet Framework Overview Skill

This skill helps GitHub Copilot understand the DKNet Framework architecture, patterns, and conventions when generating code for applications using DKNet NuGet packages.

## 🎯 Framework Purpose

DKNet is a comprehensive .NET framework for building enterprise applications with:
- **Clean Architecture** - Onion Architecture with Domain-Driven Design
- **EF Core Extensions** - Specifications, dynamic predicates, repositories, audit logs
- **ASP.NET Core Features** - Idempotency, background tasks, API extensions
- **Messaging** - Event-driven architecture with SlimBus
- **Quality First** - Zero warnings, nullable types, comprehensive testing

## 🏗️ Architecture Layers

### Domain Layer (Core)
```csharp
// Entities with business logic
public class Order : EntityBase
{
    public Guid CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    
    public void PlaceOrder()
    {
        if (Status != OrderStatus.Draft)
            throw new DomainException("Only draft orders can be placed");
            
        Status = OrderStatus.Placed;
        AddDomainEvent(new OrderPlacedEvent(Id));
    }
}
```

### Application Layer
```csharp
// CQRS Command Handler
public class PlaceOrderHandler : ICommandHandler<PlaceOrderCommand, OrderResult>
{
    private readonly IOrderRepository _repository;
    
    public async Task<OrderResult> Handle(
        PlaceOrderCommand command,
        CancellationToken cancellationToken)
    {
        var spec = new OrderByIdSpec(command.OrderId);
        var order = await _repository.FirstOrDefaultAsync(spec, cancellationToken);
        
        order.PlaceOrder();
        await _repository.UpdateAsync(order, cancellationToken);
        
        return OrderResult.FromEntity(order);
    }
}
```

### Infrastructure Layer
```csharp
// Repository implementation
public class OrderRepository : RepositoryBase<Order>, IOrderRepository
{
    public OrderRepository(AppDbContext context) : base(context)
    {
    }
}
```

### Presentation Layer
```csharp
// Minimal API endpoint
app.MapPost("/orders/{id}/place", async (
    Guid id,
    IMediator mediator,
    CancellationToken cancellationToken) =>
{
    var command = new PlaceOrderCommand(id);
    var result = await mediator.Send(command, cancellationToken);
    return Results.Ok(result);
})
.WithIdempotency(); // DKNet idempotency support
```

## 📦 Core Packages

### DKNet.Fw.Extensions
Core utilities and extension methods
```csharp
using DKNet.Fw.Extensions;

// String extensions
var isValid = email.IsValidEmail();

// Type extensions
if (typeof(MyEnum).TryConvertToEnum("Value", out MyEnum result))
{
    // Use result
}

// Collection extensions
var nonEmpty = collection.WhereNotNullOrEmpty();
```

### DKNet.EfCore.Abstractions
Base entities and common abstractions
```csharp
using DKNet.EfCore.Abstractions;

// Base entity with Id, CreatedAt, UpdatedAt
public class Product : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

// Soft delete support
public class Order : EntityBase, ISoftDelete
{
    public bool IsDeleted { get; set; }
}

// Audit trail
public class Document : EntityBase, IAuditable
{
    public string CreatedBy { get; set; } = string.Empty;
    public string? UpdatedBy { get; set; }
}
```

### DKNet.EfCore.Specifications
Specification pattern for reusable queries
```csharp
using DKNet.EfCore.Specifications;

// Define specification
public class ActiveProductsSpec : Specification<Product>
{
    public ActiveProductsSpec()
    {
        WithFilter(p => !p.IsDeleted && p.IsActive);
        AddInclude(p => p.Category);
        AddOrderBy(p => p.Name);
    }
}

// Use in repository
var products = await repository.ToListAsync(
    new ActiveProductsSpec(), 
    cancellationToken);
```

### DKNet.EfCore.Repos
Repository pattern implementation
```csharp
using DKNet.EfCore.Repos;

public interface IProductRepository : IRepositoryBase<Product>
{
}

public class ProductRepository : RepositoryBase<Product>, IProductRepository
{
    public ProductRepository(AppDbContext context) : base(context)
    {
    }
}

// Usage
var product = await repository.GetByIdAsync(id, cancellationToken);
await repository.AddAsync(product, cancellationToken);
await repository.SaveChangesAsync(cancellationToken);
```

### DKNet.AspCore.Idempotency
Idempotent API operations
```csharp
using DKNet.AspCore.Idempotency;

// Add idempotency support
builder.Services.AddIdempotency(options =>
{
    options.IdempotencyKeyHeader = "X-Idempotency-Key";
    options.CacheTimeout = TimeSpan.FromMinutes(5);
});

// Use with minimal API
app.MapPost("/orders", CreateOrder)
    .WithIdempotency();

// Or with attribute
[HttpPost]
[Idempotent]
public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
{
    // Implementation
}
```

### DKNet.SlimBus.Extensions
Lightweight message bus
```csharp
using DKNet.SlimBus.Extensions;

// Define message
public record OrderCreatedEvent(Guid OrderId, decimal Total);

// Publish message
await messageBus.PublishAsync(
    new OrderCreatedEvent(order.Id, order.Total),
    cancellationToken);

// Subscribe to message
public class OrderCreatedHandler : IMessageHandler<OrderCreatedEvent>
{
    public async Task HandleAsync(
        OrderCreatedEvent message,
        CancellationToken cancellationToken)
    {
        // Handle the event
    }
}
```

## 🎯 Key Patterns to Follow

### 1. Always Use Async/Await for I/O
```csharp
// ✅ Good
public async Task<Order> GetOrderAsync(Guid id, CancellationToken ct)
{
    return await repository.GetByIdAsync(id, ct);
}

// ❌ Bad
public Order GetOrder(Guid id)
{
    return repository.GetByIdAsync(id, default).Result; // Blocks thread
}
```

### 2. Use Specifications for Queries
```csharp
// ✅ Good - Reusable, testable
public class PendingOrdersSpec : Specification<Order>
{
    public PendingOrdersSpec()
    {
        WithFilter(o => o.Status == OrderStatus.Pending);
    }
}
var orders = await repository.ToListAsync(new PendingOrdersSpec(), ct);

// ❌ Bad - Not reusable
var orders = await context.Orders
    .Where(o => o.Status == OrderStatus.Pending)
    .ToListAsync(ct);
```

### 3. Use Dynamic Predicates for Complex Filters
```csharp
// ✅ Good - Dynamic, safe
var predicate = PredicateBuilder.New<Product>(p => !p.IsDeleted);

if (!string.IsNullOrEmpty(searchTerm))
{
    predicate = predicate.DynamicAnd(builder => builder
        .With("Name", FilterOperations.Contains, searchTerm));
}

var products = await context.Products
    .AsNoTracking()
    .AsExpandable() // Required for LinqKit
    .Where(predicate)
    .ToListAsync(ct);

// ❌ Bad - Building SQL strings (SQL injection risk)
var sql = "SELECT * FROM Products WHERE Name LIKE '%" + searchTerm + "%'";
```

### 4. Always Include CancellationToken
```csharp
// ✅ Good
public async Task<IActionResult> GetOrders(CancellationToken cancellationToken)
{
    var orders = await repository.ToListAsync(ct: cancellationToken);
    return Ok(orders);
}

// ❌ Bad
public async Task<IActionResult> GetOrders()
{
    var orders = await repository.ToListAsync(ct: default);
    return Ok(orders);
}
```

### 5. Use Nullable Reference Types
```csharp
// ✅ Good - Explicit nullability
public class Product : EntityBase
{
    public string Name { get; set; } = string.Empty; // Never null
    public string? Description { get; set; } // Can be null
}

// ❌ Bad - Unclear nullability
public class Product : EntityBase
{
    public string Name { get; set; } // Is this nullable?
    public string Description { get; set; } // Is this nullable?
}
```

### 6. Always Add XML Documentation
```csharp
// ✅ Good
/// <summary>
///     Gets the order by identifier asynchronously.
/// </summary>
/// <param name="id">The order identifier.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>The order if found, otherwise null.</returns>
public async Task<Order?> GetOrderAsync(Guid id, CancellationToken cancellationToken)
{
    return await repository.GetByIdAsync(id, cancellationToken);
}

// ❌ Bad - No documentation
public async Task<Order?> GetOrderAsync(Guid id, CancellationToken cancellationToken)
{
    return await repository.GetByIdAsync(id, cancellationToken);
}
```

## 🚫 Common Anti-Patterns to Avoid

### 1. Don't Mix Sync and Async
```csharp
// ❌ Bad - Deadlock risk
var result = asyncMethod().Result;
var result2 = asyncMethod().GetAwaiter().GetResult();

// ✅ Good
var result = await asyncMethod();
```

### 2. Don't Use .AsNoTracking() for Updates
```csharp
// ❌ Bad - Changes won't be tracked
var product = await context.Products.AsNoTracking().FirstAsync();
product.Price = 100;
await context.SaveChangesAsync(); // Nothing saved!

// ✅ Good - Track for updates
var product = await context.Products.FirstAsync();
product.Price = 100;
await context.SaveChangesAsync(); // Saved!
```

### 3. Don't Forget .AsExpandable() with LinqKit
```csharp
// ❌ Bad - Runtime error
var predicate = PredicateBuilder.New<Product>(p => p.IsActive);
var products = await context.Products
    .Where(predicate) // Error!
    .ToListAsync();

// ✅ Good
var predicate = PredicateBuilder.New<Product>(p => p.IsActive);
var products = await context.Products
    .AsExpandable() // Required!
    .Where(predicate)
    .ToListAsync();
```

### 4. Don't Create N+1 Queries
```csharp
// ❌ Bad - N+1 queries
var orders = await context.Orders.ToListAsync();
foreach (var order in orders)
{
    var customer = await context.Customers.FindAsync(order.CustomerId);
    // Use customer
}

// ✅ Good - Single query with include
var orders = await context.Orders
    .Include(o => o.Customer)
    .ToListAsync();
```

## 📝 Code Quality Standards

- **Zero Warnings**: `TreatWarningsAsErrors=true` enforced
- **Nullable Types**: Always enabled with `<Nullable>enable</Nullable>`
- **XML Docs**: Required on all public APIs
- **Test Coverage**: Target 85%+
- **Async/Await**: For all I/O operations
- **CancellationToken**: Always include in async methods

## 🔗 Related Skills

- `efcore-specifications` - Deep dive into Specification pattern
- `efcore-repos` - Repository pattern implementation details
- `efcore-abstractions` - Base entities and interfaces
- `aspcore-idempotency` - Idempotent API operations
- `fw-extensions` - Core utility methods
- `slimbus-messaging` - Message bus patterns

## 📚 Additional Resources

- [DKNet Architecture Guide](../../docs/Architecture.md)
- [Memory Bank](../../src/memory-bank/README.md)
- [API Reference](../../docs/API-Reference.md)
- [NuGet Packages](https://www.nuget.org/packages?q=DKNet)

---

**When to Use This Skill**: Reference this skill when starting a new project with DKNet Framework, understanding the overall architecture, or learning the core patterns and conventions.
