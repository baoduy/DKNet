---
name: efcore-specifications
description: DKNet EF Core Specifications - Build reusable, composable, and type-safe queries with dynamic predicates and the Specification Pattern
license: MIT
---

# EF Core Specifications Skill

This skill helps GitHub Copilot generate code using DKNet's EF Core Specifications package (`DKNet.EfCore.Specifications`) for building reusable, type-safe, and dynamic queries.

## 🎯 Package Purpose

`DKNet.EfCore.Specifications` provides:
- **Specification Pattern** - Encapsulate query logic in reusable classes
- **Dynamic Predicates** - Build queries from runtime conditions safely
- **LinqKit Integration** - Compose complex expressions with `.And()`, `.Or()`
- **Type Safety** - Compile-time checking with runtime flexibility

**NuGet Package**: `DKNet.EfCore.Specifications`

## 📦 Installation

```bash
dotnet add package DKNet.EfCore.Specifications
```

## 🏗️ Core Concepts

### 1. Specification Pattern

Specifications encapsulate query logic for reusability and testability.

#### Basic Specification
```csharp
using DKNet.EfCore.Specifications;

/// <summary>
///     Specification for filtering active products.
/// </summary>
public class ActiveProductsSpec : Specification<Product>
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ActiveProductsSpec"/> class.
    /// </summary>
    public ActiveProductsSpec()
    {
        // Define filter criteria
        WithFilter(p => !p.IsDeleted && p.IsActive);
        
        // Add ordering
        AddOrderBy(p => p.Name);
    }
}

// Usage
var spec = new ActiveProductsSpec();
var products = await repository.ToListAsync(spec, cancellationToken);
```

#### Specification with Parameters
```csharp
/// <summary>
///     Specification for products by category with optional price filter.
/// </summary>
public class ProductsByCategorySpec : Specification<Product>
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ProductsByCategorySpec"/> class.
    /// </summary>
    /// <param name="categoryId">The category identifier.</param>
    /// <param name="minPrice">Optional minimum price filter.</param>
    public ProductsByCategorySpec(Guid categoryId, decimal? minPrice = null)
    {
        // Base filter
        var predicate = PredicateBuilder.New<Product>(
            p => !p.IsDeleted && p.CategoryId == categoryId);
        
        // Add optional price filter
        if (minPrice.HasValue)
        {
            predicate = predicate.And(p => p.Price >= minPrice.Value);
        }
        
        WithFilter(predicate);
        
        // Include related entities
        AddInclude(p => p.Category);
        AddInclude(p => p.Reviews);
        
        // Add ordering
        AddOrderBy(p => p.Price);
        AddOrderByDescending(p => p.Rating);
    }
}
```

#### Specification with All Features
```csharp
/// <summary>
///     Comprehensive specification for product search.
/// </summary>
public class ProductSearchSpec : Specification<Product>
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ProductSearchSpec"/> class.
    /// </summary>
    /// <param name="searchTerm">Search term for name/description.</param>
    /// <param name="categoryId">Optional category filter.</param>
    /// <param name="minPrice">Optional minimum price.</param>
    /// <param name="maxPrice">Optional maximum price.</param>
    /// <param name="isActive">Filter by active status.</param>
    public ProductSearchSpec(
        string? searchTerm = null,
        Guid? categoryId = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        bool isActive = true)
    {
        // Build dynamic predicate
        var predicate = PredicateBuilder.New<Product>(p => !p.IsDeleted);
        
        if (isActive)
        {
            predicate = predicate.And(p => p.IsActive);
        }
        
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            predicate = predicate.And(p => 
                p.Name.Contains(searchTerm) || 
                p.Description.Contains(searchTerm));
        }
        
        if (categoryId.HasValue)
        {
            predicate = predicate.And(p => p.CategoryId == categoryId.Value);
        }
        
        if (minPrice.HasValue)
        {
            predicate = predicate.And(p => p.Price >= minPrice.Value);
        }
        
        if (maxPrice.HasValue)
        {
            predicate = predicate.And(p => p.Price <= maxPrice.Value);
        }
        
        WithFilter(predicate);
        
        // Include related data
        AddInclude(p => p.Category);
        AddInclude(p => p.Reviews);
        
        // Add ordering
        AddOrderBy(p => p.Name);
        
        // Ignore global query filters if needed
        // IgnoreQueryFilters();
    }
}
```

### 2. Dynamic Predicates

Build type-safe queries from runtime conditions using string property names.

#### Basic Dynamic Predicate
```csharp
using DKNet.EfCore.Specifications.Dynamics;
using LinqKit;

// Build predicate dynamically
var predicate = PredicateBuilder.New<Product>(p => !p.IsDeleted);

// Add conditions dynamically based on filter input
if (!string.IsNullOrEmpty(request.Name))
{
    predicate = predicate.DynamicAnd("Name", Ops.Contains, request.Name);
}

if (request.MinPrice.HasValue)
{
    predicate = predicate.DynamicAnd("Price", Ops.GreaterThanOrEqual, request.MinPrice.Value);
}

if (request.CategoryId.HasValue)
{
    predicate = predicate.DynamicAnd("CategoryId", Ops.Equal, request.CategoryId.Value);
}

// CRITICAL: Must use .AsExpandable() with LinqKit predicates
var products = await context.Products
    .AsNoTracking()
    .AsExpandable() // Required!
    .Where(predicate)
    .ToListAsync(cancellationToken);
```

#### Supported Operations (Ops)
```csharp
// Comparison operations
predicate.DynamicAnd("Price", Ops.Equal, 100);
predicate.DynamicAnd("Price", Ops.NotEqual, 0);
predicate.DynamicAnd("Price", Ops.GreaterThan, 50);
predicate.DynamicAnd("Price", Ops.GreaterThanOrEqual, 50);
predicate.DynamicAnd("Price", Ops.LessThan, 200);
predicate.DynamicAnd("Price", Ops.LessThanOrEqual, 200);

// String operations (auto-converted to Equal/NotEqual for non-strings)
predicate.DynamicAnd("Name", Ops.Contains, "laptop");
predicate.DynamicAnd("Name", Ops.NotContains, "refurbished");
predicate.DynamicAnd("Name", Ops.StartsWith, "Apple");
predicate.DynamicAnd("Name", Ops.EndsWith, "Pro");

// Collection operations
var categoryIds = new[] { guid1, guid2, guid3 };
predicate.DynamicAnd("CategoryId", Ops.In, categoryIds);
predicate.DynamicAnd("Status", Ops.NotIn, new[] { "Cancelled", "Deleted" });
```

#### Builder Pattern for OR Conditions
```csharp
// Build OR conditions using builder pattern
var predicate = PredicateBuilder.New<Product>(p => !p.IsDeleted);

predicate = predicate.DynamicAnd(builder => builder
    .With("Name", Ops.Contains, "laptop")
    .Or("Description", Ops.Contains, "laptop"));

// Multiple OR groups
predicate = predicate.DynamicAnd(builder => builder
    .With("CategoryId", Ops.Equal, electronicsId)
    .Or("CategoryId", Ops.Equal, computersId))
    .DynamicAnd(builder => builder
        .With("Price", Ops.LessThan, 1000)
        .Or("OnSale", Ops.Equal, true));

// Use with EF Core
var products = await context.Products
    .AsExpandable()
    .Where(predicate)
    .ToListAsync(cancellationToken);
```

#### Nested Property Access
```csharp
// Query nested properties using dot notation
predicate.DynamicAnd("Category.Name", Ops.Equal, "Electronics");
predicate.DynamicAnd("Address.City", Ops.Contains, "New York");
predicate.DynamicAnd("Order.Customer.Email", Ops.EndsWith, "@example.com");
```

### 3. Composing Specifications

Combine specifications using LinqKit's `.And()` and `.Or()`:

```csharp
// Define base specifications
var activeSpec = new ActiveProductsSpec();
var categorySpec = new ProductsByCategorySpec(categoryId);

// Combine with AND
var combinedFilter = activeSpec.FilterQuery
    .And(categorySpec.FilterQuery);

var spec = new Specification<Product>();
spec.WithFilter(combinedFilter);

// Use combined specification
var products = await repository.ToListAsync(spec, cancellationToken);
```

## 🎯 Usage Patterns

### Pattern 1: Simple Specification
When you have a simple, reusable query:

```csharp
public class AvailableProductsSpec : Specification<Product>
{
    public AvailableProductsSpec()
    {
        WithFilter(p => !p.IsDeleted && p.IsActive && p.Stock > 0);
        AddOrderBy(p => p.Name);
    }
}

// Usage
var products = await repository.ToListAsync(
    new AvailableProductsSpec(), 
    cancellationToken);
```

### Pattern 2: Parameterized Specification
When you need to pass parameters:

```csharp
public class ProductsInPriceRangeSpec : Specification<Product>
{
    public ProductsInPriceRangeSpec(decimal minPrice, decimal maxPrice)
    {
        WithFilter(p => p.Price >= minPrice && p.Price <= maxPrice);
        AddInclude(p => p.Category);
    }
}

// Usage
var spec = new ProductsInPriceRangeSpec(100, 500);
var products = await repository.ToListAsync(spec, cancellationToken);
```

### Pattern 3: Dynamic API Filters
When building queries from API request parameters:

```csharp
public class ProductFilterSpec : Specification<Product>
{
    public ProductFilterSpec(ProductFilterRequest request)
    {
        var predicate = PredicateBuilder.New<Product>(p => !p.IsDeleted);
        
        // Dynamically add filters based on request
        if (!string.IsNullOrEmpty(request.SearchTerm))
        {
            predicate = predicate.DynamicAnd(builder => builder
                .With("Name", Ops.Contains, request.SearchTerm)
                .Or("Description", Ops.Contains, request.SearchTerm)
                .Or("Sku", Ops.Equal, request.SearchTerm));
        }
        
        if (request.CategoryIds?.Any() == true)
        {
            predicate = predicate.DynamicAnd("CategoryId", Ops.In, request.CategoryIds);
        }
        
        if (request.MinPrice.HasValue)
        {
            predicate = predicate.DynamicAnd("Price", Ops.GreaterThanOrEqual, request.MinPrice.Value);
        }
        
        if (request.MaxPrice.HasValue)
        {
            predicate = predicate.DynamicAnd("Price", Ops.LessThanOrEqual, request.MaxPrice.Value);
        }
        
        WithFilter(predicate);
        AddInclude(p => p.Category);
    }
}
```

### Pattern 4: Complex Query with Includes
When you need related data:

```csharp
public class OrderWithDetailsSpec : Specification<Order>
{
    public OrderWithDetailsSpec(Guid orderId)
    {
        WithFilter(o => o.Id == orderId);
        
        // Include related entities
        AddInclude(o => o.Customer);
        AddInclude(o => o.OrderItems);
        AddInclude(o => o.ShippingAddress);
        AddInclude(o => o.BillingAddress);
    }
}

// Usage
var order = await repository.FirstOrDefaultAsync(
    new OrderWithDetailsSpec(orderId),
    cancellationToken);
```

## 🚨 Critical Rules

### 1. ALWAYS Use .AsExpandable() with LinqKit Predicates
```csharp
// ❌ Bad - Will throw runtime error
var predicate = PredicateBuilder.New<Product>(p => p.IsActive);
var products = await context.Products
    .Where(predicate)
    .ToListAsync();

// ✅ Good - Required for LinqKit
var predicate = PredicateBuilder.New<Product>(p => p.IsActive);
var products = await context.Products
    .AsExpandable() // REQUIRED!
    .Where(predicate)
    .ToListAsync();
```

### 2. Use .AsNoTracking() for Read-Only Queries
```csharp
// ✅ Good - Better performance for read-only
var products = await context.Products
    .AsNoTracking()
    .AsExpandable()
    .Where(predicate)
    .ToListAsync(cancellationToken);
```

### 3. Always Include CancellationToken
```csharp
// ✅ Good
var products = await repository.ToListAsync(spec, cancellationToken);

// ❌ Bad
var products = await repository.ToListAsync(spec, default);
```

### 4. Null-Safe Dynamic Predicates
```csharp
// ✅ Good - Checks for null/empty before adding filter
if (!string.IsNullOrWhiteSpace(searchTerm))
{
    predicate = predicate.DynamicAnd("Name", Ops.Contains, searchTerm);
}

// ❌ Bad - Might add empty filter
predicate = predicate.DynamicAnd("Name", Ops.Contains, searchTerm);
```

### 5. Use Specifications in Repositories
```csharp
// ✅ Good - Testable, reusable
public async Task<IReadOnlyList<Product>> GetActiveProductsAsync(
    CancellationToken cancellationToken)
{
    var spec = new ActiveProductsSpec();
    return await ToListAsync(spec, cancellationToken);
}

// ❌ Bad - Not reusable, harder to test
public async Task<IReadOnlyList<Product>> GetActiveProductsAsync(
    CancellationToken cancellationToken)
{
    return await _context.Products
        .Where(p => !p.IsDeleted && p.IsActive)
        .ToListAsync(cancellationToken);
}
```

## 🚫 Common Mistakes

### 1. Forgetting .AsExpandable()
```csharp
// ❌ Runtime error: Expression tree may not contain a dynamic operation
var products = await context.Products
    .Where(PredicateBuilder.New<Product>(p => p.IsActive))
    .ToListAsync();

// ✅ Works correctly
var products = await context.Products
    .AsExpandable()
    .Where(PredicateBuilder.New<Product>(p => p.IsActive))
    .ToListAsync();
```

### 2. Using String Operations on Non-String Properties
```csharp
// ❌ Bad - Ops.Contains only works with strings
predicate.DynamicAnd("Price", Ops.Contains, 100); // Runtime error!

// ✅ Good - Use appropriate operation for type
predicate.DynamicAnd("Price", Ops.Equal, 100);

// Note: DKNet automatically converts string operations to Equal/NotEqual 
// for non-string types, so this is actually safe, but use the right op
```

### 3. Not Handling Nullable Values
```csharp
// ❌ Bad - Might fail if value is null
predicate.DynamicAnd("OptionalField", Ops.Equal, nullableValue);

// ✅ Good - Check for null
if (nullableValue.HasValue)
{
    predicate.DynamicAnd("OptionalField", Ops.Equal, nullableValue.Value);
}
```

## 📝 Testing Specifications

```csharp
using Shouldly;
using Xunit;

public class ActiveProductsSpecTests
{
    [Fact]
    public void ActiveProductsSpec_ShouldFilterCorrectly()
    {
        // Arrange
        var spec = new ActiveProductsSpec();
        var products = GetTestProducts();
        
        // Act
        var filtered = products
            .AsQueryable()
            .AsExpandable()
            .Where(spec.FilterQuery!)
            .ToList();
        
        // Assert
        filtered.ShouldNotBeEmpty();
        filtered.ShouldAllBe(p => !p.IsDeleted && p.IsActive);
    }
    
    private static List<Product> GetTestProducts()
    {
        return new List<Product>
        {
            new() { Id = Guid.NewGuid(), Name = "Product 1", IsActive = true, IsDeleted = false },
            new() { Id = Guid.NewGuid(), Name = "Product 2", IsActive = false, IsDeleted = false },
            new() { Id = Guid.NewGuid(), Name = "Product 3", IsActive = true, IsDeleted = true }
        };
    }
}
```

## 🔗 Related Skills

- `efcore-repos` - Using specifications with repositories
- `efcore-abstractions` - Base entities used in specifications
- `dknet-overview` - Overall architecture context

## 📚 Additional Resources

- [LinqKit Documentation](https://github.com/scottksmith95/LINQKit)
- [Specification Pattern](https://en.wikipedia.org/wiki/Specification_pattern)
- [System.Linq.Dynamic.Core](https://github.com/zzzprojects/System.Linq.Dynamic.Core)

---

**When to Use This Skill**: Reference this skill when building EF Core queries, implementing search/filter functionality, or creating reusable query logic with the Specification Pattern.
