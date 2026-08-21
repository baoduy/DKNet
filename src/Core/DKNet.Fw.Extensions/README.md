# DKNet.Fw.Extensions

[![NuGet](https://img.shields.io/nuget/v/DKNet.Fw.Extensions)](https://www.nuget.org/packages/DKNet.Fw.Extensions/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DKNet.Fw.Extensions)](https://www.nuget.org/packages/DKNet.Fw.Extensions/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](../../../LICENSE)

Dependency-light, framework-agnostic extension methods that the rest of the DKNet suite is built
on: string/type/enum/date helpers, reflection-based property access, attribute checks, a fluent
assembly type scanner, and small DI-registration guards. No configuration, no startup wiring —
just install and use.

## Install

```bash
dotnet add package DKNet.Fw.Extensions
```

## Features

- **String** — `ExtractDigits()`, `IsNumber()`, `IsStringOrValueType()`
- **Type** — `IsImplementOf`, `IsAssignableFrom<T>`/`IsAssignableTo<T>`, `IsNumericType`,
  `IsEnumType`, `GetNonNullableType`, `TryConvertToEnum`
- **Enum** — `GetAttribute<T>()`, `GetEumInfo()` / `GetEumInfos<T>()` for `[Display]`-attribute
  metadata (name, description, group)
- **DateTime** — `InQuarter()`, `LastDayOfMonth()`
- **Async enumerable** — `IAsyncEnumerable<T>.ToListAsync()`
- **Property** — `GetProperty`, `GetPropertyValue` (dotted/nested paths), `SetPropertyValue`,
  `TrySetPropertyValue`
- **Attribute** — `HasAttribute<T>()`, `HasAttributeOnProperty<T>()`
- **Collection** — `AddRange`
- **Service collection** — `IsRegistered<T>()` / `IsRegisteredWithImplementation<T>()` guards,
  `ServiceDescriptor.IsImplementationOf` / `IsKeyedImplementationOf` for keyed services
- **TypeExtractors** — `assembly.Extract().Classes().NotAbstract().IsInstanceOf<T>()...` fluent
  scanning over one or more assemblies

## Quick start

```csharp
using DKNet.Fw.Extensions;

"Price: $123.45".ExtractDigits();          // "123.45"
typeof(List<string>).IsImplementOf(typeof(IEnumerable<>)); // true
DateTime.Today.InQuarter();                // 1-4
```

## Docs

Full feature walkthrough with real, compiling examples:
https://github.com/baoduy/DKNet/blob/dev/docs/Core/DKNet.Fw.Extensions.md
