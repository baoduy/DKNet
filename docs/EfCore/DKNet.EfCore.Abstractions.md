# DKNet.EfCore.Abstractions

**Core abstractions and interfaces for Entity Framework Core integration that establish the foundation for Domain-Driven Design (DDD) patterns and provide essential contracts for entity management, auditing, and domain events.**

## What is this project?

DKNet.EfCore.Abstractions defines the fundamental contracts and base classes for Entity Framework Core integration within the DKNet framework. It provides essential abstractions for:

- **Entity Identity Management**: Generic entity interfaces with flexible key types
- **Audit Tracking**: Comprehensive auditing capabilities for entity changes
- **Concurrency Control**: Optimistic concurrency support through row versioning
- **Domain Events**: Integration points for domain event handling
- **Static Data Management**: Attributes for managing reference data
- **Entity Configuration**: Attributes for customizing EF Core mapping behavior

This project serves as the cornerstone of the data access layer, ensuring consistent patterns across all EF Core implementations in the DKNet ecosystem.

### Key Features

- **IEntity<TKey>**: Generic entity interface supporting various primary key types
- **Entity<TKey>**: Base entity class with identity and concurrency management
- **AuditedEntity**: Full audit tracking with creation/modification timestamps
- **IEventEntity**: Domain event capabilities for entities
- **IConcurrencyEntity**: Optimistic concurrency control interface
- **StaticDataAttribute**: Enum-to-database table mapping for reference data
- **Configuration Attributes**: Fine-grained control over EF Core mapping

## How it contributes to DDD and Onion Architecture

### Domain Layer Foundation

DKNet.EfCore.Abstractions provides the **Domain Layer** with essential building blocks while maintaining technology independence:

```
┌─────────────────────────────────────────────────────────────────┐
│                    🌐 Presentation Layer                        │
│                                                                 │
│  No direct dependencies on EfCore.Abstractions                 │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                   🎯 Application Layer                          │
│                                                                 │
│  May use: Repository interfaces, Domain event contracts        │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                    💼 Domain Layer                             │
│                                                                 │
│  📋 Entity<TKey> - Domain entity base classes                  │
│  🎭 IEventEntity - Domain event capabilities                   │
│  📝 AuditedEntity - Audit trail for business entities          │
│  🏷️ StaticDataAttribute - Reference data management            │
│  🔒 IConcurrencyEntity - Business rule consistency             │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                 🗄️ Infrastructure Layer                        │
│                                                                 │
│  Implements: Repository patterns, EF Core configurations       │
│  Uses: All abstractions for concrete implementations           │
└─────────────────────────────────────────────────────────────────┘
```

### DDD Benefits

1. **Entity Identity**: `IEntity<TKey>` ensures every domain entity has a well-defined identity
2. **Aggregate Consistency**: `IConcurrencyEntity` supports optimistic concurrency for aggregate boundaries
3. **Domain Events**: `IEventEntity` enables domain entities to publish events for cross-aggregate communication
4. **Audit Trail**: `AuditedEntity` maintains comprehensive audit logs for compliance and business requirements
5. **Ubiquitous Language**: `StaticDataAttribute` helps maintain reference data consistency across the domain

### Onion Architecture Benefits

1. **Dependency Inversion**: Domain layer defines contracts, infrastructure implements them
2. **Technology Independence**: Abstractions don't couple domain logic to EF Core specifics
3. **Testability**: Interfaces enable easy mocking and unit testing
4. **Separation of Concerns**: Clear boundaries between domain concepts and infrastructure concerns
5. **Pluggability**: Different persistence technologies can implement the same contracts

## How to use it

### Installation

```bash
dotnet add package DKNet.EfCore.Abstractions
```

### Basic Usage Examples

#### 1. Simple Entity with GUID Identity

```csharp
using DKNet.EfCore.Abstractions.Entities;

// Domain entity with GUID primary key
public class Product : Entity
{
    public string Name { get; private set; } = null!;
    public decimal Price { get; private set; }
    public string Description { get; private set; } = string.Empty;
    
    // Private constructor for EF Core
    private Product() { }
    
    // Factory method for creating new products
    public static Product Create(string name, decimal price, string description = "")
    {
        var product = new Product
        {
            Name = name,
            Price = price,
            Description = description
        };
        
        return product;
    }
    
    // Business logic methods
    public void UpdatePrice(decimal newPrice)
    {
        if (newPrice <= 0)
            throw new ArgumentException("Price must be positive", nameof(newPrice));
            
        Price = newPrice;
    }
}
```

#### 2. Entity with Custom Key Type

```csharp
using DKNet.EfCore.Abstractions.Entities;

// Entity with integer primary key
public class Category : Entity<int>
{
    public string Name { get; private set; } = null!;
    public string Code { get; private set; } = null!;
    
    private Category() { }
    
    public Category(int id, string name, string code) : base(id)
    {
        Name = name;
        Code = code;
    }
}
```

#### 3. Audited Entity for Compliance

```csharp
using DKNet.EfCore.Abstractions.Entities;

// Entity with full audit tracking
public class Order : AuditedEntity, IEventEntity
{
    private readonly List<object> _domainEvents = new();
    private readonly List<Type> _domainEventTypes = new();
    
    public string OrderNumber { get; private set; } = null!;
    public Guid CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public decimal TotalAmount { get; private set; }
    
    private Order() { }
    
    public static Order Create(string orderNumber, Guid customerId, string createdBy)
    {
        var order = new Order
        {
            OrderNumber = orderNumber,
            CustomerId = customerId,
            Status = OrderStatus.Pending,
            TotalAmount = 0
        };
        
        // Set audit information
        order.SetCreatedBy(createdBy);
        
        // Raise domain event
        order.AddEvent(new OrderCreatedEvent(order.Id, customerId, orderNumber));
        
        return order;
    }
    
    public void CompleteOrder(string modifiedBy)
    {
        if (Status != OrderStatus.Processing)
            throw new InvalidOperationException("Only processing orders can be completed");
            
        Status = OrderStatus.Completed;
        SetUpdatedBy(modifiedBy);
        
        // Raise domain event
        AddEvent<OrderCompletedEvent>();
    }
    
    // IEventEntity implementation
    public void AddEvent(object eventObj)
    {
        _domainEvents.Add(eventObj);
    }
    
    public void AddEvent<TEvent>() where TEvent : class
    {
        _domainEventTypes.Add(typeof(TEvent));
    }
    
    public (object[]? events, Type[]? eventTypes) GetEventsAndClear()
    {
        var events = _domainEvents.ToArray();
        var eventTypes = _domainEventTypes.ToArray();
        
        _domainEvents.Clear();
        _domainEventTypes.Clear();
        
        return (events.Length > 0 ? events : null, 
                eventTypes.Length > 0 ? eventTypes : null);
    }
}

public enum OrderStatus
{
    Pending = 1,
    Processing = 2,
    Completed = 3,
    Cancelled = 4
}
```

#### 4. Static Data Management with Enums

```csharp
using DKNet.EfCore.Abstractions.Attributes;
using System.ComponentModel.DataAnnotations;

// Enum that will be stored as a reference table
[StaticData(nameof(OrderStatus))]
public enum OrderStatus
{
    [Display(Name = "Pending", Description = "Order is waiting for processing")]
    Pending = 1,
    
    [Display(Name = "Processing", Description = "Order is being processed")]
    Processing = 2,
    
    [Display(Name = "Completed", Description = "Order has been completed")]
    Completed = 3,
    
    [Display(Name = "Cancelled", Description = "Order has been cancelled")]
    Cancelled = 4
}
```

#### 5. Repository Interface Definition

```csharp
using DKNet.EfCore.Abstractions.Entities;

// Repository interface in the domain layer
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id);
    Task<Order?> GetByOrderNumberAsync(string orderNumber);
    Task<IEnumerable<Order>> GetOrdersByCustomerAsync(Guid customerId);
    Task<Order> AddAsync(Order order);
    Task UpdateAsync(Order order);
    Task DeleteAsync(Order order);
}
```

### Advanced Usage Patterns

#### 1. Aggregate Root with Child Entities

```csharp
using DKNet.EfCore.Abstractions.Entities;

// Aggregate root
public class Customer : AuditedEntity, IEventEntity
{
    private readonly List<Address> _addresses = new();
    private readonly List<object> _domainEvents = new();
    private readonly List<Type> _domainEventTypes = new();
    
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    
    // Read-only collection of child entities
    public IReadOnlyCollection<Address> Addresses => _addresses.AsReadOnly();
    
    private Customer() { }
    
    public static Customer Create(string firstName, string lastName, string email, string createdBy)
    {
        var customer = new Customer
        {
            FirstName = firstName,
            LastName = lastName,
            Email = email
        };
        
        customer.SetCreatedBy(createdBy);
        customer.AddEvent(new CustomerCreatedEvent(customer.Id, email));
        return customer;
    }
    
    // Business method that maintains aggregate consistency
    public void AddAddress(string street, string city, string postalCode, string modifiedBy)
    {
        var address = new Address(Id, street, city, postalCode);
        _addresses.Add(address);
        
        SetUpdatedBy(modifiedBy);
        AddEvent(new CustomerAddressAddedEvent(Id, address.Id));
    }
    
    // IEventEntity implementation
    public void AddEvent(object eventObj) => _domainEvents.Add(eventObj);
    public void AddEvent<TEvent>() where TEvent : class => _domainEventTypes.Add(typeof(TEvent));
    
    public (object[]? events, Type[]? eventTypes) GetEventsAndClear()
    {
        var events = _domainEvents.ToArray();
        var eventTypes = _domainEventTypes.ToArray();
        _domainEvents.Clear();
        _domainEventTypes.Clear();
        return (events.Length > 0 ? events : null, eventTypes.Length > 0 ? eventTypes : null);
    }
}

// Child entity (part of Customer aggregate)
public class Address : Entity
{
    public Guid CustomerId { get; private set; }
    public string Street { get; private set; } = null!;
    public string City { get; private set; } = null!;
    public string PostalCode { get; private set; } = null!;
    
    private Address() { }
    
    internal Address(Guid customerId, string street, string city, string postalCode)
    {
        CustomerId = customerId;
        Street = street;
        City = city;
        PostalCode = postalCode;
    }
}
```

#### 2. Custom Attribute Usage

```csharp
using DKNet.EfCore.Abstractions.Attributes;

// Entity that should be ignored by automatic mapping
[IgnoreEntityMapper]
public class TemporaryData : Entity
{
    public string Data { get; set; } = null!;
}

// Entity with custom sequence configuration
public class Invoice : Entity<long>
{
    [SqlSequence("InvoiceNumberSequence")]
    public long InvoiceNumber { get; private set; }
    
    public string CustomerName { get; private set; } = null!;
    public decimal Amount { get; private set; }
}
```

#### 3. Domain Event Patterns

```csharp
// Domain event classes
public record CustomerCreatedEvent(Guid CustomerId, string Email);
public record CustomerAddressAddedEvent(Guid CustomerId, Guid AddressId);
public record OrderCreatedEvent(Guid OrderId, Guid CustomerId, string OrderNumber);
public record OrderCompletedEvent(Guid OrderId, Guid CustomerId, DateTime CompletedAt);

// Event handler interface (would be implemented in application layer)
public interface IEventHandler<in TEvent>
{
    Task Handle(TEvent domainEvent);
}
```

### Integration with EF Core

```csharp
using Microsoft.EntityFrameworkCore;
using DKNet.EfCore.Abstractions.Entities;

public class ApplicationDbContext : DbContext
{
    public DbSet<Customer> Customers { get; set; } = null!;
    public DbSet<Order> Orders { get; set; } = null!;
    public DbSet<Product> Products { get; set; } = null!;
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure entities using the abstractions
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RowVersion).IsRowVersion();
            
            // Configure audit properties
            entity.Property(e => e.CreatedBy).IsRequired();
            entity.Property(e => e.CreatedOn).IsRequired();
            
            // Configure child entities
            entity.OwnsMany(e => e.Addresses, address =>
            {
                address.WithOwner().HasForeignKey(a => a.CustomerId);
                address.HasKey(a => a.Id);
            });
        });
        
        // Configure static data enums
        modelBuilder.Entity<OrderStatusEntity>().HasData(
            GetStaticDataFromEnum<OrderStatus>()
        );
        
        base.OnModelCreating(modelBuilder);
    }
    
    private static IEnumerable<object> GetStaticDataFromEnum<TEnum>() where TEnum : struct, Enum
    {
        return Enum.GetValues<TEnum>()
            .Select(e => new { Id = (int)(object)e, Name = e.ToString() });
    }
}
```

## Best Practices

### 1. Entity Design
- Always use factory methods for entity creation to ensure business rules are applied
- Keep entities focused on business logic, not data access concerns
- Use private setters to maintain encapsulation
- Implement domain events for cross-aggregate communication

### 2. Audit Trail
- Use `AuditedEntity` for entities that require compliance tracking
- Always pass the current user context when creating or modifying entities
- Consider the performance impact of audit logging in high-volume scenarios

### 3. Concurrency Control
- Implement `IConcurrencyEntity` for entities that may be modified concurrently
- Handle concurrency exceptions appropriately in your application layer
- Consider using optimistic concurrency for better performance

### 4. Static Data Management
- Use `StaticDataAttribute` for enums that represent reference data
- Keep static data enums stable and avoid frequent changes
- Consider the impact on database migrations when modifying enum values

### 5. Domain Events
- Use domain events to maintain loose coupling between aggregates
- Keep event payloads minimal and focused
- Handle events asynchronously when possible to avoid blocking business operations

## Integration with Other DKNet Components

DKNet.EfCore.Abstractions integrates seamlessly with other DKNet components:

- **DKNet.EfCore.Events**: Provides automatic domain event dispatching
- **DKNet.EfCore.Repos**: Implements repository patterns using these abstractions
- **DKNet.EfCore.Extensions**: Provides automatic entity configuration
- **DKNet.EfCore.Hooks**: Enables lifecycle event handling
- **DKNet.SlimBus.Extensions**: Integrates domain events with message bus

---

> 💡 **Architecture Tip**: Use DKNet.EfCore.Abstractions to define your domain entities and let the infrastructure layer handle the EF Core specifics. This maintains clean separation between business logic and data access technology.