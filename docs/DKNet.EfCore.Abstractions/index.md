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

## 🏗️ Architecture

![The domain application derives Entity and AuditedEntity, declares attributes and exposes contracts that runtime consumers read.](diagrams/components.svg)

Abstractions has no service registration or background work. Entity queues belong to each entity; attributes and interfaces are consumed by the host runtime packages. Direct references are EF Core Abstractions, annotations and FluentResults, rather than the EF Core runtime.

## 🔄 How it works

![Order queues a plain event, GetEvents copies the queue, and ClearEvents explicitly empties it.](diagrams/entity-event-queue.svg)

The Quick Start only exercises the in-memory queue. It does not publish or persist anything; use Events and Hooks for dispatch around SaveChangesAsync.

## 🧩 Features

- [Give domain entities an identity](define-entities.md) — Use `Entity<TKey>` for an identity plus event queues, or implement the covariant `IEntity<out TKey>` contract yourself.
- [Record who created and changed an entity](track-audit-fields.md) — Derive from `AuditedEntity<TKey>` or its Guid specialization to store audit identity and timestamps.
- [Expose an optimistic concurrency token](declare-concurrency.md) — Implement `IConcurrencyEntity<TType>` to give infrastructure a consistent `RowVersion` property and setter.
- [Represent a soft deletion in the domain](model-soft-delete.md) — Implement `ISoftDeletableEntity` when deletion records a flag, user and time instead of removing the object.
- [Queue domain events without choosing a transport](queue-domain-events.md) — `Entity<TKey>` implements `IEventEntity` with separate queues for event objects and event types.
- [Declare events for persistence operations](declare-save-events.md) — Apply repeatable, non-inherited `[RaisesEvent]` declarations to an entity for create, update or delete payloads.
- [Describe database sequences with enum metadata](declare-sequences.md) — Put `[SqlSequence]` on an enum and `[Sequence]` on each member that represents a database sequence.
- [Declare audit capture and sensitive properties](declare-audit-policy.md) — Attach policy markers to the domain model and let the appropriate runtime consumer enforce them.
- [Mark domain members for CRUD generation](mark-crud-members.md) — Declare which constructors and methods SlimBus.Generators can turn into request handlers and endpoints.

## 🔗 Related packages

- [DKNet.EfCore.Extensions](../DKNet.EfCore.Extensions/index.md) — reach for it to configure domain contracts in the EF model.
- [DKNet.EfCore.Events](../DKNet.EfCore.Events/index.md) — reach for it to publish queued or declared domain events.
- [DKNet.EfCore.Hooks](../DKNet.EfCore.Hooks/index.md) — reach for it to write a custom save hook.
- [DKNet.EfCore.AuditLogs](../EfCore/DKNet.EfCore.AuditLogs.md) — reach for it for field-level audit capture.
- [DKNet.EfCore.DtoGenerator](../EfCore/DKNet.EfCore.DtoGenerator.md) — reach for it to generate and validate declared event payloads.
