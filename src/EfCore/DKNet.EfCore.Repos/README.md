# DKNet.EfCore.Repos

[![NuGet](https://img.shields.io/nuget/v/DKNet.EfCore.Repos)](https://www.nuget.org/packages/DKNet.EfCore.Repos/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DKNet.EfCore.Repos)](https://www.nuget.org/packages/DKNet.EfCore.Repos/)
[![.NET](https://img.shields.io/badge/.NET-9.0-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](../../../../LICENSE)

Concrete implementations of the Repository pattern for Entity Framework Core, providing ready-to-use repository classes
that implement the abstractions from DKNet.EfCore.Repos.Abstractions. This package includes generic repositories,
automatic DI registration, and Mapster integration for projections.

## Features

- **Concrete Repository Implementations**: Ready-to-use implementations of repository abstractions
- **Generic Repository Support**: Type-safe generic repositories for any entity
- **Automatic DI Registration**: Simple service collection extensions for dependency injection
- **Mapster Integration**: Built-in projection support with Mapster for efficient queries
- **CQRS Implementation**: Separate read and write repository implementations
- **DbContext Integration**: Direct Entity Framework Core integration with change tracking
- **Transaction Support**: Built-in transaction management for complex operations
- **Performance Optimized**: Efficient query execution with projection capabilities

## Supported Frameworks

- .NET 9.0+
- Entity Framework Core 9.0+
- Mapster (for projections)

## Installation

Install via NuGet Package Manager:

```bash
dotnet add package DKNet.EfCore.Repos
```

Or via Package Manager Console:

```powershell
Install-Package DKNet.EfCore.Repos
```

## Quick Start

### Setup with Dependency Injection

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public DbSet<Product> Products { get; set; }
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Order> Orders { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
}

// Configure services
public void ConfigureServices(IServiceCollection services)
{
    // Add DbContext
    services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(connectionString));

    // Add Mapster for projections
    services.AddMapster();

    // Add generic repositories
    services.AddGenericRepositories<AppDbContext>();
}
```

### Basic Repository Usage

```csharp
public class ProductService
{
    private readonly IRepository<Product> _productRepository;
    private readonly IReadRepository<Customer> _customerRepository;

    public ProductService(
        IRepository<Product> productRepository,
        IReadRepository<Customer> customerRepository)
    {
        _productRepository = productRepository;
        _customerRepository = customerRepository;
    }

    public async Task<Product> CreateProductAsync(CreateProductRequest request)
    {
        var product = new Product(request.Name, request.Price, request.Category);
        
        await _productRepository.AddAsync(product);
        await _productRepository.SaveChangesAsync();
        
        return product;
    }

    public async Task<List<ProductDto>> GetActiveProductsAsync()
    {
        // Efficient projection using Mapster
        return await _productRepository
            .GetDto<ProductDto>(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<Product?> GetProductByIdAsync(Guid id)
    {
        return await _productRepository.FindAsync(id);
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

### Advanced CQRS Usage

```csharp
public class OrderQueryService
{
    private readonly IReadRepository<Order> _orderReadRepository;

    public OrderQueryService(IReadRepository<Order> orderReadRepository)
    {
        _orderReadRepository = orderReadRepository;
    }

    public async Task<OrderSummaryDto?> GetOrderSummaryAsync(Guid orderId)
    {
        return await _orderReadRepository
            .GetDto<OrderSummaryDto>(o => o.Id == orderId)
            .FirstOrDefaultAsync();
    }

    public async Task<List<OrderListDto>> GetCustomerOrdersAsync(Guid customerId)
    {
        return await _orderReadRepository
            .GetDto<OrderListDto>(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedDate)
            .ToListAsync();
    }
}

public class OrderCommandService
{
    private readonly IWriteRepository<Order> _orderWriteRepository;
    private readonly IReadRepository<Product> _productReadRepository;

    public OrderCommandService(
        IWriteRepository<Order> orderWriteRepository,
        IReadRepository<Product> productReadRepository)
    {
        _orderWriteRepository = orderWriteRepository;
        _productReadRepository = productReadRepository;
    }

    public async Task<Guid> CreateOrderAsync(CreateOrderRequest request)
    {
        using var transaction = await _orderWriteRepository.BeginTransactionAsync();

        try
        {
            var order = new Order(request.CustomerId);

            // Validate products exist
            foreach (var item in request.Items)
            {
                var product = await _productReadRepository.FindAsync(item.ProductId);
                if (product == null)
                    throw new InvalidOperationException($"Product {item.ProductId} not found");

                order.AddItem(item.ProductId, item.Quantity, product.Price);
            }

            await _orderWriteRepository.AddAsync(order);
            await _orderWriteRepository.SaveChangesAsync();
            await transaction.CommitAsync();

            return order.Id;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
```

## Configuration

### Mapster Configuration for Projections

```csharp
public static class MappingConfig
{
    public static void ConfigureMappings()
    {
        TypeAdapterConfig.GlobalSettings.Scan(typeof(Product).Assembly);
        
        // Custom mapping configuration
        TypeAdapterConfig<Product, ProductDto>
            .NewConfig()
            .Map(dest => dest.CategoryName, src => src.Category.Name)
            .Map(dest => dest.IsOnSale, src => src.DiscountPercentage > 0);

        TypeAdapterConfig<Order, OrderSummaryDto>
            .NewConfig()
            .Map(dest => dest.TotalAmount, src => src.Items.Sum(i => i.TotalPrice))
            .Map(dest => dest.ItemCount, src => src.Items.Count);
    }
}

// Register in Startup
public void ConfigureServices(IServiceCollection services)
{
    services.AddMapster();
    MappingConfig.ConfigureMappings();
    
    services.AddGenericRepositories<AppDbContext>();
}
```

### Custom Repository Implementation

```csharp
public interface IProductRepository : IRepository<Product>
{
    Task<IEnumerable<Product>> GetProductsByCategoryAsync(string category);
    Task<bool> ExistsBySkuAsync(string sku);
    Task<Product?> GetProductWithCategoryAsync(Guid id);
}

public class ProductRepository : Repository<Product>, IProductRepository
{
    public ProductRepository(DbContext dbContext, IEnumerable<IMapper>? mappers = null) 
        : base(dbContext, mappers)
    {
    }

    public async Task<IEnumerable<Product>> GetProductsByCategoryAsync(string category)
    {
        return await Gets()
            .Where(p => p.Category == category && p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<bool> ExistsBySkuAsync(string sku)
    {
        return await Gets().AnyAsync(p => p.Sku == sku);
    }

    public async Task<Product?> GetProductWithCategoryAsync(Guid id)
    {
        return await Gets()
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id);
    }
}

// Register custom repository
services.AddScoped<IProductRepository, ProductRepository>();
```

## API Reference

### Core Repository Classes

- `Repository<TEntity>` - Full repository implementation combining read and write operations
- `ReadRepository<TEntity>` - Read-only repository implementation
- `WriteRepository<TEntity>` - Write-only repository implementation

### Setup Extensions

- `AddGenericRepositories<TDbContext>()` - Register all generic repositories with specified DbContext

### Key Methods

#### Read Operations

- `Gets()` - Get IQueryable for building complex queries
- `GetDto<TModel>(filter?)` - Get projected DTOs with optional filtering
- `FindAsync(id)` - Find entity by primary key
- `FindAsync(filter)` - Find first entity matching filter

#### Write Operations

- `AddAsync(entity)` - Add single entity
- `AddRangeAsync(entities)` - Add multiple entities
- `UpdateAsync(entity)` - Update entity
- `DeleteAsync(entity)` - Delete entity
- `SaveChangesAsync()` - Persist changes to database

#### Transaction Operations

- `BeginTransactionAsync()` - Start database transaction
- `Entry(entity)` - Get EntityEntry for change tracking

## Advanced Usage

### Unit of Work Pattern with Generic Repositories

```csharp
public interface IUnitOfWork
{
    IRepository<Product> Products { get; }
    IRepository<Customer> Customers { get; }
    IRepository<Order> Orders { get; }
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private readonly IServiceProvider _serviceProvider;

    public UnitOfWork(AppDbContext context, IServiceProvider serviceProvider)
    {
        _context = context;
        _serviceProvider = serviceProvider;
    }

    public IRepository<Product> Products => _serviceProvider.GetRequiredService<IRepository<Product>>();
    public IRepository<Customer> Customers => _serviceProvider.GetRequiredService<IRepository<Customer>>();
    public IRepository<Order> Orders => _serviceProvider.GetRequiredService<IRepository<Order>>();

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => _context.Database.BeginTransactionAsync(cancellationToken);
}
```

### Bulk Operations with Repositories

```csharp
public class BulkOperationService
{
    private readonly IWriteRepository<Product> _productRepository;

    public BulkOperationService(IWriteRepository<Product> productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task BulkUpdatePricesAsync(Dictionary<Guid, decimal> priceUpdates)
    {
        using var transaction = await _productRepository.BeginTransactionAsync();

        try
        {
            var productIds = priceUpdates.Keys.ToList();
            var products = await _productRepository.Gets()
                .Where(p => productIds.Contains(p.Id))
                .ToListAsync();

            foreach (var product in products)
            {
                if (priceUpdates.TryGetValue(product.Id, out var newPrice))
                {
                    product.UpdatePrice(newPrice);
                }
            }

            await _productRepository.UpdateRangeAsync(products);
            await _productRepository.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task BulkDeleteByIdsAsync(List<Guid> ids)
    {
        var entities = await _productRepository.Gets()
            .Where(p => ids.Contains(p.Id))
            .ToListAsync();

        if (entities.Any())
        {
            await _productRepository.DeleteRangeAsync(entities);
            await _productRepository.SaveChangesAsync();
        }
    }
}
```

### Repository with Caching

```csharp
public class CachedProductRepository : IReadRepository<Product>
{
    private readonly IReadRepository<Product> _repository;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(15);

    public CachedProductRepository(IReadRepository<Product> repository, IMemoryCache cache)
    {
        _repository = repository;
        _cache = cache;
    }

    public async ValueTask<Product?> FindAsync(object keyValue, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"product:{keyValue}";
        
        if (_cache.TryGetValue(cacheKey, out Product? cachedProduct))
            return cachedProduct;

        var product = await _repository.FindAsync(keyValue, cancellationToken);
        
        if (product != null)
        {
            _cache.Set(cacheKey, product, _cacheExpiry);
        }

        return product;
    }

    public IQueryable<Product> Gets() => _repository.Gets();

    public IQueryable<TModel> GetDto<TModel>(Expression<Func<Product, bool>>? filter = null) 
        where TModel : class => _repository.GetDto<TModel>(filter);

    // Implement other methods...
}
```

### Integration with MediatR

```csharp
public class GetProductQueryHandler : IRequestHandler<GetProductQuery, ProductDto?>
{
    private readonly IReadRepository<Product> _repository;

    public GetProductQueryHandler(IReadRepository<Product> repository)
    {
        _repository = repository;
    }

    public async Task<ProductDto?> Handle(GetProductQuery request, CancellationToken cancellationToken)
    {
        return await _repository
            .GetDto<ProductDto>(p => p.Id == request.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}

public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Guid>
{
    private readonly IWriteRepository<Product> _repository;

    public CreateProductCommandHandler(IWriteRepository<Product> repository)
    {
        _repository = repository;
    }

    public async Task<Guid> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var product = new Product(request.Name, request.Price, request.Category);
        
        await _repository.AddAsync(product, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        
        return product.Id;
    }
}
```

## Performance Considerations

- **Projections**: Always use `GetDto<T>()` for read-only queries to minimize data transfer
- **Change Tracking**: Write repositories automatically handle EF Core change tracking
- **Query Optimization**: `Gets()` returns IQueryable for efficient query composition
- **Batch Operations**: Use range methods for bulk operations to reduce database round trips
- **Mapster Integration**: Efficient projections without manual mapping code

## Best Practices

- **Separation of Concerns**: Use read repositories for queries, write repositories for modifications
- **Transaction Management**: Always use transactions for multi-entity operations
- **Custom Repositories**: Extend generic repositories for complex business logic
- **Projection Usage**: Prefer DTOs over entities for read operations
- **Error Handling**: Wrap transaction operations in try-catch blocks

## Thread Safety

- Repository instances are designed to be used within a single request/operation scope
- DbContext instances should not be shared across threads
- Concurrent read operations are safe
- Write operations require external coordination for shared entities

## Contributing

See the main [CONTRIBUTING.md](../../../../CONTRIBUTING.md) for guidelines on how to contribute to this project.

## License

This project is licensed under the [MIT License](../../../../LICENSE).

## Related Packages

- [DKNet.EfCore.Repos.Abstractions](../DKNet.EfCore.Repos.Abstractions) - Repository pattern abstractions
- [DKNet.EfCore.Abstractions](../DKNet.EfCore.Abstractions) - Core entity abstractions
- [DKNet.EfCore.Extensions](../DKNet.EfCore.Extensions) - EF Core functionality extensions
- [DKNet.EfCore.Specifications](../DKNet.EfCore.Specifications) - Specification pattern support

---

Part of the [DKNet Framework](https://github.com/baoduy/DKNet) - A comprehensive .NET framework for building modern,
scalable applications.