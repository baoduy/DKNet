# DKNet.EfCore.Abstractions

[![NuGet](https://img.shields.io/nuget/v/DKNet.EfCore.Abstractions)](https://www.nuget.org/packages/DKNet.EfCore.Abstractions/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DKNet.EfCore.Abstractions)](https://www.nuget.org/packages/DKNet.EfCore.Abstractions/)
[![.NET](https://img.shields.io/badge/.NET-9.0-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](../../../../LICENSE)

Core abstractions and interfaces for Entity Framework Core applications implementing Domain-Driven Design (DDD) patterns. This package provides essential base classes, interfaces, and attributes for building robust data access layers with auditing, events, and entity management capabilities.

## Features

- **Entity Base Classes**: Generic entity base classes with flexible key types
- **Audit Interfaces**: Built-in auditing capabilities with creation and modification tracking
- **Event Management**: Domain event support for entities (IEventEntity)
- **Soft Delete Support**: Soft deletion patterns for logical record removal
- **Concurrency Control**: Optimistic concurrency control interfaces
- **Sequence Attributes**: Database sequence generation for unique identifiers
- **Static Data Attributes**: Marking entities for static/reference data
- **Ignore Entity Attributes**: Control over entity discovery and mapping

## Supported Frameworks

- .NET 9.0+
- Entity Framework Core 9.0+

## Installation

Install via NuGet Package Manager:

```bash
dotnet add package DKNet.EfCore.Abstractions
```

Or via Package Manager Console:

```powershell
Install-Package DKNet.EfCore.Abstractions
```

## Quick Start

### Basic Entity Implementation

```csharp
using DKNet.EfCore.Abstractions.Entities;

public class Product : Entity<Guid>
{
    public Product(string name, decimal price, string createdBy) 
        : base(Guid.NewGuid(), createdBy)
    {
        Name = name;
        Price = price;
    }

    public string Name { get; private set; }
    public decimal Price { get; private set; }
    
    public void UpdatePrice(decimal newPrice, string updatedBy)
    {
        Price = newPrice;
        SetUpdatedBy(updatedBy);
    }
}
```

### Auditable Entity

```csharp
using DKNet.EfCore.Abstractions.Entities;

public class Customer : AuditEntity<int>
{
    public Customer(string name, string email, string createdBy) 
        : base(createdBy)
    {
        Name = name;
        Email = email;
    }

    public string Name { get; private set; }
    public string Email { get; private set; }
    
    // Inherits: CreatedBy, CreatedOn, UpdatedBy, UpdatedOn
}
```

### Soft Deletable Entity

```csharp
using DKNet.EfCore.Abstractions.Entities;

public class Document : Entity<Guid>, ISoftDeletableEntity
{
    public Document(string title, string createdBy) : base(Guid.NewGuid(), createdBy)
    {
        Title = title;
    }

    public string Title { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedOn { get; private set; }
    public string? DeletedBy { get; private set; }

    public void SoftDelete(string deletedBy)
    {
        IsDeleted = true;
        DeletedOn = DateTimeOffset.UtcNow;
        DeletedBy = deletedBy;
    }
}
```

## Configuration

### Entity Configuration with Attributes

```csharp
using DKNet.EfCore.Abstractions.Attributes;

[StaticData] // Marks as reference/static data
public class Category : Entity<int>
{
    [Sequence(typeof(int))] // Auto-generate sequence values
    public int Order { get; set; }
    
    public string Name { get; set; }
}

[IgnoreEntity] // Exclude from EF discovery
public class TemporaryData
{
    public string Value { get; set; }
}
```

### SQL Sequence Configuration

```csharp
using DKNet.EfCore.Abstractions.Attributes;

public class Invoice : Entity<long>
{
    [SqlSequence("invoice_number_seq", Schema = "billing")]
    public long InvoiceNumber { get; set; }
    
    public decimal Amount { get; set; }
}
```

## API Reference

### Core Interfaces

- `IEntity<TKey>` - Basic entity contract with generic key
- `IAuditedProperties` - Auditing properties (CreatedBy, CreatedOn, etc.)
- `ISoftDeletableEntity` - Soft deletion capabilities
- `IEventEntity` - Domain event management
- `IConcurrencyEntity` - Optimistic concurrency control

### Base Classes

- `Entity<TKey>` - Generic entity base with event support
- `AuditEntity<TKey>` - Entity with full audit trail capabilities

### Attributes

- `[Sequence(Type)]` - Generate sequential values for fields
- `[SqlSequence(string)]` - SQL-based sequence generation
- `[StaticData]` - Mark entity as static/reference data
- `[IgnoreEntity]` - Exclude entity from EF discovery

## Advanced Usage

### Domain Events with Entities

```csharp
public class Order : Entity<Guid>
{
    public Order(string customerName, string createdBy) : base(Guid.NewGuid(), createdBy)
    {
        CustomerName = customerName;
        Status = OrderStatus.Pending;
        
        // Add domain event
        AddEvent(new OrderCreatedEvent(Id, customerName));
    }

    public string CustomerName { get; private set; }
    public OrderStatus Status { get; private set; }
    
    public void Complete(string updatedBy)
    {
        Status = OrderStatus.Completed;
        SetUpdatedBy(updatedBy);
        
        // Add domain event
        AddEvent(new OrderCompletedEvent(Id));
    }
}

public record OrderCreatedEvent(Guid OrderId, string CustomerName);
public record OrderCompletedEvent(Guid OrderId);
```

### Custom Audit Implementation

```csharp
public class CustomAuditEntity : Entity<Guid>, IAuditedProperties
{
    protected CustomAuditEntity(string createdBy) : base(Guid.NewGuid(), createdBy)
    {
        CreatedBy = createdBy;
        CreatedOn = DateTimeOffset.UtcNow;
    }

    public string CreatedBy { get; protected set; }
    public DateTimeOffset CreatedOn { get; protected set; }
    public string? UpdatedBy { get; protected set; }
    public DateTimeOffset? UpdatedOn { get; protected set; }

    protected void SetUpdatedBy(string updatedBy)
    {
        UpdatedBy = updatedBy;
        UpdatedOn = DateTimeOffset.UtcNow;
    }
}
```

### Aggregate Root Pattern

```csharp
public class AggregateRoot : Entity<Guid>
{
    protected AggregateRoot(string createdBy) : base(Guid.NewGuid(), createdBy)
    {
    }

    // Additional aggregate-specific behavior
    // Event management, invariant enforcement, etc.
}
```

## Entity Lifecycle

The abstractions support full entity lifecycle management:

1. **Creation**: Entities initialized with required audit information
2. **Modification**: Automatic tracking of changes and updates
3. **Event Handling**: Domain events queued and managed
4. **Soft Deletion**: Logical removal without physical deletion
5. **Concurrency**: Optimistic concurrency control support

## Thread Safety

- Entity instances are not thread-safe by design (following EF Core patterns)
- Event collections are managed internally and should not be accessed concurrently
- Use appropriate concurrency control mechanisms in your DbContext

## Performance Considerations

- Generic key types provide flexibility without boxing overhead
- Event collections use efficient Collection<T> internally
- Audit properties use DateTimeOffset for timezone-aware timestamps
- Sequence attributes optimize database-generated values

## Contributing

See the main [CONTRIBUTING.md](../../../../CONTRIBUTING.md) for guidelines on how to contribute to this project.

## License

This project is licensed under the [MIT License](../../../../LICENSE).

## Related Packages

- [DKNet.EfCore.Extensions](../DKNet.EfCore.Extensions) - EF Core functionality extensions
- [DKNet.EfCore.Events](../DKNet.EfCore.Events) - Domain event handling and dispatching
- [DKNet.EfCore.Repos](../DKNet.EfCore.Repos) - Repository pattern implementations
- [DKNet.EfCore.Hooks](../DKNet.EfCore.Hooks) - EF Core lifecycle hooks

---

Part of the [DKNet Framework](https://github.com/baoduy/DKNet) - A comprehensive .NET framework for building modern, scalable applications.