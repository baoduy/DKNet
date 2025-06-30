# DKNet.EfCore.Events

**Domain event handling and dispatching capabilities for Entity Framework Core that enable event-driven architecture patterns, supporting loose coupling between aggregates and implementing Domain-Driven Design (DDD) principles within the Onion Architecture.**

## What is this project?

DKNet.EfCore.Events provides a comprehensive event-driven architecture framework for Entity Framework Core applications. It enables entities to raise domain events that are automatically collected and dispatched during database operations, facilitating communication between different parts of the application without tight coupling.

### Key Features

- **IEventEntity Interface**: Contract for entities that can raise domain events
- **EntityEventItem**: Strongly-typed event container with metadata
- **Event Collection**: Automatic event queuing and retrieval from entities
- **Event Dispatching**: Automatic event publishing after successful database operations
- **Event Publisher Integration**: Seamless integration with messaging systems
- **Pre/Post Save Events**: Support for events before and after database operations
- **Exception Handling**: Comprehensive error handling for event processing
- **Async Support**: Full async/await support for event handling
- **Event Metadata**: Rich event information with timestamps and context

## How it contributes to DDD and Onion Architecture

### Domain Event Implementation

DKNet.EfCore.Events implements the **Domain Events pattern** that spans across multiple layers of the Onion Architecture, enabling clean communication between aggregates:

```
┌─────────────────────────────────────────────────────────────────┐
│                    🌐 Presentation Layer                        │
│                   (Controllers, API Endpoints)                  │
│                                                                 │
│  May listen to: Integration events for external notifications  │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                   🎯 Application Layer                          │
│              (Event Handlers, Application Services)            │
│                                                                 │
│  🎭 Domain Event Handlers - Cross-aggregate coordination       │
│  📊 Integration Event Handlers - External system integration   │
│  🔄 Event Orchestration - Complex business workflows           │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                    💼 Domain Layer                             │
│           (Entities, Aggregates, Domain Services)              │
│                                                                 │
│  🎯 IEventEntity - Entities that raise domain events           │
│  📋 Domain Events - Business-significant occurrences           │
│  🏷️ Event-driven Aggregate communication                       │
│  ✨ Pure domain logic with event publishing                    │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                 🗄️ Infrastructure Layer                        │
│                  (Event Dispatching, Persistence)              │
│                                                                 │
│  📡 Event Collection - Automatic event gathering from entities │
│  🚌 Event Dispatching - Publishing events after successful ops │
│  🗃️ EF Core Integration - Hooks into SaveChanges lifecycle     │
│  📊 Event Publishers - Message bus integration                 │
└─────────────────────────────────────────────────────────────────┘
```

### DDD Benefits

1. **Aggregate Communication**: Loose coupling between aggregates through events
2. **Business Logic Isolation**: Domain events express business concepts clearly
3. **Eventual Consistency**: Support for eventual consistency patterns
4. **Cross-Bounded Context**: Communication between different bounded contexts
5. **Audit Trail**: Comprehensive business event history
6. **Ubiquitous Language**: Events expressed in business terminology

### Onion Architecture Benefits

1. **Dependency Inversion**: Domain defines events, infrastructure handles dispatching
2. **Separation of Concerns**: Event handling separated from business logic
3. **Testability**: Domain events can be verified without infrastructure
4. **Technology Independence**: Event handling abstracted from specific technologies
5. **Extensibility**: Easy to add new event handlers without changing domain code

## How to use it

### Installation

```bash
dotnet add package DKNet.EfCore.Events
dotnet add package DKNet.EfCore.Abstractions
```

### Basic Usage Examples

#### 1. Implementing Event Entities

```csharp
using DKNet.EfCore.Abstractions;
using DKNet.EfCore.Events;

// Domain event definition
public record CustomerCreatedEvent(
    int CustomerId, 
    string CustomerName, 
    string Email, 
    DateTime CreatedAt) : IDomainEvent;

public record CustomerEmailChangedEvent(
    int CustomerId, 
    string OldEmail, 
    string NewEmail, 
    DateTime ChangedAt) : IDomainEvent;

// Entity that raises domain events
public class Customer : Entity<int>, IEventEntity
{
    private readonly List<EntityEventItem> _events = new();
    
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public string Email { get; private set; }
    public bool IsActive { get; private set; }
    
    // Constructor for new customers
    public Customer(string firstName, string lastName, string email)
    {
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        IsActive = true;
        
        // Raise domain event for customer creation
        AddEvent(new CustomerCreatedEvent(Id, $"{firstName} {lastName}", email, DateTime.UtcNow));
    }
    
    // Business method that raises domain event
    public void ChangeEmail(string newEmail)
    {
        if (Email == newEmail) return;
        
        var oldEmail = Email;
        Email = newEmail;
        
        // Raise domain event for email change
        AddEvent(new CustomerEmailChangedEvent(Id, oldEmail, newEmail, DateTime.UtcNow));
    }
    
    public void Deactivate()
    {
        if (!IsActive) return;
        
        IsActive = false;
        AddEvent(new CustomerDeactivatedEvent(Id, DateTime.UtcNow));
    }
    
    // IEventEntity implementation
    public void AddEvent(object eventItem)
    {
        _events.Add(new EntityEventItem(eventItem));
    }
    
    public IEnumerable<EntityEventItem> GetEvents()
    {
        return _events.AsReadOnly();
    }
    
    public void ClearEvents()
    {
        _events.Clear();
    }
}
```

#### 2. Event Handlers

```csharp
using DKNet.EfCore.Events;

// Domain event handler
public class CustomerCreatedEventHandler : IEventHandler<CustomerCreatedEvent>
{
    private readonly IEmailService _emailService;
    private readonly ICustomerStatsService _statsService;
    private readonly ILoggerFactory _logger;
    
    public CustomerCreatedEventHandler(
        IEmailService emailService,
        ICustomerStatsService statsService,
        ILoggerFactory logger)
    {
        _emailService = emailService;
        _statsService = statsService;
        _logger = logger;
    }
    
    public async Task Handle(CustomerCreatedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var logger = _logger.CreateLogger<CustomerCreatedEventHandler>();
        
        try
        {
            // Send welcome email
            await _emailService.SendWelcomeEmailAsync(
                domainEvent.Email, 
                domainEvent.CustomerName,
                cancellationToken);
            
            // Update customer statistics
            await _statsService.IncrementNewCustomerCountAsync(domainEvent.CreatedAt);
            
            logger.LogInformation("Successfully processed CustomerCreatedEvent for customer {CustomerId}", 
                domainEvent.CustomerId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process CustomerCreatedEvent for customer {CustomerId}", 
                domainEvent.CustomerId);
            throw;
        }
    }
}

// Cross-aggregate coordination handler
public class CustomerEmailChangedEventHandler : IEventHandler<CustomerEmailChangedEvent>
{
    private readonly IRepository<Order> _orderRepository;
    private readonly INotificationService _notificationService;
    
    public CustomerEmailChangedEventHandler(
        IRepository<Order> orderRepository,
        INotificationService notificationService)
    {
        _orderRepository = orderRepository;
        _notificationService = notificationService;
    }
    
    public async Task Handle(CustomerEmailChangedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        // Update related orders with new email for notifications
        var pendingOrders = await _orderRepository.Gets()
            .Where(o => o.CustomerId == domainEvent.CustomerId && 
                       o.Status == OrderStatus.Pending)
            .ToListAsync(cancellationToken);
        
        foreach (var order in pendingOrders)
        {
            order.UpdateCustomerEmail(domainEvent.NewEmail);
        }
        
        // Send notification about email change
        await _notificationService.NotifyEmailChangeAsync(
            domainEvent.CustomerId,
            domainEvent.OldEmail,
            domainEvent.NewEmail,
            cancellationToken);
    }
}
```

#### 3. DbContext Integration

```csharp
using DKNet.EfCore.Events;

public class ApplicationDbContext : DbContext
{
    private readonly IEventPublisher _eventPublisher;
    
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        IEventPublisher eventPublisher) : base(options)
    {
        _eventPublisher = eventPublisher;
    }
    
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Order> Orders { get; set; }
    
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Collect domain events before saving
        var eventEntities = ChangeTracker.Entries<IEventEntity>()
            .Where(e => e.Entity.GetEvents().Any())
            .ToList();
        
        var domainEvents = eventEntities
            .SelectMany(e => e.Entity.GetEvents())
            .ToList();
        
        // Save changes to database
        var result = await base.SaveChangesAsync(cancellationToken);
        
        // Dispatch events after successful save
        if (domainEvents.Any())
        {
            await DispatchEventsAsync(domainEvents, cancellationToken);
            
            // Clear events from entities
            foreach (var entityEntry in eventEntities)
            {
                entityEntry.Entity.ClearEvents();
            }
        }
        
        return result;
    }
    
    private async Task DispatchEventsAsync(
        IEnumerable<EntityEventItem> domainEvents, 
        CancellationToken cancellationToken)
    {
        foreach (var eventItem in domainEvents)
        {
            try
            {
                await _eventPublisher.PublishAsync(eventItem.EventData, cancellationToken);
            }
            catch (Exception ex)
            {
                // Log error but don't fail the transaction
                // Consider implementing compensation patterns for critical events
                var logger = this.GetService<ILogger<ApplicationDbContext>>();
                logger?.LogError(ex, "Failed to publish domain event {EventType}", 
                    eventItem.EventData.GetType().Name);
            }
        }
    }
}
```

#### 4. Service Registration

```csharp
using DKNet.EfCore.Events;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDomainEvents(this IServiceCollection services)
    {
        // Register event handlers
        services.AddScoped<IEventHandler<CustomerCreatedEvent>, CustomerCreatedEventHandler>();
        services.AddScoped<IEventHandler<CustomerEmailChangedEvent>, CustomerEmailChangedEventHandler>();
        services.AddScoped<IEventHandler<OrderCompletedEvent>, OrderCompletedEventHandler>();
        
        // Register event publisher (implementation depends on messaging system)
        services.AddScoped<IEventPublisher, SlimBusEventPublisher>();
        
        return services;
    }
}

// In Program.cs or Startup.cs
services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

services.AddDomainEvents();
```

### Advanced Usage Examples

#### 1. Complex Event Orchestration

```csharp
public class OrderCompletedEventHandler : IEventHandler<OrderCompletedEvent>
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IInventoryService _inventoryService;
    private readonly IShippingService _shippingService;
    private readonly ILoyaltyService _loyaltyService;
    
    public async Task Handle(OrderCompletedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        // Update customer statistics
        var customer = await _customerRepository.FindAsync(domainEvent.CustomerId);
        customer?.UpdateOrderHistory(domainEvent.OrderId, domainEvent.OrderTotal);
        
        // Update inventory levels
        await _inventoryService.UpdateInventoryAsync(domainEvent.OrderItems, cancellationToken);
        
        // Schedule shipping
        await _shippingService.ScheduleShippingAsync(domainEvent.OrderId, cancellationToken);
        
        // Award loyalty points
        await _loyaltyService.AwardPointsAsync(
            domainEvent.CustomerId, 
            domainEvent.OrderTotal, 
            cancellationToken);
    }
}
```

#### 2. Integration Events

```csharp
// Integration event for external systems
public record CustomerCreatedIntegrationEvent(
    int CustomerId,
    string CustomerName,
    string Email,
    DateTime CreatedAt) : IIntegrationEvent;

public class CustomerCreatedIntegrationEventHandler : IEventHandler<CustomerCreatedEvent>
{
    private readonly IMessageBus _messageBus;
    
    public async Task Handle(CustomerCreatedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        // Transform domain event to integration event
        var integrationEvent = new CustomerCreatedIntegrationEvent(
            domainEvent.CustomerId,
            domainEvent.CustomerName,
            domainEvent.Email,
            domainEvent.CreatedAt);
        
        // Publish to external systems
        await _messageBus.PublishAsync(integrationEvent, cancellationToken);
    }
}
```

#### 3. Event Sourcing Pattern

```csharp
public class EventSourcedAggregate : Entity<Guid>, IEventEntity
{
    private readonly List<EntityEventItem> _uncommittedEvents = new();
    private readonly List<IDomainEvent> _eventHistory = new();
    
    public int Version { get; private set; }
    
    // Apply events to rebuild state
    public void LoadFromHistory(IEnumerable<IDomainEvent> events)
    {
        foreach (var evt in events)
        {
            ApplyEvent(evt, false);
            Version++;
        }
    }
    
    // Apply new events and mark as uncommitted
    protected void ApplyEvent(IDomainEvent domainEvent, bool markAsNew = true)
    {
        // Apply event to aggregate state
        When(domainEvent);
        
        if (markAsNew)
        {
            _uncommittedEvents.Add(new EntityEventItem(domainEvent));
            Version++;
        }
        
        _eventHistory.Add(domainEvent);
    }
    
    // Pattern matching for event application
    private void When(IDomainEvent domainEvent)
    {
        switch (domainEvent)
        {
            case CustomerCreatedEvent evt:
                // Apply customer created logic
                break;
            case CustomerEmailChangedEvent evt:
                // Apply email changed logic
                break;
            default:
                throw new InvalidOperationException($"Unknown event type: {domainEvent.GetType()}");
        }
    }
    
    public void AddEvent(object eventItem) => 
        ApplyEvent((IDomainEvent)eventItem);
    
    public IEnumerable<EntityEventItem> GetEvents() => 
        _uncommittedEvents.AsReadOnly();
    
    public void ClearEvents() => 
        _uncommittedEvents.Clear();
}
```

## Best Practices

### 1. Event Design

```csharp
// Good: Immutable events with rich business information
public record CustomerEmailChangedEvent(
    int CustomerId,
    string OldEmail,
    string NewEmail,
    DateTime ChangedAt,
    string ChangedBy) : IDomainEvent;

// Good: Events represent business facts
public record OrderShippedEvent(
    int OrderId,
    string TrackingNumber,
    string ShippingAddress,
    DateTime ShippedAt) : IDomainEvent;

// Avoid: Technical events that don't represent business concepts
public record EntityUpdatedEvent(int EntityId, Dictionary<string, object> Changes) : IDomainEvent;
```

### 2. Event Handler Idempotency

```csharp
public class OrderCompletedEventHandler : IEventHandler<OrderCompletedEvent>
{
    private readonly IIdempotencyService _idempotencyService;
    
    public async Task Handle(OrderCompletedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var idempotencyKey = $"order-completed-{domainEvent.OrderId}";
        
        if (await _idempotencyService.HasBeenProcessedAsync(idempotencyKey))
        {
            return; // Already processed
        }
        
        try
        {
            // Process event
            await ProcessOrderCompletionAsync(domainEvent, cancellationToken);
            
            // Mark as processed
            await _idempotencyService.MarkAsProcessedAsync(idempotencyKey);
        }
        catch (Exception)
        {
            // Don't mark as processed if failed
            throw;
        }
    }
}
```

### 3. Error Handling

```csharp
public class ResilientEventHandler : IEventHandler<CustomerCreatedEvent>
{
    private readonly IRetryPolicy _retryPolicy;
    private readonly ICircuitBreaker _circuitBreaker;
    private readonly IDeadLetterQueue _deadLetterQueue;
    
    public async Task Handle(CustomerCreatedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            await _retryPolicy.ExecuteAsync(async () =>
            {
                await _circuitBreaker.ExecuteAsync(async () =>
                {
                    await ProcessEventAsync(domainEvent, cancellationToken);
                });
            });
        }
        catch (Exception ex)
        {
            // Send to dead letter queue for manual processing
            await _deadLetterQueue.SendAsync(domainEvent, ex, cancellationToken);
            throw;
        }
    }
}
```

### 4. Testing Domain Events

```csharp
[Test]
public void ChangeEmail_ValidEmail_RaisesCustomerEmailChangedEvent()
{
    // Arrange
    var customer = new Customer("John", "Doe", "john@example.com");
    customer.ClearEvents(); // Clear creation event
    
    // Act
    customer.ChangeEmail("john.doe@example.com");
    
    // Assert
    var events = customer.GetEvents();
    Assert.Single(events);
    
    var emailChangedEvent = events.First().EventData as CustomerEmailChangedEvent;
    Assert.NotNull(emailChangedEvent);
    Assert.Equal(customer.Id, emailChangedEvent.CustomerId);
    Assert.Equal("john@example.com", emailChangedEvent.OldEmail);
    Assert.Equal("john.doe@example.com", emailChangedEvent.NewEmail);
}

[Test]
public async Task Handle_CustomerCreatedEvent_SendsWelcomeEmail()
{
    // Arrange
    var emailService = new Mock<IEmailService>();
    var handler = new CustomerCreatedEventHandler(emailService.Object, null, null);
    var domainEvent = new CustomerCreatedEvent(1, "John Doe", "john@example.com", DateTime.UtcNow);
    
    // Act
    await handler.Handle(domainEvent);
    
    // Assert
    emailService.Verify(x => x.SendWelcomeEmailAsync("john@example.com", "John Doe", It.IsAny<CancellationToken>()), 
        Times.Once);
}
```

## Integration with Other DKNet Components

DKNet.EfCore.Events integrates seamlessly with other DKNet components:

- **DKNet.EfCore.Abstractions**: Uses entity base classes and interfaces
- **DKNet.EfCore.Repos**: Events are collected and dispatched during repository operations
- **DKNet.SlimBus.Extensions**: Integrates with SlimMessageBus for event publishing
- **DKNet.EfCore.Hooks**: Provides hooks for event lifecycle management
- **DKNet.Fw.Extensions**: Leverages core framework utilities for event processing

---

> 💡 **Architecture Tip**: Use DKNet.EfCore.Events to implement the Domain Events pattern in your DDD applications. Domain events enable loose coupling between aggregates and provide a clean way to handle cross-cutting concerns without violating aggregate boundaries. Always ensure events represent business facts and are immutable once created.