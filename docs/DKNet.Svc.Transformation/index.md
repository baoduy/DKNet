# DKNet.Svc.Transformation

Fills bracketed tokens in a template string — `[Name]`, `{Email}`, `<Amount>`, {% raw %}`{{Ref}}`{% endraw %} — from plain objects or string dictionaries, formatting each resolved value on the way in.

## ✨ Why use it?

- **No `string.Replace` chains.** One `Transform(template, data)` call resolves every token in the template against the objects you pass, in order.
- **The data source can be whatever you already have.** A DTO, an anonymous object, or a collection of objects — property lookup is case-insensitive. An `IDictionary<string, string>` is looked up by key, case-insensitively, wherever it sits: passed directly to `Transform`/`TransformAsync`, inside a collection, or in `GlobalParameters`.
- **Values are formatted, not just stringified.** Numbers, dates, and booleans go through a pluggable `IValueFormatter`, so `true` renders as `Yes` and `1234.5` as `1,234.50` without per-call formatting code.
- **Your template's brackets, not the library's.** Square brackets by default; curly, angled, double-curly, or a custom pair are one option away.

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

## 🏗️ Architecture

![The host application configures AddTransformerService once, which registers TransformOptions and a transient TransformerService; each call reads TokenExtractor instances, TokenResolver and IValueFormatter.](diagrams/components.svg)

`AddTransformerService` returns immediately if `ITransformerService` is already registered, so only the **first** call's options take effect. `TransformerService` is transient, `TransformOptions` is held as a singleton `IOptions<TransformOptions>`. This does not show request scoping in the host app, or that `TokenExtractor`/`TokenResolver` are internal types the service builds for itself.

## 🔄 How it works

![The template string is scanned by one extractor per token definition, producing tokens with a key and an index; TokenResolver looks each key up against the call's parameters and then the global parameters, taking the first non-null value; resolved values go through IValueFormatter and unresolved ones through the not-found policy, and both feed the filled string that is rebuilt in a single pass.](diagrams/svc-transformation-token-resolution.svg)

`Transform` runs extraction, resolution, formatting, and rebuilding synchronously; `TransformAsync` differs only in that extraction runs on the thread pool — resolution itself is always synchronous reflection. This does not show the per-call token cache (one dictionary created fresh inside each `Transform`/`TransformAsync` call, so a token repeated in the same template resolves once and nothing ever leaks between calls), or that the default `TokenNotFoundBehavior` is `ThrowError`.

## 🧩 Features

- [Fill a template from objects or dictionaries](fill-a-template-from-data.md) — resolve tokens against the call's own parameters, then `GlobalParameters`, with the not-found policy for what cannot be resolved.
- [Choose token syntax](choose-token-syntax.md) — pick built-in bracket styles or define a custom token pair.
- [Format resolved values](format-resolved-values.md) — control how bool/number/date values become display text.
- [Register the service](register-the-service.md) — wire up `AddTransformerService` and configure `GlobalParameters`.

## 🔗 Related packages

- [DKNet.Svc.PdfGenerators](../DKNet.Svc.PdfGenerators/index.md) — fill a Markdown or HTML template here, then convert the filled result to PDF.
- [DKNet.Svc.BlobStorage.Abstractions](../DKNet.Svc.BlobStorage.Abstractions/index.md) — store the rendered output, or read the template itself out of blob storage before transforming it.
- [DKNet.Fw.Extensions](../DKNet.Fw.Extensions/index.md) — reach for it for the general-purpose string, type, and reflection helpers used across DKNet.
