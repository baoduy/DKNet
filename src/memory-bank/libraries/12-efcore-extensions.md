# DKNet.EfCore.Extensions — AI Skill File

> **Package**: `DKNet.EfCore.Extensions`  
> **Minimum Version**: `10.0.0`  
> **Minimum .NET**: `net10.0`

## Purpose
Provides EF Core setup helpers (auto model config, global filters, snapshot/change helpers) so DbContext configuration is centralized and consistent.

## When To Use
- ✅ Auto-register entity configurations from assemblies
- ✅ Apply global query filters (soft delete, tenant filter)
- ✅ Utility extensions for metadata and pagination helpers

## When NOT To Use
- ❌ Replacing business-query logic; use `DKNet.EfCore.Specifications` for that

## Installation
```bash
dotnet add package DKNet.EfCore.Extensions
```

## Setup / DI Registration
```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.UseAutoConfigModel<AppDbContext>(cfg =>
    {
        cfg.AddAssembly(typeof(Product).Assembly);
    });

    base.OnModelCreating(modelBuilder);
}
```

## Key API Surface
- `UseAutoConfigModel<TDbContext>(...)` for assembly-scanned entity configuration.
- Global filter and model helper extensions for centralized EF behavior.

## Usage Pattern
```csharp
// Keep DbContext model wiring centralized and deterministic.
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.UseAutoConfigModel<AppDbContext>(cfg => cfg.AddAssembly(typeof(Product).Assembly));
    base.OnModelCreating(modelBuilder);
}
```

## Anti-Patterns
```csharp
// ❌ Wrong: per-entity manual mapping spread across files + duplicated conventions
// ✅ Correct: centralize via UseAutoConfigModel + explicit IEntityTypeConfiguration classes
```

## Composes With
- `DKNet.EfCore.Specifications`
- `DKNet.EfCore.DataAuthorization`
- `DKNet.EfCore.AuditLogs`

## Test Example
```csharp
// Uses TestContainers.MsSql-backed integration test environment when verifying model behavior.
[Fact]
public void OnModelCreating_UsesAutoConfigModel_AppliesEntityConfiguration()
{
    var model = new TestDbContext(_options).Model;
    model.FindEntityType(typeof(Product)).ShouldNotBeNull();
}
```

## Quick Decision Guide
- Use this package to centralize model configuration and shared EF conventions.
- Keep business query rules in Specifications rather than DbContext extension helpers.
- Pair with DataAuthorization/AuditLogs when global behaviors are required.

## Version
- `10.0.0`: Initial documentation
