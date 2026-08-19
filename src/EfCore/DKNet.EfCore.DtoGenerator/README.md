# DKNet.EfCore.DtoGenerator

## What is DtoGenerator

DKNet.EfCore.DtoGenerator is a lightweight Roslyn Incremental Source Generator that automatically creates immutable
DTO (Data Transfer Object) types from your EF Core entities or any POCO classes at compile time. It eliminates the need
to manually write repetitive DTO classes while maintaining type safety and reducing boilerplate code.

The generator synthesizes matching `public init` properties for every public instance readable property on the entity (
excluding indexers & statics). **It also automatically copies validation attributes** from entity properties to DTO
properties, ensuring that validation rules are consistently applied across your application layers.

When Mapster is available, it uses `TypeAdapter.Adapt` for efficient mapping; otherwise, it falls back to
property-by-property initialization.

## NuGet Package

Add the NuGet package to your project:

```xml
<ItemGroup>
  <PackageReference Include="DKNet.EfCore.DtoGenerator" Version="1.0.0" PrivateAssets="all" OutputItemType="Analyzer" />
</ItemGroup>
```

**Optional but recommended**: Add Mapster for rich mapping capabilities and configuration:

```xml
<ItemGroup>
  <PackageReference Include="Mapster" Version="7.4.0" />
</ItemGroup>
```

## Project Configuration

To enable and configure the source generator, add the following properties to your project file (`.csproj`):

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)Generated</CompilerGeneratedFilesOutputPath>
  <!-- Force analyzer to reload on every build to avoid caching issues -->
  <EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>
</PropertyGroup>
```

These settings enable the generator to emit generated files in the `obj/Generated` directory and ensure the analyzer
runs correctly on every build.

## DTO Declaration

To generate a DTO from an entity, create an empty partial record (recommended) or class/struct and apply the
`[GenerateDto]` attribute:

**Example Entity:**

```csharp
public class MerchantBalance
{
    public Guid Id { get; set; }
    public string MerchantId { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public DateTime LastUpdated { get; set; }
}
```

**DTO Declaration:**

```csharp
using DKNet.EfCore.DtoGenerator;

[GenerateDto(typeof(MerchantBalance))]
public partial record BalanceDto;
```

The generator will automatically create a `BalanceDto.g.cs` file with all properties from `MerchantBalance` and mapping
helper methods.

## Event Declaration

Declaring a domain event is two separate steps, not one combined attribute:

1. Shape the event payload as an ordinary generated DTO via `[GenerateDto]` (a `partial record` shell, same
   as any other DTO — `Include`/`Exclude`/`IgnoreComplexType` all apply).
2. Declare a raise rule on the entity via the repeatable `DKNet.EfCore.Abstractions.Events.RaisesEventAttribute`,
   naming the payload record, the persistence operation(s) that raise it, and — for updates — an optional
   narrowing property list.

```csharp
using DKNet.EfCore.Abstractions.Events;

[GenerateDto(typeof(Product))]
public partial record ProductCreatedEvent;

[GenerateDto(typeof(Product), Exclude = new[] { "InternalCost" })]
public partial record ProductPriceChangedEvent;

[RaisesEvent(typeof(ProductCreatedEvent), EventOperations.Created)]
[RaisesEvent(typeof(ProductPriceChangedEvent), EventOperations.Updated, nameof(Product.Price))]
public class Product
{
    public Guid Id { get; set; }
    public decimal Price { get; set; }
}
```

This generator emits **no code** for `[RaisesEvent]` — the payload is generated entirely from its own
`[GenerateDto]` declaration, exactly as any other DTO. `[RaisesEvent]` is read via reflection at runtime by
`DKNet.EfCore.Events`' save hook — see that package's README for the raise mechanics.

**Update narrowing** (the raise rule's trailing `params string[] properties`): non-empty raises the event only
when at least one listed property changed; empty (default) raises on any change. Entries must name a direct
property of the entity via `nameof(...)` — a nested path (e.g. `nameof-style "Address.Line"`) is a build error
(`DKRAISEVT001`), and narrowing on a rule with no `Updated` flag is a build warning (`DKRAISEVT003`) since it
has no effect at runtime.

**Payload/entity match**: the named event type must be a `[GenerateDto]` payload generated from the SAME entity
carrying the `[RaisesEvent]` rule — naming a payload generated from a different entity, or a type with no
`[GenerateDto]` at all, is a build error (`DKRAISEVT002`).

**Build-time validation** — this generator fails the build (or warns) on:

- `DKRAISEVT001` — narrowing property is a nested path, or not a property of the entity.
- `DKRAISEVT002` — the named event type is generated from a different entity, or carries no `[GenerateDto]`.
- `DKRAISEVT003` (warning) — narrowing set on a rule with no `Updated` flag.
- `DKRAISEVT004` — string-form name already resolves to an existing type; use the type-naming form instead.
- `DKRAISEVT005` — string-form name is not a compile-time constant, or not a single valid C# identifier.
- `DKRAISEVT006` — two entities in the same namespace name the same string-form event; never merged into one record.

### String form — generated payload record with no hand-written `[GenerateDto]`

Naming the raise rule by string skips step 1 above entirely: the build generates the default-shape payload
record for you, in the carrying entity's own namespace.

```csharp
[RaisesEvent("CustomerTouched", EventOperations.Created)]
public class Customer
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

// Optional — extend the generated record from your own file (it is a public partial record):
public partial record CustomerTouched
{
    public string Greeting => $"Hello, {Name}";
}
```

The generated record is identical in shape to `[GenerateDto(typeof(Customer))]` with no `Exclude`/`Include`
and the built-in `IgnoreComplexType` default (`true`) — no payload-shaping knobs are available on the string
form. It carries no `[GenerateDto]` attribute itself and is `public partial record`, so a hand-authored partial
with the same name in the same namespace merges with it rather than colliding (`DKRAISEVT004` only fires for
a genuinely incompatible existing type — a non-partial type, or a type of a different kind). Narrowing
(`DKRAISEVT001`/`DKRAISEVT003`) applies identically to both forms. The type-naming form's syntax and runtime
behaviour are entirely unaffected by the string form.

**Migration note**: adopting this on an existing domain needs no base-class change — any mapped entity may
declare rules, aggregate root or not. A domain project referencing only `DKNet.EfCore.Abstractions` and this
package builds and packs fine with rules declared; reference `DKNet.EfCore.Events` and register an `IMapper`
(e.g. Mapster) alongside `AddEventPublisher` in the application to actually raise them. Existing hand-raised
events (via `AddEvent(...)`) keep firing unchanged and coexist with declared ones as distinct types.

**Nested owned values**: a change confined to a nested `[Owned]` value (e.g. `Customer.Address.Line`) does **not**
raise the owner's update event — EF Core does not report the owner entity itself as `Modified` for an owned-type-only
change. Narrow the rule's properties to the owner's own direct properties only.

**Security note**: like any `[GenerateDto]` payload, a raised event mirrors the entity's properties by default —
sensitive values are included unless `Exclude`d on the payload's `[GenerateDto]` declaration. Review each payload
for fields that shouldn't reach event subscribers.

## Validation Attributes

**NEW:** The generator automatically copies all validation attributes from entity properties to DTO properties. This
ensures consistent validation rules across your application layers without manual duplication.

**Supported Validation Attributes:**

- `[MaxLength]`
- `[StringLength]` (including MinimumLength parameter)
- `[Required]`
- `[Range]`
- `[EmailAddress]`
- `[Url]`
- `[Phone]`
- All other `System.ComponentModel.DataAnnotations` attributes

**Example Entity with Validation:**

```csharp
public class Product
{
    public Guid Id { get; set; }
    
    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string Sku { get; set; } = string.Empty;
    
    [Range(0.01, 999999.99)]
    public decimal Price { get; set; }
    
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
```

**Generated DTO with Copied Attributes:**

```csharp
public partial record ProductDto
{
    public Guid Id { get; init; }
    
    [Required]
    [StringLength(100, MinimumLength = 3)]
    public required string Name { get; init; }
    
    [MaxLength(50)]
    public required string Sku { get; init; }
    
    [Range(0.01, 999999.99)]
    public decimal Price { get; init; }
    
    [EmailAddress]
    public required string Email { get; init; }
}
```

The copied validation attributes work seamlessly with ASP.NET Core model validation, allowing you to validate DTOs using
`ModelState.IsValid` or `Validator.TryValidateObject()`.

### Excluding Properties

You can exclude specific properties from the generated DTO using the `Exclude` parameter:

```csharp
[GenerateDto(typeof(MerchantBalance), Exclude = new[] { "LastUpdated", "Id" })]
public partial record BalanceSummaryDto;
```

### Including Only Specific Properties

Alternatively, you can specify only the properties you want to include using the `Include` parameter. When `Include` is
provided, only those properties will be generated:

```csharp
[GenerateDto(typeof(MerchantBalance), Include = new[] { "MerchantId", "Balance" })]
public partial record BalanceOnlyDto;
```

**Note:** `Include` and `Exclude` are mutually exclusive. If you specify `Include`, the `Exclude` parameter will be
ignored, and a warning will be generated if both are provided.

### Ignoring Complex Types (Entity Navigation Properties)

Navigation properties that link to other entities are excluded from generated DTOs **by default** — `IgnoreComplexType`
is implicitly `true` unless overridden. This gives you a flat DTO shape (primitive and value type properties only)
without repeating anything on every `[GenerateDto]` declaration:

```csharp
// Assuming Customer has Orders (List<Order>) and PrimaryAddress (Address) navigation properties
[GenerateDto(typeof(Customer))]
public partial record CustomerSimpleDto;
// Orders and PrimaryAddress are excluded automatically — no IgnoreComplexType argument needed
```

A navigation property is a reference-type class in the consumer's own code that is not a record and not `[Owned]`.
By default, the generator excludes:

- Single entity properties (e.g., `public Address? PrimaryAddress { get; set; }`)
- Collection properties of entities (e.g., `public List<Order> Orders { get; set; }`)

**Note:** Properties marked with the `[Owned]` attribute (EF Core owned types) are NOT excluded since they're considered
part of the entity, not navigation properties.

**Note:** .NET framework/BCL types (`System.*`, `Microsoft.*` — e.g. `Uri`, `Version`) are never treated as navigation
properties and are always kept in the generated DTO, even though they are non-record reference-type classes.

To include navigation properties for a specific DTO, set `IgnoreComplexType = false`:

```csharp
[GenerateDto(typeof(Customer), IgnoreComplexType = false)]
public partial record CustomerWithNavigationsDto;
// Orders and PrimaryAddress are included
```

You can combine `IgnoreComplexType = false` with `Exclude` to still exclude specific properties:

```csharp
[GenerateDto(typeof(Customer), IgnoreComplexType = false, Exclude = new[] { "Email" })]
public partial record CustomerBasicDto;
// Generated DTO will include Orders, PrimaryAddress but exclude Email
```

To change the default project-wide instead of per-DTO, set the `DtoGeneratorIgnoreComplexType` MSBuild property in
your `.csproj` (consumers get this via the package's `buildTransitive` props):

```xml
<PropertyGroup>
  <DtoGeneratorIgnoreComplexType>false</DtoGeneratorIgnoreComplexType>
</PropertyGroup>
```

Precedence: a per-DTO `IgnoreComplexType` argument always wins over the project-wide property, which in turn wins
over the built-in default of `true`.

However, when you use `Include`, it overrides `IgnoreComplexType`, allowing you to explicitly include navigation
properties if needed:

```csharp
// Orders navigation property will be included even though IgnoreComplexType = true
// because Include parameter takes precedence
[GenerateDto(typeof(Customer), IgnoreComplexType = true, Include = new[] { "CustomerId", "Name", "Orders" })]
public partial record CustomerWithOrdersDto;
```

### Customizing DTOs

You can add custom properties or override generated ones by declaring them in your partial DTO:

```csharp
[GenerateDto(typeof(MerchantBalance))]
public partial record BalanceDto
{
    // Add custom computed property
    public string DisplayBalance => $"${Balance:N2}";
    
    // Override generated property with custom logic
    public new string MerchantId { get; init; } = string.Empty;
}
```

## Copy Generated DTOs to Project Folder

For verification and debugging purposes, you can copy generated DTOs to your project folder using a custom MSBuild
target. Add the following to your project file (`.csproj`):

```xml
<!-- Custom target to copy generated DTOs to project/GeneratedDtos folder, flattening structure and preserving names/extensions -->
<Target Name="CopyGeneratedDtosToOutputFolder" AfterTargets="CoreCompile" Condition="Exists('$(CompilerGeneratedFilesOutputPath)')">
    <ItemGroup>
        <GeneratedDtoFiles Include="$(CompilerGeneratedFilesOutputPath)\**\*Dto.g.cs"/>
    </ItemGroup>
    <MakeDir Directories="$(ProjectDir)GeneratedDtos" Condition="'@(GeneratedDtoFiles)' != ''"/>
    <Copy SourceFiles="@(GeneratedDtoFiles)"
          DestinationFiles="$(ProjectDir)GeneratedDtos\%(Filename)%(Extension)"
          SkipUnchangedFiles="false"
          OverwriteReadOnlyFiles="true"
          Condition="'@(GeneratedDtoFiles)' != ''"/>
    <Message Text="Copied %(Filename)%(Extension) to $(ProjectDir)GeneratedDtos" Importance="high" Condition="'@(GeneratedDtoFiles)' != ''"/>
</Target>

<!-- Exclude generated DTOs from compilation, but keep them visible in Solution Explorer -->
<ItemGroup>
    <Compile Remove="GeneratedDtos\**\*.cs"/>
    <None Include="GeneratedDtos\**\*.cs"/>
</ItemGroup>
```

This MSBuild target will:

- Copy all generated `*Dto.g.cs` files to a `GeneratedDtos` folder in your project
- Exclude these files from compilation to avoid duplicates
- Keep them visible in Solution Explorer for inspection
- Show a message during build indicating which files were copied

## Generated Code Examples

### With Mapster Present

```csharp
public partial record BalanceDto
{
    public System.Guid Id { get; init; }
    
    [MaxLength(100)]
    public string MerchantId { get; init; } = default!;
    
    public decimal Balance { get; init; }
    public System.DateTime LastUpdated { get; init; }

    public static BalanceDto FromEntity(MerchantBalance entity) 
        => Mapster.TypeAdapter.Adapt<BalanceDto>(entity);
    
    public MerchantBalance ToEntity() 
        => Mapster.TypeAdapter.Adapt<MerchantBalance>(this);
    
    public static IEnumerable<BalanceDto> FromEntities(IEnumerable<MerchantBalance> entities) 
        => Mapster.TypeAdapter.Adapt<IEnumerable<BalanceDto>>(entities);
}
```

### Without Mapster (Fallback)

```csharp
public partial record BalanceDto
{
    public System.Guid Id { get; init; }
    
    [MaxLength(100)]
    public string MerchantId { get; init; } = default!;
    
    public decimal Balance { get; init; }
    public System.DateTime LastUpdated { get; init; }

    public static BalanceDto FromEntity(MerchantBalance entity) => new BalanceDto
    {
        Id = entity.Id,
        MerchantId = entity.MerchantId,
        Balance = entity.Balance,
        LastUpdated = entity.LastUpdated
    };

    public MerchantBalance ToEntity() => new MerchantBalance
    {
        Id = this.Id,
        MerchantId = this.MerchantId,
        Balance = this.Balance,
        LastUpdated = this.LastUpdated
    };

    public static IEnumerable<BalanceDto> FromEntities(IEnumerable<MerchantBalance> entities)
    {
        foreach (var e in entities) yield return FromEntity(e);
    }
}
```

Note: All validation attributes from entity properties are automatically copied to DTO properties in the generated code.

## Mapster Configuration

When using Mapster, you can customize mapping behavior with global or type-specific configurations:

```csharp
TypeAdapterConfig<MerchantBalance, BalanceDto>
    .NewConfig()
    .Map(dest => dest.DisplayBalance, src => $"${src.Balance:N2}")
    .Ignore(dest => dest.Id);
```

For EF Core query projections, use Mapster's `.ProjectToType<T>()` extension instead of `FromEntity` to enable
database-side translation:

```csharp
var balances = await dbContext.MerchantBalances
    .ProjectToType<BalanceDto>()
    .ToListAsync();
```

## Additional Notes

- **Navigation Properties**:
    - By default, navigation and collection properties are excluded (`IgnoreComplexType` is implicitly `true`).
    - Use `IgnoreComplexType = false` per DTO, or the `DtoGeneratorIgnoreComplexType` MSBuild property project-wide,
      to include entity navigation properties (both single and collection).
    - Properties marked with `[Owned]` attribute are NOT excluded by `IgnoreComplexType` as they're considered owned
      types, not navigations.
    - .NET framework/BCL types (`System.*`, `Microsoft.*` — e.g. `Uri`, `Version`) are never treated as navigation
      properties and are always kept, regardless of `IgnoreComplexType`.
    - Customize via Mapster configuration or override in partial DTO for more control.
- **Nullable Reference Types**: Non-nullable reference type properties receive a `= default!;` initializer to satisfy
  compiler null-state analysis.
- **Generic Entities**: Limited support for generic entities (non-generic DTO shells only).
- **Diagnostics**: `DKDTOGEN001` warning is reported if generation fails for a target type; build continues.
- **Validation Attributes**: All `System.ComponentModel.DataAnnotations` attributes are automatically copied from entity
  properties to DTO properties, ensuring consistent validation across layers.

## Local Development

Build and pack the source generator:

```bash
# Build
dotnet build -c Release

# Pack
dotnet pack -c Release
```

For local consumption in another project:

```xml
<ItemGroup>
  <ProjectReference Include="..\EfCore\DKNet.EfCore.DtoGenerator\DKNet.EfCore.DtoGenerator.csproj"
                    OutputItemType="Analyzer" />
</ItemGroup>
```

### Cross-Compiler Compatibility (CS9057)

This is a dev-only `netstandard2.0` source generator, so its `.csproj` disables the SDK
code-style / NetAnalyzers from running **on the generator's own build**:

```xml
<PropertyGroup>
  <RunAnalyzers>false</RunAnalyzers>
  <EnforceCodeStyleInBuild>false</EnforceCodeStyleInBuild>
</PropertyGroup>
```

Without these, the project fails to build under a `csc` older than the SDK's analyzers, e.g.:

```
CSC : error CS9057: Analyzer assembly '.../codestyle/cs/Microsoft.CodeAnalysis.CodeStyle.dll'
cannot be used because it references version '5.6.0.0' of the compiler, which is newer than
the currently running version '5.3.0.0'.
```

CS9057 means an analyzer was built against a newer Roslyn than the compiler running the build
(a mixed-SDK environment: e.g. MSBuild from the `10.0.3xx` band feeding `5.6` analyzers into a
`5.3` `csc`). These analyzers add no value to a generator project, so turning them off here
decouples the build from any specific SDK band. Pin the SDK in `global.json` if you want the
band itself to be deterministic.

## Planned Enhancements

- `[DtoIgnore]` attribute to skip specific entity properties
- `[DtoName("...")]` attribute for renaming properties
- Partial method hooks for custom mapping logic
- Optional deep copy of collections and navigation properties
- Multi-targeting for broader analyzer compatibility

---

Happy generating! For more information and complete documentation, visit
the [DKNet Framework Documentation](https://github.com/baoduy/DKNet/tree/dev/docs).
