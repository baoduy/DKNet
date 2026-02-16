---
name: efcore-repos
description: DKNet EF Core Repositories - Implement the Repository pattern with generic repositories, CQRS support, and Mapster integration
license: MIT
---

# EF Core Repositories Skill

This skill helps GitHub Copilot generate code using DKNet's EF Core Repositories package (`DKNet.EfCore.Repos`) for implementing the Repository pattern with Entity Framework Core.

## 🎯 Package Purpose

`DKNet.EfCore.Repos` provides:
- **Repository Pattern** - Abstract data access with repositories
- **Generic Repositories** - Type-safe repositories for any entity
- **CQRS Support** - Separate read/write repositories
- **Mapster Integration** - Efficient projections for DTOs
- **Transaction Management** - Built-in transaction support

**NuGet Package**: `DKNet.EfCore.Repos`

## 📦 Installation

```bash
dotnet add package DKNet.EfCore.Repos
dotnet add package DKNet.EfCore.Repos.Abstractions
dotnet add package Mapster
```

## 🏗️ Setup

### Register Repositories
```csharp
using Microsoft.Extensions.DependencyInjection;
using DKNet.EfCore.Repos;

public void ConfigureServices(IServiceCollection services)
{
    // Add DbContext
    services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(connectionString));
    
    // Add Mapster for projections
    services.AddMapster();
    
    // Register generic repositories
    services.AddGenericRepositories<AppDbContext>();
    
    // Or register specific repositories
    services.AddScoped<IProductRepository, ProductRepository>();
}
```

## 🎯 Usage Patterns

### Pattern 1: Generic Repository
Use built-in generic repository for simple CRUD:

```csharp
using DKNet.EfCore.Repos.Abstractions;

public class ProductService
{
    private readonly IRepository<Product> _repository;
    
    public ProductService(IRepository<Product> repository)
    {
        _repository = repository;
    }
    
    public async Task<Product> CreateProductAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var product = new Product
        {
            Name = request.Name,
            Price = request.Price,
            CategoryId = request.CategoryId
        };
        
        await _repository.AddAsync(product, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        
        return product;
    }
    
    public async Task<Product?> GetProductAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await _repository.GetByIdAsync(id, cancellationToken);
    }
    
    public async Task<IReadOnlyList<Product>> GetAllProductsAsync(
        CancellationToken cancellationToken)
    {
        var spec = new ActiveProductsSpec();
        return await _repository.ToListAsync(spec, cancellationToken);
    }
    
    public async Task UpdateProductAsync(
        Product product,
        CancellationToken cancellationToken)
    {
        await _repository.UpdateAsync(product, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
    }
    
    public async Task DeleteProductAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        await _repository.DeleteAsync(id, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
    }
}
```

### Pattern 2: Custom Repository
Create custom repository for domain-specific methods:

```csharp
using DKNet.EfCore.Repos;
using DKNet.EfCore.Repos.Abstractions;

/// <summary>
///     Repository interface for Product entity.
/// </summary>
public interface IProductRepository : IRepository<Product>
{
    /// <summary>
    ///     Gets products by category asynchronously.
    /// </summary>
    Task<IReadOnlyList<Product>> GetByCategoryAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    ///     Gets low stock products asynchronously.
    /// </summary>
    Task<IReadOnlyList<Product>> GetLowStockProductsAsync(
        int threshold,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Repository implementation for Product entity.
/// </summary>
public class ProductRepository : RepositoryBase<Product>, IProductRepository
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ProductRepository"/> class.
    /// </summary>
    public ProductRepository(AppDbContext context) : base(context)
    {
    }
    
    /// <inheritdoc />
    public async Task<IReadOnlyList<Product>> GetByCategoryAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        var spec = new ProductsByCategorySpec(categoryId);
        return await ToListAsync(spec, cancellationToken);
    }
    
    /// <inheritdoc />
    public async Task<IReadOnlyList<Product>> GetLowStockProductsAsync(
        int threshold,
        CancellationToken cancellationToken = default)
    {
        var spec = new LowStockProductsSpec(threshold);
        return await ToListAsync(spec, cancellationToken);
    }
}
```

### Pattern 3: Read-Only Repository (CQRS)
Use read-only repositories for queries:

```csharp
using DKNet.EfCore.Repos.Abstractions;

public class ProductQueryService
{
    private readonly IReadRepository<Product> _readRepository;
    
    public ProductQueryService(IReadRepository<Product> readRepository)
    {
        _readRepository = readRepository;
    }
    
    public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(
        CancellationToken cancellationToken)
    {
        // Use AsNoTracking for better read performance
        var spec = new ActiveProductsSpec();
        
        return await _readRepository.ToListAsync<ProductDto>(
            spec,
            cancellationToken);
    }
    
    public async Task<ProductDto?> GetProductByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var spec = new ProductByIdSpec(id);
        
        return await _readRepository.FirstOrDefaultAsync<ProductDto>(
            spec,
            cancellationToken);
    }
}
```

### Pattern 4: Projection with Mapster
Project to DTOs efficiently:

```csharp
using DKNet.EfCore.Repos.Abstractions;
using Mapster;

public class ProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string CategoryName { get; set; } = string.Empty;
}

public class ProductQueryService
{
    private readonly IReadRepository<Product> _repository;
    
    public ProductQueryService(IReadRepository<Product> repository)
    {
        _repository = repository;
    }
    
    // Projection is done at the database level (efficient!)
    public async Task<IReadOnlyList<ProductDto>> GetProductDtosAsync(
        CancellationToken cancellationToken)
    {
        var spec = new ActiveProductsSpec();
        
        // Mapster automatically creates the projection expression
        return await _repository.ToListAsync<ProductDto>(
            spec,
            cancellationToken);
    }
}
```

### Pattern 5: Transaction Management
Handle complex operations with transactions:

```csharp
public class OrderService
{
    private readonly IRepository<Order> _orderRepository;
    private readonly IRepository<Product> _productRepository;
    private readonly AppDbContext _context;
    
    public OrderService(
        IRepository<Order> orderRepository,
        IRepository<Product> productRepository,
        AppDbContext context)
    {
        _orderRepository = orderRepository;
        _productRepository = productRepository;
        _context = context;
    }
    
    public async Task<Order> PlaceOrderAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        // Begin transaction
        await using var transaction = await _context.Database
            .BeginTransactionAsync(cancellationToken);
        
        try
        {
            // Create order
            var order = new Order
            {
                CustomerId = request.CustomerId,
                OrderDate = DateTime.UtcNow
            };
            
            await _orderRepository.AddAsync(order, cancellationToken);
            
            // Update product stock
            foreach (var item in request.Items)
            {
                var product = await _productRepository
                    .GetByIdAsync(item.ProductId, cancellationToken);
                
                if (product == null)
                    throw new ProductNotFoundException(item.ProductId);
                
                product.Stock -= item.Quantity;
                await _productRepository.UpdateAsync(product, cancellationToken);
            }
            
            // Save all changes
            await _orderRepository.SaveChangesAsync(cancellationToken);
            
            // Commit transaction
            await transaction.CommitAsync(cancellationToken);
            
            return order;
        }
        catch
        {
            // Rollback on error
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
```

## 🎯 Repository Methods

### Basic CRUD Operations
```csharp
// Create
await repository.AddAsync(entity, ct);
await repository.AddRangeAsync(entities, ct);

// Read
var entity = await repository.GetByIdAsync(id, ct);
var entities = await repository.ToListAsync(spec, ct);
var entity = await repository.FirstOrDefaultAsync(spec, ct);
var count = await repository.CountAsync(spec, ct);
var exists = await repository.AnyAsync(spec, ct);

// Update
await repository.UpdateAsync(entity, ct);
await repository.UpdateRangeAsync(entities, ct);

// Delete
await repository.DeleteAsync(id, ct);
await repository.DeleteAsync(entity, ct);
await repository.DeleteRangeAsync(entities, ct);

// Save changes
await repository.SaveChangesAsync(ct);
```

### Specification-Based Queries
```csharp
// List
var products = await repository.ToListAsync(spec, ct);

// Single result
var product = await repository.FirstOrDefaultAsync(spec, ct);
var product = await repository.SingleOrDefaultAsync(spec, ct);

// Aggregations
var count = await repository.CountAsync(spec, ct);
var exists = await repository.AnyAsync(spec, ct);

// Projections
var dtos = await repository.ToListAsync<ProductDto>(spec, ct);
```

## 🚨 Critical Rules

### 1. Always Call SaveChangesAsync()
```csharp
// ❌ Bad - Changes not persisted
await repository.AddAsync(product, ct);
return product; // Lost!

// ✅ Good - Changes persisted
await repository.AddAsync(product, ct);
await repository.SaveChangesAsync(ct);
return product;
```

### 2. Use IReadRepository for Queries
```csharp
// ✅ Good - Read-only, better performance
private readonly IReadRepository<Product> _repository;

// Use for queries only
var products = await _repository.ToListAsync(spec, ct);

// ❌ Don't use IRepository if you only need read operations
private readonly IRepository<Product> _repository; // Too broad
```

### 3. Include CancellationToken
```csharp
// ✅ Good
public async Task<Product?> GetProductAsync(
    Guid id,
    CancellationToken cancellationToken)
{
    return await _repository.GetByIdAsync(id, cancellationToken);
}

// ❌ Bad
public async Task<Product?> GetProductAsync(Guid id)
{
    return await _repository.GetByIdAsync(id, default);
}
```

### 4. Use Specifications for Complex Queries
```csharp
// ✅ Good - Testable, reusable
var spec = new ActiveProductsSpec();
var products = await repository.ToListAsync(spec, ct);

// ❌ Bad - Direct DbContext access
var products = await context.Products
    .Where(p => !p.IsDeleted && p.IsActive)
    .ToListAsync(ct);
```

## 🚫 Common Mistakes

### 1. Forgetting SaveChangesAsync
```csharp
// ❌ Bad - Changes not saved
await repository.AddAsync(product, ct);
// Missing SaveChangesAsync!

// ✅ Good
await repository.AddAsync(product, ct);
await repository.SaveChangesAsync(ct);
```

### 2. Using Repository Outside Transaction Scope
```csharp
// ❌ Bad - Multiple SaveChanges in transaction
using var transaction = await context.Database.BeginTransactionAsync(ct);
await repo1.AddAsync(entity1, ct);
await repo1.SaveChangesAsync(ct); // Don't save here!
await repo2.AddAsync(entity2, ct);
await repo2.SaveChangesAsync(ct); // Don't save here!
await transaction.CommitAsync(ct);

// ✅ Good - Single SaveChanges
using var transaction = await context.Database.BeginTransactionAsync(ct);
await repo1.AddAsync(entity1, ct);
await repo2.AddAsync(entity2, ct);
await repo1.SaveChangesAsync(ct); // Or repo2, same DbContext
await transaction.CommitAsync(ct);
```

### 3. Not Using Specifications
```csharp
// ❌ Bad - Query logic scattered
var products = await repository.ToListAsync(
    new Specification<Product>(p => p.IsActive && !p.IsDeleted),
    ct);

// ✅ Good - Reusable specification
var products = await repository.ToListAsync(
    new ActiveProductsSpec(),
    ct);
```

## 🔗 Related Skills

- `efcore-specifications` - Building specifications for repositories
- `efcore-abstractions` - Base entities used in repositories
- `dknet-overview` - Overall architecture

## 📚 Additional Resources

- [Repository Pattern](https://martinfowler.com/eaaCatalog/repository.html)
- [CQRS Pattern](https://martinfowler.com/bliki/CQRS.html)
- [Mapster Documentation](https://github.com/MapsterMapper/Mapster)

---

**When to Use This Skill**: Reference this skill when implementing data access layers, creating repositories, or following CQRS patterns with EF Core.
