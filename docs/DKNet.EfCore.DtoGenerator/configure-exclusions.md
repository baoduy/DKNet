# Choose DTO properties

Part of [DKNet.EfCore.DtoGenerator](index.md). Using the Quick Start entity, replace the DTO shell with an explicit allow-list and adjust its initializer to omit `Price`:

```csharp
[GenerateDto(typeof(Product), Include = new[] { nameof(Product.Id), nameof(Product.Name) })]
public partial record ProductDto;
```

![Public readable entity properties pass through Include or the combined local and global exclusions, then navigation filtering determines the generated property set.](diagrams/efcore-dtogenerator-property-filters.svg)

For an ordinary `[GenerateDto]` DTO, a non-empty `Include` bypasses global exclusions and navigation filtering. Otherwise local and global exclusion names are combined, and the effective `IgnoreComplexType` selects whether navigation-like properties are omitted.

## ⚙️ Configuration reference

| Option | Type | Default | Effect |
|---|---|---|---|
| `entityType` | `Type` constructor argument | Required | Entity whose properties are inspected. |
| `Include` | `string[]` | Empty | Non-empty allow-list; mutually exclusive with non-empty `Exclude`. |
| `Exclude` | `string[]` | Empty | Local property names removed alongside global names. |
| `IgnoreComplexType` | `bool` attribute argument | Unset, then project value, then `true` | Omit navigation-like properties unless explicitly disabled. |
| `DtoGeneratorExclusions` | MSBuild string | Unset | Comma/semicolon-separated, trimmed names excluded project-wide. |
| `DtoGeneratorIgnoreComplexType` | MSBuild boolean | Unset | Default for DTOs without an explicit attribute value; an unparseable value falls through to `true`. |

Add project defaults inside a `.csproj` property group:

```xml
<PropertyGroup>
  <DtoGeneratorExclusions>CreatedBy,UpdatedBy</DtoGeneratorExclusions>
  <DtoGeneratorIgnoreComplexType>true</DtoGeneratorIgnoreComplexType>
</PropertyGroup>
```

The package exposes both switches to the compiler automatically. See the [Global Exclusions Guide](../EfCore/GLOBAL_EXCLUSIONS_GUIDE.md) for project-wide setup.

## 🧱 Where it fits

![The compiler reads project defaults and per-DTO arguments, then DtoGenerator emits only the selected DTO members.](diagrams/filter-settings.svg)

Defaults are compile-time inputs; there is no DI registration or runtime options object.

## ⚠️ Gotchas & limits

- Non-empty `Include` and `Exclude` together emit `DKDTOGEN004` and skip that DTO entirely. `DKDTOGEN005` reports when an allow-list bypasses a global list.
- Names are matched case-sensitively; use `nameof`. A misspelled ordinary DTO filter name does not receive the convention-event validator's invalid-property error.
- Navigation filtering unwraps arrays, `List<T>`, `IList<T>`, `ICollection<T>` and `IEnumerable<T>` only. User classes are treated as navigation targets unless they are records, marked with an `Owned`/`OwnedAttribute` name, or in `System`/`Microsoft` namespaces. Structs and records remain value-like.
- Convention event payloads omit navigation-like properties (in one of those five shapes, or a navigation class) even if `Include` names them; their rule is stricter than ordinary DTO selection.
- Read-only and set collection navigations (`IReadOnlyCollection<T>`, `IReadOnlyList<T>`, `HashSet<T>`) are not unwrapped. They count as framework types, so they stay in the DTO and in convention event payloads, without an `= []` initializer.
