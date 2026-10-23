# Queue domain events without choosing a transport

Part of [DKNet.EfCore.Abstractions](index.md). `Entity<TKey>` implements `IEventEntity` with separate queues for event objects and event types.

Use the console-project setup in the [Quick Start](index.md#-quick-start), then replace `Program.cs` with this example.

```csharp
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.Abstractions.Events;

var order = new Order();
order.Place();
Console.WriteLine(order.GetEvents().Events.Length); // 1
order.ClearEvents();
Console.WriteLine(order.GetEvents().Events.Length); // 0

public sealed class Order : Entity
{
    public void Place() => AddEvent(new OrderPlaced(Id));
}
public sealed record OrderPlaced(Guid OrderId) : EventItem;
```

![Order.Place queues an OrderPlaced instance, GetEvents copies the queue, and ClearEvents explicitly empties it.](diagrams/queue-domain-events.svg)

`AddEvent(object)` records the supplied object. `AddEvent<TEvent>()` records a type to map from the entity later. `GetEvents()` returns array copies and does not drain either queue.

## ⚙️ Configuration reference

There is no DI setup or options object in this package. `EventItem` supplies a case-insensitive `AdditionalData` dictionary, excluded from JSON, and an `EventType` based on the runtime full type name. `IEventItem` is the matching contract.

`IEventPublisher` declares single and batch `PublishAsync` methods with a default cancellation token. `DefaultEventPublisher` implements the batch as a sequential loop over your single-event override.

## 🧱 Where it fits

Domain methods queue events through IEventEntity. Events consumes those queues in the Hooks pipeline and calls the host IEventPublisher adapters; Abstractions has no transport dependency.

![The package component map shows the host, contracts and consumers involved in this capability.](diagrams/components.svg)

## ⚠️ Gotchas & limits

- Queuing does not publish. Register [Events](../DKNet.EfCore.Events/index.md) and its Hooks pipeline in the host.
- The generic overload requires `MapsterMapper.IMapper` at dispatch time; the object overload does not.
- Queues are ordinary collections without synchronization; do not mutate a shared entity concurrently.
