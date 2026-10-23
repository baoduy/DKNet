# Generate and validate event payloads

Part of [DKNet.EfCore.DtoGenerator](index.md). Install `DKNet.EfCore.Abstractions` alongside the generator. Replace `Program.cs` with this convention-form example:

```csharp
using DKNet.EfCore.Abstractions.Events;

var payload = new ProductTouchedCreatedEvent { Id = 1, Name = "Coffee" };
Console.WriteLine(payload.Name); // Coffee

[RaisesEvent("Touched", EventOperations.Created, Exclude = new[] { nameof(Product.InternalCost) })]
public sealed class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal InternalCost { get; set; }
}
```

![RaisesEventValidator checks event operations, names and payload filters, then either validates a named GenerateDto payload or composes and emits a convention record.](diagrams/generate-event-payloads.svg)

| Form | Declaration | Payload shape owner |
|---|---|---|
| Explicit type | `[RaisesEvent(typeof(ProductCreatedEvent), EventOperations.Created)]` | Your `[GenerateDto(typeof(Product))]` partial payload. |
| Labelled convention | `[RaisesEvent("Touched", EventOperations.Created)]` | Generated `ProductTouchedCreatedEvent`. |
| Label-less convention | `[RaisesEvent(EventOperations.Created)]` | Generated `ProductCreatedEvent`. |

Convention names combine entity name, optional label, sorted narrowing properties, operations in Created/Updated/Deleted order, then `Event`. `EventNameComposer` is compile-linked from Abstractions and shared with the runtime. Convention payloads accept `Include`/`Exclude` and project exclusions but always omit navigation properties. A compatible handwritten partial record without `[GenerateDto]` can extend the emitted record.

## ⚙️ Configuration reference

| Diagnostic | Severity | Meaning |
|---|---|---|
| `DKDTOGEN001` | Warning | A target threw during generation; that target is skipped. |
| `DKDTOGEN002` | Warning | The entity has no eligible properties. |
| `DKDTOGEN003` | Info | Property filtering summary. |
| `DKDTOGEN004` | Warning | Non-empty Include and Exclude conflict; no DTO emitted. |
| `DKDTOGEN005` | Info | Include bypasses global exclusions. |
| `DKRAISEVT001` | Error | A narrowing property is invalid or nested. |
| `DKRAISEVT002` | Error | Named payload lacks GenerateDto or targets a different entity. |
| `DKRAISEVT003` | Warning | Narrowing supplied without Updated; ignored at runtime. |
| `DKRAISEVT004` | Error | Composed name collides with an incompatible type. |
| `DKRAISEVT005` | Error | Label is not a constant or composed identifier is invalid. |
| `DKRAISEVT006` | Error | Different entities in one namespace compose the same name. |
| `DKRAISEVT007` | Error | No operation is declared. |
| `DKRAISEVT008` | Error | Multiple rules on one entity compose the same name. |
| `DKRAISEVT009` | Error | Convention Include and Exclude conflict. |
| `DKRAISEVT010` | Error | Convention payload filter property is invalid or nested. |
| `DKRAISEVT011` | Error | Payload filters supplied on the explicit type form. |

All use category `DKNet.EfCore.DtoGenerator`. Standard MSBuild `EmitCompilerGeneratedFiles` and `CompilerGeneratedFilesOutputPath` can write generated source for inspection; use an intermediate directory such as `obj/Generated`.

## 🧱 Where it fits

The [compiler architecture](index.md#-architecture) separates generation from [runtime event dispatch](../DKNet.EfCore.Events/index.md). The latter needs its own hook, mapper and publisher registration.

## ⚠️ Gotchas & limits

- Filters on the explicit type form are rejected: put them on that payload's `[GenerateDto]` instead.
- A type can compile with warnings while no DTO was emitted; downstream member access can then fail. Read diagnostic severity as well as the build result.
- Narrowing lists only affect Updated event rules. Generated payloads do not dispatch themselves.
