# DKNet.EfCore.DtoGenerator

A lightweight Roslyn Incremental Source Generator that creates immutable DTO types from your EF Core (or any POCO)
entity classes.

Apply the `[GenerateDto(typeof(EntityType))]` attribute to an **empty partial record (recommended)** (or class / struct)
and the generator will:

- Synthesize matching `public init` properties for every public instance readable property on the entity (excluding
  indexers & statics)
- Generate mapping helpers: `FromEntity`, `ToEntity`, and `FromEntities`
- If the consuming project references **Mapster**, those helpers use `TypeAdapter.Adapt` for mapping
- If Mapster is **not** referenced, a manual property-by-property initializer fallback is emitted
- Preserve any properties you manually declare (you can override or add custom ones)

## Package Contents

- `GenerateDtoAttribute` – marks a DTO shell for generation
- `DtoGenerator` – incremental source generator emitting `*.g.cs` files at compile time

## Quick Start

1. Add the NuGet package (once published):

```xml
<ItemGroup>
  <PackageReference Include="DKNet.EfCore.DtoGenerator" Version="1.0.0" PrivateAssets="all" OutputItemType="Analyzer" />
</ItemGroup>
```

2. (Optional but recommended) Add Mapster for rich mapping & configuration:

```xml
<ItemGroup>
  <PackageReference Include="Mapster" Version="7.4.0" />
</ItemGroup>
```

3. Define your entity:

```csharp
public class Person
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public DateOnly? BirthDate { get; set; }
}
```

4. Add an empty partial record with the attribute:

```csharp
using DKNet.EfCore.DtoGenerator;

[GenerateDto(typeof(Person))]
public partial record PersonDto; // Properties & mapping helpers are auto-generated
```

5. Build the project. A generated `PersonDto.g.cs` (visible under `obj/Generated` when
   `EmitCompilerGeneratedFiles` is enabled) will appear similar to:

### With Mapster Present
```csharp
public partial record PersonDto
{
    public System.Guid Id { get; init; }
    public string Name { get; init; } = default!;
    public int Age { get; init; }
    public System.DateOnly? BirthDate { get; init; }

    public static PersonDto FromEntity(Person entity) => Mapster.TypeAdapter.Adapt<PersonDto>(entity);
    public Person ToEntity() => Mapster.TypeAdapter.Adapt<Person>(this);
    public static IEnumerable<PersonDto> FromEntities(IEnumerable<Person> entities) => Mapster.TypeAdapter.Adapt<IEnumerable<PersonDto>>(entities);
}
```

### Without Mapster Present (Fallback)
```csharp
public partial record PersonDto
{
    public System.Guid Id { get; init; }
    public string Name { get; init; } = default!;
    public int Age { get; init; }
    public System.DateOnly? BirthDate { get; init; }

    public static PersonDto FromEntity(Person entity) => new PersonDto
    {
        Id = entity.Id,
        Name = entity.Name,
        Age = entity.Age,
        BirthDate = entity.BirthDate
    };

    public Person ToEntity() => new Person
    {
        Id = this.Id,
        Name = this.Name,
        Age = this.Age,
        BirthDate = this.BirthDate
    };

    public static IEnumerable<PersonDto> FromEntities(IEnumerable<Person> entities)
    {
        foreach (var e in entities) yield return FromEntity(e);
    }
}
```

## Overriding / Customizing Properties
If you declare a property with the same name inside your partial DTO shell, the generator will **not** emit a duplicate.
This lets you override types, add logic, or augment the DTO.

```csharp
[GenerateDto(typeof(Person))]
public partial record PersonDto
{
    public string DisplayName => $"{Name} ({Age})"; // Extra computed property
}
```

## Mapster Configuration
Because generation uses `TypeAdapter.Adapt<T>`, any global or scoped Mapster configuration you define will automatically
apply:

```csharp
TypeAdapterConfig<Person, PersonDto>
    .NewConfig()
    .Ignore(dest => dest.Age); // Example
```

If you need per-DTO customization without affecting entity mappings, you can introduce partial methods in a future
extension (see planned enhancements) or manually override properties in the DTO shell.

## Project Configuration Tips
To inspect generated code during development in a consuming project:

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)Generated</CompilerGeneratedFilesOutputPath>
</PropertyGroup>
```

## IQueryable Projection
For EF Core queries:
```csharp
var projected = db.People.Select(p => PersonDto.FromEntity(p));
```
When Mapster is present, EF Core may not translate `TypeAdapter.Adapt` inside an expression tree. For translation-safe
queries, use the fallback initializer pattern or Mapster's `.ProjectToType<PersonDto>()` extension (Mapster EF Core
package) instead of `FromEntity` inside queryable contexts.

## Edge Cases / Notes
- Navigation / collection properties are included (shallow copy). Customize via Mapster config or make them nullable & override.
- Non-nullable reference type properties receive a `= default!;` initializer to satisfy compiler flow analysis.
- Generic entity support is limited to non-generic DTO shells (matching generic arity would require additional logic).

## Diagnostics
`DKDTOGEN001` is reported as a warning if generation fails for a target type; build continues.

## Local Development (This Project)
```bash
# Build
DOTNET_CLI_TELEMETRY_OPTOUT=1 dotnet build -c Release
# Pack
DOTNET_CLI_TELEMETRY_OPTOUT=1 dotnet pack -c Release
```

Consume locally:
```xml
<ItemGroup>
  <ProjectReference Include="..\EfCore\DKNet.EfCore.DtoGenerator\DKNet.EfCore.DtoGenerator.csproj"
                    OutputItemType="Analyzer" />
</ItemGroup>
```

## Versioning / Compatibility
Target framework: `net9.0`. For analyzer distribution best-practice, consider multi-targeting `netstandard2.0` to remove RS1041 warnings.

## Planned Enhancements
- `[DtoIgnore]` attribute to skip specific entity properties
- `[DtoName("...")]` attribute for renaming
- Partial method hooks `OnAfterFromEntity(ref TEntity entity)` / `OnAfterToEntity(ref TEntity dto)`
- Optional deep copy of collections / navigation properties
- Multi-targeting for analyzer compatibility (netstandard2.0)

## License
(Provide license details here.)

---
Happy generating! Please report issues or ideas in the project repository.
