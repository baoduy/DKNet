# DKNet.EfCore.Specifications

**Specification Pattern implementation for Entity Framework Core that provides flexible, reusable, and composable query logic without the repository pattern overhead, enabling clean separation between domain logic and data access concerns in Domain-Driven Design applications.**

## What is this project?

DKNet.EfCore.Specifications implements the Specification Pattern for Entity Framework Core, allowing you to build flexible and reusable database queries by encapsulating query logic in specification classes. Instead of using large, hard-to-maintain repositories with numerous methods, you can create small, focused specification classes that can be combined using logical operators (AND/OR) to build complex queries.

### Key Features

- **Specification Pattern Implementation**: Clean, reusable query specifications
- **Composable Logic**: Combine specifications using AND/OR operators
- **EF Core Integration**: Seamless integration with Entity Framework Core and IQueryable
- **Repository Integration**: Works perfectly with DKNet.EfCore.Repos
- **Include Support**: Automatic handling of navigation property includes
- **Ordering Support**: Built-in support for sorting with multiple order criteria
- **Expression Visitor**: Advanced expression tree manipulation for complex combinations
- **Type Safety**: Generic constraints ensuring compile-time type safety
- **Performance Optimized**: IQueryable support for efficient database query execution

## How it contributes to DDD and Onion Architecture

### Domain Layer Integration

DKNet.EfCore.Specifications enables domain-driven query logic that belongs in the **Domain Layer** while providing infrastructure implementations for the **Infrastructure Layer**:

```
┌─────────────────────────────────────────────────────────────────┐
│                    🌐 Presentation Layer                        │
│                   (Controllers, API Endpoints)                  │
│                                                                 │
│  No direct knowledge of specifications                         │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                   🎯 Application Layer                          │
│              (Use Cases, Application Services)                  │
│                                                                 │
│  Uses: Combined specifications for complex queries             │
│  var specs = activeCustomers & inRegion & withOrders;          │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                    💼 Domain Layer                             │
│           (Entities, Aggregates, Domain Services)              │
│                                                                 │
│  📋 ActiveCustomersSpec - Business rule: active customers      │
│  📋 CustomersInRegionSpec - Domain logic: region filtering    │
│  📋 CustomersWithOrdersSpec - Relationship rules              │
│  🎯 Specification<Customer> - Base specification contracts     │
│  🔗 AND/OR operators for business logic combinations           │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                 🗄️ Infrastructure Layer                        │
│                  (Data Access, Persistence)                    │
│                                                                 │
│  🗃️ EfCoreSpecification<T> - EF Core integration              │
│  📖 SpecificationExtensions - Repository integration          │
│  ⚙️ ReplaceExpressionVisitor - Expression manipulation         │
│  📊 ApplySpecs() - Query application and execution             │
└─────────────────────────────────────────────────────────────────┘
```

### DDD Benefits

1. **Business Rules as Code**: Specifications encapsulate business rules as executable code
2. **Ubiquitous Language**: Specification names reflect domain terminology
3. **Testable Business Logic**: Query logic can be unit tested independently
4. **Aggregate Queries**: Support for complex queries that respect aggregate boundaries
5. **Domain Events Integration**: Specifications can trigger domain events during query execution

### Onion Architecture Benefits

1. **Dependency Inversion**: Domain defines specifications, infrastructure applies them
2. **Separation of Concerns**: Query logic separated from data access implementation
3. **Technology Independence**: Specifications work with any IQueryable provider
4. **Composability**: Business rules can be combined without changing existing code
5. **Testability**: Easy to mock and test specification logic

## How to use it

### Installation

```bash
dotnet add package DKNet.EfCore.Specifications
dotnet add package DKNet.EfCore.Repos  # For repository integration
```

### Basic Usage Examples

#### 1. Creating Simple Specifications

```csharp
using DKNet.EfCore.Specifications;

// Domain specification for active customers
public class ActiveCustomersSpec : Specification<Customer>
{
    public ActiveCustomersSpec()
    {
        WithFilter(customer => customer.IsActive);
        AddOrderBy(customer => customer.LastName);
    }
}

// Domain specification for customers in a specific region
public class CustomersInRegionSpec : Specification<Customer>
{
    public CustomersInRegionSpec(string region)
    {
        WithFilter(customer => customer.Region == region);
    }
}

// Specification with includes for related data
public class CustomersWithOrdersSpec : Specification<Customer>
{
    public CustomersWithOrdersSpec()
    {
        WithFilter(customer => customer.Orders.Any());
        AddInclude(customer => customer.Orders);
        AddInclude(customer => customer.Address);
        AddOrderByDescending(customer => customer.CreatedDate);
    }
}
```

#### 2. Combining Specifications with Logical Operators

```csharp
public class CustomerQueryService
{
    private readonly IReadRepository<Customer> _customerRepository;
    
    public CustomerQueryService(IReadRepository<Customer> customerRepository)
    {
        _customerRepository = customerRepository;
    }
    
    public async Task<List<Customer>> GetActiveCustomersInRegionAsync(string region)
    {
        // Combine specifications using AND operator
        var specification = new ActiveCustomersSpec() & new CustomersInRegionSpec(region);
        
        return await _customerRepository.SpecsListAsync(specification);
    }
    
    public async Task<List<Customer>> GetActiveOrVipCustomersAsync()
    {
        // Combine specifications using OR operator
        var activeSpec = new ActiveCustomersSpec();
        var vipSpec = new VipCustomersSpec();
        var combinedSpec = activeSpec | vipSpec;
        
        return await _customerRepository.SpecsListAsync(combinedSpec);
    }
    
    public async Task<List<Customer>> ComplexCustomerQueryAsync(string region, decimal minOrderValue)
    {
        // Complex specification combination
        var activeCustomers = new ActiveCustomersSpec();
        var inRegion = new CustomersInRegionSpec(region);
        var highValueOrders = new CustomersWithHighValueOrdersSpec(minOrderValue);
        
        var specification = activeCustomers & (inRegion | highValueOrders);
        
        return await _customerRepository.SpecsListAsync(specification);
    }
}
```

#### 3. Using Specifications with Repository Extensions

```csharp
public class OrderQueryService
{
    private readonly IReadRepository<Order> _orderRepository;
    
    public OrderQueryService(IReadRepository<Order> orderRepository)
    {
        _orderRepository = orderRepository;
    }
    
    // Get single result
    public async Task<Order?> GetLatestOrderForCustomerAsync(int customerId)
    {
        var spec = new OrdersForCustomerSpec(customerId);
        return await _orderRepository.SpecsFirstOrDefaultAsync(spec);
    }
    
    // Get paginated results
    public async Task<IPagedList<Order>> GetOrdersPagedAsync(
        ISpecification<Order> specification, 
        int pageNumber, 
        int pageSize)
    {
        return await _orderRepository.SpecsToPageListAsync(
            specification, 
            pageNumber, 
            pageSize);
    }
    
    // Stream large result sets
    public IAsyncEnumerable<Order> GetOrdersStreamAsync(ISpecification<Order> specification)
    {
        return _orderRepository.SpecsToPageEnumerable(specification);
    }
}
```

#### 4. Advanced Specification Patterns

```csharp
// Specification with complex filtering logic
public class CustomerSearchSpec : Specification<Customer>
{
    public CustomerSearchSpec(string searchTerm)
    {
        WithFilter(customer => 
            customer.FirstName.Contains(searchTerm) ||
            customer.LastName.Contains(searchTerm) ||
            customer.Email.Contains(searchTerm) ||
            customer.Company.Contains(searchTerm));
        
        AddInclude(customer => customer.Address);
        AddOrderBy(customer => customer.LastName);
        AddOrderBy(customer => customer.FirstName);
    }
}

// Specification with date range filtering
public class OrdersInDateRangeSpec : Specification<Order>
{
    public OrdersInDateRangeSpec(DateTime startDate, DateTime endDate)
    {
        WithFilter(order => order.OrderDate >= startDate && order.OrderDate <= endDate);
        AddInclude(order => order.Customer);
        AddInclude(order => order.OrderItems);
        AddOrderByDescending(order => order.OrderDate);
    }
}

// Dynamic specification building
public class DynamicCustomerSpec : Specification<Customer>
{
    public DynamicCustomerSpec(CustomerFilter filter)
    {
        if (!string.IsNullOrEmpty(filter.Region))
        {
            WithFilter(c => c.Region == filter.Region);
        }
        
        if (filter.IsActive.HasValue)
        {
            var activeFilter = filter.IsActive.Value 
                ? (Expression<Func<Customer, bool>>)(c => c.IsActive)
                : (Expression<Func<Customer, bool>>)(c => !c.IsActive);
            
            if (FilterQuery == null)
                WithFilter(activeFilter);
            else
                WithFilter(CombineWithAnd(FilterQuery, activeFilter));
        }
        
        if (filter.MinOrderCount > 0)
        {
            WithFilter(c => c.Orders.Count >= filter.MinOrderCount);
            AddInclude(c => c.Orders);
        }
        
        // Apply sorting
        switch (filter.SortBy)
        {
            case "Name":
                AddOrderBy(c => c.LastName);
                AddOrderBy(c => c.FirstName);
                break;
            case "Date":
                AddOrderByDescending(c => c.CreatedDate);
                break;
            default:
                AddOrderBy(c => c.Id);
                break;
        }
    }
    
    private Expression<Func<Customer, bool>> CombineWithAnd(
        Expression<Func<Customer, bool>> left, 
        Expression<Func<Customer, bool>> right)
    {
        var visitor = new ReplaceExpressionVisitor(
            right.Parameters.Single(), 
            left.Parameters.Single());
        var replacedBody = visitor.Visit(right.Body);
        var andExpression = Expression.AndAlso(left.Body, replacedBody);
        return Expression.Lambda<Func<Customer, bool>>(andExpression, left.Parameters.Single());
    }
}
```

### Advanced Usage Examples

#### 1. Specification Factory Pattern

```csharp
public static class CustomerSpecifications
{
    public static Specification<Customer> Active() 
        => new ActiveCustomersSpec();
    
    public static Specification<Customer> InRegion(string region) 
        => new CustomersInRegionSpec(region);
    
    public static Specification<Customer> WithMinOrderValue(decimal minValue) 
        => new CustomersWithMinOrderValueSpec(minValue);
    
    public static Specification<Customer> CreatedAfter(DateTime date) 
        => new CustomersCreatedAfterSpec(date);
    
    // Convenience methods for common combinations
    public static Specification<Customer> ActiveInRegion(string region)
        => Active() & InRegion(region);
    
    public static Specification<Customer> HighValueCustomers(string region, decimal minOrderValue)
        => Active() & InRegion(region) & WithMinOrderValue(minOrderValue);
}

// Usage
public async Task<List<Customer>> GetHighValueCustomersAsync(string region)
{
    var specification = CustomerSpecifications.HighValueCustomers(region, 1000m);
    return await _customerRepository.SpecsListAsync(specification);
}
```

#### 2. Specification with Custom Expression Visitor

```csharp
public class SecurityFilterSpec<TEntity> : Specification<TEntity>
    where TEntity : class, IOwnedEntity
{
    public SecurityFilterSpec(string userId, IEnumerable<string> roles)
    {
        if (roles.Contains("Admin"))
        {
            // Admins can see everything - no filter needed
            return;
        }
        
        if (roles.Contains("Manager"))
        {
            // Managers can see their department's data
            WithFilter(entity => entity.Department == GetUserDepartment(userId));
        }
        else
        {
            // Regular users can only see their own data
            WithFilter(entity => entity.OwnerId == userId);
        }
    }
    
    private string GetUserDepartment(string userId)
    {
        // Implementation to get user's department
        return "Sales"; // Simplified for example
    }
}
```

#### 3. Specification Testing

```csharp
[Test]
public void ActiveCustomersSpec_FilterExpression_ShouldOnlyIncludeActiveCustomers()
{
    // Arrange
    var specification = new ActiveCustomersSpec();
    var activeCustomer = new Customer { IsActive = true };
    var inactiveCustomer = new Customer { IsActive = false };
    
    // Act & Assert
    Assert.IsTrue(specification.Match(activeCustomer));
    Assert.IsFalse(specification.Match(inactiveCustomer));
}

[Test]
public void CombinedSpecification_AndOperator_ShouldCombineFilters()
{
    // Arrange
    var activeSpec = new ActiveCustomersSpec();
    var regionSpec = new CustomersInRegionSpec("North");
    var combinedSpec = activeSpec & regionSpec;
    
    var customer = new Customer { IsActive = true, Region = "North" };
    var inactiveCustomer = new Customer { IsActive = false, Region = "North" };
    var wrongRegionCustomer = new Customer { IsActive = true, Region = "South" };
    
    // Act & Assert
    Assert.IsTrue(combinedSpec.Match(customer));
    Assert.IsFalse(combinedSpec.Match(inactiveCustomer));
    Assert.IsFalse(combinedSpec.Match(wrongRegionCustomer));
}
```

## Best Practices

### 1. Specification Design Principles

```csharp
// Good: Single responsibility - one business rule per specification
public class ActiveCustomersSpec : Specification<Customer>
{
    public ActiveCustomersSpec()
    {
        WithFilter(customer => customer.IsActive);
    }
}

// Good: Parameterized specifications for flexibility
public class CustomersInRegionSpec : Specification<Customer>
{
    public CustomersInRegionSpec(string region)
    {
        if (string.IsNullOrEmpty(region))
            throw new ArgumentException("Region cannot be null or empty", nameof(region));
            
        WithFilter(customer => customer.Region == region);
    }
}

// Avoid: Multiple unrelated business rules in one specification
public class CustomerSpec : Specification<Customer> // ❌ Too generic
{
    public CustomerSpec(bool isActive, string region, DateTime createdAfter)
    {
        // Multiple responsibilities combined
    }
}
```

### 2. Naming Conventions

```csharp
// Good: Clear, business-focused naming
public class ActiveCustomersSpec : Specification<Customer> { }
public class CustomersInRegionSpec : Specification<Customer> { }
public class HighValueOrdersSpec : Specification<Order> { }
public class ExpiredSubscriptionsSpec : Specification<Subscription> { }

// Good: Factory methods with intention-revealing names
public static class CustomerSpecs
{
    public static Specification<Customer> Active() => new ActiveCustomersSpec();
    public static Specification<Customer> InRegion(string region) => new CustomersInRegionSpec(region);
    public static Specification<Customer> WithHighValueOrders() => new HighValueCustomersSpec();
}
```

### 3. Performance Considerations

```csharp
// Good: Use AddInclude for necessary navigation properties
public class OrdersWithDetailsSpec : Specification<Order>
{
    public OrdersWithDetailsSpec()
    {
        AddInclude(order => order.Customer);
        AddInclude(order => order.OrderItems);
        AddOrderByDescending(order => order.OrderDate);
    }
}

// Good: Optimize ordering for database indexes
public class CustomersByNameSpec : Specification<Customer>
{
    public CustomersByNameSpec()
    {
        AddOrderBy(customer => customer.LastName);  // Primary sort
        AddOrderBy(customer => customer.FirstName); // Secondary sort
        AddOrderBy(customer => customer.Id);        // Tie breaker for pagination
    }
}

// Avoid: Unnecessary includes that impact performance
public class SimpleCustomerListSpec : Specification<Customer>
{
    public SimpleCustomerListSpec()
    {
        AddInclude(customer => customer.Orders);           // ❌ Not needed for list view
        AddInclude(customer => customer.OrderHistory);     // ❌ Heavy navigation property
        AddInclude(customer => customer.PaymentMethods);   // ❌ Unrelated data
    }
}
```

### 4. Specification Composition

```csharp
// Good: Use clear logical operators
public async Task<List<Customer>> GetTargetCustomersAsync(string region, decimal minOrderValue)
{
    var activeCustomers = new ActiveCustomersSpec();
    var inRegion = new CustomersInRegionSpec(region);
    var highValue = new HighValueCustomersSpec(minOrderValue);
    
    // Clear business logic: Active customers in region with high-value orders
    var specification = activeCustomers & inRegion & highValue;
    
    return await _customerRepository.SpecsListAsync(specification);
}

// Good: Complex business rules using parentheses for clarity
public async Task<List<Customer>> GetMarketingTargetsAsync(string region)
{
    var active = new ActiveCustomersSpec();
    var inRegion = new CustomersInRegionSpec(region);
    var highValue = new HighValueCustomersSpec(1000m);
    var recent = new RecentCustomersSpec(TimeSpan.FromDays(30));
    
    // Active customers in region who are either high-value OR recent
    var specification = active & inRegion & (highValue | recent);
    
    return await _customerRepository.SpecsListAsync(specification);
}
```

### 5. Error Handling and Validation

```csharp
public class CustomersInRegionSpec : Specification<Customer>
{
    public CustomersInRegionSpec(string region)
    {
        if (string.IsNullOrWhiteSpace(region))
            throw new ArgumentException("Region cannot be null or empty", nameof(region));
        
        WithFilter(customer => customer.Region.ToLower() == region.ToLower());
    }
}

public class OrdersInDateRangeSpec : Specification<Order>
{
    public OrdersInDateRangeSpec(DateTime startDate, DateTime endDate)
    {
        if (startDate >= endDate)
            throw new ArgumentException("Start date must be before end date");
        
        if (endDate > DateTime.Now)
            throw new ArgumentException("End date cannot be in the future");
        
        WithFilter(order => order.OrderDate >= startDate && order.OrderDate <= endDate);
    }
}
```

## Integration with Other DKNet Components

DKNet.EfCore.Specifications integrates seamlessly with other DKNet components:

### With DKNet.EfCore.Repos
```csharp
// Repository extensions provide specification-aware methods
var activeCustomers = new ActiveCustomersSpec();
var customers = await _customerRepository.SpecsListAsync(activeCustomers);
var firstCustomer = await _customerRepository.SpecsFirstOrDefaultAsync(activeCustomers);
var pagedCustomers = await _customerRepository.SpecsToPageListAsync(activeCustomers, 1, 20);
```

### With DKNet.EfCore.Events
```csharp
public class CustomerQueryHandler : IEventHandler<CustomerUpdatedEvent>
{
    public async Task Handle(CustomerUpdatedEvent eventData)
    {
        // Use specifications to find related customers for cache invalidation
        var sameRegionSpec = new CustomersInRegionSpec(eventData.Region);
        var relatedCustomers = await _repository.SpecsListAsync(sameRegionSpec);
        
        // Invalidate cache for related customers
        await _cache.InvalidateAsync(relatedCustomers.Select(c => c.Id));
    }
}
```

### With DKNet.EfCore.DataAuthorization
```csharp
public class SecureCustomerSpec : Specification<Customer>
{
    public SecureCustomerSpec(ClaimsPrincipal user)
    {
        var userId = user.GetUserId();
        var roles = user.GetRoles();
        
        if (!roles.Contains("Admin"))
        {
            // Non-admin users can only see customers in their territory
            var territory = user.GetTerritory();
            WithFilter(customer => customer.Territory == territory);
        }
        
        // Apply additional security filters based on user context
        if (user.HasClaim("ViewInactiveCustomers", "false"))
        {
            WithFilter(customer => customer.IsActive);
        }
    }
}
```

---

> 💡 **Architecture Tip**: Use DKNet.EfCore.Specifications to implement query logic that belongs in your domain layer. Create small, focused specification classes that encapsulate business rules and can be easily combined. This approach eliminates the need for large repository classes with numerous query methods while maintaining clean separation between domain logic and data access concerns.