# DKNet.EfCore.Repos.Abstractions

**Repository pattern abstractions that define contracts for data access operations, enabling clean separation between domain logic and data persistence concerns while supporting Domain-Driven Design (DDD) and Onion Architecture principles.**

## What is this project?

DKNet.EfCore.Repos.Abstractions provides the fundamental contracts and interfaces for implementing the Repository pattern in Entity Framework Core applications. It defines clear separation between read and write operations, supports projection patterns, and enables testable data access layers.

### Key Features

- **IReadRepository<TEntity>**: Read-only operations with IQueryable support
- **IWriteRepository<TEntity>**: Write operations with transaction management
- **IRepository<TEntity>**: Combined read/write operations interface
- **Projection Support**: Efficient querying with projection to DTOs/ViewModels
- **Transaction Management**: Built-in transaction support for complex operations
- **Bulk Operations**: Support for high-performance bulk operations (extensible)
- **Async/Await Support**: Full async support for scalable applications
- **Generic Design**: Type-safe operations with compile-time validation

## How it contributes to DDD and Onion Architecture

### Repository Pattern in Onion Architecture

The Repository Abstractions implement the Repository pattern as defined in DDD, providing a clean separation between the domain and infrastructure layers:

```
┌─────────────────────────────────────────────────────────────────┐
│                    🌐 Presentation Layer                        │
│                   (Controllers, API Endpoints)                  │
│                                                                 │
│  No direct dependencies on repository abstractions             │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                   🎯 Application Layer                          │
│              (Use Cases, Application Services)                  │
│                                                                 │
│  Uses: IRepository<T> for orchestrating domain operations      │
│  Benefits: Clear contracts, easy testing, dependency injection │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                    💼 Domain Layer                             │
│           (Entities, Aggregates, Domain Services)              │
│                                                                 │
│  📋 Defines: ICustomerRepository, IOrderRepository             │
│  📝 Extends: IRepository<T> with domain-specific operations    │
│  🏷️ Benefits: Technology-agnostic, testable, focused on domain │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                 🗄️ Infrastructure Layer                        │
│                  (Data Access, Persistence)                    │
│                                                                 │
│  🗃️ Implements: CustomerRepository : ICustomerRepository       │
│  📊 Implements: OrderRepository : IOrderRepository             │
│  ⚙️ Uses: EF Core, DbContext, specific database technologies    │
└─────────────────────────────────────────────────────────────────┘
```

### DDD Benefits

1. **Aggregate Boundary Enforcement**: Each repository typically corresponds to an aggregate root
2. **Domain-Focused Interfaces**: Repository interfaces are defined in terms of domain concepts
3. **Persistence Ignorance**: Domain layer doesn't know about EF Core, SQL, or database specifics
4. **Testability**: Easy to mock repositories for unit testing domain logic
5. **Encapsulation**: Hide complex queries and data access logic from domain services

### Onion Architecture Benefits

1. **Dependency Inversion**: Domain defines contracts, infrastructure implements them
2. **Technology Independence**: Can switch from EF Core to another ORM without changing domain
3. **Testability**: Mock repositories enable fast, isolated unit tests
4. **Separation of Concerns**: Clear boundary between domain logic and data access
5. **Flexibility**: Different implementations for different contexts (testing, production, etc.)

## How to use it

### Installation

```bash
dotnet add package DKNet.EfCore.Repos.Abstractions
```

### Basic Usage Examples

#### 1. Defining Domain-Specific Repository Interfaces

```csharp
using DKNet.EfCore.Repos.Abstractions;
using DKNet.EfCore.Abstractions.Entities;

// Domain entity
public class Customer : Entity
{
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public CustomerStatus Status { get; private set; }
    
    // Domain methods...
}

// Domain-specific repository interface (defined in Domain layer)
public interface ICustomerRepository : IRepository<Customer>
{
    // Domain-specific query methods
    Task<Customer?> GetByEmailAsync(string email);
    Task<IEnumerable<Customer>> GetActiveCustomersAsync();
    Task<IEnumerable<Customer>> GetCustomersByStatusAsync(CustomerStatus status);
    Task<bool> EmailExistsAsync(string email, Guid? excludeCustomerId = null);
    
    // Projection methods for read models
    Task<CustomerSummaryDto?> GetCustomerSummaryAsync(Guid customerId);
    Task<IEnumerable<CustomerListDto>> GetCustomerListAsync(int page, int pageSize);
}

// DTOs for projections
public record CustomerSummaryDto(Guid Id, string FullName, string Email, int OrderCount);
public record CustomerListDto(Guid Id, string FullName, string Email, CustomerStatus Status);
```

#### 2. Read Repository Usage

```csharp
using DKNet.EfCore.Repos.Abstractions;

public class CustomerQueryService
{
    private readonly IReadRepository<Customer> _customerReadRepository;
    
    public CustomerQueryService(IReadRepository<Customer> customerReadRepository)
    {
        _customerReadRepository = customerReadRepository;
    }
    
    // Basic querying with IQueryable
    public async Task<List<Customer>> GetActiveCustomersAsync()
    {
        return await _customerReadRepository
            .Gets()
            .Where(c => c.Status == CustomerStatus.Active)
            .OrderBy(c => c.LastName)
            .ToListAsync();
    }
    
    // Efficient projection queries
    public async Task<List<CustomerListDto>> GetCustomerListAsync()
    {
        return await _customerReadRepository
            .GetProjection<CustomerListDto>()
            .Where(c => c.Status == CustomerStatus.Active)
            .OrderBy(c => c.FullName)
            .ToListAsync();
    }
    
    // Find by ID
    public async Task<Customer?> GetCustomerAsync(Guid id)
    {
        return await _customerReadRepository.FindAsync(id);
    }
    
    // Find by filter
    public async Task<Customer?> GetCustomerByEmailAsync(string email)
    {
        return await _customerReadRepository.FindAsync(c => c.Email == email);
    }
}
```

#### 3. Write Repository Usage

```csharp
using DKNet.EfCore.Repos.Abstractions;

public class CustomerManagementService
{
    private readonly IWriteRepository<Customer> _customerWriteRepository;
    
    public CustomerManagementService(IWriteRepository<Customer> customerWriteRepository)
    {
        _customerWriteRepository = customerWriteRepository;
    }
    
    // Single entity operations
    public async Task<Result> CreateCustomerAsync(string firstName, string lastName, string email)
    {
        try
        {
            var customer = Customer.Create(firstName, lastName, email);
            _customerWriteRepository.Add(customer);
            
            await _customerWriteRepository.SaveChangesAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to create customer: {ex.Message}");
        }
    }
    
    // Batch operations
    public async Task<Result> CreateMultipleCustomersAsync(IEnumerable<CreateCustomerRequest> requests)
    {
        try
        {
            var customers = requests.Select(r => Customer.Create(r.FirstName, r.LastName, r.Email));
            _customerWriteRepository.AddRange(customers);
            
            await _customerWriteRepository.SaveChangesAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to create customers: {ex.Message}");
        }
    }
    
    // Transaction management
    public async Task<Result> TransferCustomerDataAsync(Guid fromCustomerId, Guid toCustomerId)
    {
        using var transaction = await _customerWriteRepository.BeginTransactionAsync();
        
        try
        {
            // Complex business operation requiring transaction
            var fromCustomer = await _customerWriteRepository.FindAsync(fromCustomerId);
            var toCustomer = await _customerWriteRepository.FindAsync(toCustomerId);
            
            if (fromCustomer == null || toCustomer == null)
                return Result.Failure("Customer not found");
            
            // Perform business logic
            fromCustomer.DeactivateAccount();
            toCustomer.MergeDataFrom(fromCustomer);
            
            _customerWriteRepository.Update(fromCustomer);
            _customerWriteRepository.Update(toCustomer);
            
            await _customerWriteRepository.SaveChangesAsync();
            await transaction.CommitAsync();
            
            return Result.Success();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return Result.Failure($"Failed to transfer customer data: {ex.Message}");
        }
    }
}
```

#### 4. Combined Repository Usage (Full CRUD)

```csharp
using DKNet.EfCore.Repos.Abstractions;

public class CustomerService
{
    private readonly IRepository<Customer> _customerRepository;
    
    public CustomerService(IRepository<Customer> customerRepository)
    {
        _customerRepository = customerRepository;
    }
    
    // Create
    public async Task<Result<Guid>> CreateCustomerAsync(CreateCustomerRequest request)
    {
        // Check if email already exists
        var existingCustomer = await _customerRepository.FindAsync(c => c.Email == request.Email);
        if (existingCustomer != null)
            return Result<Guid>.Failure("Email already exists");
        
        var customer = Customer.Create(request.FirstName, request.LastName, request.Email);
        _customerRepository.Add(customer);
        
        await _customerRepository.SaveChangesAsync();
        return Result<Guid>.Success(customer.Id);
    }
    
    // Read
    public async Task<Result<CustomerDto>> GetCustomerAsync(Guid id)
    {
        var customer = await _customerRepository.FindAsync(id);
        if (customer == null)
            return Result<CustomerDto>.Failure("Customer not found");
        
        return Result<CustomerDto>.Success(MapToDto(customer));
    }
    
    // Update
    public async Task<Result> UpdateCustomerAsync(Guid id, UpdateCustomerRequest request)
    {
        var customer = await _customerRepository.FindAsync(id);
        if (customer == null)
            return Result.Failure("Customer not found");
        
        customer.UpdateDetails(request.FirstName, request.LastName);
        _customerRepository.Update(customer);
        
        await _customerRepository.SaveChangesAsync();
        return Result.Success();
    }
    
    // Delete
    public async Task<Result> DeleteCustomerAsync(Guid id)
    {
        var customer = await _customerRepository.FindAsync(id);
        if (customer == null)
            return Result.Failure("Customer not found");
        
        _customerRepository.Delete(customer);
        await _customerRepository.SaveChangesAsync();
        return Result.Success();
    }
    
    // List with projections
    public async Task<PagedResult<CustomerListDto>> GetCustomersAsync(int page, int pageSize)
    {
        var query = _customerRepository.GetProjection<CustomerListDto>();
        
        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        
        return new PagedResult<CustomerListDto>(items, totalCount, page, pageSize);
    }
}
```

### Advanced Usage Patterns

#### 1. Implementing Domain-Specific Repository

```csharp
using DKNet.EfCore.Repos.Abstractions;

// Implementation in Infrastructure layer
public class CustomerRepository : ICustomerRepository
{
    private readonly IRepository<Customer> _baseRepository;
    
    public CustomerRepository(IRepository<Customer> baseRepository)
    {
        _baseRepository = baseRepository;
    }
    
    // Delegate base operations
    public IQueryable<Customer> Gets() => _baseRepository.Gets();
    public IQueryable<TModel> GetProjection<TModel>() where TModel : class => _baseRepository.GetProjection<TModel>();
    public ValueTask<Customer?> FindAsync(params object[] id) => _baseRepository.FindAsync(id);
    public Task<Customer?> FindAsync(Expression<Func<Customer, bool>> filter, CancellationToken cancellationToken = default) => _baseRepository.FindAsync(filter, cancellationToken);
    
    public void Add(Customer entity) => _baseRepository.Add(entity);
    public void AddRange(IEnumerable<Customer> entities) => _baseRepository.AddRange(entities);
    public void Update(Customer entity) => _baseRepository.Update(entity);
    public void UpdateRange(IEnumerable<Customer> entities) => _baseRepository.UpdateRange(entities);
    public void Delete(Customer entity) => _baseRepository.Delete(entity);
    public void DeleteRange(IEnumerable<Customer> entities) => _baseRepository.DeleteRange(entities);
    
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => _baseRepository.SaveChangesAsync(cancellationToken);
    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) => _baseRepository.BeginTransactionAsync(cancellationToken);
    
    // Domain-specific implementations
    public async Task<Customer?> GetByEmailAsync(string email)
    {
        return await _baseRepository.FindAsync(c => c.Email.ToLower() == email.ToLower());
    }
    
    public async Task<IEnumerable<Customer>> GetActiveCustomersAsync()
    {
        return await _baseRepository
            .Gets()
            .Where(c => c.Status == CustomerStatus.Active)
            .OrderBy(c => c.LastName)
            .ToListAsync();
    }
    
    public async Task<IEnumerable<Customer>> GetCustomersByStatusAsync(CustomerStatus status)
    {
        return await _baseRepository
            .Gets()
            .Where(c => c.Status == status)
            .ToListAsync();
    }
    
    public async Task<bool> EmailExistsAsync(string email, Guid? excludeCustomerId = null)
    {
        var query = _baseRepository.Gets().Where(c => c.Email.ToLower() == email.ToLower());
        
        if (excludeCustomerId.HasValue)
            query = query.Where(c => c.Id != excludeCustomerId.Value);
        
        return await query.AnyAsync();
    }
    
    public async Task<CustomerSummaryDto?> GetCustomerSummaryAsync(Guid customerId)
    {
        return await _baseRepository
            .GetProjection<CustomerSummaryDto>()
            .FirstOrDefaultAsync(c => c.Id == customerId);
    }
    
    public async Task<IEnumerable<CustomerListDto>> GetCustomerListAsync(int page, int pageSize)
    {
        return await _baseRepository
            .GetProjection<CustomerListDto>()
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }
}
```

#### 2. Unit Testing with Repository Abstractions

```csharp
using Moq;
using DKNet.EfCore.Repos.Abstractions;

public class CustomerServiceTests
{
    private readonly Mock<ICustomerRepository> _mockRepository;
    private readonly CustomerService _service;
    
    public CustomerServiceTests()
    {
        _mockRepository = new Mock<ICustomerRepository>();
        _service = new CustomerService(_mockRepository.Object);
    }
    
    [Fact]
    public async Task CreateCustomerAsync_WithValidData_ShouldReturnSuccess()
    {
        // Arrange
        var request = new CreateCustomerRequest("John", "Doe", "john@example.com");
        _mockRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Customer, bool>>>(), default))
                      .ReturnsAsync((Customer?)null);
        
        _mockRepository.Setup(r => r.SaveChangesAsync(default))
                      .ReturnsAsync(1);
        
        // Act
        var result = await _service.CreateCustomerAsync(request);
        
        // Assert
        Assert.True(result.IsSuccess);
        _mockRepository.Verify(r => r.Add(It.IsAny<Customer>()), Times.Once);
        _mockRepository.Verify(r => r.SaveChangesAsync(default), Times.Once);
    }
    
    [Fact]
    public async Task CreateCustomerAsync_WithExistingEmail_ShouldReturnFailure()
    {
        // Arrange
        var request = new CreateCustomerRequest("John", "Doe", "john@example.com");
        var existingCustomer = Customer.Create("Jane", "Doe", "john@example.com");
        
        _mockRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Customer, bool>>>(), default))
                      .ReturnsAsync(existingCustomer);
        
        // Act
        var result = await _service.CreateCustomerAsync(request);
        
        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Email already exists", result.Error);
        _mockRepository.Verify(r => r.Add(It.IsAny<Customer>()), Times.Never);
    }
}
```

#### 3. Dependency Injection Setup

```csharp
using Microsoft.Extensions.DependencyInjection;
using DKNet.EfCore.Repos.Abstractions;

public void ConfigureServices(IServiceCollection services)
{
    // Register DbContext
    services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString));
    
    // Register generic repository
    services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
    services.AddScoped(typeof(IReadRepository<>), typeof(ReadRepository<>));
    services.AddScoped(typeof(IWriteRepository<>), typeof(WriteRepository<>));
    
    // Register domain-specific repositories
    services.AddScoped<ICustomerRepository, CustomerRepository>();
    services.AddScoped<IOrderRepository, OrderRepository>();
    
    // Register application services
    services.AddScoped<CustomerService>();
    services.AddScoped<OrderService>();
}
```

## Best Practices

### 1. Repository Interface Design
- Define repository interfaces in the domain layer
- Keep interfaces focused on aggregate boundaries
- Use domain terminology in method names
- Include only operations that make sense for the domain

### 2. Projection Usage
- Use `GetProjection<T>()` for read-only scenarios
- Create specific DTOs for different use cases
- Avoid returning entities directly from API endpoints
- Use projections to optimize database queries

### 3. Transaction Management
- Use transactions for operations that span multiple aggregates
- Keep transactions as short as possible
- Handle transaction failures gracefully
- Consider using Unit of Work pattern for complex scenarios

### 4. Testing
- Mock repository interfaces for unit tests
- Use in-memory databases for integration tests
- Test repository implementations separately
- Verify both successful and failure scenarios

### 5. Performance Considerations
- Use `IQueryable` for complex queries
- Implement pagination for large result sets
- Consider bulk operations for high-volume scenarios
- Profile and optimize database queries

## Integration with Other DKNet Components

DKNet.EfCore.Repos.Abstractions integrates seamlessly with other DKNet components:

- **DKNet.EfCore.Abstractions**: Provides entity base classes and interfaces
- **DKNet.EfCore.Repos**: Implements the repository abstractions
- **DKNet.EfCore.Events**: Supports domain event publishing through repositories
- **DKNet.SlimBus.Extensions**: Integrates with CQRS patterns and message handling

---

> 💡 **Architecture Tip**: Use repository abstractions to define clear contracts in your domain layer. This enables dependency inversion and makes your domain logic completely independent of data access technology.