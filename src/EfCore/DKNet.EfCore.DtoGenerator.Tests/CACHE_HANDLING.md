# Cache Handling for DtoGenerator

## Problem
Source generators can sometimes suffer from caching issues where:
- Old generated files persist after entity changes
- The generator doesn't regenerate files when it should
- Stale DTOs cause compilation errors

## Solutions Implemented

### 1. Automatic Cache Cleaning (On Every Build)
The project now automatically cleans old generated files before each compilation:

```xml
<Target Name="CleanGeneratedDtos" BeforeTargets="CoreCompile">
```

This target runs **before** the compiler and removes:
- All files in `GeneratedDtos\*.cs`
- All files in `$(OutDir)GeneratedDtos\*.cs`

### 2. Force Overwrite
The copy operation now uses:
- `SkipUnchangedFiles="false"` - Always copy even if timestamps match
- `OverwriteReadOnlyFiles="true"` - Overwrite read-only files

This ensures fresh files on every build.

### 3. Enhanced Analyzer Rules
```xml
<EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>
```

Forces the source generator analyzer to reload properly on each build.

## Manual Cache Clearing

If you experience persistent cache issues, run:

```bash
# Option 1: Deep clean using MSBuild target
dotnet build /t:CleanGeneratedFiles

# Option 2: Standard clean + rebuild
dotnet clean
dotnet build --no-incremental

# Option 3: Nuclear option - clean everything
dotnet clean
rm -rf obj/ bin/ GeneratedDtos/
dotnet build --no-incremental
```

## How to Use

### Normal Development
Just build as usual:
```bash
dotnet build
```

The cache handling is automatic!

### After Entity Changes
If you modify entities in `DKNet.EfCore.DtoEntities`:

```bash
# Rebuild to regenerate DTOs
dotnet build --no-incremental
```

### When Things Go Wrong
If you see stale or missing properties:

```bash
# Step 1: Clean everything
dotnet build /t:CleanGeneratedFiles

# Step 2: Rebuild
dotnet build --no-incremental

# Step 3: If still broken, nuclear option
dotnet clean
rm -rf obj/ bin/ GeneratedDtos/
dotnet build
```

## Verifying Generated Files

Generated DTOs are copied to two locations:

1. **Source folder**: `GeneratedDtos/*.cs` (for inspection)
2. **Build output**: `obj/Generated/DKNet.EfCore.DtoGenerator/DKNet.EfCore.DtoGenerator.DtoGenerator/*.cs` (actual generated files)

Always check the `obj/Generated` folder for the **true** source of truth.

## IDE Cache Issues

### JetBrains Rider
If Rider shows stale files:
1. File → Invalidate Caches
2. Restart Rider
3. Rebuild project

### Visual Studio
1. Tools → Options → Text Editor → C# → Advanced
2. Restart Visual Studio
3. Clean and rebuild

## Troubleshooting

### Problem: Properties not showing up in generated DTO
**Solution**: 
```bash
dotnet clean
dotnet build --no-incremental
```

Check `obj/Generated/.../CustomerDto.cs` - that's the real generated file.

### Problem: Namespace errors (CS0234)
**Cause**: The generator couldn't resolve entity types from the referenced project.

**Solution**:
1. Ensure `DKNet.EfCore.DtoEntities.csproj` builds successfully
2. Check that entity classes are `public`
3. Rebuild with `--no-incremental`

### Problem: Duplicate property definitions
**Cause**: Old cached files + new generated files.

**Solution**:
```bash
dotnet build /t:CleanGeneratedFiles
dotnet build
```

## Best Practices

1. **Always use `--no-incremental` after entity changes** - This forces full regeneration
2. **Check `obj/Generated` folder** - This is the source of truth, not `GeneratedDtos`
3. **Don't edit generated files** - They'll be overwritten on next build
4. **Use the CleanGeneratedFiles target** - It's faster than full `dotnet clean`

## Configuration

The cache handling behavior is controlled by:

```xml
<!-- Enable file emission -->
<EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>

<!-- Where to output generated files -->
<CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)Generated</CompilerGeneratedFilesOutputPath>

<!-- Force analyzer reload -->
<EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>
```

These settings are already configured in the `.csproj` file.

