# DKNet.Services.Transformation
![.NET Core](https://img.shields.io/badge/.NET-6.0-blue)
![NuGet Version](https://img.shields.io/nuget/v/DKNet.Services.Transformation)

A .NET transformation engine for complex data processing scenarios.

## Project Overview

The `DKNet.Services.Transformation` project provides a robust framework for:
- Data format conversion (currency, date-time, boolean)
- Token extraction and resolution patterns
- Dynamic template processing
- Type-safe transformation pipelines

Key features:
✅ Chainable transformation workflows  
✅ Extensible converter architecture  
✅ Comprehensive error handling  
✅ Test coverage over 90%  

## Directory Structure

```
Services/DKNet.Services.Transformation/
├── Converters/
│   ├── ValueFormatter.cs          # Core conversion logic
│   └── CurrencyConverter.cs       # Specific currency handling
├── TokenHandlers/
│   ├── TokenExtractor.cs          # Pattern-based token extraction
│   ├── TokenResolver.cs           # Data source integration
│   └── TokenResultValidator.cs    # Validation pipelines
└── Services.Transform.Tests/      # xUnit test suite
    ├── ConversionTests/
    └── TokenHandlingTests/
```

## Key Components

### Core Converters
**ValueFormatter (`Converters/ValueFormatter.cs`)**
```csharp
// Format numeric value with culture-specific rules
var formatted = new ValueFormatter().Format(
    value: 12345.67m, 
    format: "C", 
    culture: CultureInfo.CreateSpecificCulture("en-US")
);
// Returns "$12,345.67"
```

**CurrencyConverter (`Converters/CurrencyConverter.cs`)**
```csharp
// Handle currency conversions with automatic rounding
var converter = new CurrencyConverter();
var result = converter.Convert(135.99m, "JPY", 2);
// Returns ¥136 (using banker's rounding)
```

### Token Handling System
**TokenExtractor (`TokenHandlers/TokenExtractor.cs`)**
```csharp
// Extract tokens with format validation
var extractor = new TokenExtractor(
    pattern: @"\{(?<token>[a-zA-Z0-9_.:-]+)\}"
);
var tokens = extractor.Extract("Order #{order.id}: {item.price:USD}");
// Returns ["order.id", "item.price:USD"]
```

**TokenResolver (`TokenHandlers/TokenResolver.cs`)**
```csharp
// Resolve tokens from data source
var resolver = new TokenResolver(new Dictionary<string, object> {
    {"user.email", "test@example.com"},
    {"transaction.date", DateTime.Now}
});
var resolved = resolver.Resolve("{user.email}");
// Returns "test@example.com"
```

**TokenResultValidator (`TokenHandlers/TokenResultValidator.cs`)**
```csharp
// Validate token syntax and availability
var validator = new TokenResultValidator();
var validationResult = validator.Validate(
    new TokenResult("user.profile.age"),
    new ValidationContext { MaxDepth = 3 }
);
// Returns IsValid=true when token structure is valid
```

## Getting Started

### Requirements
- .NET 6 SDK
- NuGet package manager

### Installation
```bash
dotnet add package DKNet.Services.Transformation
```

Test categories:
- **Unit Tests**: Isolated component validation
- **Integration Tests**: End-to-end transformation workflows
- **Performance Tests**: Benchmark critical paths

## Contribution Guidelines

1. Fork the repository
2. Create feature branch (`git checkout -b feature/improvement`)
3. Add tests for new functionality
4. Submit PR with:
   - Description of changes
   - Updated documentation
   - Evidence of passing tests

⚠️ All contributions must maintain 100% test coverage on modified code.

---

*Documentation version: 2.2 | Last updated: 2025-02-05*