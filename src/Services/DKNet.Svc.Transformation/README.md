# DKNet.Svc.Transformation

Fills bracketed tokens in a template string (`[Name]` by default; `{Email}`, `<Amount>` and `{{Ref}}` are opt-in) from a plain object or string dictionary, with pluggable value formatting.

## ✨ Why use it?

- Resolve every token in a template against one or more parameter sources in a single `Transform`/`TransformAsync` call — no `string.Replace` chains.
- Use whatever data you already have: a DTO, an anonymous object, or a collection of objects. An `IDictionary<string, string>` resolves by key only when passed directly to `Transform`/`TransformAsync`.
- Render numbers, dates, and booleans through a pluggable `IValueFormatter` instead of per-call formatting code.

## 🚀 Quick Start

Use a .NET 10 console project. Install the package, then replace `Program.cs` with the example.

```bash
dotnet new console -f net10.0 -n TransformDemo
cd TransformDemo
dotnet add package DKNet.Svc.Transformation
dotnet add package Microsoft.Extensions.DependencyInjection
```

```csharp
using Microsoft.Extensions.DependencyInjection;
using DKNet.Svc.Transformation;

var services = new ServiceCollection();
services.AddTransformerService(); // ITransformerService, transient
var provider = services.BuildServiceProvider();

var transformer = provider.GetRequiredService<ITransformerService>();
var order = new { OrderId = 4711, Total = 129.5m };
Console.WriteLine(transformer.Transform("Order [OrderId] — [Total]", order));
// Order 4,711 — 129.50
```

Run with `dotnet run`.

## Documentation

| Guide | Read when |
|---|---|
| [Package overview](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Svc.Transformation/index.md) | Start with architecture and the end-to-end flow. |
| [Fill a template from objects or dictionaries](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Svc.Transformation/fill-a-template-from-data.md) | Resolve tokens against the call's own parameters, then `GlobalParameters`, and choose the not-found policy. |
| [Choose token syntax](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Svc.Transformation/choose-token-syntax.md) | Pick built-in bracket styles or define a custom token pair. |
| [Format resolved values](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Svc.Transformation/format-resolved-values.md) | Control how bool/number/date values become display text. |
| [Register the service](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.Svc.Transformation/register-the-service.md) | Wire up `AddTransformerService` and configure `GlobalParameters`. |
