# Messaging & CQRS

The Messaging & CQRS components provide a lightweight, efficient alternative to MediatR using SlimMessageBus, implementing Command Query Responsibility Segregation (CQRS) patterns and event-driven architecture for Domain-Driven Design applications.

## Components

- [DKNet.SlimBus.Extensions](./DKNet.SlimBus.Extensions.md) - SlimMessageBus extensions for EF Core integration

## Architecture Role in DDD & Onion Architecture

The Messaging components implement **CQRS patterns** that span multiple layers of the Onion Architecture, enabling clean separation between read and write operations:

```
┌─────────────────────────────────────────────────────────────────┐
│                    🌐 Presentation Layer                        │
│                   (Controllers, API Endpoints)                  │
│                                                                 │
│  Sends: Commands and Queries via IMessageBus                   │
│  Receives: Results and DTOs from handlers                      │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                   🎯 Application Layer                          │
│              (Command/Query Handlers, Use Cases)               │
│                                                                 │
│  📨 Command Handlers - Write operations (IHandler<TRequest>)   │
│  📊 Query Handlers - Read operations (IQueryHandler<TQuery>)   │
│  🔄 Event Handlers - Cross-cutting concerns                    │
│  ⚡ Auto-save behavior - EF Core integration                   │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                    💼 Domain Layer                             │
│           (Entities, Aggregates, Domain Services)              │
│                                                                 │
│  🎭 Domain Events - Aggregate communication                    │
│  📝 Business Logic - Pure domain operations                    │
│  🏷️ No messaging dependencies - Clean separation               │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                 🗄️ Infrastructure Layer                        │
│                  (Message Bus, Event Dispatching)              │
│                                                                 │
│  🚌 SlimMessageBus - Message routing and delivery              │
│  📡 Event Publishers - Domain event dispatching                │
│  🗄️ DbContext Integration - Automatic change persistence       │
└─────────────────────────────────────────────────────────────────┘
```

## Key CQRS Patterns Implemented

### 1. Command Query Separation
- **Commands**: Write operations that modify state (`IWitResponse<T>`, `INoResponse`)
- **Queries**: Read operations that return data (`IWitResponse<T>`, `IWitPageResponse<T>`)
- **Handlers**: Separate handlers for commands and queries
- **Results**: Type-safe result handling with FluentResults

### 2. Event-Driven Architecture
- **Domain Events**: Published automatically after successful operations
- **Event Handlers**: Decoupled cross-cutting concerns
- **Integration Events**: External system communication
- **Event Sourcing**: Support for event-based state management

### 3. Pipeline Behaviors
- **Auto-Save**: Automatic EF Core change persistence
- **Validation**: Request validation before processing
- **Logging**: Comprehensive operation logging
- **Error Handling**: Centralized exception management

### 4. Messaging Patterns
- **Request/Response**: Synchronous command and query processing
- **Publish/Subscribe**: Asynchronous event handling
- **Message Routing**: Flexible message delivery strategies
- **Multiple Transports**: Support for in-memory, Azure Service Bus, etc.

## DDD Implementation Benefits

### 1. Aggregate Coordination
- Commands operate on single aggregates maintaining consistency
- Events enable communication between aggregates without direct coupling
- Queries provide optimized read models independent of domain structure
- Transaction boundaries aligned with business operations

### 2. Domain Event Integration
- Automatic domain event publishing after successful state changes
- Loose coupling between bounded contexts through events
- Support for eventual consistency patterns
- Integration with external systems via published events

### 3. Clean Architecture
- Application layer orchestrates without containing business logic
- Domain layer remains free of infrastructure concerns
- Clear separation between read and write models
- Technology-agnostic message contracts

### 4. Scalability Patterns
- Separate scaling of read and write operations
- Async processing for non-critical operations
- Support for distributed messaging scenarios
- Optimized query paths for different read models

## Onion Architecture Benefits

### 1. Dependency Inversion
- Application layer defines message contracts
- Infrastructure layer provides message bus implementation
- Domain layer is completely unaware of messaging technology
- Easy to swap messaging implementations

### 2. Testability
- Mock message bus for unit testing
- Test handlers in isolation
- Verify message publishing without infrastructure
- Integration tests with in-memory bus

### 3. Separation of Concerns
- Commands focus on business operations
- Queries optimize for specific read scenarios
- Events handle cross-cutting concerns
- Each handler has single responsibility

### 4. Technology Independence
- Abstract message contracts independent of SlimMessageBus
- Support for multiple message transports
- Easy migration between messaging technologies
- Consistent patterns across different environments

## Integration Patterns

### 1. Command Processing
```csharp
// Command definition
public record CreateOrderCommand(Guid CustomerId, List<OrderItem> Items) 
    : Fluents.Requests.IWitResponse<Result<Guid>>;

// Command handler
public class CreateOrderHandler : Fluents.Requests.IHandler<CreateOrderCommand, Result<Guid>>
{
    public async Task<IResult<Result<Guid>>> Handle(CreateOrderCommand command)
    {
        // Business logic here
        // Auto-save handled by EfAutoSavePostProcessor
    }
}
```

### 2. Query Processing
```csharp
// Query definition
public record GetCustomerOrdersQuery(Guid CustomerId, int Page, int PageSize) 
    : Fluents.Queries.IWitPageResponse<OrderSummaryDto>;

// Query handler
public class GetCustomerOrdersHandler : Fluents.Queries.IPageHandler<GetCustomerOrdersQuery, OrderSummaryDto>
{
    public async Task<IPagedList<OrderSummaryDto>> Handle(GetCustomerOrdersQuery query)
    {
        // Optimized read logic here
    }
}
```

### 3. Event Handling
```csharp
// Domain event
public record OrderCreatedEvent(Guid OrderId, Guid CustomerId, decimal TotalAmount);

// Event handler
public class OrderCreatedEventHandler : IEventHandler<OrderCreatedEvent>
{
    public async Task Handle(OrderCreatedEvent evt)
    {
        // Cross-cutting concerns (notifications, logging, etc.)
    }
}
```

## Performance Features

### 1. SlimMessageBus Advantages
- **Lightweight**: Minimal overhead compared to MediatR
- **High Performance**: Optimized for throughput and low latency
- **Memory Efficient**: Reduced allocations and garbage collection
- **Flexible Routing**: Efficient message delivery strategies

### 2. EF Core Integration
- **Auto-Save**: Automatic change tracking and persistence
- **Transaction Management**: Proper DbContext lifecycle
- **Change Detection**: Only save when changes exist
- **Bulk Operations**: Support for batch processing

### 3. Caching Support
- **Query Caching**: Cache frequently accessed read models
- **Result Caching**: Cache expensive computation results
- **Distributed Caching**: Support for Redis and other providers
- **Cache Invalidation**: Event-driven cache updates

## Security & Compliance

### 1. Authorization Integration
- **Command Authorization**: Validate permissions before execution
- **Query Authorization**: Filter results based on user access
- **Event Security**: Secure event publishing and handling
- **Audit Logging**: Comprehensive operation tracking

### 2. Data Protection
- **Input Validation**: Validate all command and query inputs
- **Output Sanitization**: Clean sensitive data from responses
- **Encryption Support**: Protect sensitive message content
- **Compliance Ready**: GDPR and regulatory compliance support