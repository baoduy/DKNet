# DKNet.Fw.Extensions — AI Skill File

> **Package**: `DKNet.Fw.Extensions`  
> **Minimum Version**: `10.0.0`  
> **Minimum .NET**: `net10.0`

## Purpose
Provides common framework-level extension methods (string/date/enum/type/reflection helpers) used across handlers, specs, and services.

## When To Use
- ✅ Reusable low-level utility logic
- ✅ Type conversion checks and helper methods

## When NOT To Use
- ❌ Business/domain logic specific to one module

## Installation
```bash
dotnet add package DKNet.Fw.Extensions
```

## Setup / DI Registration
No DI setup is required; consume extension methods via `using DKNet.Fw.Extensions;`.

## Key API Surface
- String helpers (number parsing, extraction).
- Date helpers (month/quarter/day calculations).
- Enum/type helpers for metadata and reflection scenarios.

## Usage Pattern
```csharp
using DKNet.Fw.Extensions;

var isNumber = "123.45".IsNumber();
var lastDay = DateTime.UtcNow.LastDayOfMonth();
```

## Anti-Patterns
```csharp
// ❌ Wrong: re-implementing existing extension logic in every project
// ✅ Correct: use the shared extension and unit-test only project-specific behavior
```

## Composes With
- All DKNet modules

## Test Example
```csharp
// Uses TestContainers in integration-heavy modules; this utility example remains unit-focused.
[Fact]
public void IsNumber_ValidDecimal_ReturnsTrue()
{
    "123.45".IsNumber().ShouldBeTrue();
}
```

## Quick Decision Guide
- Use shared extensions for generic utility behavior reused across modules.
- Keep domain-specific rules out of this package.
- Prefer explicit naming and null-safe overloads for extension APIs.

## Version
- `10.0.0`: Initial documentation
