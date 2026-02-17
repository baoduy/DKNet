---
name: slimbus-messaging
description: DKNet SlimBus - Lightweight message bus for event-driven architectures with publish/subscribe patterns
license: MIT
---

# SlimBus Messaging Skill

This skill helps GitHub Copilot generate code using DKNet's SlimBus package (`DKNet.SlimBus.Extensions`) for building event-driven applications with a lightweight message bus.

## 🎯 Package Purpose

`DKNet.SlimBus.Extensions` provides:
- **Publish/Subscribe** - Decouple components with events
- **Message Handlers** - Type-safe message handling
- **Async Processing** - Non-blocking message processing
- **DI Integration** - Automatic handler registration
- **Error Handling** - Built-in retry and error handling

**NuGet Package**: `DKNet.SlimBus.Extensions`

## 📦 Installation

```bash
dotnet add package DKNet.SlimBus.Extensions
```

## 🏗️ Setup

```csharp
using DKNet.SlimBus.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Register SlimBus
builder.Services.AddSlimBus(options =>
{
    options.EnableLogging = true;
    options.MaxRetries = 3;
    options.RetryDelay = TimeSpan.FromSeconds(1);
});

// Auto-register all handlers in assembly
builder.Services.AddSlimBusHandlers(typeof(Program).Assembly);

var app = builder.Build();
app.Run();
```

## 🎯 Usage Patterns

### Pattern 1: Define Messages
```csharp
using DKNet.SlimBus;

/// <summary>
///     Event published when an order is created.
/// </summary>
public record OrderCreatedEvent(
    Guid OrderId,
    Guid CustomerId,
    decimal TotalAmount,
    DateTime CreatedAt) : IMessage;

/// <summary>
///     Event published when a payment is processed.
/// </summary>
public record PaymentProcessedEvent(
    Guid PaymentId,
    Guid OrderId,
    decimal Amount,
    PaymentStatus Status) : IMessage;

/// <summary>
///     Command to send order confirmation email.
/// </summary>
public record SendOrderConfirmationCommand(
    Guid OrderId,
    string CustomerEmail) : IMessage;
```

### Pattern 2: Implement Message Handlers
```csharp
using DKNet.SlimBus;

/// <summary>
///     Handles OrderCreatedEvent by sending notifications.
/// </summary>
public class OrderCreatedEventHandler : IMessageHandler<OrderCreatedEvent>
{
    private readonly ILogger<OrderCreatedEventHandler> _logger;
    private readonly IEmailService _emailService;
    private readonly IMessageBus _messageBus;
    
    public OrderCreatedEventHandler(
        ILogger<OrderCreatedEventHandler> logger,
        IEmailService emailService,
        IMessageBus messageBus)
    {
        _logger = logger;
        _emailService = emailService;
        _messageBus = messageBus;
    }
    
    /// <summary>
    ///     Handles the OrderCreatedEvent.
    /// </summary>
    public async Task HandleAsync(
        OrderCreatedEvent message,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing order created event for order {OrderId}",
            message.OrderId);
        
        // Send confirmation email command
        await _messageBus.PublishAsync(
            new SendOrderConfirmationCommand(
                message.OrderId,
                await GetCustomerEmailAsync(message.CustomerId, cancellationToken)),
            cancellationToken);
        
        _logger.LogInformation(
            "Order created event processed for order {OrderId}",
            message.OrderId);
    }
    
    private async Task<string> GetCustomerEmailAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        // Get customer email from database
        return "customer@example.com";
    }
}
```

### Pattern 3: Publish Messages
```csharp
public class OrderService
{
    private readonly IRepository<Order> _repository;
    private readonly IMessageBus _messageBus;
    
    public OrderService(
        IRepository<Order> repository,
        IMessageBus messageBus)
    {
        _repository = repository;
        _messageBus = messageBus;
    }
    
    public async Task<Order> CreateOrderAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        // Create order
        var order = new Order
        {
            CustomerId = request.CustomerId,
            TotalAmount = request.Items.Sum(i => i.Price * i.Quantity),
            Status = OrderStatus.Pending
        };
        
        await _repository.AddAsync(order, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        
        // Publish event
        await _messageBus.PublishAsync(
            new OrderCreatedEvent(
                order.Id,
                order.CustomerId,
                order.TotalAmount,
                DateTime.UtcNow),
            cancellationToken);
        
        return order;
    }
}
```

### Pattern 4: Multiple Handlers for Same Event
```csharp
// Handler 1: Send email
public class OrderCreatedEmailHandler : IMessageHandler<OrderCreatedEvent>
{
    public async Task HandleAsync(
        OrderCreatedEvent message,
        CancellationToken cancellationToken)
    {
        // Send confirmation email
        await _emailService.SendOrderConfirmationAsync(
            message.OrderId,
            cancellationToken);
    }
}

// Handler 2: Update inventory
public class OrderCreatedInventoryHandler : IMessageHandler<OrderCreatedEvent>
{
    public async Task HandleAsync(
        OrderCreatedEvent message,
        CancellationToken cancellationToken)
    {
        // Update inventory
        await _inventoryService.ReserveStockAsync(
            message.OrderId,
            cancellationToken);
    }
}

// Handler 3: Send analytics
public class OrderCreatedAnalyticsHandler : IMessageHandler<OrderCreatedEvent>
{
    public async Task HandleAsync(
        OrderCreatedEvent message,
        CancellationToken cancellationToken)
    {
        // Track in analytics
        await _analyticsService.TrackOrderCreatedAsync(
            message,
            cancellationToken);
    }
}

// All three handlers run when OrderCreatedEvent is published
```

### Pattern 5: Domain Events from Entities
```csharp
public class Order : AuditEntity<Guid>, IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = new();
    
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    
    public void PlaceOrder(string placedBy)
    {
        Status = OrderStatus.Placed;
        SetUpdatedBy(placedBy);
        
        // Add domain event
        _domainEvents.Add(new OrderPlacedEvent(Id, CustomerId, TotalAmount));
    }
    
    public void ClearDomainEvents() => _domainEvents.Clear();
}

// Publish domain events after saving
public class OrderService
{
    private readonly IRepository<Order> _repository;
    private readonly IMessageBus _messageBus;
    
    public async Task PlaceOrderAsync(
        Guid orderId,
        string placedBy,
        CancellationToken cancellationToken)
    {
        // Get order
        var order = await _repository.GetByIdAsync(orderId, cancellationToken);
        
        // Place order (adds domain event)
        order.PlaceOrder(placedBy);
        
        // Save changes
        await _repository.SaveChangesAsync(cancellationToken);
        
        // Publish domain events
        foreach (var domainEvent in order.DomainEvents)
        {
            await _messageBus.PublishAsync(domainEvent, cancellationToken);
        }
        
        order.ClearDomainEvents();
    }
}
```

## 🚨 Critical Rules

### 1. Always Use Record Types for Messages
```csharp
// ✅ Good - Immutable record
public record OrderCreatedEvent(Guid OrderId, decimal Total) : IMessage;

// ❌ Bad - Mutable class
public class OrderCreatedEvent : IMessage
{
    public Guid OrderId { get; set; }
    public decimal Total { get; set; }
}
```

### 2. Include CancellationToken in Handlers
```csharp
// ✅ Good
public async Task HandleAsync(
    OrderCreatedEvent message,
    CancellationToken cancellationToken)
{
    await _service.ProcessAsync(message, cancellationToken);
}

// ❌ Bad
public async Task HandleAsync(
    OrderCreatedEvent message,
    CancellationToken cancellationToken)
{
    await _service.ProcessAsync(message); // Missing cancellation
}
```

### 3. Log Message Processing
```csharp
// ✅ Good - Log for observability
public async Task HandleAsync(
    OrderCreatedEvent message,
    CancellationToken cancellationToken)
{
    _logger.LogInformation(
        "Processing OrderCreatedEvent for order {OrderId}",
        message.OrderId);
    
    // Process message
    
    _logger.LogInformation(
        "Completed OrderCreatedEvent for order {OrderId}",
        message.OrderId);
}

// ❌ Bad - No logging
public async Task HandleAsync(
    OrderCreatedEvent message,
    CancellationToken cancellationToken)
{
    // Silent processing - hard to debug
}
```

### 4. Handle Errors Gracefully
```csharp
// ✅ Good - Error handling
public async Task HandleAsync(
    PaymentProcessedEvent message,
    CancellationToken cancellationToken)
{
    try
    {
        await _service.ProcessPaymentAsync(message, cancellationToken);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex,
            "Error processing payment {PaymentId}",
            message.PaymentId);
        
        // Optionally publish failure event
        await _messageBus.PublishAsync(
            new PaymentProcessingFailedEvent(message.PaymentId, ex.Message),
            cancellationToken);
        
        throw; // Re-throw for retry logic
    }
}
```

### 5. Keep Messages Serializable
```csharp
// ✅ Good - Simple, serializable properties
public record OrderCreatedEvent(
    Guid OrderId,
    Guid CustomerId,
    decimal TotalAmount,
    DateTime CreatedAt) : IMessage;

// ❌ Bad - Complex objects that might not serialize
public record OrderCreatedEvent(
    Order Order, // Entire entity!
    DbContext Context) : IMessage; // Not serializable!
```

## 🚫 Common Mistakes

### 1. Forgetting to Register Handlers
```csharp
// ❌ Bad - Handlers not registered
builder.Services.AddSlimBus();
// Missing: builder.Services.AddSlimBusHandlers(...);

// ✅ Good - Auto-register handlers
builder.Services.AddSlimBus();
builder.Services.AddSlimBusHandlers(typeof(Program).Assembly);
```

### 2. Circular Message Dependencies
```csharp
// ❌ Bad - Handler A publishes message that handler B handles,
//          and handler B publishes message that handler A handles
public class HandlerA : IMessageHandler<EventB>
{
    public async Task HandleAsync(EventB msg, CancellationToken ct)
    {
        await _bus.PublishAsync(new EventA(), ct); // Circular!
    }
}

public class HandlerB : IMessageHandler<EventA>
{
    public async Task HandleAsync(EventA msg, CancellationToken ct)
    {
        await _bus.PublishAsync(new EventB(), ct); // Circular!
    }
}

// ✅ Good - Clear message flow
```

### 3. Publishing Too Much Data
```csharp
// ❌ Bad - Entire entity in event
public record OrderCreatedEvent(Order Order) : IMessage;

// ✅ Good - Only necessary data
public record OrderCreatedEvent(
    Guid OrderId,
    Guid CustomerId,
    decimal TotalAmount) : IMessage;
```

## 🔗 Related Skills

- `dknet-overview` - Overall architecture
- `efcore-abstractions` - Domain events in entities
- `efcore-repos` - Publishing events after persistence

---

**When to Use This Skill**: Reference this skill when implementing event-driven architecture, decoupling components with messages, or building reactive systems with publish/subscribe patterns.
