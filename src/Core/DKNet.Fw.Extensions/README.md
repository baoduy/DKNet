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

## Documentation

| Guide | Read when |
|---|---|
| [Package overview](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Fw.Extensions/index.md) | Start with architecture and the end-to-end flow. |
| [Find types in loaded assemblies](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Fw.Extensions/scan-assemblies.md) | Find implementations and branch a reusable query without changing its source. |
| [Read and update properties by name](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Fw.Extensions/access-properties.md) | Read nested values and convert input before writing a property. |
| [Inspect nullable and generic type shapes](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Fw.Extensions/inspect-types.md) | Classify runtime types, including nullable values and open generic relationships. |
| [Convert numeric values into enums](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Fw.Extensions/convert-enums.md) | Turn boxed numbers into enums while handling conversion failures. |
| [Read enum display metadata](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Fw.Extensions/read-enum-metadata.md) | Build display labels and descriptions from enum field metadata. |
| [Check attributes on types and properties](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Fw.Extensions/check-attributes.md) | Check metadata presence with null-safe reflection inputs. |
| [Extract and inspect numeric-looking text](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Fw.Extensions/inspect-numeric-text.md) | Retain numeric-looking characters and understand the limits of heuristic checks. |
| [Remove control characters from log text](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Fw.Extensions/sanitize-log-text.md) | Remove embedded controls and trim text before logging. |
| [Find calendar quarters and month ends](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Fw.Extensions/calculate-calendar-dates.md) | Find the quarter or month end while preserving the input time. |
| [Append items to an ICollection](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Fw.Extensions/add-collection-ranges.md) | Append items when the destination exposes only a collection interface. |
| [Inspect service registrations before adding duplicates](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Fw.Extensions/inspect-service-registrations.md) | Choose a contract guard or an exact implementation guard. |
