# Generate DTO properties

Part of [DKNet.EfCore.DtoGenerator](index.md). The [Quick Start](index.md#-quick-start) declares a public partial record. Replace its `ProductDto` shell to keep one property under your control:

```csharp
[GenerateDto(typeof(Product))]
public partial record ProductDto
{
    public string Name { get; init; } = "fallback";
}
```

![DtoGenerator walks readable entity properties through the base chain, removes static members and indexers, and emits selected properties except names already declared by the DTO.](diagrams/generate-dtos.svg)

Candidate properties must be public instance properties with public getters and no index parameters. Base-type properties are included and de-duplicated by name. A `partial class` or `partial record struct` also receives a matching partial declaration.

## 🧱 Where it fits

The [compiler component diagram](index.md#-architecture) shows the analyzer boundary. `GenerateDtoAttribute` is internal source compiled into your project; `GlobalDtoConfiguration` is a documentation marker inside the analyzer, not a runtime configuration API.

## ⚠️ Gotchas & limits

- Use a public, partial, non-generic DTO shell. Generic or nested declaration shapes are not generated as matching generic/nested wrappers.
- Handwritten properties win by name; the generator does not validate that your replacement matches the entity type.
- Generated hint names are the simple DTO name plus `.g.cs`. Avoid two DTO shells with the same simple name, even in different namespaces.
- No `FromEntity`, `ToEntity` or mapping helpers are generated. A DTO initializer must satisfy generated `required` string members.
