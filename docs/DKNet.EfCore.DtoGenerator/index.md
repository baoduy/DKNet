# DKNet.EfCore.DtoGenerator

Generate DTO property declarations and validate event payloads at compile time for .NET developers.

## ✨ Why use it?

- Keep DTO property lists aligned with entities and inherited properties.
- Apply local and project-wide property selection without repeating lists on every DTO.
- Validate RaisesEvent declarations and generate convention-named payload records during compilation.

## 🚀 Quick Start

Use a .NET 10 console project. Install the packages, then replace `Program.cs` with the example.

```bash
dotnet new console -f net10.0 -n EfCoreDemo
cd EfCoreDemo
dotnet add package DKNet.EfCore.DtoGenerator
```

```csharp
using DKNet.EfCore.DtoGenerator;

var dto = new ProductDto { Id = 1, Name = "Coffee", Price = 12m };
Console.WriteLine($"{dto.Name}: {dto.Price}"); // Coffee: 12

public sealed class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
}
[GenerateDto(typeof(Product))]
public partial record ProductDto;
```

Run with `dotnet run`.

## 🏗️ Architecture

![The consuming compiler runs DtoGenerator and RaisesEventValidator with entity symbols, project analyzer options and the shared event name composer, emitting partial DTO and event source.](diagrams/components.svg)

The generator is a `netstandard2.0` analyzer running in the compiler process. The package compiles `GenerateDtoAttribute` source into the consuming assembly and exposes its MSBuild switches through `buildTransitive` props; generated types have no generator runtime service.

## 🔄 How it works

![GenerateDto resolves the entity and partial shell, selects readable properties and emits init-only members, while conflicting filters skip that target with a diagnostic.](diagrams/generation-flow.svg)

The source generator emits data properties, not mapping methods. Non-nullable strings become `required`; non-nullable collections receive an empty collection initializer, and retained complex references receive a null-forgiving initializer. Mapping remains the caller's responsibility.

## 🧩 Features

- [Generate DTO properties](generate-dtos.md) — Declare a partial DTO shell and extend its generated shape.
- [Choose DTO properties](configure-exclusions.md) — Set Include, Exclude and navigation-property defaults.
- [Carry property attributes](carry-property-attributes.md) — Preserve validation and sensitive-data declarations.
- [Generate and validate event payloads](generate-event-payloads.md) — Use RaisesEvent conventions and read compiler diagnostics.

## 🔗 Related packages

- [DKNet.EfCore.Abstractions](../DKNet.EfCore.Abstractions/index.md) — use it for entity, SensitiveData and RaisesEvent declarations.
- [DKNet.EfCore.Events](../DKNet.EfCore.Events/index.md) — use it to dispatch the declared events after saving.
- [DKNet.EfCore.Specifications](../DKNet.EfCore.Specifications/index.md) — use it to project queries into generated models.
- [Global Exclusions Guide](../EfCore/GLOBAL_EXCLUSIONS_GUIDE.md) — use it when configuring exclusions across a project.
