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

## Customisation reference

No options object and no DI registration — the customisation surface is the attributes and the interfaces.

`[RaisesEvent]` (`AttributeTargets.Class`, repeatable, not inherited) — three constructor forms:
`(Type eventType, EventOperations operations, params string[] properties)`,
`(string label, EventOperations operations, params string[] properties)`, and
`(EventOperations operations, params string[] properties)`.

| Member | Type | Default | Effect |
|---|---|---|---|
| `EventType` | `Type?` | `null` | Type-naming form only; must be a `[GenerateDto]` record generated from the same entity. |
| `Label` | `string?` | `null` | Label form only; one segment of the composed name. Never affects the payload's shape. |
| `Operations` | `EventOperations` | required | `Created = 1`, `Updated = 2`, `Deleted = 4`, combinable. `0` is a build error. |
| `Properties` | trailing `params string[]` | empty | Narrows an `Updated` rule to the listed direct properties. Empty means any change qualifies. |
| `Exclude` | `string[]` | `[]` | Convention forms only; drops properties from the composed payload. Mutually exclusive with `Include`. |
| `Include` | `string[]` | `[]` | Convention forms only; when non-empty it is the whole truth for the payload shape and overrides `DtoGeneratorExclusions`. |

Convention-form names are composed in a fixed order: entity name, label (when given), narrowing properties
(de-duplicated, ordinal-sorted), operations in the order Created, Updated, Deleted, then the suffix `Event`.

`[Sequence]` (`AttributeTargets.Field` — on an enum member):

| Member | Type | Default | Effect |
|---|---|---|---|
| `Type` (ctor arg) | `Type` | `typeof(int)` | Only `byte`, `short`, `int`, `long`; anything else throws `NotSupportedException`. |
| `Cyclic` | `bool` | `true` | Wrap back to the minimum after the maximum. |
| `StartAt` / `IncrementsBy` / `Min` / `Max` | `long` / `int` / `long` / `long` | `-1` | Applied only when greater than zero; otherwise the database default stands. |
| `FormatString` | `string?` | `null` | Used by `NextSeqValueWithFormat`. `{1}` is the value; the literal token `DateTime` becomes `{0}` bound to `DateTime.UtcNow`. |

`[SqlSequence]` (`AttributeTargets.Enum`): `Schema` (ctor arg) — `string`, default `"seq"`.

Marker attributes with no properties: `[AuditLog]` (class or property), `[IgnoreAuditLog]` (class or property),
`[SensitiveData]` (property), `[IgnoreEntity]` (class).

CRUD vertical-slice markers, consumed by `DKNet.SlimBus.Generators`: `[CrudCreate]` and `[CrudUpdate]` each expose
`Name` (`string?`, default `null`); `[CrudAction]` adds `route` (ctor arg, `string?`, default `null` → the
kebab-cased method name) and `Verb` (`CrudActionVerb`, default `Post`; `Post`/`Put`/`Patch`, no `Delete`).

Full feature walkthrough, configuration defaults, and how this package composes with
`DKNet.EfCore.Events`/`Hooks`/`AuditLogs`/`DataAuthorization`/`Encryption`/`DtoGenerator`:
[docs/EfCore/DKNet.EfCore.Abstractions.md](https://github.com/baoduy/DKNet/blob/main/docs/EfCore/DKNet.EfCore.Abstractions.md)
