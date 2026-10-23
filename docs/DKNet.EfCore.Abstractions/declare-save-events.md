# Declare events for persistence operations

Part of [DKNet.EfCore.Abstractions](index.md). Apply repeatable, non-inherited `[RaisesEvent]` declarations to an entity for create, update or delete payloads.

Use the console-project setup in the [Quick Start](index.md#-quick-start), then replace `Program.cs` with this example.

```csharp
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.Abstractions.Events;

Console.WriteLine(EventNameComposer.Compose(nameof(Customer), "Contact",
    [nameof(Customer.Email)], (int)EventOperations.Updated));
// CustomerContactEmailUpdatedEvent

[RaisesEvent("Contact", EventOperations.Updated, nameof(Email),
    Include = new[] { nameof(Id), nameof(Email) })]
public sealed class Customer : Entity
{
    public string Email { get; set; } = "";
}
```

![A RaisesEvent declaration names a payload directly or composes a name that DtoGenerator emits and EventHook resolves at save time.](diagrams/efcore-abstractions-raisesevent-naming.svg)

`EventOperations` is a flags enum: `Created = 1`, `Updated = 2`, `Deleted = 4`. `EventNameComposer` appends entity, optional label, distinct ordinal-sorted properties, operations in Created/Updated/Deleted order, then `Event`.

## ⚙️ Configuration reference

| Form | Example | Payload owner |
|---|---|---|
| Type-naming | `[RaisesEvent(typeof(CustomerPayload), EventOperations.Created)]` | Existing `[GenerateDto(typeof(Customer))]` record. |
| Label | `[RaisesEvent("Contact", EventOperations.Updated)]` | DtoGenerator composes a record name with the label. |
| Label-less | `[RaisesEvent(EventOperations.Created)]` | DtoGenerator composes a record name without a label. |

| Member | Type | Default | Effect |
|---|---|---|---|
| `EventType` / `Label` | `Type?` / `string?` | `null` when not supplied | Select named payload or label segment. |
| `Operations` | `EventOperations` | required | Operations that qualify. |
| `Properties` | `IReadOnlyList<string>` | empty | Any direct change qualifies for Updated; a list narrows it. |
| `Include` / `Exclude` | `string[]` | `[]` | Shape composed payloads; never change their names. |

A nonempty `Include` overrides global `DtoGeneratorExclusions`; navigations are still omitted. Without Include, global exclusions also apply to composed payloads. See [DtoGenerator](../DKNet.EfCore.DtoGenerator/index.md) for build diagnostics and payload generation.

## 🧱 Where it fits

DtoGenerator validates declarations and emits convention payloads at build time. Events reads the same declarations through the save hook at runtime and maps entities to payloads. EventNameComposer is shared as linked source between the two assemblies.

![The package component map shows the host, contracts and consumers involved in this capability.](diagrams/components.svg)

## ⚠️ Gotchas & limits

- The example prints a name using only Abstractions. It generates no payload until you reference DtoGenerator and publishes nothing until Events is registered.
- A string argument is a label segment, not a literal event type name. Revisit declarations when upgrading from the earlier literal-name form.
- Update narrowing checks direct properties only. Owned-value changes alone do not mark the owner Modified.
- Empty operation flags, invalid names, collisions and invalid property names are diagnosed by DtoGenerator. `Include` and `Exclude` cannot both be nonempty, and are invalid on the type-naming form.
