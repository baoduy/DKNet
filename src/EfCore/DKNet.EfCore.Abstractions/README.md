# DKNet.EfCore.Abstractions

[![NuGet](https://img.shields.io/nuget/v/DKNet.EfCore.Abstractions)](https://www.nuget.org/packages/DKNet.EfCore.Abstractions/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DKNet.EfCore.Abstractions)](https://www.nuget.org/packages/DKNet.EfCore.Abstractions/)

Persistence-technology-agnostic base classes, interfaces, and attributes shared by every DKNet EF Core package —
entity identity, audit tracking, domain events, concurrency, and cross-cutting attributes for audit-log and
sequence behavior. It has no dependency on `Microsoft.EntityFrameworkCore` itself, so domain projects can model
entities without pulling in the full EF Core runtime.

## Install

```bash
dotnet add package DKNet.EfCore.Abstractions
```

## Features

- `IEntity<TKey>` / `Entity<TKey>` / `Entity` — entity identity base classes (Guid-keyed `Entity` by default)
- `IEventEntity` — per-entity domain event queue (`AddEvent`, `AddEvent<TEvent>()`, `GetEvents`, `ClearEvents`),
  implemented by `Entity<TKey>`
- `[RaisesEvent]` / `EventOperations` — declare events raised automatically on Created/Updated/Deleted, with
  optional property narrowing
- `IAuditedProperties` / `IAuditedEntity<TKey>` / `AuditedEntity<TKey>` / `AuditedEntity` — creation/modification
  tracking with `SetCreatedBy`/`SetUpdatedBy`
- `IConcurrencyEntity<TType>` — optimistic concurrency via a `RowVersion` token
- `ISoftDeletableEntity` — soft-delete contract (`IsDeleted`, `DeletedOn`, `DeletedBy`, `Delete(...)`)
- `[Sequence]` / `[SqlSequence]` — database-generated sequential values for fields and enum-backed SQL sequences
- `[AuditLog]` / `[IgnoreAuditLog]` / `[SensitiveDataAttribute]` — audit-log opt-in and redaction markers (consumed
  by `DKNet.EfCore.AuditLogs`)
- `[IgnoreEntity]` — marker to exclude a class from automatic entity mapping
- `IEventPublisher` / `DefaultEventPublisher` / `IEventItem` / `EventItem` — the event-publishing contract consumed
  by `DKNet.EfCore.Events`

## Quick start

```csharp
using DKNet.EfCore.Abstractions.Entities;

public class Order : AuditedEntity // Guid-keyed, audit-tracked, event-capable
{
    private Order() { }

    public static Order Create(string createdBy)
    {
        var order = new Order();
        order.SetCreatedBy(createdBy);
        order.AddEvent(new OrderCreatedEvent(order.Id));
        return order;
    }

    public string Status { get; private set; } = "Pending";
}

public record OrderCreatedEvent(Guid OrderId);
```

Full feature walkthrough, configuration defaults, and how this package composes with
`DKNet.EfCore.Events`/`Hooks`/`AuditLogs`/`DataAuthorization`/`Encryption`/`DtoGenerator`:
[docs/EfCore/DKNet.EfCore.Abstractions.md](https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/DKNet.EfCore.Abstractions.md)
