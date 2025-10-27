# DKNet.EfCore.Events

[![NuGet](https://img.shields.io/nuget/v/DKNet.EfCore.Events)](https://www.nuget.org/packages/DKNet.EfCore.Events/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DKNet.EfCore.Events)](https://www.nuget.org/packages/DKNet.EfCore.Events/)
[![.NET](https://img.shields.io/badge/.NET-9.0-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](../../../../LICENSE)

Enhanced Entity Framework Core event-based functionality for implementing domain-driven design (DDD) patterns. This
library provides centralized event management, automatic event publishing during EF Core operations, and seamless
integration with domain entities.

## Features

- **Domain Event Management**: Queue and publish domain events from entities
- **Automatic Event Publishing**: Events fired automatically during EF Core SaveChanges
- **Event Publisher Abstraction**: Central hub for event routing and handling
- **EF Core Hooks Integration**: Pre and post-save event triggers
- **Custom Event Handlers**: Flexible event handling with dependency injection
- **Entity Event Tracking**: Track and manage events at the entity level
- **Exception Handling**: Robust error handling for event processing
- **Performance Optimized**: Efficient event queuing and batch processing

## Supported Frameworks

- .NET 9.0+
- Entity Framework Core 9.0+

## Installation

Install via NuGet Package Manager:

```bash
dotnet add package DKNet.EfCore.Events
```

Or via Package Manager Console:

```powershell
Install-Package DKNet.EfCore.Events
```

## Quick Start

### Setup Event Publisher

```csharp
using DKNet.EfCore.Events.Handlers;
using Microsoft.Extensions.DependencyInjection;

// Register event publisher implementation
services.AddEventPublisher<AppDbContext, EventPublisher>();

// Or use your custom implementation
public class CustomEventPublisher : IEventPublisher
{
    public async Task PublishAsync(object eventItem, CancellationToken cancellationToken = default)
    {
        // Custom event publishing logic
        await Task.CompletedTask;
    }
}

services.AddEventPublisher<AppDbContext, CustomEventPublisher>();
```

### Domain Entity with Events

```csharp
using DKNet.EfCore.Abstractions.Entities;

public class Product : Entity<Guid>
{
    public Product(string name, decimal price, string createdBy) 
        : base(Guid.NewGuid(), createdBy)
    {
        Name = name;
        Price = price;
        
        // Add domain event
        AddEvent(new ProductCreatedEvent(Id, name, price));
    }

    public string Name { get; private set; }
    public decimal Price { get; private set; }
    
    public void UpdatePrice(decimal newPrice, string updatedBy)
    {
        var oldPrice = Price;
        Price = newPrice;
        SetUpdatedBy(updatedBy);
        
        // Add domain event for price change
        AddEvent(new ProductPriceChangedEvent(Id, oldPrice, newPrice));
    }
}

// Domain events
public record ProductCreatedEvent(Guid ProductId, string Name, decimal Price);
public record ProductPriceChangedEvent(Guid ProductId, decimal OldPrice, decimal NewPrice);
```

### Event Handlers

```csharp
using DKNet.EfCore.Events.Handlers;

public class ProductCreatedHandler : INotificationHandler<ProductCreatedEvent>
{
    private readonly ILogger<ProductCreatedHandler> _logger;
    private readonly IEmailService _emailService;

    public ProductCreatedHandler(ILogger<ProductCreatedHandler> logger, IEmailService emailService)
    {
        _logger = logger;
        _emailService = emailService;
    }

    public async Task Handle(ProductCreatedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Product created: {ProductId} - {Name} (${Price})", 
            notification.ProductId, notification.Name, notification.Price);
            
        // Send notification email
        await _emailService.SendProductCreatedNotificationAsync(notification, cancellationToken);
    }
}

public class ProductPriceChangedHandler : INotificationHandler<ProductPriceChangedEvent>
{
    private readonly IInventoryService _inventoryService;

    public ProductPriceChangedHandler(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    public async Task Handle(ProductPriceChangedEvent notification, CancellationToken cancellationToken)
    {
        // Update inventory records
        await _inventoryService.UpdatePriceAsync(notification.ProductId, notification.NewPrice, cancellationToken);
    }
}
```

## Configuration

### DbContext Setup

Events are automatically published during SaveChanges when the event hook is registered:

```csharp
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Product> Products { get; set; }
    public DbSet<Order> Orders { get; set; }

    // Event publishing happens automatically via EventHook
}
```

### Event Handler Registration

```csharp
// Register event handlers
services.AddScoped<INotificationHandler<ProductCreatedEvent>, ProductCreatedHandler>();
services.AddScoped<INotificationHandler<ProductPriceChangedEvent>, ProductPriceChangedHandler>();

// Or use MediatR for automatic discovery
services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ProductCreatedHandler).Assembly));
```

## API Reference

### Core Interfaces

- `IEventPublisher` - Central event publishing abstraction
- `IEventEntity` - Interface for entities that can raise domain events (from DKNet.EfCore.Abstractions)
- `EntityEventItem` - Wrapper for entity events with metadata

### Event Management

- `AddEvent(object)` - Queue domain event on entity
- `ClearEvents()` - Clear all queued events
- `GetEvents()` - Retrieve all queued events

### Setup Extensions

- `AddEventPublisher<TDbContext, TImplementation>()` - Register event publisher with EF Core hooks

## Advanced Usage

### Custom Event Publisher

```csharp
public class MediatREventPublisher : IEventPublisher
{
    private readonly IMediator _mediator;
    private readonly ILogger<MediatREventPublisher> _logger;

    public MediatREventPublisher(IMediator mediator, ILogger<MediatREventPublisher> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task PublishAsync(object eventItem, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Publishing event: {EventType}", eventItem.GetType().Name);
            
            if (eventItem is INotification notification)
            {
                await _mediator.Publish(notification, cancellationToken);
            }
            else
            {
                _logger.LogWarning("Event {EventType} does not implement INotification", eventItem.GetType().Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event: {EventType}", eventItem.GetType().Name);
            throw new EventException($"Failed to publish event of type {eventItem.GetType().Name}", ex);
        }
    }
}
```

### Complex Domain Event Scenarios

```csharp
public class Order : AggregateRoot
{
    private readonly List<OrderItem> _items = [];

    public Order(Guid customerId, string createdBy) : base(createdBy)
    {
        CustomerId = customerId;
        Status = OrderStatus.Pending;
        
        AddEvent(new OrderCreatedEvent(Id, customerId));
    }

    public Guid CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();
    public decimal TotalAmount => _items.Sum(i => i.TotalPrice);

    public void AddItem(Guid productId, int quantity, decimal unitPrice)
    {
        var item = new OrderItem(productId, quantity, unitPrice);
        _items.Add(item);
        
        AddEvent(new OrderItemAddedEvent(Id, productId, quantity, unitPrice));
    }

    public void Complete(string updatedBy)
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException("Only pending orders can be completed");

        Status = OrderStatus.Completed;
        SetUpdatedBy(updatedBy);
        
        AddEvent(new OrderCompletedEvent(Id, CustomerId, TotalAmount, Items.Count));
    }

    public void Cancel(string reason, string updatedBy)
    {
        if (Status == OrderStatus.Completed)
            throw new InvalidOperationException("Completed orders cannot be cancelled");

        Status = OrderStatus.Cancelled;
        SetUpdatedBy(updatedBy);
        
        AddEvent(new OrderCancelledEvent(Id, reason));
    }
}

// Domain events
public record OrderCreatedEvent(Guid OrderId, Guid CustomerId);
public record OrderItemAddedEvent(Guid OrderId, Guid ProductId, int Quantity, decimal UnitPrice);
public record OrderCompletedEvent(Guid OrderId, Guid CustomerId, decimal TotalAmount, int ItemCount);
public record OrderCancelledEvent(Guid OrderId, string Reason);
```

### Event Handler with Side Effects

```csharp
public class OrderCompletedHandler : INotificationHandler<OrderCompletedEvent>
{
    private readonly IInventoryService _inventoryService;
    private readonly IPaymentService _paymentService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<OrderCompletedHandler> _logger;

    public OrderCompletedHandler(
        IInventoryService inventoryService,
        IPaymentService paymentService,
        INotificationService notificationService,
        ILogger<OrderCompletedHandler> logger)
    {
        _inventoryService = inventoryService;
        _paymentService = paymentService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task Handle(OrderCompletedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            // Update inventory
            await _inventoryService.ReserveItemsAsync(notification.OrderId, cancellationToken);
            
            // Process payment
            await _paymentService.ProcessPaymentAsync(notification.OrderId, notification.TotalAmount, cancellationToken);
            
            // Send confirmation
            await _notificationService.SendOrderConfirmationAsync(notification.CustomerId, notification.OrderId, cancellationToken);
            
            _logger.LogInformation("Order {OrderId} completed successfully. Total: ${TotalAmount}, Items: {ItemCount}",
                notification.OrderId, notification.TotalAmount, notification.ItemCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process order completion for {OrderId}", notification.OrderId);
            
            // Could add compensating actions or raise error events
            throw new EventException($"Failed to process order completion for {notification.OrderId}", ex);
        }
    }
}
```

## Event Lifecycle

1. **Event Creation**: Domain events are added to entities during business operations
2. **Event Queuing**: Events are stored in entity's event collection until SaveChanges
3. **Event Publishing**: Events are automatically published during EF Core SaveChanges via EventHook
4. **Event Handling**: Registered event handlers process the events asynchronously
5. **Event Cleanup**: Successfully processed events are cleared from entities

## Error Handling

```csharp
public class RobustEventPublisher : IEventPublisher
{
    private readonly IMediator _mediator;
    private readonly ILogger<RobustEventPublisher> _logger;

    public async Task PublishAsync(object eventItem, CancellationToken cancellationToken = default)
    {
        var maxRetries = 3;
        var retryDelay = TimeSpan.FromMilliseconds(100);

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await _mediator.Publish((INotification)eventItem, cancellationToken);
                return;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                _logger.LogWarning(ex, "Event publishing failed on attempt {Attempt} for {EventType}. Retrying...", 
                    attempt, eventItem.GetType().Name);
                    
                await Task.Delay(retryDelay * attempt, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Event publishing failed after {MaxRetries} attempts for {EventType}", 
                    maxRetries, eventItem.GetType().Name);
                throw new EventException($"Failed to publish event after {maxRetries} attempts", ex);
            }
        }
    }
}
```

## Performance Considerations

- **Batch Processing**: Events are published in batches during SaveChanges
- **Async Handlers**: All event handlers should be async for non-blocking execution
- **Memory Management**: Events are cleared after successful publishing
- **Transaction Scope**: Events are published within the same transaction as data changes

## Best Practices

- **Single Responsibility**: Keep event handlers focused on one concern
- **Idempotency**: Design event handlers to be idempotent
- **Error Isolation**: Don't let event handler failures affect the main transaction
- **Event Versioning**: Plan for event schema evolution
- **Testing**: Test event handlers independently from entities

## Contributing

See the main [CONTRIBUTING.md](../../../../CONTRIBUTING.md) for guidelines on how to contribute to this project.

## License

This project is licensed under the [MIT License](../../../../LICENSE).

## Related Packages

- [DKNet.EfCore.Abstractions](../DKNet.EfCore.Abstractions) - Core abstractions including IEventEntity
- [DKNet.EfCore.Extensions](../DKNet.EfCore.Extensions) - EF Core functionality extensions
- [DKNet.EfCore.Hooks](../DKNet.EfCore.Hooks) - EF Core lifecycle hooks (used internally)
- [DKNet.SlimBus.Extensions](../../SlimBus/DKNet.SlimBus.Extensions) - Alternative CQRS event handling

---

Part of the [DKNet Framework](https://github.com/baoduy/DKNet) - A comprehensive .NET framework for building modern,
scalable applications.