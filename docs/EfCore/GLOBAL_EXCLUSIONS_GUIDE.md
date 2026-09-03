# Global Exclusions Guide for DKNet.EfCore.DtoGenerator

## Overview

Global exclusions allow you to configure a centralized list of property names that should be excluded from all generated DTOs by default. This feature is particularly useful for excluding common audit properties, internal tracking fields, or sensitive data across your entire project.

## Configuration

### Add the MSBuild property

In your `.csproj` file, add the `DtoGeneratorExclusions` property with a comma- or semicolon-separated list of
property names. Names are trimmed, so whitespace around a separator is fine, and matching is **case-sensitive**
(the generator collects them into a `HashSet<string>` with the default ordinal comparer):

```xml
<PropertyGroup>
  <DtoGeneratorExclusions>CreatedBy,UpdatedBy,CreatedAt,UpdatedAt</DtoGeneratorExclusions>
</PropertyGroup>
```

That's it — no `CompilerVisibleProperty` item is needed in the consuming project. `DKNet.EfCore.DtoGenerator` ships its own `.props` file that already declares `DtoGeneratorExclusions` (and `DtoGeneratorIgnoreComplexType`) as `CompilerVisibleProperty`, and that `.props` file flows transitively to every project referencing the package.

### Complete Example

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)Generated</CompilerGeneratedFilesOutputPath>

    <!-- Global exclusions for DTO generator -->
    <DtoGeneratorExclusions>CreatedBy,UpdatedBy,CreatedAt,UpdatedAt</DtoGeneratorExclusions>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="DKNet.EfCore.DtoGenerator" Version="*" /> <!-- use the current version -->
  </ItemGroup>
</Project>
```

## Usage Examples

### Example 1: Basic Usage with Global Exclusions

```csharp
using DKNet.EfCore.DtoGenerator;

// Entity with audit properties
public class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public DateTime CreatedAt { get; set; }      // Globally excluded
    public string CreatedBy { get; set; }        // Globally excluded
    public DateTime UpdatedAt { get; set; }      // Globally excluded
    public string UpdatedBy { get; set; }        // Globally excluded
}

// DTO automatically excludes global properties
[GenerateDto(typeof(Product))]
public partial record ProductDto;

// Generated DTO will only include: Id, Name, Price
```

### Example 2: Combining Global and Local Exclusions

```csharp
using DKNet.EfCore.DtoGenerator;

public class Order
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; }
    public decimal Amount { get; set; }
    public string InternalNotes { get; set; }
    public DateTime CreatedAt { get; set; }      // Globally excluded
    public string CreatedBy { get; set; }        // Globally excluded
}

// Combine global exclusions with local exclusions
[GenerateDto(typeof(Order), Exclude = [nameof(Order.InternalNotes)])]
public partial record OrderDto;

// Generated DTO will exclude: CreatedAt, CreatedBy (global) + InternalNotes (local)
// Result: Id, OrderNumber, Amount
```

### Example 3: Include Overrides Global Exclusions

```csharp
using DKNet.EfCore.DtoGenerator;

// Include specific properties, ignoring global exclusions
[GenerateDto(typeof(Product), Include = [
    nameof(Product.Id),
    nameof(Product.Name),
    nameof(Product.CreatedAt)  // Explicitly included despite being globally excluded
])]
public partial record ProductSummaryDto;

// Generated DTO will only include: Id, Name, CreatedAt
```

## Behavior Matrix

| Scenario | Local Exclude | Local Include | Global Exclusions | Result |
|----------|---------------|---------------|-------------------|--------|
| 1 | None | None | Applied | Global exclusions applied |
| 2 | Specified | None | Applied | Global + Local exclusions combined |
| 3 | None | Specified | **Ignored** | Only Include list properties |
| 4 | Specified | Specified | **Error** | Cannot use both Include and Exclude |

### `[RaisesEvent]` convention-form payloads

The list is not limited to hand-written `[GenerateDto]` records. A `[RaisesEvent]` **convention form** — the label
and label-less constructors, which compose their payload record's name instead of naming an existing one — has its
generated payload narrowed by `DtoGeneratorExclusions` exactly like the table above:

```csharp
// <DtoGeneratorExclusions>CreatedBy,UpdatedBy,CreatedAt,UpdatedAt</DtoGeneratorExclusions>

using DKNet.EfCore.Abstractions.Events;

[RaisesEvent(EventOperations.Created)]
public class Customer { /* ... */ }
// CustomerCreatedEvent is generated without CreatedBy, UpdatedBy, CreatedAt, UpdatedAt
```

The same precedence applies: the declaration's own `Exclude` combines with the global list, and a non-empty
`Include` on the declaration replaces both. `Exclude`/`Include` on the **type-naming** form is a build error
(`DKRAISEVT011`) — that form's payload record owns its own shape through its own `[GenerateDto]` filters, which
are then subject to the matrix above.

## Diagnostics

The generator provides helpful diagnostics:

### DKDTOGEN005: Include parameter ignores global exclusions

When you use the `Include` parameter with global exclusions configured, you'll receive an informational diagnostic:

```
Info DKDTOGEN005: DTO ProductSummaryDto: Using Include parameter ignores the 4 global exclusion(s). Only specified properties will be included.
```

This is informational only and doesn't indicate an error. It reminds you that the `Include` parameter takes precedence over global exclusions.

## Common Use Cases

### Audit Properties

Exclude standard audit fields across all DTOs:

```xml
<DtoGeneratorExclusions>CreatedBy,CreatedAt,UpdatedBy,UpdatedAt,LastModifiedBy,LastModifiedOn</DtoGeneratorExclusions>
```

### Internal Tracking

Exclude internal system fields:

```xml
<DtoGeneratorExclusions>InternalId,RowVersion,IsDeleted,DeletedAt</DtoGeneratorExclusions>
```

### Security Sensitive

Exclude sensitive or security-related fields:

```xml
<DtoGeneratorExclusions>Password,PasswordHash,Salt,SecurityStamp,ConcurrencyToken</DtoGeneratorExclusions>
```

## Benefits

1. **Reduced Boilerplate**: No need to specify common exclusions on every DTO
2. **Consistency**: Ensures audit/internal properties are consistently excluded
3. **Maintainability**: Change exclusions in one place
4. **Flexibility**: Override with `Include` parameter when needed
5. **Type Safety**: Compile-time enforcement ensures properties don't leak

## Migration Guide

### Migrating from Per-Entity Exclusions

**Before** (without global exclusions):
```csharp
using DKNet.EfCore.DtoGenerator;

[GenerateDto(typeof(Product), Exclude = ["CreatedBy", "UpdatedBy", "CreatedAt", "UpdatedAt"])]
public partial record ProductDto;

[GenerateDto(typeof(Order), Exclude = ["CreatedBy", "UpdatedBy", "CreatedAt", "UpdatedAt"])]
public partial record OrderDto;

[GenerateDto(typeof(Customer), Exclude = ["CreatedBy", "UpdatedBy", "CreatedAt", "UpdatedAt"])]
public partial record CustomerDto;
```

**After** (with global exclusions):
```xml
<!-- In .csproj -->
<DtoGeneratorExclusions>CreatedBy,UpdatedBy,CreatedAt,UpdatedAt</DtoGeneratorExclusions>
```

```csharp
using DKNet.EfCore.DtoGenerator;

[GenerateDto(typeof(Product))]
public partial record ProductDto;

[GenerateDto(typeof(Order))]
public partial record OrderDto;

[GenerateDto(typeof(Customer))]
public partial record CustomerDto;
```

## Troubleshooting

### Global Exclusions Not Applied

1. **Verify MSBuild property**: Check your `.csproj` file uses `DtoGeneratorExclusions` (not an older/renamed variant)
2. **Clean and rebuild**: Delete `obj` and `bin` folders
3. **Check generated files**: Look in `obj/Generated` folder
4. **Check build output**: Look for DKDTOGEN diagnostics

### Unexpected Properties in DTO

If a globally excluded property appears in a DTO:

1. Check if the DTO uses `Include` parameter (overrides global exclusions)
2. Verify property name matches exactly (case-sensitive)
3. Ensure the property exists in the global exclusion list

## Best Practices

1. **Start Small**: Begin with common audit properties
2. **Document Decisions**: Comment why certain properties are globally excluded
3. **Review Regularly**: Periodically review if global exclusions still make sense
4. **Use Include Sparingly**: Only override global exclusions when truly necessary
5. **Test Thoroughly**: Verify DTOs don't expose sensitive data

## Performance Considerations

Global exclusions are processed at **compile-time**, not runtime:
- ✅ No performance impact at runtime
- ✅ Same performance as local exclusions
- ✅ Results in smaller DTO classes
- ✅ Reduces generated code size

## Related Documentation

- [DKNet.EfCore.DtoGenerator](./DKNet.EfCore.DtoGenerator.md) — the package's full reference
- [EF Core packages index](./README.md) — where this page sits in the family
- [Source Generator Documentation](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview)
- [MSBuild Properties](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-properties)
