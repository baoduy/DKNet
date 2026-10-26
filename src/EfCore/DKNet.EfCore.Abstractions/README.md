# DKNet.EfCore.Abstractions

Entity identities, event contracts and policy attributes for .NET developers modelling a domain without the EF Core runtime.

## ✨ Why use it?

- Give domain entities a consistent identity and event queue without depending on a transport.
- Declare audit, sequence, CRUD and sensitivity policy next to the model while keeping enforcement in infrastructure.
- Share audit, concurrency and soft-delete contracts across the DKNet persistence packages.

## 🚀 Quick Start

Use a .NET 10 console project. Install the package, then replace `Program.cs` with the example.

```bash
dotnet new console -f net10.0 -n EfCoreDemo
cd EfCoreDemo
dotnet add package DKNet.EfCore.Abstractions
```

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

Run with `dotnet run`.

## Documentation

| Guide | Read when |
|---|---|
| [Package overview](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.Abstractions/index.md) | Start with architecture and the end-to-end flow. |
| [Give domain entities an identity](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.Abstractions/define-entities.md) | Use `Entity<TKey>` for an identity plus event queues, or implement the covariant `IEntity<out TKey>` contract yourself. |
| [Record who created and changed an entity](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.Abstractions/track-audit-fields.md) | Derive from `AuditedEntity<TKey>` or its Guid specialization to store audit identity and timestamps. |
| [Expose an optimistic concurrency token](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.Abstractions/declare-concurrency.md) | Implement `IConcurrencyEntity<TType>` to give infrastructure a consistent `RowVersion` property and setter. |
| [Represent a soft deletion in the domain](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.Abstractions/model-soft-delete.md) | Implement `ISoftDeletableEntity` when deletion records a flag, user and time instead of removing the object. |
| [Queue domain events without choosing a transport](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.Abstractions/queue-domain-events.md) | `Entity<TKey>` implements `IEventEntity` with separate queues for event objects and event types. |
| [Declare events for persistence operations](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.Abstractions/declare-save-events.md) | Apply repeatable, non-inherited `[RaisesEvent]` declarations to an entity for create, update or delete payloads. |
| [Describe database sequences with enum metadata](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.Abstractions/declare-sequences.md) | Put `[SqlSequence]` on an enum and `[Sequence]` on each member that represents a database sequence. |
| [Declare audit capture and sensitive properties](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.Abstractions/declare-audit-policy.md) | Attach policy markers to the domain model and let the appropriate runtime consumer enforce them. |
| [Mark domain members for CRUD generation](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.Abstractions/mark-crud-members.md) | Declare which constructors and methods SlimBus.Generators can turn into request handlers and endpoints. |
