# Global Exclusions Guide for DKNet.EfCore.DtoGenerator

## Overview

Global exclusions allow you to configure a centralized list of property names that should be excluded from all generated DTOs by default. This feature is particularly useful for excluding common audit properties, internal tracking fields, or sensitive data across your entire project.

## Configuration

### Step 1: Add MSBuild Property

In your `.csproj` file, add the `DtoGenerator_GlobalExclusions` property with a comma or semicolon-separated list of property names:

```xml
<PropertyGroup>
  <DtoGenerator_GlobalExclusions>CreatedBy,UpdatedBy,CreatedAt,UpdatedAt</DtoGenerator_GlobalExclusions>
</PropertyGroup>
```

### Step 2: Make Property Visible to Source Generator

Add the `CompilerVisibleProperty` item to expose the property to the source generator:

```xml
<ItemGroup>
  <CompilerVisibleProperty Include="DtoGenerator_GlobalExclusions" />
</ItemGroup>
```

### Complete Example

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)Generated</CompilerGeneratedFilesOutputPath>
    
    <!-- Global exclusions for DTO generator -->
    <DtoGenerator_GlobalExclusions>CreatedBy,UpdatedBy,CreatedAt,UpdatedAt</DtoGenerator_GlobalExclusions>
  </PropertyGroup>
  
  <ItemGroup>
    <CompilerVisibleProperty Include="DtoGenerator_GlobalExclusions" />
  </ItemGroup>
  
  <ItemGroup>
    <PackageReference Include="DKNet.EfCore.DtoGenerator" Version="1.0.0" />
  </ItemGroup>
</Project>
```

## Usage Examples

### Example 1: Basic Usage with Global Exclusions

```csharp
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
<DtoGenerator_GlobalExclusions>CreatedBy,CreatedAt,UpdatedBy,UpdatedAt,LastModifiedBy,LastModifiedOn</DtoGenerator_GlobalExclusions>
```

### Internal Tracking

Exclude internal system fields:

```xml
<DtoGenerator_GlobalExclusions>InternalId,RowVersion,IsDeleted,DeletedAt</DtoGenerator_GlobalExclusions>
```

### Security Sensitive

Exclude sensitive or security-related fields:

```xml
<DtoGenerator_GlobalExclusions>Password,PasswordHash,Salt,SecurityStamp,ConcurrencyToken</DtoGenerator_GlobalExclusions>
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
<DtoGenerator_GlobalExclusions>CreatedBy,UpdatedBy,CreatedAt,UpdatedAt</DtoGenerator_GlobalExclusions>
```

```csharp
[GenerateDto(typeof(Product))]
public partial record ProductDto;

[GenerateDto(typeof(Order))]
public partial record OrderDto;

[GenerateDto(typeof(Customer))]
public partial record CustomerDto;
```

## Troubleshooting

### Global Exclusions Not Applied

1. **Verify MSBuild property**: Check your `.csproj` file
2. **Verify CompilerVisibleProperty**: Ensure it's configured
3. **Clean and rebuild**: Delete `obj` and `bin` folders
4. **Check generated files**: Look in `obj/Generated` folder
5. **Check build output**: Look for DKDTOGEN diagnostics

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

- [DKNet.EfCore.DtoGenerator Main Documentation](./DKNet.EfCore.DtoGenerator.md)
- [Source Generator Documentation](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview)
- [MSBuild Properties](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-properties)
