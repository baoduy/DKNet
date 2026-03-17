# DKNet.EfCore.DtoGenerator — AI Skill File

> **Package**: `DKNet.EfCore.DtoGenerator`  
> **Minimum Version**: `10.0.0`  
> **Minimum .NET**: `net10.0`

## Purpose
Generates DTO types at compile time from entities/POCOs to remove repetitive mapping boilerplate.

## When To Use
- ✅ Creating DTOs that mirror entity properties
- ✅ Keeping validation attributes aligned between entity and DTO

## When NOT To Use
- ❌ Complex DTO composition across multiple aggregates; hand-write those DTOs

## Installation
```bash
dotnet add package DKNet.EfCore.DtoGenerator
```

## Setup / DI Registration
```xml
<ItemGroup>
  <PackageReference Include="DKNet.EfCore.DtoGenerator" Version="*" PrivateAssets="all" OutputItemType="Analyzer" />
</ItemGroup>
```

```csharp
[GenerateDto(typeof(Product))]
public partial record ProductDto;
```

## Key API Surface
- `[GenerateDto(typeof(TEntity))]` marks DTO declarations for generation.
- Generated partial DTOs include property projection helpers at compile time.

## Usage Pattern
```csharp
[GenerateDto(typeof(Product))]
public partial record ProductDto;

// Use generated DTO in query handler projection paths.
```

## Anti-Patterns
```csharp
// ❌ Wrong: manually copy 40+ properties for simple mirror DTOs
// ✅ Correct: [GenerateDto(typeof(Product))] partial record ProductDto;
```

## Composes With
- `DKNet.EfCore.Repos` (project to generated DTO)
- `DKNet.SlimBus.Extensions` (return generated DTO from query handlers)

## Test Example
```csharp
// Uses TestContainers.MsSql-backed integration tests in consuming modules; this snippet validates generated DTO shape.
[Fact]
public void GeneratedDto_HasExpectedProperty()
{
    var dto = new ProductDto { Name = "Widget" };
    dto.Name.ShouldBe("Widget");
}
```

## Quick Decision Guide
- Use generator output for simple 1:1 entity DTOs.
- Hand-write DTOs for aggregate/composite projections.
- Keep generated DTO declarations partial and minimal.

## Version
- `10.0.0`: Initial documentation
