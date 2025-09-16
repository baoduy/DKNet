# DKNet.Svc.Transformation

A flexible .NET library for template-based data transformation, designed for APIs, DDD, and EF Core scenarios. Supports
custom token extraction, dynamic value resolution, and is fully configurable via dependency injection.

## Features

- Transform template strings using data objects or custom token factories
- Extensible token extractors for different template formats
- Configurable value formatting and caching
- Designed for dependency injection
- .NET 9.0 compatible

## Installation

Add the NuGet package to your project:

```
dotnet add package DKNet.Svc.Transformation
```

## Usage

Register the service in your DI container:

```csharp
using Microsoft.Extensions.DependencyInjection;
using DKNet.Svc.Transformation;

var services = new ServiceCollection();
services.AddTransformerService(options =>
{
    // Customize TransformOptions if needed
    options.DisabledLocalCache = false;
});
```

Transform a template string:

```csharp
var transformer = services.BuildServiceProvider().GetRequiredService<ITransformerService>();

string template = "Hello [Name]. Your {Email} had been [ApprovedStatus]";
var result = await transformer.TransformAsync(template, new { Name = "Duy", Email = "drunkcoding@outlook.net", ApprovedStatus = "Approved" });
// result: "Hello Duy. Your drunkcoding@outlook.net had been Approved"
```

Or use a custom token factory:

```csharp
var result = await transformer.TransformAsync(template, async token =>
{
    // Custom logic to resolve token value
    return await GetValueForTokenAsync(token);
});
```

## API

- `ITransformerService.TransformAsync(string templateString, params object[] parameters)`: Transform using provided data
  objects.
- `ITransformerService.TransformAsync(string templateString, Func<IToken, Task<object>> tokenFactory)`: Transform using
  a custom token resolver.
- `TransformOptions`: Configure extractors, formatters, caching, and global parameters.
- `TransformSetup.AddTransformerService(IServiceCollection, Action<TransformOptions>?)`: Register the service with DI.

## License

MIT © 2026 drunkcoding

## Repository

[https://github.com/baoduy/DKNet](https://github.com/baoduy/DKNet)

## Contributing

Pull requests and issues are welcome!

