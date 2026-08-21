> [!IMPORTANT]
> This page was previously a near-complete fabrication (types like `ICustomConverter`, `TransformationException`,
> and template syntax such as `{amount:currency:USD}` never existed in source). Everything below is re-derived from
> `src/Services/DKNet.Svc.Transformation` on `dev` — treat any older cached copy of this page as wrong.

# DKNet.Svc.Transformation

Template-token substitution: given a template string containing bracketed tokens (`[Name]`, `{Email}`, `<Amount>`,
`{{Ref}}`) and one or more data objects, it resolves each token by reflection or dictionary lookup and formats the
value back into the string. It is **not** an object mapper and has no currency/date "converter" plugin architecture —
formatting is a single pluggable `IValueFormatter`.

## When to reach for it

Use it for filling human-readable templates — email bodies, notification text, generated document placeholders —
from a plain object or dictionary, without hand-writing `string.Replace` chains.

## Install and minimal wiring

```bash
dotnet add package DKNet.Svc.Transformation
```

```csharp
using DKNet.Svc.Transformation;

builder.Services.AddTransformerService(); // ITransformerService, transient
```

```csharp
public sealed class WelcomeEmailBuilder(ITransformerService transformer)
{
    public Task<string> BuildAsync(User user) =>
        transformer.TransformAsync("Hello [Name], your account [Email] is ready.", user);
}
```

## Features

### Transform a template (`ITransformerService`)

```csharp
string Transform(string templateString, params object[] parameters);
Task<string> TransformAsync(string templateString, params object[] parameters);
```

`parameters` can be plain objects (resolved via case-insensitive public-then-non-public property lookup),
`IDictionary<string,string>` instances, or nested `IEnumerable<object>` collections — the resolver tries each
`parameters` entry in order, then falls back to `TransformOptions.GlobalParameters`. Resolved values are cached per
`ITransformerService` instance (a `ConcurrentDictionary`), so repeated tokens in one template resolve once.

```csharp
var result = transformer.Transform("Order [OrderId] for [Customer.Name]", order);
```

### Token syntax — pick your brackets

Four built-in `ITokenDefinition`s, any combination of which can be active at once via
`TransformOptions.DefaultDefinitions`:

| Definition | Syntax |
|---|---|
| `TransformOptions.SquareBrackets` (default) | `[Token]` |
| `TransformOptions.CurlyBrackets` | `{Token}` |
| `TransformOptions.AngledBrackets` | `<Token>` |
| `TransformOptions.DoubleCurlyBrackets` | `{{Token}}` |

```csharp
services.AddTransformerService(options =>
{
    options.DefaultDefinitions.Add(TransformOptions.CurlyBrackets);
    options.DefaultDefinitions.Add(TransformOptions.AngledBrackets);
});
```

Define a custom bracket pair with `new TokenDefinition(begin, end)` (throws `ArgumentException` for a
null/whitespace begin or end tag) if none of the four built-ins fit.

### What happens when a token can't be resolved

```csharp
public enum TokenNotFoundBehavior { LeaveAsIs, Remove, ThrowError } // default: ThrowError
```

```csharp
services.AddTransformerService(options => options.TokenNotFoundBehavior = TokenNotFoundBehavior.LeaveAsIs);
```

`ThrowError` (the default) throws `UnResolvedTokenException` for the first token nothing in `parameters` or
`GlobalParameters` can resolve.

### Value formatting (`IValueFormatter`)

The default `ValueFormatter` converts a resolved value to its display string:

```csharp
virtual string DateFormat { get; }    // "dd/MM/yyyy hh.mm.ss"
virtual string IntegerFormat { get; } // "###,##0"
virtual string NumberFormat { get; }  // "###,##0.00"
```

`bool` becomes `"Yes"`/`"No"`; numeric types use `IntegerFormat`/`NumberFormat`; `DateTime`/`DateTimeOffset` use
`DateFormat`; anything else falls back to `.ToString()`. Subclass `ValueFormatter` and override the format strings,
or implement `IValueFormatter` from scratch, then set `TransformOptions.Formatter`.

### Global parameters

```csharp
services.AddTransformerService(options => options.GlobalParameters = [new { CompanyName = "Acme Corp" } ]);
```

Tokens resolve against every template's own `parameters` first, then fall back to `GlobalParameters` — useful for
values (a company name, a support email) shared across every template your app renders.

## Configuration — `TransformOptions`

| Property | Default | Notes |
|---|---|---|
| `ICollection<ITokenDefinition> DefaultDefinitions` | `[SquareBrackets]` | Add more bracket styles as needed. |
| `IValueFormatter Formatter` | `new ValueFormatter()` | Swap for custom formatting. |
| `IEnumerable<object> GlobalParameters` | `[]` | Shared fallback resolution sources. |
| `TokenNotFoundBehavior TokenNotFoundBehavior` | `ThrowError` | `LeaveAsIs`/`Remove` for lenient rendering. |

Configured entirely in code via the `Action<TransformOptions>` passed to `AddTransformerService` — there is no
`IConfiguration`/`appsettings.json` binding path for this package.

## Composing with other DKNet packages

Pairs naturally with [`DKNet.Svc.PdfGenerators`](./DKNet.Svc.PdfGenerators.md) — transform a Markdown/HTML template
with dynamic data, then convert the result to PDF — and with
[`DKNet.Svc.BlobStorage.*`](./DKNet.Svc.BlobStorage.Abstractions.md) to store the rendered output.

## Gotchas and limits

- Resolution caching is per `ITransformerService` instance and always on — there is no option to disable it (an
  earlier revision of this page claimed a `DisabledLocalCache` option; it does not exist).
- There is no built-in currency/date "converter" registry and no `{token:format:culture}` syntax — formatting is a
  single `IValueFormatter.Convert` call per resolved value. Build your own formatting inside a custom
  `IValueFormatter` if you need per-token format specifiers.
- A dictionary parameter must be `IDictionary<string,string>` — passing a dictionary with non-string values throws
  `ArgumentException`.
- `ITokenExtractor`/`ITokenResolver`/`TokenResult` implementations are `internal` — extend behavior through
  `TransformOptions` (definitions, formatter, not-found behavior), not by implementing these interfaces directly.
