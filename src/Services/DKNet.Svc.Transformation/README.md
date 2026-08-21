# DKNet.Svc.Transformation

[![NuGet](https://img.shields.io/nuget/v/DKNet.Svc.Transformation)](https://www.nuget.org/packages/DKNet.Svc.Transformation/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DKNet.Svc.Transformation)](https://www.nuget.org/packages/DKNet.Svc.Transformation/)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](../../../../LICENSE)

Template-token substitution: fills bracketed tokens (`[Name]`, `{Email}`, `<Amount>`, `{{Ref}}`) in a string from a
plain object or dictionary, with pluggable value formatting. Not an object mapper.

## Features

- `ITransformerService` — `Transform`/`TransformAsync` a template against one or more parameter sources
- Four built-in token styles (square/curly/angle/double-curly brackets), or define your own
- Configurable behavior when a token can't be resolved (`ThrowError`, `LeaveAsIs`, `Remove`)
- Pluggable `IValueFormatter` for bool/number/date formatting
- Global parameters shared across every template

## Installation

```bash
dotnet add package DKNet.Svc.Transformation
```

## Quick Start

```csharp
using DKNet.Svc.Transformation;

builder.Services.AddTransformerService();

public sealed class WelcomeEmailBuilder(ITransformerService transformer)
{
    public Task<string> BuildAsync(User user) =>
        transformer.TransformAsync("Hello [Name], your account [Email] is ready.", user);
}
```

## Documentation

Full feature reference, configuration, and gotchas:
https://github.com/baoduy/DKNet/blob/dev/docs/Services/DKNet.Svc.Transformation.md
