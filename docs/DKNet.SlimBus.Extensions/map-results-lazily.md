# Map results lazily

Part of [DKNet.SlimBus.Extensions](index.md). `ILazyMap<T>` and the `LazyMapExtensions.LazyMap<T>` /
`ResultOf<T>` extensions on Mapster's `IMapper` defer a mapping until the result value is actually read, so a caller
that never inspects `Value` never pays for the mapping. Both extensions need an `IMapper` (Mapster). This package
references `Mapster` but not `Mapster.DependencyInjection`, so `MapsterMapper.ServiceMapper` — the implementation
that reads a registered `TypeAdapterConfig` from DI — is not available unless the host also adds that package;
construct an `IMapper` directly instead. Use a .NET 10 console project, then replace `Program.cs` with this example:

```bash
dotnet new console -f net10.0 -n LazyMapDemo
cd LazyMapDemo
dotnet add package DKNet.SlimBus.Extensions
```

```csharp
using DKNet.SlimBus.Extensions.LazyMapper;
using FluentResults;
using Mapster;
using MapsterMapper;

IMapper mapper = new Mapper(new TypeAdapterConfig());
var product = new Product(Guid.NewGuid(), "Lazy", 2m);

IResult<ProductDto> lazyResult = mapper.ResultOf<ProductDto>(product);
Console.WriteLine($"ResultOf<T> before read: value not computed yet. IsSuccess={lazyResult.IsSuccess}");
Console.WriteLine($"ResultOf<T> on read: {lazyResult.Value}");

ILazyMap<ProductDto> lazyNull = mapper.LazyMap<ProductDto>(null!);
Console.WriteLine($"LazyMap<T> over null -> ValueOrDefault={lazyNull.ValueOrDefault}");
try
{
    _ = lazyNull.Value;
}
catch (InvalidOperationException)
{
    Console.WriteLine("LazyMap<T>.Value over null -> threw InvalidOperationException, as documented.");
}

public sealed class Product
{
    public Product(Guid id, string name, decimal price)
    {
        Id = id;
        Name = name;
        Price = price;
    }

    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public sealed record ProductDto(Guid Id, string Name);
```

Run with `dotnet run`. `ProductDto` and `Product` share the `Id` and `Name` property names, so Mapster's convention
mapping needs no configuration for this example.

![A handler wraps a source value with mapper.ResultOf<T> or mapper.LazyMap<T>, returning an IResult<T> / ILazyMap<T> whose mapping has not run yet; the mapping (or a same-type reuse) happens only when Value or ValueOrDefault is first read, and a null source either throws from Value or returns default from ValueOrDefault.](diagrams/map-results-lazily.svg)

## ⚙️ Configuration reference

| Member | Behaviour |
|---|---|
| `Value` | Throws `InvalidOperationException` when the underlying source value was `null`. |
| `ValueOrDefault` | Returns `default(T)` when the underlying source value was `null`; otherwise same as `Value`. |
| Already a `T` | When the source value is already an instance of `T`, that same instance is returned — no mapping call. |
| Caching | The mapped value is computed once and cached; a second read of `Value` / `ValueOrDefault` does not re-map. |

## 🧱 Where it fits

Used from inside a [write](send-commands.md) or [query](run-queries.md) handler that returns a DTO mapped from an
entity — the mapping defers past auto-save, so it runs only if and when the caller reads the value.

## ⚠️ Gotchas & limits

- **`LazyMap<T>` and `LazyResult<T>` are `internal`.** You create instances through the `IMapper` extension methods
  (`LazyMap<T>` / `ResultOf<T>`), never by naming the implementation types.
- **Reading `Value` on a lazily-mapped `null` source throws.** Use `ValueOrDefault` when a missing source is a valid
  outcome rather than a bug.
- **The mapping still needs a valid Mapster configuration for `T`.** Deferring the call does not remove the
  requirement that `IMapper` knows how to map the source type to `T`; an unmapped type throws when the value is
  first read, not when the handler returns.
