---
name: efcore-abstractions
description: DKNet EF Core Abstractions - Base entities, interfaces, and attributes for Entity Framework Core with DDD patterns
license: MIT
---

# EF Core Abstractions Skill

This skill helps GitHub Copilot generate code using DKNet's EF Core Abstractions package (`DKNet.EfCore.Abstractions`) for creating domain entities with audit trails, events, and soft delete support.

## 🎯 Package Purpose

`DKNet.EfCore.Abstractions` provides:
- **Base Entity Classes** - Generic entity foundations with various key types
- **Audit Interfaces** - Automatic tracking of who/when records were created/modified
- **Event Management** - Domain event support for entities
- **Soft Delete** - Logical deletion without physical removal
- **Concurrency Control** - Optimistic concurrency with row versions

**NuGet Package**: `DKNet.EfCore.Abstractions`

## 📦 Installation

```bash
dotnet add package DKNet.EfCore.Abstractions
```

## 🏗️ Base Entity Types

### EntityBase (Simple Guid ID Entity)
```csharp
using DKNet.EfCore.Abstractions;

/// <summary>
///     Product entity.
/// </summary>
public class Product : EntityBase
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="Product"/> class.
    /// </summary>
    public Product()
    {
        Id = Guid.NewGuid();
    }
    
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public bool IsActive { get; set; } = true;
}

// Inherits: Guid Id, DateTime CreatedAt, DateTime? UpdatedAt
```

### Entity<TKey> (Custom Key Type)
```csharp
using DKNet.EfCore.Abstractions;

/// <summary>
///     Order entity with integer key.
/// </summary>
public class Order : Entity<int>
{
    public Guid CustomerId { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; }
}

// Inherits: int Id, DateTime CreatedAt, DateTime? UpdatedAt
```

### AuditEntity<TKey> (With Audit Trail)
```csharp
using DKNet.EfCore.Abstractions;

/// <summary>
///     Customer entity with full audit trail.
/// </summary>
public class Customer : AuditEntity<Guid>
{
    public Customer()
    {
        Id = Guid.NewGuid();
    }
    
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
}

// Inherits: Guid Id, DateTime CreatedAt, DateTime? UpdatedAt
//           string CreatedBy, string? UpdatedBy
```

## 🎯 Common Patterns

### Pattern 1: Simple Entity
For basic entities without complex requirements:

```csharp
/// <summary>
///     Category entity.
/// </summary>
public class Category : EntityBase
{
    public Category()
    {
        Id = Guid.NewGuid();
    }
    
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    
    // Navigation properties
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
```

### Pattern 2: Auditable Entity
When you need to track who created/modified records:

```csharp
/// <summary>
///     Document entity with audit trail.
/// </summary>
public class Document : AuditEntity<Guid>
{
    public Document()
    {
        Id = Guid.NewGuid();
    }
    
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DocumentStatus Status { get; set; }
    
    /// <summary>
    ///     Approves the document.
    /// </summary>
    public void Approve(string approvedBy)
    {
        Status = DocumentStatus.Approved;
        SetUpdatedBy(approvedBy); // Updates UpdatedBy and UpdatedAt
    }
}
```

### Pattern 3: Soft Deletable Entity
For entities that shouldn't be physically deleted:

```csharp
using DKNet.EfCore.Abstractions;

/// <summary>
///     User entity with soft delete support.
/// </summary>
public class User : AuditEntity<Guid>, ISoftDelete
{
    public User()
    {
        Id = Guid.NewGuid();
    }
    
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    ///     Indicates whether this user is soft deleted.
    /// </summary>
    public bool IsDeleted { get; set; }
    
    /// <summary>
    ///     Soft deletes the user.
    /// </summary>
    public void SoftDelete(string deletedBy)
    {
        IsDeleted = true;
        IsActive = false;
        SetUpdatedBy(deletedBy);
    }
}

// Configure global query filter in DbContext
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Automatically filter out soft-deleted records
    modelBuilder.Entity<User>().HasQueryFilter(u => !u.IsDeleted);
}
```

### Pattern 4: Event-Driven Entity
For entities that publish domain events:

```csharp
using DKNet.EfCore.Abstractions;

/// <summary>
///     Order entity that publishes domain events.
/// </summary>
public class Order : AuditEntity<Guid>, IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = new();
    
    public Order()
    {
        Id = Guid.NewGuid();
    }
    
    public Guid CustomerId { get; set; }
    public OrderStatus Status { get; private set; }
    public decimal TotalAmount { get; set; }
    
    /// <summary>
    ///     Gets the domain events collection.
    /// </summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    
    /// <summary>
    ///     Places the order.
    /// </summary>
    public void PlaceOrder(string placedBy)
    {
        if (Status != OrderStatus.Draft)
            throw new InvalidOperationException("Only draft orders can be placed");
        
        Status = OrderStatus.Placed;
        SetUpdatedBy(placedBy);
        
        // Publish domain event
        _domainEvents.Add(new OrderPlacedEvent(Id, CustomerId, TotalAmount));
    }
    
    /// <summary>
    ///     Clears all domain events.
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}

/// <summary>
///     Domain event for when an order is placed.
/// </summary>
public record OrderPlacedEvent(
    Guid OrderId,
    Guid CustomerId,
    decimal TotalAmount) : IDomainEvent;
```

### Pattern 5: Aggregate Root
For complex aggregates with value objects:

```csharp
/// <summary>
///     Order aggregate root.
/// </summary>
public class Order : AuditEntity<Guid>
{
    private readonly List<OrderItem> _items = new();
    
    public Order(Guid customerId, Address shippingAddress, string createdBy)
    {
        Id = Guid.NewGuid();
        CustomerId = customerId;
        ShippingAddress = shippingAddress;
        Status = OrderStatus.Draft;
        SetCreatedBy(createdBy);
    }
    
    // Private constructor for EF Core
    private Order() { }
    
    public Guid CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public Address ShippingAddress { get; private set; } = null!;
    
    // Expose as read-only
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();
    
    /// <summary>
    ///     Adds an item to the order.
    /// </summary>
    public void AddItem(Guid productId, int quantity, decimal unitPrice)
    {
        if (Status != OrderStatus.Draft)
            throw new InvalidOperationException("Cannot add items to non-draft orders");
        
        var item = new OrderItem(Id, productId, quantity, unitPrice);
        _items.Add(item);
    }
    
    /// <summary>
    ///     Calculates the total amount.
    /// </summary>
    public decimal GetTotalAmount() => _items.Sum(i => i.GetSubtotal());
}

/// <summary>
///     Order item entity (part of Order aggregate).
/// </summary>
public class OrderItem : EntityBase
{
    public OrderItem(Guid orderId, Guid productId, int quantity, decimal unitPrice)
    {
        Id = Guid.NewGuid();
        OrderId = orderId;
        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }
    
    private OrderItem() { } // For EF Core
    
    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    
    public decimal GetSubtotal() => Quantity * UnitPrice;
}

/// <summary>
///     Address value object.
/// </summary>
public record Address(
    string Street,
    string City,
    string State,
    string ZipCode,
    string Country);
```

## 🎯 Interface Implementation

### IAuditable
```csharp
public interface IAuditable
{
    string CreatedBy { get; set; }
    DateTime CreatedAt { get; set; }
    string? UpdatedBy { get; set; }
    DateTime? UpdatedAt { get; set; }
}

// Usage
public class MyEntity : EntityBase, IAuditable
{
    public string CreatedBy { get; set; } = string.Empty;
    public string? UpdatedBy { get; set; }
    
    // CreatedAt and UpdatedAt inherited from EntityBase
}
```

### ISoftDelete
```csharp
public interface ISoftDelete
{
    bool IsDeleted { get; set; }
}

// Usage
public class MyEntity : EntityBase, ISoftDelete
{
    public bool IsDeleted { get; set; }
}

// Global query filter in DbContext
modelBuilder.Entity<MyEntity>().HasQueryFilter(e => !e.IsDeleted);
```

### IHasConcurrencyToken
```csharp
public interface IHasConcurrencyToken
{
    byte[] RowVersion { get; set; }
}

// Usage
public class MyEntity : EntityBase, IHasConcurrencyToken
{
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

// Configure in DbContext
modelBuilder.Entity<MyEntity>()
    .Property(e => e.RowVersion)
    .IsRowVersion();
```

## 🚨 Critical Rules

### 1. Initialize Guid IDs in Constructor
```csharp
// ✅ Good - ID set immediately
public class Product : EntityBase
{
    public Product()
    {
        Id = Guid.NewGuid();
    }
}

// ❌ Bad - ID might be default (all zeros)
public class Product : EntityBase
{
    // Missing initialization
}
```

### 2. Use Private Setters for Business Logic
```csharp
// ✅ Good - Encapsulation
public class Order : EntityBase
{
    public OrderStatus Status { get; private set; }
    
    public void PlaceOrder()
    {
        if (Status != OrderStatus.Draft)
            throw new InvalidOperationException();
        Status = OrderStatus.Placed;
    }
}

// ❌ Bad - No encapsulation
public class Order : EntityBase
{
    public OrderStatus Status { get; set; } // Can be set from anywhere
}
```

### 3. Always Add XML Documentation
```csharp
// ✅ Good
/// <summary>
///     Represents a product in the catalog.
/// </summary>
public class Product : EntityBase
{
    /// <summary>
    ///     Gets or sets the product name.
    /// </summary>
    public string Name { get; set; } = string.Empty;
}

// ❌ Bad - No documentation
public class Product : EntityBase
{
    public string Name { get; set; } = string.Empty;
}
```

### 4. Use SetUpdatedBy() for Audit Entities
```csharp
// ✅ Good - Use helper method
public class Customer : AuditEntity<Guid>
{
    public void UpdateEmail(string email, string updatedBy)
    {
        Email = email;
        SetUpdatedBy(updatedBy); // Updates UpdatedBy and UpdatedAt
    }
}

// ❌ Bad - Manual update (error-prone)
public class Customer : AuditEntity<Guid>
{
    public void UpdateEmail(string email, string updatedBy)
    {
        Email = email;
        UpdatedBy = updatedBy;
        UpdatedAt = DateTime.UtcNow; // Might forget this
    }
}
```

### 5. Initialize Collections
```csharp
// ✅ Good - Initialized
public class Category : EntityBase
{
    public ICollection<Product> Products { get; set; } = new List<Product>();
}

// ❌ Bad - Null reference exception risk
public class Category : EntityBase
{
    public ICollection<Product> Products { get; set; } = null!;
}
```

## 🚫 Common Mistakes

### 1. Not Setting CreatedBy in Constructor
```csharp
// ❌ Bad - CreatedBy not set
public class Customer : AuditEntity<Guid>
{
    public Customer(string name, string email)
    {
        Name = name;
        Email = email;
        // Missing: SetCreatedBy(createdBy);
    }
}

// ✅ Good - Always set CreatedBy
public class Customer : AuditEntity<Guid>
{
    public Customer(string name, string email, string createdBy)
    {
        Name = name;
        Email = email;
        SetCreatedBy(createdBy);
    }
}
```

### 2. Exposing Collections as Mutable
```csharp
// ❌ Bad - External code can modify list
public class Order : EntityBase
{
    public List<OrderItem> Items { get; set; } = new();
}

// ✅ Good - Controlled access
public class Order : EntityBase
{
    private readonly List<OrderItem> _items = new();
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();
    
    public void AddItem(OrderItem item) => _items.Add(item);
}
```

### 3. Forgetting Private Constructor for EF Core
```csharp
// ❌ Bad - EF Core can't instantiate
public class Order : EntityBase
{
    public Order(Guid customerId, string createdBy)
    {
        CustomerId = customerId;
        SetCreatedBy(createdBy);
    }
}

// ✅ Good - Private constructor for EF Core
public class Order : EntityBase
{
    public Order(Guid customerId, string createdBy)
    {
        CustomerId = customerId;
        SetCreatedBy(createdBy);
    }
    
    private Order() { } // For EF Core
}
```

## 🔗 Related Skills

- `efcore-specifications` - Querying entities with specifications
- `efcore-repos` - Persisting entities with repositories
- `dknet-overview` - Overall architecture

---

**When to Use This Skill**: Reference this skill when creating domain entities, implementing DDD patterns, or setting up entity base classes with audit trails and soft delete.
