# DKNet.Fw.Extensions

[![NuGet](https://img.shields.io/nuget/v/DKNet.Fw.Extensions)](https://www.nuget.org/packages/DKNet.Fw.Extensions/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DKNet.Fw.Extensions)](https://www.nuget.org/packages/DKNet.Fw.Extensions/)
[![.NET](https://img.shields.io/badge/.NET-9.0-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](../../../LICENSE)

A comprehensive collection of framework-level extensions and utilities for .NET applications. This package provides
essential extension methods for common types, encryption utilities, and type manipulation tools to enhance productivity
and code readability.

## Features

- **String Extensions**: Number validation, digit extraction, and type checking utilities
- **DateTime Extensions**: Quarter calculations, month operations, and date manipulations
- **Enum Extensions**: Display attribute retrieval and enum information extraction
- **Type Extensions**: Generic type checking, value type validation, and reflection utilities
- **Async Extensions**: IAsyncEnumerable utilities for async data processing
- **Property Extensions**: Reflection-based property manipulation and type checking
- **Attribute Extensions**: Custom attribute retrieval and metadata operations
- **Encryption Utilities**: Built-in encryption and hashing capabilities

## Supported Frameworks

- .NET 9.0+

## Installation

Install via NuGet Package Manager:

```bash
dotnet add package DKNet.Fw.Extensions
```

Or via Package Manager Console:

```powershell
Install-Package DKNet.Fw.Extensions
```

## Quick Start

### String Extensions

```csharp
using DKNet.Fw.Extensions;

// Extract digits from mixed content
string text = "Price: $123.45";
string digits = text.ExtractDigits(); // "123.45"

// Check if string is a valid number
bool isValid = "123.45".IsNumber(); // true
```

### DateTime Extensions

```csharp
using DKNet.Fw.Extensions;

var date = DateTime.Now;

// Get last day of current month
DateTime lastDay = date.LastDayOfMonth();

// Get quarter information
int quarter = date.Quarter(); // 1, 2, 3, or 4
```

### Enum Extensions

```csharp
using DKNet.Fw.Extensions;
using System.ComponentModel.DataAnnotations;

public enum Status
{
    [Display(Name = "Active Status")]
    Active,
    
    [Display(Name = "Inactive Status")]
    Inactive
}

var status = Status.Active;
var displayAttr = status.GetAttribute<DisplayAttribute>();
string displayName = displayAttr?.Name ?? status.ToString();
```

### Async Extensions

```csharp
using System.Collections.Generic;

IAsyncEnumerable<int> asyncNumbers = GetAsyncNumbers();
IList<int> numbers = await asyncNumbers.ToListAsync();
```

## Configuration

This package requires no specific configuration. Simply add the using statements for the namespaces you need:

```csharp
using DKNet.Fw.Extensions;           // Core extensions
using System.Collections.Generic;   // Async extensions
```

## API Reference

### StringExtensions

- `ExtractDigits(string)` - Extracts numeric characters from a string
- `IsNumber(string)` - Validates if a string represents a valid number
- `IsStringOrValueType(PropertyInfo)` - Checks if property can store string or value types

### DateTimeExtensions

- `LastDayOfMonth(DateTime)` - Returns the last day of the month
- `Quarter(DateTime)` - Determines the quarter of the year (1-4)

### EnumExtensions

- `GetAttribute<T>(Enum)` - Retrieves custom attributes from enum values
- `GetEnumInfo(Enum)` - Gets comprehensive enum information including display attributes

### TypeExtensions

- `IsStringOrValueType(Type)` - Determines if type is string or value type
- Various generic type checking utilities

### AsyncEnumerableExtensions

- `ToListAsync<T>(IAsyncEnumerable<T>)` - Converts async enumerable to list

## Advanced Usage

### Type Checking with Reflection

```csharp
using DKNet.Fw.Extensions;
using System.Reflection;

public class Example
{
    public string Name { get; set; }
    public int Age { get; set; }
    public DateTime? BirthDate { get; set; }
}

// Check if properties can store simple values
var properties = typeof(Example).GetProperties();
foreach (var prop in properties)
{
    bool canStoreSimpleValue = prop.IsStringOrValueType();
    Console.WriteLine($"{prop.Name}: {canStoreSimpleValue}");
}
```

### Working with Display Attributes

```csharp
using DKNet.Fw.Extensions;
using System.ComponentModel.DataAnnotations;

public enum Priority
{
    [Display(Name = "Low Priority", Description = "Non-urgent items")]
    Low,
    
    [Display(Name = "High Priority", Description = "Urgent items")]
    High
}

// Get detailed enum information
var priority = Priority.High;
var enumInfo = priority.GetEnumInfo();
// Access enumInfo.DisplayName, enumInfo.Description, etc.
```

## Thread Safety

- All extension methods are thread-safe as they are stateless
- Encryption utilities maintain thread safety
- No shared mutable state across method calls

## Performance Considerations

- String operations use modern .NET patterns and are optimized for performance
- Reflection-based operations are cached where possible
- Async operations follow .NET async best practices

## Contributing

See the main [CONTRIBUTING.md](../../../CONTRIBUTING.md) for guidelines on how to contribute to this project.

## License

This project is licensed under the [MIT License](../../../LICENSE).

## Related Packages

- [DKNet.EfCore.Extensions](../EfCore/DKNet.EfCore.Extensions) - Entity Framework Core specific extensions
- [DKNet.SlimBus.Extensions](../SlimBus/DKNet.SlimBus.Extensions) - Messaging extensions for SlimMessageBus

---

Part of the [DKNet Framework](https://github.com/baoduy/DKNet) - A comprehensive .NET framework for building modern,
scalable applications.