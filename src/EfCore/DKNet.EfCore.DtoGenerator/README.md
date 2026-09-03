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
- **`[RaisesEvent]` build-time validation** — a second generator in this package validates `DKNet.EfCore.Abstractions.Events.RaisesEventAttribute` declarations against their `[GenerateDto]` payloads, and generates the payload record for the attribute's convention forms, named by fixed convention (entity name + optional label + narrowing properties + operations + `Event`).
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

## Customisation reference

Everything is compile-time: attribute arguments and MSBuild properties. There is nothing to register at runtime.

`[GenerateDto]` — `AttributeTargets.Class | AttributeTargets.Struct`, `AllowMultiple = false`, not inherited:

| Member | Type | Default | Effect |
|---|---|---|---|
| `entityType` (ctor arg) | `Type` | required | The entity to mirror, as `typeof(Entity)`. |
| `Exclude` | `string[]` | `[]` | Property names to omit. Combined with the project-wide exclusions. Mutually exclusive with `Include`. |
| `Include` | `string[]` | `[]` | When non-empty, **only** these properties are generated — local `Exclude`, the global list and `IgnoreComplexType` are all bypassed. |
| `IgnoreComplexType` | `bool` | unset → project-wide value → `true` | When effectively `true`, navigation-style properties are dropped. |

MSBuild properties, set in the consuming `.csproj`:

| Property | Default | Effect |
|---|---|---|
| `DtoGeneratorExclusions` | unset | Comma/semicolon-separated property names excluded from every DTO in the project, and from `[RaisesEvent]` convention-form payloads. Already exposed to the compiler by this package. |
| `DtoGeneratorIgnoreComplexType` | unset → built-in `true` | Project-wide default for `IgnoreComplexType` when a DTO does not set it. |
| `EmitCompilerGeneratedFiles` / `CompilerGeneratedFilesOutputPath` | unset | Standard Roslyn switches for writing the generated `.g.cs` to disk for inspection. |

Property selection, before any filter: every `public` instance property with a `public` getter and no index
parameters, walked up the entity's base chain and de-duplicated by name.

Build diagnostics: `DKDTOGEN001`–`DKDTOGEN005` from the DTO generator, and `DKRAISEVT001`–`DKRAISEVT011` from the
`[RaisesEvent]` validator. The full table, with severity and meaning for each, is in the docs page below.

Full documentation: https://github.com/baoduy/DKNet/blob/main/docs/EfCore/DKNet.EfCore.DtoGenerator.md
