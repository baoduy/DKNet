# DKNet Framework - Copilot Quick Reference

Fast-access code patterns, templates, and common tasks for GitHub Copilot when working with the DKNet Framework.

---

## 🎯 Quick Access Checklist

**Before generating code, verify:**
- [ ] Loaded `activeContext.md` for current focus
- [ ] Checked `systemPatterns.md` for design patterns
- [ ] Reviewed `copilot-rules.md` for standards
- [ ] Understand the technology stack (.NET 10+, C# 13, EF Core 10+)

---

## 📋 Common Code Patterns

### 1. Entity Framework Core Patterns

#### A. Specification Pattern
```csharp
/// <summary>
///     Specification for filtering active products by category.
/// </summary>
public class ActiveProductsByCategorySpec : Specification<Product>
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ActiveProductsByCategorySpec"/> class.
    /// </summary>
    /// <param name="categoryId">The category identifier to filter by.</param>
    public ActiveProductsByCategorySpec(Guid categoryId)
    {
        // Static predicate
        WithFilter(p => p.IsActive && !p.IsDeleted && p.CategoryId == categoryId);
        
        // Include related entities
        AddInclude(p => p.Category);
        AddInclude(p => p.Reviews);
        
        // Add ordering
        AddOrderBy(p => p.Name);
    }
}

// Usage
var spec = new ActiveProductsByCategorySpec(categoryId);
var products = await _repository.ToListAsync(spec, cancellationToken);
```

#### B. Dynamic Predicate Building
```csharp
/// <summary>
///     Builds a dynamic search predicate for products.
/// </summary>
/// <param name="searchTerm">Optional search term for name/description.</param>
/// <param name="minPrice">Optional minimum price filter.</param>
/// <param name="categoryId">Optional category filter.</param>
/// <returns>A predicate expression for Product entities.</returns>
private Expression<Func<Product, bool>> BuildSearchPredicate(
    string? searchTerm,
    decimal? minPrice,
    Guid? categoryId)
{
    var predicate = PredicateBuilder.New<Product>(p => !p.IsDeleted);
    
    // Add dynamic filters conditionally
    if (!string.IsNullOrWhiteSpace(searchTerm))
    {
        predicate = predicate.DynamicAnd(builder => builder
            .With("Name", FilterOperations.Contains, searchTerm)
            .Or("Description", FilterOperations.Contains, searchTerm));
    }
    
    if (minPrice.HasValue)
    {
        predicate = predicate.DynamicAnd("Price", FilterOperations.GreaterThanOrEqual, minPrice.Value);
    }
    
    if (categoryId.HasValue)
    {
        predicate = predicate.DynamicAnd("CategoryId", FilterOperations.Equal, categoryId.Value);
    }
    
    return predicate;
}

// Usage with .AsExpandable() (CRITICAL for LinqKit)
var predicate = BuildSearchPredicate(searchTerm, minPrice, categoryId);
var products = await _context.Products
    .AsNoTracking()
    .AsExpandable()  // <- REQUIRED for LinqKit predicates
    .Where(predicate)
    .ToListAsync(cancellationToken);
```

#### C. Repository Pattern
```csharp
/// <summary>
///     Product repository implementation.
/// </summary>
public class ProductRepository : RepositoryBase<Product>, IProductRepository
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ProductRepository"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    public ProductRepository(AppDbContext context) : base(context)
    {
    }

    /// <summary>
    ///     Gets products by category asynchronously.
    /// </summary>
    /// <param name="categoryId">The category identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of products in the specified category.</returns>
    public async Task<IReadOnlyList<Product>> GetByCategoryAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        var spec = new ActiveProductsByCategorySpec(categoryId);
        return await ToListAsync(spec, cancellationToken);
    }
}
```

---

### 2. ASP.NET Core Patterns

#### A. Minimal API with Idempotency
```csharp
/// <summary>
///     Creates a new order.
/// </summary>
/// <param name="request">The order creation request.</param>
/// <param name="repository">The order repository.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>The created order result.</returns>
[ProducesResponseType(typeof(OrderResult), StatusCodes.Status201Created)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
public static async Task<IResult> CreateOrderAsync(
    [FromBody] CreateOrderRequest request,
    [FromServices] IOrderRepository repository,
    CancellationToken cancellationToken)
{
    // Validate request
    if (string.IsNullOrWhiteSpace(request.CustomerName))
    {
        return TypedResults.Problem(
            "Customer name is required.",
            statusCode: StatusCodes.Status400BadRequest);
    }

    // Create order
    var order = new Order(request.CustomerName, request.Items);
    await repository.AddAsync(order, cancellationToken);
    
    // Return created result
    var result = new OrderResult
    {
        Id = order.Id,
        CustomerName = order.CustomerName,
        Total = order.Total,
        CreatedAt = order.CreatedAt
    };
    
    return TypedResults.Created($"/api/orders/{order.Id}", result);
}

// Register endpoint with idempotency
app.MapPost("/api/orders", CreateOrderAsync)
    .WithName("CreateOrder")
    .WithOpenApi()
    .RequiredIdempotentKey(); // <- Enforces idempotency
```

#### B. Idempotency Configuration
```csharp
// In Program.cs
builder.Services.AddIdempotency(options =>
{
    // Header containing the idempotency key
    options.IdempotencyHeaderKey = "X-Idempotency-Key"; // default
    
    // Cache key prefix for namespacing
    options.CachePrefix = "idem"; // default
    
    // TTL for cached responses
    options.Expiration = TimeSpan.FromHours(4); // default
    
    // Conflict handling strategy
    options.ConflictHandling = IdempotentConflictHandling.CachedResult; // or ConflictResponse
    
    // JSON serialization options
    options.JsonSerializerOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    
    // Key validation
    options.MaxIdempotencyKeyLength = 255; // default
    options.IdempotencyKeyPattern = @"^[a-zA-Z0-9\-_]+$"; // default
    
    // Status code caching
    options.MinStatusCodeForCaching = 200; // default
    options.MaxStatusCodeForCaching = 299; // default
});

// Add distributed cache (required)
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "DKNet_";
});
```

---

### 3. Domain Entity Patterns

#### A. Aggregate Root
```csharp
/// <summary>
///     Represents an order aggregate root.
/// </summary>
public class Order : AggregateRoot
{
    private readonly List<OrderItem> _items = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="Order"/> class.
    /// </summary>
    /// <param name="customerName">The customer name.</param>
    /// <param name="items">The order items.</param>
    public Order(string customerName, IEnumerable<OrderItem> items)
        : base(Guid.NewGuid(), "System")
    {
        CustomerName = customerName ?? throw new ArgumentNullException(nameof(customerName));
        _items.AddRange(items ?? throw new ArgumentNullException(nameof(items)));
        
        // Raise domain event
        AddEvent(new OrderCreatedEvent(Id, customerName, Total));
    }

    /// <summary>
    ///     Gets the customer name.
    /// </summary>
    public string CustomerName { get; private set; }

    /// <summary>
    ///     Gets the order items.
    /// </summary>
    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();

    /// <summary>
    ///     Gets the order total.
    /// </summary>
    public decimal Total => _items.Sum(i => i.Price * i.Quantity);

    /// <summary>
    ///     Adds an item to the order.
    /// </summary>
    /// <param name="item">The item to add.</param>
    /// <param name="userId">The user making the change.</param>
    /// <exception cref="ArgumentNullException">When item is null.</exception>
    public void AddItem(OrderItem item, string userId)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));
        
        _items.Add(item);
        SetUpdatedBy(userId);
        
        AddEvent(new OrderItemAddedEvent(Id, item.ProductId, item.Quantity));
    }
}
```

#### B. Domain Event
```csharp
/// <summary>
///     Event raised when an order is created.
/// </summary>
/// <param name="OrderId">The order identifier.</param>
/// <param name="CustomerName">The customer name.</param>
/// <param name="Total">The order total.</param>
public sealed record OrderCreatedEvent(
    Guid OrderId,
    string CustomerName,
    decimal Total) : IDomainEvent;
```

---

### 4. Testing Patterns

#### A. Unit Test with AAA Pattern
```csharp
/// <summary>
///     Tests for the Order aggregate.
/// </summary>
public class OrderTests
{
    [Fact]
    public void AddItem_WhenItemIsValid_AddsItemToOrder()
    {
        // Arrange: Setup test data
        var order = new Order("John Doe", new List<OrderItem>());
        var item = new OrderItem(Guid.NewGuid(), "Product", 10.00m, 2);
        var userId = "test-user";

        // Act: Execute the operation
        order.AddItem(item, userId);

        // Assert: Verify expected outcomes
        order.Items.ShouldContain(item);
        order.Items.Count.ShouldBe(1);
        order.Total.ShouldBe(20.00m);
        order.UpdatedBy.ShouldBe(userId);
    }

    [Fact]
    public void AddItem_WhenItemIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var order = new Order("John Doe", new List<OrderItem>());

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => order.AddItem(null!, "user"));
    }
}
```

#### B. Integration Test with TestContainers
```csharp
/// <summary>
///     Integration tests for OrderRepository using real SQL Server.
/// </summary>
public class OrderRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    private AppDbContext _context = null!;
    private OrderRepository _repository = null!;

    /// <summary>
    ///     Initializes the test container and database.
    /// </summary>
    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(_container.GetConnectionString())
            .Options;

        _context = new AppDbContext(options);
        await _context.Database.MigrateAsync();

        _repository = new OrderRepository(_context);
    }

    /// <summary>
    ///     Cleans up resources.
    /// </summary>
    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task AddAsync_WhenOrderIsValid_SavesOrderToDatabase()
    {
        // Arrange
        var order = new Order("Jane Doe", new List<OrderItem>
        {
            new(Guid.NewGuid(), "Product A", 15.00m, 3)
        });

        // Act
        await _repository.AddAsync(order);
        await _context.SaveChangesAsync();

        // Assert
        var savedOrder = await _repository.GetByIdAsync(order.Id);
        savedOrder.ShouldNotBeNull();
        savedOrder.CustomerName.ShouldBe("Jane Doe");
        savedOrder.Items.Count.ShouldBe(1);
        savedOrder.Total.ShouldBe(45.00m);
    }
}
```

---

### 5. Extension Method Pattern

```csharp
/// <summary>
///     Extension methods for type validation and conversion.
/// </summary>
public static class TypeExtensions
{
    /// <summary>
    ///     Tries to convert a string value to an enum of the specified type.
    /// </summary>
    /// <typeparam name="TEnum">The enum type to convert to.</typeparam>
    /// <param name="value">The string value to convert.</param>
    /// <param name="result">The converted enum value if successful.</param>
    /// <returns><c>true</c> if conversion succeeded; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentException">When TEnum is not an enum type.</exception>
    public static bool TryConvertToEnum<TEnum>(this string? value, out TEnum result)
        where TEnum : struct, Enum
    {
        result = default;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        return Enum.TryParse(value, ignoreCase: true, out result);
    }

    /// <summary>
    ///     Checks if a property exists on a type.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <param name="propertyName">The property name to look for.</param>
    /// <returns><c>true</c> if the property exists; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">When type or propertyName is null.</exception>
    public static bool HasProperty(this Type type, string propertyName)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));
        if (string.IsNullOrWhiteSpace(propertyName))
            throw new ArgumentNullException(nameof(propertyName));

        return type.GetProperty(propertyName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase) != null;
    }
}
```

---

## 🔑 Critical Patterns to ALWAYS Follow

### 1. LinqKit Integration
**ALWAYS** use `.AsExpandable()` before `.Where()` when using LinqKit predicates:

```csharp
// ✅ CORRECT
var results = await _context.Products
    .AsExpandable()  // <- REQUIRED
    .Where(predicate)
    .ToListAsync();

// ❌ WRONG - Will throw runtime error
var results = await _context.Products
    .Where(predicate)
    .ToListAsync();
```

### 2. Async/Await for I/O
**ALWAYS** use async/await for database operations:

```csharp
// ✅ CORRECT
var product = await _repository.GetByIdAsync(id, cancellationToken);

// ❌ WRONG - Blocking call
var product = _repository.GetByIdAsync(id).Result;
```

### 3. Nullable Reference Types
**ALWAYS** handle nullable types properly:

```csharp
// ✅ CORRECT
public string? OptionalValue { get; init; }
public required string RequiredValue { get; init; }

if (OptionalValue is not null)
{
    // Use OptionalValue safely
}

// ❌ WRONG - May cause null reference exception
public string OptionalValue { get; init; } // Should be string?
```

### 4. XML Documentation
**ALWAYS** add XML docs to public APIs:

```csharp
// ✅ CORRECT
/// <summary>
///     Gets a product by its unique identifier.
/// </summary>
/// <param name="id">The product identifier.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>The product if found; otherwise null.</returns>
public async Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
{
    // Implementation
}

// ❌ WRONG - Missing documentation
public async Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
{
    // Implementation
}
```

### 5. File Headers
**ALWAYS** include copyright headers:

```csharp
// <copyright file="ProductRepository.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace DKNet.Domain.Repositories;

/// <summary>
///     Product repository implementation.
/// </summary>
public class ProductRepository : RepositoryBase<Product>, IProductRepository
{
    // Implementation
}
```

---

## 🚫 Anti-Patterns to AVOID

### 1. InMemory Database for EF Core Tests
```csharp
// ❌ NEVER - InMemory doesn't catch SQL-specific issues
builder.UseInMemoryDatabase("TestDb");

// ✅ ALWAYS - Use TestContainers with real SQL Server
var container = new MsSqlBuilder().Build();
await container.StartAsync();
builder.UseSqlServer(container.GetConnectionString());
```

### 2. Mixing Sync and Async
```csharp
// ❌ NEVER - Causes deadlocks
var result = asyncMethod().Result;

// ✅ ALWAYS - Use await
var result = await asyncMethod();
```

### 3. Materializing Queries Too Early
```csharp
// ❌ NEVER - Loads all records then filters in memory
var products = await _context.Products.ToListAsync();
var filtered = products.Where(p => p.Price > 100);

// ✅ ALWAYS - Filter in database
var filtered = await _context.Products
    .Where(p => p.Price > 100)
    .ToListAsync();
```

### 4. Forgetting AsExpandable with LinqKit
```csharp
// ❌ NEVER - Runtime error with LinqKit predicates
var results = await _context.Products
    .Where(dynamicPredicate)
    .ToListAsync();

// ✅ ALWAYS - Add AsExpandable
var results = await _context.Products
    .AsExpandable()
    .Where(dynamicPredicate)
    .ToListAsync();
```

### 5. Using async void
```csharp
// ❌ NEVER - Exceptions can't be caught
public async void ProcessOrderAsync()
{
    // Implementation
}

// ✅ ALWAYS - Use async Task
public async Task ProcessOrderAsync()
{
    // Implementation
}
```

---

## 📦 Common NuGet Package Imports

### Core Framework
```xml
<ItemGroup>
  <PackageReference Include="DKNet.Fw.Extensions" Version="*" />
  <PackageReference Include="DKNet.EfCore.Extensions" Version="*" />
  <PackageReference Include="DKNet.EfCore.Specifications" Version="*" />
  <PackageReference Include="DKNet.EfCore.Repos" Version="*" />
</ItemGroup>
```

### Idempotency
```xml
<ItemGroup>
  <PackageReference Include="DKNet.AspCore.Idempotency" Version="*" />
  <PackageReference Include="Microsoft.Extensions.Caching.StackExchangeRedis" Version="*" />
</ItemGroup>
```

### Testing
```xml
<ItemGroup>
  <PackageReference Include="xunit" Version="*" />
  <PackageReference Include="Shouldly" Version="*" />
  <PackageReference Include="Testcontainers.MsSql" Version="*" />
  <PackageReference Include="Bogus" Version="*" />
</ItemGroup>
```

---

## 🎯 Quick Decision Tree

**Need to query data dynamically?**
→ Use `PredicateBuilder` with `.DynamicAnd()` / `.DynamicOr()`

**Need reusable query logic?**
→ Use Specification Pattern (`Specification<TEntity>`)

**Need to prevent duplicate requests?**
→ Use `.RequiredIdempotentKey()` on endpoints

**Need to test with real database?**
→ Use TestContainers with `IAsyncLifetime`

**Need to encapsulate business logic?**
→ Use Aggregate Root pattern

**Need to notify other parts of the system?**
→ Use Domain Events

**Need to validate input?**
→ Use FluentValidation or custom validation with `TypedResults.Problem()`

---

## 🔍 Finding Examples

| Need Example For | Look In |
|------------------|---------|
| **Specification Pattern** | `EfCore.Specifications.Tests/` |
| **Dynamic Predicates** | `EfCore.Specifications.Tests/DynamicPredicateBuilderTests.cs` |
| **Repository Pattern** | `EfCore.Repos.Tests/` |
| **Idempotency** | `AspCore.Idempotency.Tests/` |
| **Integration Tests** | `AspCore.Idempotency.MsSqlStore.Tests/Integration/` |
| **Aggregate Roots** | `EfCore.Abstractions.Tests/` |
| **Domain Events** | `EfCore.Events.Tests/` |

---

## 💡 Pro Tips

1. **Use Semantic Regions in Tests**
   ```csharp
   public class OrderTests
   {
       #region Constructor Tests
       
       [Fact]
       public void Constructor_WhenValid_CreatesOrder() { }
       
       #endregion
       
       #region AddItem Tests
       
       [Fact]
       public void AddItem_WhenValid_AddsItem() { }
       
       #endregion
   }
   ```

2. **Use Helper Methods in Tests**
   ```csharp
   private Order CreateTestOrder() => new("Test Customer", new List<OrderItem>());
   
   private OrderItem CreateTestItem() => new(Guid.NewGuid(), "Product", 10.00m, 1);
   ```

3. **Use Shouldly for Fluent Assertions**
   ```csharp
   // Readable and expressive
   result.ShouldNotBeNull();
   result.Count.ShouldBe(5);
   result.ShouldContain(expectedItem);
   ```

4. **Use CancellationToken for Long Operations**
   ```csharp
   public async Task<List<Product>> GetAllAsync(CancellationToken cancellationToken = default)
   {
       return await _context.Products
           .AsNoTracking()
           .ToListAsync(cancellationToken);
   }
   ```

5. **Use Property Name Normalization**
   ```csharp
   // All of these work (normalize to PascalCase internally)
   .DynamicAnd("firstName", ...)      // camelCase
   .DynamicAnd("first_name", ...)     // snake_case
   .DynamicAnd("first-name", ...)     // kebab-case
   .DynamicAnd("FirstName", ...)      // PascalCase
   ```

---

## 📊 Verification Checklist

Before considering code complete:
- [ ] Compiles without errors
- [ ] Zero warnings (`TreatWarningsAsErrors=true`)
- [ ] XML documentation on public APIs
- [ ] File header present
- [ ] Follows naming conventions
- [ ] Null safety considered
- [ ] Error handling implemented
- [ ] Async/await for I/O operations
- [ ] Tests written (AAA pattern)
- [ ] TestContainers used for integration tests
- [ ] `.AsExpandable()` used with LinqKit predicates

---

## 🆘 Common Issues & Solutions

### Issue: "Expression tree may not contain dynamic operation"
**Solution**: Add `.AsExpandable()` before `.Where()` with LinqKit predicates

### Issue: "Property not found" with DynamicAnd
**Solution**: Check property name casing and existence on entity type

### Issue: Test passes locally but fails in CI
**Solution**: Ensure TestContainers properly disposed with `IAsyncLifetime`

### Issue: Null reference warnings
**Solution**: Enable nullable reference types and use `string?` for optional values

### Issue: Idempotency not working
**Solution**: Verify distributed cache is configured and `RequiredIdempotentKey()` is applied

---

**Version**: 1.0.0  
**Last Updated**: February 2, 2026  
**For**: DKNet Framework .NET 10+ / C# 13

**Quick Links**:
- [Memory Bank README](README.md)
- [Active Context](activeContext.md)
- [System Patterns](systemPatterns.md)
- [Copilot Rules](copilot-rules.md)
