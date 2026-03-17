# DKNet.EfCore.Events — AI Skill File

> **Package**: `DKNet.EfCore.Events`  
> **Minimum Version**: `10.0.0`  
> **Minimum .NET**: `net10.0`  
> **Source**: `src/EfCore/DKNet.EfCore.Events/`

---

## Purpose

Enables entities to raise typed domain events that are automatically published via SlimMessageBus after a successful EF Core `SaveChanges`, ensuring events and data are always consistent (events only fire if the save succeeds).

---

## When To Use

- ✅ Raising domain events from entities when they change state (e.g., `OrderShipped`, `PaymentReceived`)
- ✅ Triggering downstream reactions (notifications, projections, sagas) without coupling the entity to the handler
- ✅ When events must be consistent with the database write (publish only if save succeeds)

## When NOT To Use

- ❌ Audit logging — use `DKNet.EfCore.AuditLogs` for structured change records
- ❌ Fire-and-forget tasks that don't need to be tied to a specific entity save — use `DKNet.AspCore.Tasks` or a background worker
- ❌ External message broker publishing without transactional outbox guarantees — use a full outbox pattern for that

---

## Installation

```bash
dotnet add package DKNet.EfCore.Events
```

---

## Setup / DI Registration

```csharp
// 1. Implement IEventPublisher (or use the built-in SlimBus publisher)
public class SlimBusEventPublisher(IMessageBus bus) : IEventPublisher
{
    public Task PublishAsync(object eventItem, CancellationToken ct = default)
        => bus.Publish(eventItem, cancellationToken: ct);
}

// 2. Register in DI
services.AddEventPublisher<AppDbContext, SlimBusEventPublisher>();
```

---

## Key API Surface

| Type / Method | Role |
|---|---|
| `IHasDomainEvents` | Entity interface — implement to enable event queuing |
| `entity.AddDomainEvent(event)` | Queue an event on the entity (called inside entity methods) |
| `IEventPublisher` | Implement to route events to SlimBus, MassTransit, etc. |
| `services.AddEventPublisher<TDbContext, TPublisher>()` | Register the hook + publisher |

---

## Usage Pattern

```csharp
// ── Domain event ──────────────────────────────────────────────────────────
/// <summary>Raised when an order transitions to Shipped status.</summary>
public record OrderShippedEvent(Guid OrderId, DateTimeOffset ShippedAt);

// ── Entity raises event ───────────────────────────────────────────────────
public class Order : IHasDomainEvents
{
    private readonly List<object> _events = [];
    public IReadOnlyCollection<object> DomainEvents => _events.AsReadOnly();
    public void ClearDomainEvents() => _events.Clear();

    public Guid Id { get; set; }
    public string Status { get; private set; } = "Pending";

    /// <summary>Marks the order as shipped and raises the domain event.</summary>
    public void Ship()
    {
        Status = "Shipped";
        _events.Add(new OrderShippedEvent(Id, DateTimeOffset.UtcNow));
    }
}

// ── Event consumer (SlimBus handler) ─────────────────────────────────────
public class OrderShippedHandler : Fluents.EventsConsumers.IHandler<OrderShippedEvent>
{
    public async Task OnHandle(OrderShippedEvent message, CancellationToken cancellationToken)
    {
        // Send confirmation email, update read model, etc.
        await Task.CompletedTask;
    }
}

// ── Command handler — events publish automatically after SaveChanges ──────
public class ShipOrderHandler(IWriteRepository<Order> repo)
    : Fluents.Requests.IHandler<ShipOrderCommand>
{
    public async Task<IResultBase> Handle(ShipOrderCommand cmd, CancellationToken ct)
    {
        var order = await repo.FindAsync(cmd.OrderId, ct);
        if (order is null) return Results.Fail("Order not found");
        order.Ship();   // ← event queued on entity
        // SaveChanges (auto-save) fires → hook publishes OrderShippedEvent via IEventPublisher
        return Results.Ok();
    }
}
```

---

## Anti-Patterns

```csharp
// ❌ WRONG — publishing events manually inside the command handler before SaveChanges
public async Task<IResultBase> Handle(ShipOrderCommand cmd, CancellationToken ct)
{
    order.Ship();
    await _bus.Publish(new OrderShippedEvent(order.Id, DateTimeOffset.UtcNow));  // ← publishes even if SaveChanges fails!
    await _repo.SaveChangesAsync(ct);
    return Results.Ok();
}

// ✅ CORRECT — let the hook publish after a successful save
public async Task<IResultBase> Handle(ShipOrderCommand cmd, CancellationToken ct)
{
    order.Ship();   // event queued on entity
    // auto-save + event publishing happen automatically after this returns
    return Results.Ok();
}

// ❌ WRONG — using static event bus (breaks testability)
DomainEvents.Raise(new OrderShippedEvent(...));   // ← static/ambient context

// ✅ CORRECT — events are queued on the entity and published via injected IEventPublisher
```

---

## Composes With

| Library | How |
|---|---|
| `DKNet.SlimBus.Extensions` | `SlimBusEventPublisher` routes domain events to SlimBus consumers (`Fluents.EventsConsumers.IHandler<T>`) |
| `DKNet.EfCore.Hooks` | Events library uses the Hooks pipeline internally — do not add a competing post-save hook that interferes with event publishing |

---

## Test Example

```csharp
// Uses TestContainers.MsSql-backed integration test environment.
[Fact]
public async Task Ship_PublishesOrderShippedEvent()
{
    // Arrange
    await using var db = CreateDbContext(_container.GetConnectionString());
    var published = new List<object>();
    var publisher = new CapturingEventPublisher(published);
    services.AddEventPublisher<TestDbContext, CapturingEventPublisher>();

    var order = new Order();
    db.Orders.Add(order);
    await db.SaveChangesAsync();

    // Act
    order.Ship();
    await db.SaveChangesAsync();

    // Assert
    published.OfType<OrderShippedEvent>().ShouldHaveSingleItem();
    published.OfType<OrderShippedEvent>().First().OrderId.ShouldBe(order.Id);
}
```

---

## Quick Decision Guide

- Use domain events for post-save business reactions tied to aggregate state changes.
- Avoid manual event publish inside handlers before persistence succeeds.
- Keep event payloads small and deterministic for downstream consumers.

---

## Version

| Version | Notes |
|---|---|
| `10.0.0` | Initial documentation — `IHasDomainEvents`, `IEventPublisher`, `AddEventPublisher` |

