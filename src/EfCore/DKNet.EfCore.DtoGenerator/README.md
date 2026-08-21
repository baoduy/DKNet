# DKNet.EfCore.DtoGenerator

A Roslyn incremental source generator that emits DTO properties from an entity type at compile time. Declare an empty `partial` record, apply `[GenerateDto(typeof(Entity))]`, and the generator mirrors the entity's public readable properties onto it — no hand-written mapping boilerplate, no runtime reflection, and no runtime dependency on this package.

## Install

```xml
<ItemGroup>
  <PackageReference Include="DKNet.EfCore.DtoGenerator" Version="{latest}" PrivateAssets="all" OutputItemType="Analyzer" />
</ItemGroup>
```

## Features

- **`[GenerateDto(typeof(Entity))]`** — generates `init`-only properties for every public instance readable property of the entity, following the entity's inheritance chain.
- **`Exclude` / `Include`** — per-DTO property filtering (mutually exclusive; `Include` takes only the listed properties).
- **`IgnoreComplexType`** — flattens out navigation-style properties by default; per-DTO or project-wide (`DtoGeneratorIgnoreComplexType`) override.
- **Global exclusions** (`DtoGeneratorExclusions` MSBuild property) — exclude the same properties (e.g. audit columns) across every DTO in the project.
- **Validation attribute copy-through** — `System.ComponentModel.DataAnnotations` attributes on entity properties are copied to the generated DTO properties.
- **`[RaisesEvent]` build-time validation** — a second generator in this package validates `DKNet.EfCore.Abstractions.Events.RaisesEventAttribute` declarations against their `[GenerateDto]` payloads, and generates the payload record for the attribute's string form.
- **Zero runtime coupling** — generated DTOs are plain `record`/`class` types with no base type, interface, or attribute left on them.

## Quick start

```csharp
public class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

using DKNet.EfCore.DtoGenerator;

[GenerateDto(typeof(Product))]
public partial record ProductDto;
```

This emits `ProductDto.g.cs` with `Id`, `required string Name`, and `Price` as `init`-only properties.

Full documentation: https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/DKNet.EfCore.DtoGenerator.md
