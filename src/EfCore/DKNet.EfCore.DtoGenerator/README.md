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

Use the `IgnoreComplexType` parameter to automatically exclude navigation properties that link to other entities. This
is useful for creating simple DTOs that only contain primitive and value type properties:

```csharp
// Assuming Customer has Orders (List<Order>) and PrimaryAddress (Address) navigation properties
[GenerateDto(typeof(Customer), IgnoreComplexType = true)]
public partial record CustomerSimpleDto;
```

When `IgnoreComplexType` is set to `true`, the generator automatically excludes:

- Single entity properties (e.g., `public Address? PrimaryAddress { get; set; }`)
- Collection properties of entities (e.g., `public List<Order> Orders { get; set; }`)

**Note:** Properties marked with the `[Owned]` attribute (EF Core owned types) are NOT excluded since they're considered
part of the entity, not navigation properties.

You can combine `IgnoreComplexType` with `Exclude` to exclude additional properties:

```csharp
[GenerateDto(typeof(Customer), IgnoreComplexType = true, Exclude = new[] { "Email" })]
public partial record CustomerBasicDto;
// Generated DTO will exclude Orders, PrimaryAddress (complex types) AND Email
```

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
    - By default, navigation and collection properties are included as shallow copies in DTOs.
    - Use `IgnoreComplexType = true` to automatically exclude all entity navigation properties (both single and
      collection).
    - Properties marked with `[Owned]` attribute are NOT excluded by `IgnoreComplexType` as they're considered owned
      types, not navigations.
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

## Planned Enhancements

- `[DtoIgnore]` attribute to skip specific entity properties
- `[DtoName("...")]` attribute for renaming properties
- Partial method hooks for custom mapping logic
- Optional deep copy of collections and navigation properties
- Multi-targeting for broader analyzer compatibility

---

Happy generating! For more information and complete documentation, visit
the [DKNet Framework Documentation](https://github.com/baoduy/DKNet/tree/dev/docs).
