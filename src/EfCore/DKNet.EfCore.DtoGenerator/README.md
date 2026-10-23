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

## Documentation

| Guide | Read when |
|---|---|
| [Package overview](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.DtoGenerator/index.md) | Start with architecture and the end-to-end flow. |
| [Generate DTO properties](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.DtoGenerator/generate-dtos.md) | Declare a partial DTO shell and extend its generated shape. |
| [Choose DTO properties](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.DtoGenerator/configure-exclusions.md) | Set Include, Exclude and navigation-property defaults. |
| [Carry property attributes](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.DtoGenerator/carry-property-attributes.md) | Preserve validation and sensitive-data declarations. |
| [Generate and validate event payloads](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.DtoGenerator/generate-event-payloads.md) | Use RaisesEvent conventions and read compiler diagnostics. |
