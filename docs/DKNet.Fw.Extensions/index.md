# DKNet.Fw.Extensions

Reflection, type discovery and small value helpers for .NET developers who need reusable in-process operations.

## ✨ Why use it?

- Find concrete implementations across assemblies with a filter chain you can branch and reuse.
- Read or update object properties by name without repeating lookup and conversion code.
- Build enum labels and inspect registrations with helpers that expose their edge cases.

## 🚀 Quick Start

Use a .NET 10 console project. Install the package, then replace `Program.cs` with the example.

```bash
dotnet new console -f net10.0 -n CoreDemo
cd CoreDemo
dotnet add package DKNet.Fw.Extensions
```

```csharp
using System;
using System.Linq;
using DKNet.Fw.Extensions.TypeExtractors;

var types = typeof(Worker).Assembly.Extract()
    .Classes().NotAbstract().IsInstanceOf<IWorker>().ToList();
Console.WriteLine(types.Contains(typeof(Worker))); // True

public interface IWorker { }
public sealed class Worker : IWorker { }
```

Run with `dotnet run`.

## 🏗️ Architecture

![The caller uses DKNet.Fw.Extensions helpers inside its process, with the .NET APIs and shared metadata they depend on.](diagrams/components.svg)

All helpers run synchronously in the caller's process. Assembly type arrays and property metadata are shared through static concurrent dictionaries; query predicates and object values remain specific to each call or extractor. The project references DI abstractions and component-model annotations; no service registration or background worker is required.

## 🔄 How it works

![Extract creates a lazy type query, filters narrow it, and enumeration retrieves assembly types before returning the matching implementations.](diagrams/fw-extensions-type-extractor.svg)

`Extract` rejects a null or empty assembly array and removes duplicates. Filters build independent predicate chains; enumeration loads or reuses cached assembly types, then evaluates those predicates in order. Type loading and predicate exceptions propagate to the caller.

## 🧩 Features

- [Find types in loaded assemblies](scan-assemblies.md) — Find implementations and branch a reusable query without changing its source.
- [Read and update properties by name](access-properties.md) — Read nested values and convert input before writing a property.
- [Inspect nullable and generic type shapes](inspect-types.md) — Classify runtime types, including nullable values and open generic relationships.
- [Convert numeric values into enums](convert-enums.md) — Turn boxed numbers into enums while handling conversion failures.
- [Read enum display metadata](read-enum-metadata.md) — Build display labels and descriptions from enum field metadata.
- [Check attributes on types and properties](check-attributes.md) — Check metadata presence with null-safe reflection inputs.
- [Extract and inspect numeric-looking text](inspect-numeric-text.md) — Retain numeric-looking characters and understand the limits of heuristic checks.
- [Remove control characters from log text](sanitize-log-text.md) — Remove embedded controls and trim text before logging.
- [Find calendar quarters and month ends](calculate-calendar-dates.md) — Find the quarter or month end while preserving the input time.
- [Append items to an ICollection](add-collection-ranges.md) — Append items when the destination exposes only a collection interface.
- [Inspect service registrations before adding duplicates](inspect-service-registrations.md) — Choose a contract guard or an exact implementation guard.

## 🔗 Related packages

- [DKNet.RandomCreator](../DKNet.RandomCreator/index.md) — reach for it when you need random strings or character buffers.
- [DKNet.EfCore.Extensions](../EfCore/DKNet.EfCore.Extensions.md) — reach for it when assembly discovery should configure an EF Core model.
- [DKNet.Svc.Transformation](../Services/DKNet.Svc.Transformation.md) — reach for it when values must be resolved into a text template.
