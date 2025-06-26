# Entity Framework Core Extensions

The EfCore Extensions provide comprehensive enhancements to Entity Framework Core that implement repository patterns, domain events, data access abstractions, and advanced EF Core functionality specifically designed for Domain-Driven Design (DDD) and Onion Architecture patterns.

## Components

### Core Abstractions & Patterns
- [DKNet.EfCore.Abstractions](./DKNet.EfCore.Abstractions.md) - Core abstractions and interfaces
- [DKNet.EfCore.Repos.Abstractions](./DKNet.EfCore.Repos.Abstractions.md) - Repository pattern abstractions
- [DKNet.EfCore.Repos](./DKNet.EfCore.Repos.md) - Repository pattern implementations

### Domain Events & Lifecycle Management
- [DKNet.EfCore.Events](./DKNet.EfCore.Events.md) - Domain event handling and dispatching
- [DKNet.EfCore.Hooks](./DKNet.EfCore.Hooks.md) - Lifecycle hooks for EF Core operations

### Data Access & Security
- [DKNet.EfCore.DataAuthorization](./DKNet.EfCore.DataAuthorization.md) - Data authorization and access control
- [DKNet.EfCore.Extensions](./DKNet.EfCore.Extensions.md) - EF Core functionality enhancements

### Database Utilities
- [DKNet.EfCore.Relational.Helpers](./DKNet.EfCore.Relational.Helpers.md) - Relational database utilities

## Architecture Role in DDD & Onion Architecture

The EfCore Extensions form the **Infrastructure Layer** in the Onion Architecture, providing all data access concerns while maintaining proper dependency inversion:

```
┌─────────────────────────────────────────────────────────────────┐
│                    🌐 Presentation Layer                        │
│                   (Controllers, API Endpoints)                  │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                   🎯 Application Layer                          │
│              (Use Cases, Application Services)                  │
│                                                                 │
│  Dependencies: Repository Interfaces (from Domain)             │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                    💼 Domain Layer                             │
│           (Entities, Aggregates, Domain Services)              │
│                                                                 │
│  • IEventEntity (from EfCore.Abstractions)                    │
│  • Domain Events (published via EfCore.Events)                │
│  • Repository Interfaces (from EfCore.Repos.Abstractions)     │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                 🗄️ Infrastructure Layer                        │
│                  (Data Access, Persistence)                    │
│                                                                 │
│  🗃️ DKNet.EfCore.Repos - Repository Implementations           │
│  📋 DKNet.EfCore.Events - Domain Event Dispatching            │
│  🔒 DKNet.EfCore.DataAuthorization - Access Control           │
│  ⚡ DKNet.EfCore.Hooks - Lifecycle Management                  │
│  ⚙️ DKNet.EfCore.Extensions - EF Core Enhancements            │
│  🔧 DKNet.EfCore.Relational.Helpers - DB Utilities            │
└─────────────────────────────────────────────────────────────────┘
```

## Key Design Patterns Implemented

### 1. Repository Pattern
- **Abstractions**: Define contracts in the domain layer
- **Implementations**: Concrete implementations in infrastructure layer
- **Generic Repositories**: Common CRUD operations with type safety
- **Specialized Repositories**: Domain-specific data access patterns

### 2. Domain Events
- **Event Sourcing**: Entities can raise domain events
- **Event Dispatching**: Automatic event publishing after successful operations
- **Event Handlers**: Decoupled event processing
- **Integration Events**: Support for external system communication

### 3. Unit of Work
- **Transaction Management**: Coordinate multiple repository operations
- **Change Tracking**: EF Core change tracking integration
- **Bulk Operations**: Efficient batch operations
- **Concurrency Control**: Optimistic concurrency support

### 4. Data Authorization
- **Role-Based Access Control**: Permission-based data filtering
- **Policy-Based Authorization**: Flexible authorization rules
- **Field-Level Security**: Granular access control
- **Audit Logging**: Comprehensive audit trail

## DDD Implementation Benefits

### 1. Aggregate Consistency
- Entity base classes enforce identity and concurrency rules
- Event entities support domain event publishing
- Repository patterns maintain aggregate boundaries

### 2. Domain Events
- Loosely coupled domain logic through events
- Cross-aggregate communication without direct dependencies
- Integration with external systems via published events

### 3. Ubiquitous Language
- Repository interfaces use domain terminology
- Entity configurations reflect business rules
- Static data attributes maintain reference data consistency

### 4. Bounded Context Support
- Separate DbContexts for different bounded contexts
- Context-specific repository implementations
- Cross-context event integration

## Onion Architecture Benefits

### 1. Dependency Inversion
- Domain layer defines repository interfaces
- Infrastructure layer implements concrete repositories
- Application layer depends on abstractions, not implementations

### 2. Testability
- Repository abstractions enable easy mocking
- Domain events can be tested in isolation
- Business logic is independent of data access technology

### 3. Technology Agnostic
- Abstract repositories can be implemented with any data access technology
- Domain layer is completely unaware of EF Core
- Easy to swap out persistence implementations

### 4. Separation of Concerns
- Each component has a single, well-defined responsibility
- Cross-cutting concerns (authorization, events) are properly separated
- Infrastructure concerns don't leak into domain logic

## Integration Patterns

### 1. Domain Entity with Events
```csharp
public class Order : Entity, IEventEntity
{
    // Domain properties and business logic
    
    public void CompleteOrder()
    {
        Status = OrderStatus.Completed;
        AddEvent(new OrderCompletedEvent(Id, CustomerId));
    }
}
```

### 2. Repository Pattern Implementation
```csharp
public interface IOrderRepository : IRepository<Order>
{
    Task<Order?> GetByOrderNumberAsync(string orderNumber);
}

public class OrderRepository : GenericRepository<Order>, IOrderRepository
{
    // Implementation in infrastructure layer
}
```

### 3. Domain Event Handling
```csharp
public class OrderCompletedEventHandler : IEventHandler<OrderCompletedEvent>
{
    public async Task Handle(OrderCompletedEvent evt)
    {
        // Handle cross-aggregate concerns
        // Send notifications, update read models, etc.
    }
}
```

## Performance & Scalability Features

- **Query Optimization**: IQueryable support for efficient database queries
- **Change Tracking**: Optimized EF Core change tracking patterns
- **Bulk Operations**: Support for high-performance batch operations
- **Connection Management**: Proper DbContext lifecycle management
- **Caching Integration**: Support for distributed caching patterns

## Security & Compliance

- **Data Authorization**: Comprehensive access control mechanisms
- **Audit Logging**: Built-in audit trail capabilities
- **Encryption Support**: Integration with encryption services
- **Compliance Ready**: GDPR and other regulatory compliance support