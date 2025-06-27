# DKNet.EfCore.Repos

**Repository pattern implementations that provide a comprehensive foundation for data access operations in Entity Framework Core applications, implementing the Repository and Unit of Work patterns while supporting Domain-Driven Design (DDD) and Onion Architecture principles.**

## What is this project?

DKNet.EfCore.Repos provides concrete implementations of the repository pattern for Entity Framework Core applications. Building upon the abstractions defined in DKNet.EfCore.Repos.Abstractions, this library offers ready-to-use repository classes that encapsulate common data access operations while maintaining clean separation between domain logic and data persistence concerns.

### Key Features

- **Generic Repository Implementation**: Complete CRUD operations with EF Core optimization
- **Specialized Repository Classes**: ReadRepository, WriteRepository, and combined Repository
- **Projection Support**: Integrated AutoMapper support for efficient data projections
- **Query Optimization**: IQueryable support for flexible and performant database queries
- **Async Operations**: Full async/await support with cancellation tokens
- **Change Tracking**: Optimized EF Core change tracking patterns
- **Unit of Work Integration**: Seamless integration with DbContext for transaction management
- **Type Safety**: Generic constraints ensuring compile-time type safety

## How it contributes to DDD and Onion Architecture

### Infrastructure Layer Implementation

DKNet.EfCore.Repos implements the **Infrastructure Layer** of the Onion Architecture, providing concrete repository implementations that fulfill the contracts defined in the domain layer:

```
┌─────────────────────────────────────────────────────────────────┐
│                    🌐 Presentation Layer                        │
│                   (Controllers, API Endpoints)                  │
│                                                                 │
│  No direct dependencies on repository implementations          │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                   🎯 Application Layer                          │
│              (Use Cases, Application Services)                  │
│                                                                 │
│  Depends on: Repository abstractions (interfaces)              │
│  Uses: ICustomerRepository, IOrderRepository, etc.             │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                    💼 Domain Layer                             │
│           (Entities, Aggregates, Domain Services)              │
│                                                                 │
│  Defines: Repository interfaces and contracts                  │
│  📋 ICustomerRepository interface                              │
│  📋 IOrderRepository interface                                 │
│  🏷️ No dependency on concrete implementations                  │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                 🗄️ Infrastructure Layer                        │
│                  (Data Access, Persistence)                    │
│                                                                 │
│  🗃️ Repository<TEntity> - Generic repository base class       │
│  📖 ReadRepository<TEntity> - Read-only operations            │
│  ✏️ WriteRepository<TEntity> - Write operations only           │
│  🎯 CustomerRepository : Repository<Customer>                  │
│  🛒 OrderRepository : Repository<Order>                        │
└─────────────────────────────────────────────────────────────────┘
```

### DDD Benefits

1. **Aggregate Boundary Enforcement**: Repositories enforce aggregate consistency rules
2. **Domain Logic Isolation**: Business logic remains in domain entities, not repositories
3. **Technology Independence**: Domain layer unaware of EF Core specifics
4. **Query Flexibility**: Support for complex domain queries without exposing EF Core
5. **Transaction Management**: Proper aggregate transaction boundaries

### Onion Architecture Benefits

1. **Dependency Inversion**: Infrastructure implements interfaces defined in domain
2. **Testability**: Easy to mock repository interfaces for unit testing
3. **Separation of Concerns**: Data access logic isolated from business logic
4. **Pluggability**: Easy to swap repository implementations
5. **Maintainability**: Clear boundaries between layers

## How to use it

### Installation

```bash
dotnet add package DKNet.EfCore.Repos
dotnet add package DKNet.EfCore.Repos.Abstractions
```

### Basic Usage Examples

#### 1. Generic Repository Usage

```csharp
using DKNet.EfCore.Repos;

public class CustomerService
{
    private readonly IRepository<Customer> _customerRepository;
    private readonly DbContext _context;
    
    public CustomerService(IRepository<Customer> customerRepository, DbContext context)
    {
        _customerRepository = customerRepository;
        _context = context;
    }
    
    // Read operations
    public async Task<Customer?> GetCustomerAsync(int customerId)
    {
        return await _customerRepository.FindAsync(customerId);
    }
    
    public async Task<IEnumerable<Customer>> GetCustomersByRegionAsync(string region)
    {
        return await _customerRepository.Gets()
            .Where(c => c.Region == region)
            .ToListAsync();
    }
    
    // Write operations
    public async Task<Customer> CreateCustomerAsync(Customer customer)
    {
        await _customerRepository.AddAsync(customer);
        await _context.SaveChangesAsync();
        return customer;
    }
}
```

#### 2. Specialized Repository Implementation

```csharp
// Domain interface (in Domain layer)
public interface ICustomerRepository : IRepository<Customer>
{
    Task<Customer?> GetByEmailAsync(string email);
    Task<IEnumerable<Customer>> GetActiveCustomersAsync();
    Task<PagedList<Customer>> GetCustomersPagedAsync(int page, int pageSize);
}

// Infrastructure implementation (in Infrastructure layer)
public class CustomerRepository : Repository<Customer>, ICustomerRepository
{
    public CustomerRepository(DbContext dbContext, IEnumerable<IMapper>? mappers = null) 
        : base(dbContext, mappers)
    {
    }
    
    public async Task<Customer?> GetByEmailAsync(string email)
    {
        return await FindAsync(c => c.Email == email);
    }
    
    public async Task<IEnumerable<Customer>> GetActiveCustomersAsync()
    {
        return await Gets()
            .Where(c => c.IsActive)
            .OrderBy(c => c.LastName)
            .ToListAsync();
    }
    
    public async Task<PagedList<Customer>> GetCustomersPagedAsync(int page, int pageSize)
    {
        var query = Gets().OrderBy(c => c.LastName);
        return await PagedList<Customer>.CreateAsync(query, page, pageSize);
    }
}
```

#### 3. Read-Only Repository Usage

```csharp
public class CustomerQueryService
{
    private readonly IReadRepository<Customer> _customerReadRepository;
    
    public CustomerQueryService(IReadRepository<Customer> customerReadRepository)
    {
        _customerReadRepository = customerReadRepository;
    }
    
    // Projection for reporting
    public async Task<IEnumerable<CustomerSummaryDto>> GetCustomerSummariesAsync()
    {
        return await _customerReadRepository.GetProjection<CustomerSummaryDto>()
            .ToListAsync();
    }
    
    // Complex query with filtering
    public async Task<IEnumerable<Customer>> SearchCustomersAsync(string searchTerm)
    {
        return await _customerReadRepository.Gets()
            .Where(c => c.FirstName.Contains(searchTerm) || 
                       c.LastName.Contains(searchTerm) ||
                       c.Email.Contains(searchTerm))
            .Take(50)
            .ToListAsync();
    }
}
```

#### 4. Unit of Work Pattern with Multiple Repositories

```csharp
public class OrderService
{
    private readonly IRepository<Order> _orderRepository;
    private readonly IRepository<Customer> _customerRepository;
    private readonly IRepository<Product> _productRepository;
    private readonly DbContext _context;
    
    public OrderService(
        IRepository<Order> orderRepository,
        IRepository<Customer> customerRepository, 
        IRepository<Product> productRepository,
        DbContext context)
    {
        _orderRepository = orderRepository;
        _customerRepository = customerRepository;
        _productRepository = productRepository;
        _context = context;
    }
    
    public async Task<Order> CreateOrderAsync(CreateOrderRequest request)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Validate customer exists
            var customer = await _customerRepository.FindAsync(request.CustomerId);
            if (customer == null)
                throw new InvalidOperationException("Customer not found");
            
            // Validate products exist and update inventory
            var products = new List<Product>();
            foreach (var item in request.Items)
            {
                var product = await _productRepository.FindAsync(item.ProductId);
                if (product == null)
                    throw new InvalidOperationException($"Product {item.ProductId} not found");
                
                product.ReduceInventory(item.Quantity);
                products.Add(product);
            }
            
            // Create order
            var order = new Order(customer.Id, request.Items);
            await _orderRepository.AddAsync(order);
            
            // Save all changes in single transaction
            await _context.SaveChangesAsync();
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

### Advanced Usage Examples

#### 1. Custom Repository with Complex Queries

```csharp
public class OrderRepository : Repository<Order>, IOrderRepository
{
    public OrderRepository(DbContext dbContext, IEnumerable<IMapper>? mappers = null) 
        : base(dbContext, mappers)
    {
    }
    
    public async Task<IEnumerable<Order>> GetOrdersByDateRangeAsync(
        DateTime startDate, 
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        return await Gets()
            .Include(o => o.Customer)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .Where(o => o.OrderDate >= startDate && o.OrderDate <= endDate)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync(cancellationToken);
    }
    
    public async Task<decimal> GetTotalRevenueAsync(int customerId)
    {
        return await Gets()
            .Where(o => o.CustomerId == customerId && o.Status == OrderStatus.Completed)
            .SelectMany(o => o.OrderItems)
            .SumAsync(oi => oi.Price * oi.Quantity);
    }
}
```

#### 2. Repository with AutoMapper Projections

```csharp
public class ProductService
{
    private readonly IRepository<Product> _productRepository;
    
    public ProductService(IRepository<Product> productRepository)
    {
        _productRepository = productRepository;
    }
    
    // Efficient projection using AutoMapper
    public async Task<IEnumerable<ProductListDto>> GetProductListAsync()
    {
        return await _productRepository.GetProjection<ProductListDto>()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }
    
    // Projection with pagination
    public async Task<PagedList<ProductSummaryDto>> GetProductsPagedAsync(
        int page, 
        int pageSize,
        string? categoryFilter = null)
    {
        var query = _productRepository.GetProjection<ProductSummaryDto>();
        
        if (!string.IsNullOrEmpty(categoryFilter))
        {
            query = query.Where(p => p.Category == categoryFilter);
        }
        
        return await PagedList<ProductSummaryDto>.CreateAsync(
            query.OrderBy(p => p.Name), 
            page, 
            pageSize);
    }
}
```

## Best Practices

### 1. Repository Interface Design

```csharp
// Good: Domain-specific methods with business meaning
public interface IOrderRepository : IRepository<Order>
{
    Task<IEnumerable<Order>> GetPendingOrdersAsync();
    Task<Order?> GetOrderByNumberAsync(string orderNumber);
    Task<IEnumerable<Order>> GetOrdersByCustomerAsync(int customerId);
}

// Avoid: Generic queries that leak implementation details
public interface IOrderRepository : IRepository<Order>
{
    Task<IEnumerable<Order>> GetOrdersAsync(Expression<Func<Order, bool>> predicate);
}
```

### 2. Transaction Management

```csharp
// Good: Explicit transaction control for business operations
public async Task<Result> ProcessBulkOrderAsync(IEnumerable<CreateOrderRequest> requests)
{
    using var transaction = await _context.Database.BeginTransactionAsync();
    try
    {
        foreach (var request in requests)
        {
            var order = await CreateOrderInternalAsync(request);
            await _orderRepository.AddAsync(order);
        }
        
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return Result.Success();
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        return Result.Failure(ex.Message);
    }
}
```

### 3. Efficient Querying

```csharp
// Good: Use projections for read-heavy operations
public async Task<IEnumerable<CustomerSummaryDto>> GetCustomerSummariesAsync()
{
    return await _customerRepository.GetProjection<CustomerSummaryDto>()
        .Where(c => c.IsActive)
        .ToListAsync();
}

// Good: Use Include for related data when needed
public async Task<Order?> GetOrderWithDetailsAsync(int orderId)
{
    return await _orderRepository.Gets()
        .Include(o => o.Customer)
        .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.Product)
        .FirstOrDefaultAsync(o => o.Id == orderId);
}
```

### 4. Cancellation Token Usage

```csharp
public async Task<IEnumerable<Product>> SearchProductsAsync(
    string searchTerm, 
    CancellationToken cancellationToken = default)
{
    return await _productRepository.Gets()
        .Where(p => p.Name.Contains(searchTerm) || p.Description.Contains(searchTerm))
        .ToListAsync(cancellationToken);
}
```

### 5. Repository Testing

```csharp
[Test]
public async Task GetCustomerByEmail_ExistingEmail_ReturnsCustomer()
{
    // Arrange
    var dbContext = CreateInMemoryDbContext();
    var repository = new CustomerRepository(dbContext);
    
    var customer = new Customer("John", "Doe", "john@example.com");
    await repository.AddAsync(customer);
    await dbContext.SaveChangesAsync();
    
    // Act
    var result = await repository.GetByEmailAsync("john@example.com");
    
    // Assert
    Assert.NotNull(result);
    Assert.Equal("john@example.com", result.Email);
}
```

## Integration with Other DKNet Components

DKNet.EfCore.Repos integrates seamlessly with other DKNet components:

- **DKNet.EfCore.Abstractions**: Uses base entity types and interfaces
- **DKNet.EfCore.Events**: Supports automatic domain event publishing
- **DKNet.EfCore.Hooks**: Enables lifecycle event handling
- **DKNet.EfCore.DataAuthorization**: Provides data access filtering
- **DKNet.SlimBus.Extensions**: Integrates with CQRS message handling
- **DKNet.Fw.Extensions**: Leverages core framework utilities

---

> 💡 **Architecture Tip**: Use DKNet.EfCore.Repos to implement the infrastructure layer of your Onion Architecture. Define repository interfaces in your domain layer and implement them using these repository base classes in your infrastructure layer. This maintains proper dependency inversion and keeps your domain logic clean and testable.