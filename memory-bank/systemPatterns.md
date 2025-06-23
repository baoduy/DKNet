# Architectural Enforcement Patterns

## Layer Access Rules (ArchUnitNET)
```csharp
// Core layer isolation
ArchRuleDefinition.Types().That().ResideInNamespace("Core")
    .Should().OnlyDependOnTypesThat().ResideInNamespace("Core")

// EfCore layer dependencies
ArchRuleDefinition.Types().That().ResideInNamespace("EfCore")
    .Should().OnlyDependOnTypesThat().ResideInAnyNamespace(
        "EfCore", 
        "Core", 
        "Microsoft.EntityFrameworkCore"
    )

// Service layer boundaries
ArchRuleDefinition.Types().That().ResideInNamespace("Services")
    .Should().NotDependOnAnyTypesThat().ResideInNamespace("EfCore")