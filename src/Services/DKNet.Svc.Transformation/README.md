# DKNet.Svc.Transformation

[![NuGet](https://img.shields.io/nuget/v/DKNet.Svc.Transformation)](https://www.nuget.org/packages/DKNet.Svc.Transformation/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DKNet.Svc.Transformation)](https://www.nuget.org/packages/DKNet.Svc.Transformation/)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](https://github.com/baoduy/DKNet/blob/main/LICENSE)

Template-token substitution: fills bracketed tokens (`[Name]`, `{Email}`, `<Amount>`, `{{Ref}}`) in a string from
a plain object or a `IDictionary<string, string>`, with pluggable value formatting. Not an object mapper, and not
a template engine — there are no conditionals, loops, or partials.

## Features

- `ITransformerService` — `Transform`/`TransformAsync` a template against one or more parameter sources
- Four built-in token styles (square/curly/angle/double-curly brackets), or define your own pair
- Configurable behaviour when a token cannot be resolved (`ThrowError`, `LeaveAsIs`, `Remove`)
- Pluggable `IValueFormatter` for bool/number/date formatting, invariant-culture by default
- Global parameters shared across every template

## Installation

```bash
dotnet add package DKNet.Svc.Transformation
```

## Quick Start

```csharp
using DKNet.Svc.Transformation;
using Microsoft.Extensions.DependencyInjection;

builder.Services.AddTransformerService(); // ITransformerService, transient

public sealed class WelcomeEmailBuilder(ITransformerService transformer)
{
    public Task<string> BuildAsync(User user) =>
        transformer.TransformAsync("Hello [Name], your account [Email] is ready.", user);
}
```

`AddTransformerService(Action<TransformOptions>? optionFactory = null)` is the only registration entry point.
It builds one `TransformOptions`, registers it as `IOptions<TransformOptions>`, and registers
`ITransformerService → TransformerService` as **transient**. It returns immediately if `ITransformerService` is
already registered, so the first call's options win and a later call's delegate is silently ignored.

## Configuration — `TransformOptions`

Configured in code through the delegate passed to `AddTransformerService`. There is no `IConfiguration` binding
path for this package.

| Option | Type | Default | Effect |
|---|---|---|---|
| `DefaultDefinitions` | `ICollection<ITokenDefinition>` (get-only) | `[SquareBrackets]` | Bracket styles recognised. Add more; the property has no setter, so square brackets stay active unless you `Clear()` the collection first. |
| `Formatter` | `IValueFormatter` | `new ValueFormatter()` | Converts each resolved value to its display string. |
| `GlobalParameters` | `IEnumerable<object>` | `[]` | Fallback resolution sources, tried after the call's own parameters. |
| `TokenNotFoundBehavior` | `TokenNotFoundBehavior` | `ThrowError` | `ThrowError` throws `UnResolvedTokenException`; `LeaveAsIs` keeps the token text; `Remove` substitutes an empty string. |

Built-in definitions, all `static readonly` on `TransformOptions`: `SquareBrackets` (`[Token]`),
`CurlyBrackets` (`{Token}`), `AngledBrackets` (`<Token>`), `DoubleCurlyBrackets` (`{{Token}}`). Define your own
with `new TokenDefinition(begin, end)`, which throws `ArgumentException` for a null or whitespace tag.

`ValueFormatter` is a public class with virtual members — subclass it to change just the formats:

| Property | Type | Default |
|---|---|---|
| `DateFormat` | `string` | `"dd/MM/yyyy hh.mm.ss"` |
| `IntegerFormat` | `string` | `"###,##0"` |
| `NumberFormat` | `string` | `"###,##0.00"` |

`bool` renders as `Yes`/`No`; `int`/`long` use `IntegerFormat`; `double`/`decimal`/`float` use `NumberFormat`;
`DateTime`/`DateTimeOffset` use `DateFormat`; anything else falls back to `ToString()`. All formatting runs
against `CultureInfo.InvariantCulture`.

## Public extension points

| Type | Accessibility | Can you supply your own? |
|---|---|---|
| `ITokenDefinition` / `TokenDefinition` | public interface / public sealed class | **Yes** — add to `DefaultDefinitions`. |
| `IValueFormatter` / `ValueFormatter` | public interface / public class with virtual members | **Yes** — assign `Formatter`. |
| `ITransformerService` | public interface | **Yes** — register your own before calling `AddTransformerService`. |
| `ITokenExtractor`, `ITokenResolver` | public interfaces, `internal sealed` implementations | **No** — `TransformerService` constructs them itself; nothing reads yours. |

## Documentation

Full feature reference, diagrams, and gotchas:
https://github.com/baoduy/DKNet/blob/main/docs/Services/DKNet.Svc.Transformation.md
