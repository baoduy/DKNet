# DKNet.Fw.Extensions

[![NuGet](https://img.shields.io/nuget/v/DKNet.Fw.Extensions)](https://www.nuget.org/packages/DKNet.Fw.Extensions/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DKNet.Fw.Extensions)](https://www.nuget.org/packages/DKNet.Fw.Extensions/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](https://github.com/baoduy/DKNet/blob/main/LICENSE)

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
- **Enum** — `GetAttribute<T>()`, `GetEnumInfo()` / `GetEnumInfos<T>()` for `[Display]`-attribute
  metadata (name, description, group)
- **DateTime** — `InQuarter()`, `LastDayOfMonth()`
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
using DKNet.Fw.Extensions.Primitives;
using DKNet.Fw.Extensions.Reflection;

"Price: $123.45".ExtractDigits();          // "123.45"
typeof(List<string>).IsImplementOf(typeof(IEnumerable<>)); // true
DateTime.Today.InQuarter();                // 1-4
```

## Customisation reference

There is no options object, no `IOptions<T>`, and no startup wiring — every member is a static or extension
method with fixed behaviour. The whole configurable surface is two optional parameters plus the type-scanner
chain:

| Knob | Type | Default | Effect |
|---|---|---|---|
| `GetProperty(propertyName, flags)` — `flags` | `BindingFlags` | `IgnoreCase \| Public \| NonPublic \| Instance` | Which properties the reflection lookup considers. `GetPropertyValue`, `SetPropertyValue`, `TrySetPropertyValue` and `HasAttributeOnProperty` all resolve through this default and do not expose the parameter. |
| `HasAttribute<TAttribute>(inherit)` / `HasAttributeOnProperty<TAttribute>(propertyName, inherit)` — `inherit` | `bool` | `true` | Whether an attribute inherited from a base type or property counts as present. |
| `ITypeExtractor` shape filters | chained calls | no filter | `Classes()`/`NotClass()`, `Enums()`/`NotEnum()`, `Interfaces()`/`NotInterface()`, `Abstract()`/`NotAbstract()`, `Generic()`/`NotGeneric()`, `Nested()`/`NotNested()`, `Publics()`/`NotPublic()`. Additive — each call narrows further. |
| `ITypeExtractor` relationship filters | chained calls | no filter | `IsInstanceOf(Type?)` / `IsInstanceOf<T>()` / `IsInstanceOfAny(params Type[])` / `NotInstanceOf(...)`, `HasAttribute<TAttribute>()` / `HasAttribute(Type)`, and `Where(Expression<Func<Type, bool>>?)` as the escape hatch. |
| `ITypeExtractor` evaluation | — | lazy | Nothing is scanned until you enumerate (`ToList()`, `foreach`, LINQ). Duplicate assemblies are de-duplicated; a null or empty assembly array throws `ArgumentException`. |

## Migration — namespace changes in this release

Root types were grouped into concern folders; the namespace of each moved type now ends
with its folder name. This is an import-only source break: no type was renamed, removed,
resignatured, or had its behaviour changed — update the `using` line and you're done.

| Type | Old namespace | New namespace |
|---|---|---|
| `CollectionExtensions` | `DKNet.Fw.Extensions` | `DKNet.Fw.Extensions.Collections` |
| `StringExtensions`, `DateTimeExtensions` | `DKNet.Fw.Extensions` | `DKNet.Fw.Extensions.Primitives` |
| `AttributeExtensions`, `PropertyExtensions`, `TypeExtensions` | `DKNet.Fw.Extensions` | `DKNet.Fw.Extensions.Reflection` |
| `EnumExtensions`, `EnumInfo` | `DKNet.Fw.Extensions` | `DKNet.Fw.Extensions.Enums` |

One file kept its namespace on purpose — it declares an **ambient namespace** (a
namespace owned by the framework it extends, so its extension methods resolve without an
extra import):

- `ServiceCollectionExtensions` — stays at root, namespace `Microsoft.Extensions.DependencyInjection`.

`TypeExtractors` (fluent assembly scanner) was already grouped before this release and is unchanged.

## Docs

Full feature walkthrough with real, compiling examples:
https://github.com/baoduy/DKNet/blob/main/docs/Core/DKNet.Fw.Extensions.md
