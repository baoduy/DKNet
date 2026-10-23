# Carry property attributes

Part of [DKNet.EfCore.DtoGenerator](index.md). Replace the Quick Start `Product` declaration with this version; also install `DKNet.EfCore.Abstractions` to use `[SensitiveData]`:

```csharp
public sealed class Product
{
    public int Id { get; set; }
    [System.ComponentModel.DataAnnotations.MaxLength(100)]
    public string Name { get; set; } = "";
    [DKNet.EfCore.Abstractions.Attributes.SensitiveData("pricing")]
    public decimal Price { get; set; }
}
```

![DtoGenerator selects each retained property, copies supported attribute arguments, adds required namespaces and emits those attributes on the DTO property.](diagrams/carry-property-attributes.svg)

Attributes in `System.ComponentModel.DataAnnotations` and `DKNet.EfCore.Abstractions.Attributes.SensitiveDataAttribute` are carried with best-effort constructor/named argument serialization. Other namespaces, including JSON and custom validation attributes, are not copied.

## 🧱 Where it fits

[Withhold sensitive properties from unauthorised callers](../DKNet.EfCore.Extensions/filter-sensitive-responses.md#withhold-sensitive-properties-from-unauthorised-callers) describes the opt-in runtime response filter. The generator carries the role declaration; it does not authorize requests. Role-less `[SensitiveData]` is emitted as a bare attribute.

## ⚠️ Gotchas & limits

- A carried `[SensitiveData]` makes the DTO project depend on Abstractions; the analyzer itself does not reference that assembly.
- An excluded property is absent from the DTO, including its attributes.
- Copying a validation attribute does not execute validation. The consumer decides when to validate or filter the DTO.
