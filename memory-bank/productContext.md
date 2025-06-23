# WIXO.FW Technical Vision

## Core Architectural Tenets
1. Layered domain-driven design
2. Strict separation between Core/EfCore/Service layers
3. Contract-first API development
4. Automated architectural governance

## Architectural Testing Strategy
- **Tools**: ArchUnitNET for structural validation
- **Patterns to Enforce**:
  - Layer access restrictions
  - Namespace containment rules
  - Inheritance constraints
  - DI configuration validations

## Technology Standards
```csharp
// Example ArchUnitNET rule placeholder
ArchRuleDefinition.Types().That().ResideInNamespace("Core")
    .Should().OnlyDependOnTypesThat().ResideInNamespace("Core")