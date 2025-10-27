# DKNet.EfCore.Repos.Abstractions

[![NuGet](https://img.shields.io/nuget/v/DKNet.EfCore.Repos.Abstractions)](https://www.nuget.org/packages/DKNet.EfCore.Repos.Abstractions/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DKNet.EfCore.Repos.Abstractions)](https://www.nuget.org/packages/DKNet.EfCore.Repos.Abstractions/)
[![.NET](https://img.shields.io/badge/.NET-9.0-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](../../../../LICENSE)

Repository pattern abstractions for Entity Framework Core, providing clean separation between read and write operations
with strongly-typed interfaces. This package defines the contracts for data access operations following CQRS principles
and Domain-Driven Design patterns.

## Features

- **Repository Pattern Abstractions**: Clean interfaces for data access operations
- **CQRS Support**: Separate read and write operations with dedicated interfaces
- **Generic Entity Support**: Type-safe operations for any entity type
- **Async/Await Operations**: Full async support with cancellation tokens
- **Transaction Management**: Built-in transaction support for write operations
- **Projection Support**: Efficient DTO projections for read operations
- **Query Flexibility**: IQueryable support for complex queries
- **Batch Operations**: Support for bulk add, update, and delete operations

## Supported Frameworks

- .NET 9.0+
- Entity Framework Core 9.0+

## Installation

Install via NuGet Package Manager:

```bash
dotnet add package DKNet.EfCore.Repos.Abstractions
```

Or via Package Manager Console:

```powershell
Install-Package DKNet.EfCore.Repos.Abstractions
```

## Quick Start

### Basic Repository Interface Usage

```csharp
using DKNet.EfCore.Repos.Abstractions;

// Domain entity
public class Product : Entity<Guid>
{
    public string Name { get; set; }
    public decimal Price { get; set; }
    public string Category { get; set; }
}

// Custom repository interface
public interface IProductRepository : IRepository<Product>
{
    Task<IEnumerable<Product>> GetProductsByCategoryAsync(string category, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default);
}

// Service using repository
public class ProductService
{
    private readonly IProductRepository _productRepository;

    public ProductService(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<Product> CreateProductAsync(string name, decimal price, string category)
    {
        // Check if product already exists
        if (await _productRepository.ExistsByNameAsync(name))
            throw new InvalidOperationException($"Product with name '{name}' already exists");

        var product = new Product 
        { 
            Name = name, 
            Price = price, 
            Category = category 
        };

        await _productRepository.AddAsync(product);
        await _productRepository.SaveChangesAsync();

        return product;
    }
}
```

### Read-Only Operations

```csharp
public class ProductQueryService
{
    private readonly IReadRepository<Product> _readRepository;

    public ProductQueryService(IReadRepository<Product> readRepository)
    {
        _readRepository = readRepository;
    }

    public async Task<List<ProductDto>> GetActiveProductsAsync()
    {
        // Use projection for efficient queries
        return await _readRepository
            .GetDto<ProductDto>(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<Product?> GetProductByIdAsync(Guid id)
    {
        return await _readRepository.FindAsync(id);
    }

    public IQueryable<Product> GetProductsQuery()
    {
        // Return IQueryable for complex filtering
        return _readRepository.Gets();
    }
}

public class ProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public string Category { get; set; }
}
```

### Write Operations with Transactions

```csharp
public class ProductManagementService
{
    private readonly IWriteRepository<Product> _writeRepository;

    public ProductManagementService(IWriteRepository<Product> writeRepository)
    {
        _writeRepository = writeRepository;
    }

    public async Task BulkUpdatePricesAsync(List<ProductPriceUpdate> updates)
    {
        using var transaction = await _writeRepository.BeginTransactionAsync();

        try
        {
            foreach (var update in updates)
            {
                var product = await _writeRepository.FindAsync(update.ProductId);
                if (product != null)
                {
                    product.Price = update.NewPrice;
                    await _writeRepository.UpdateAsync(product);
                }
            }

            await _writeRepository.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task AddProductsInBatchAsync(List<Product> products)
    {
        await _writeRepository.AddRangeAsync(products);
        await _writeRepository.SaveChangesAsync();
    }
}

public class ProductPriceUpdate
{
    public Guid ProductId { get; set; }
    public decimal NewPrice { get; set; }
}
```

## Configuration

### Repository Registration Pattern

```csharp
using Microsoft.Extensions.DependencyInjection;
using DKNet.EfCore.Repos.Abstractions;

// Register repositories in DI container
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        // Register read-only repositories
        services.AddScoped<IReadRepository<Product>, ProductReadRepository>();
        services.AddScoped<IReadRepository<Customer>, CustomerReadRepository>();

        // Register write repositories
        services.AddScoped<IWriteRepository<Product>, ProductWriteRepository>();
        services.AddScoped<IWriteRepository<Customer>, CustomerWriteRepository>();

        // Register full repositories
        services.AddScoped<IRepository<Product>, ProductRepository>();
        services.AddScoped<IRepository<Customer>, CustomerRepository>();

        // Register custom repositories
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();

        return services;
    }
}
```

## API Reference

### Core Interfaces

- `IRepository<TEntity>` - Combined read and write operations
- `IReadRepository<TEntity>` - Read-only operations and queries
- `IWriteRepository<TEntity>` - Write operations and transaction management

### Read Operations

- `Gets()` - Get IQueryable for entity
- `GetDto<TModel>(Expression<Func<TEntity, bool>>?)` - Get projection with optional filter
- `FindAsync(object, CancellationToken)` - Find entity by primary key
- `FindAsync(object[], CancellationToken)` - Find entity by composite key
- `AnyAsync(Expression<Func<TEntity, bool>>, CancellationToken)` - Check if any entity matches condition
- `CountAsync(Expression<Func<TEntity, bool>>, CancellationToken)` - Count entities matching condition

### Write Operations

- `AddAsync(TEntity, CancellationToken)` - Add single entity
- `AddRangeAsync(IEnumerable<TEntity>, CancellationToken)` - Add multiple entities
- `UpdateAsync(TEntity, CancellationToken)` - Update single entity
- `UpdateRangeAsync(IEnumerable<TEntity>, CancellationToken)` - Update multiple entities
- `DeleteAsync(TEntity, CancellationToken)` - Delete single entity
- `DeleteRangeAsync(IEnumerable<TEntity>, CancellationToken)` - Delete multiple entities
- `SaveChangesAsync(CancellationToken)` - Persist changes to database

### Transaction Management

- `BeginTransactionAsync(CancellationToken)` - Begin database transaction
- `Entry(TEntity)` - Get EntityEntry for change tracking

## Advanced Usage

### Custom Repository with Business Logic

```csharp
public interface IOrderRepository : IRepository<Order>
{
    Task<IEnumerable<Order>> GetOrdersByStatusAsync(OrderStatus status, CancellationToken cancellationToken = default);
    Task<Order?> GetOrderWithItemsAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<decimal> GetTotalSalesAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
}

public class OrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProductRepository _productRepository;

    public OrderService(IOrderRepository orderRepository, IProductRepository productRepository)
    {
        _orderRepository = orderRepository;
        _productRepository = productRepository;
    }

    public async Task<Order> CreateOrderAsync(Guid customerId, List<OrderItemRequest> items)
    {
        using var transaction = await _orderRepository.BeginTransactionAsync();

        try
        {
            var order = new Order(customerId);

            // Validate and add items
            foreach (var item in items)
            {
                var product = await _productRepository.FindAsync(item.ProductId);
                if (product == null)
                    throw new InvalidOperationException($"Product {item.ProductId} not found");

                order.AddItem(item.ProductId, item.Quantity, product.Price);
            }

            await _orderRepository.AddAsync(order);
            await _orderRepository.SaveChangesAsync();
            await transaction.CommitAsync();

            return order;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
```

### Repository with Specifications Pattern

```csharp
public class ProductSpecificationService
{
    private readonly IReadRepository<Product> _readRepository;

    public ProductSpecificationService(IReadRepository<Product> readRepository)
    {
        _readRepository = readRepository;
    }

    public async Task<List<Product>> GetProductsAsync(ProductSearchCriteria criteria)
    {
        var query = _readRepository.Gets();

        if (!string.IsNullOrEmpty(criteria.Category))
            query = query.Where(p => p.Category == criteria.Category);

        if (criteria.MinPrice.HasValue)
            query = query.Where(p => p.Price >= criteria.MinPrice.Value);

        if (criteria.MaxPrice.HasValue)
            query = query.Where(p => p.Price <= criteria.MaxPrice.Value);

        if (!string.IsNullOrEmpty(criteria.SearchTerm))
            query = query.Where(p => p.Name.Contains(criteria.SearchTerm));

        return await query
            .OrderBy(p => p.Name)
            .Skip(criteria.Skip)
            .Take(criteria.Take)
            .ToListAsync();
    }
}

public class ProductSearchCriteria
{
    public string? Category { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public string? SearchTerm { get; set; }
    public int Skip { get; set; } = 0;
    public int Take { get; set; } = 50;
}
```

### Unit of Work Pattern Integration

```csharp
public interface IUnitOfWork
{
    IProductRepository Products { get; }
    ICustomerRepository Customers { get; }
    IOrderRepository Orders { get; }
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}

public class BusinessService
{
    private readonly IUnitOfWork _unitOfWork;

    public BusinessService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task ProcessOrderAsync(OrderRequest request)
    {
        using var transaction = await _unitOfWork.BeginTransactionAsync();

        try
        {
            // Create customer if not exists
            var customer = await _unitOfWork.Customers.FindAsync(request.CustomerId);
            if (customer == null)
            {
                customer = new Customer(request.CustomerEmail);
                await _unitOfWork.Customers.AddAsync(customer);
            }

            // Create order
            var order = new Order(customer.Id);
            await _unitOfWork.Orders.AddAsync(order);

            // Update product inventory
            foreach (var item in request.Items)
            {
                var product = await _unitOfWork.Products.FindAsync(item.ProductId);
                if (product != null)
                {
                    product.ReduceInventory(item.Quantity);
                    await _unitOfWork.Products.UpdateAsync(product);
                }
            }

            await _unitOfWork.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
```

## Design Principles

### CQRS Separation

- **IReadRepository**: Optimized for queries and projections
- **IWriteRepository**: Focused on data modifications and transactions
- **IRepository**: Combines both when full access is needed

### Entity Framework Integration

- Direct integration with EF Core change tracking
- Transaction support through IDbContextTransaction
- IQueryable support for deferred execution

### Testability

- Interface-based design for easy mocking
- Separation of concerns between read and write operations
- Clear contracts for business logic testing

## Performance Considerations

- **Projections**: Use `GetDto<T>()` for efficient queries that only select needed columns
- **IQueryable**: Leverage deferred execution for complex query building
- **Batch Operations**: Use range methods for bulk operations
- **Transactions**: Use transactions for atomic operations across multiple entities
- **Change Tracking**: EF Core change tracking is automatically managed

## Thread Safety

- Repository instances should be scoped to request/operation lifetime
- Concurrent access to different entities is safe
- Shared entity instances require external synchronization
- Transaction isolation follows EF Core/database provider rules

## Contributing

See the main [CONTRIBUTING.md](../../../../CONTRIBUTING.md) for guidelines on how to contribute to this project.

## License

This project is licensed under the [MIT License](../../../../LICENSE).

## Related Packages

- [DKNet.EfCore.Repos](../DKNet.EfCore.Repos) - Concrete repository implementations
- [DKNet.EfCore.Abstractions](../DKNet.EfCore.Abstractions) - Core entity abstractions
- [DKNet.EfCore.Extensions](../DKNet.EfCore.Extensions) - EF Core functionality extensions
- [DKNet.EfCore.Specifications](../DKNet.EfCore.Specifications) - Specification pattern support

---

Part of the [DKNet Framework](https://github.com/baoduy/DKNet) - A comprehensive .NET framework for building modern,
scalable applications.