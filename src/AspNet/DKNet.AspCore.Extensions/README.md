# DKNet.AspCore.Extensions

[![NuGet](https://img.shields.io/nuget/v/DKNet.AspCore.Extensions.svg)](https://www.nuget.org/packages/DKNet.AspCore.Extensions/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

The minimal-API glue for DKNet-based ASP.NET Core web APIs: claim-backed request binding,
versioned endpoint-group discovery, one-line SlimMessageBus mapping helpers, a shared paging
envelope, and FluentResults-to-`IResult`/`ProblemDetails` conversion.

## Installation

```bash
dotnet add package DKNet.AspCore.Extensions
```

## Features

- **Contextual request binding** — `[FromClaim]` (and any custom `IContextualSource`) populates
  a request property from the authenticated caller before validation and before the handler
  runs, so it can never be forged through the request body or querystring; automatically excluded
  from the published OpenAPI description.
- **Endpoint group discovery** — implement `IEndpointConfig` per feature area and let
  `UseEndpointConfigs()` discover, version, tag, and authorize every group across your assemblies.
- **Fluent minimal-API mappers** — `MapPost`/`MapPut`/`MapPatch`/`MapDelete`/`MapGet`/`MapGetPage`
  wire a verb straight onto a SlimMessageBus fluent command/query, plus generic `MapGetById`/
  `MapGetList` backed by `DKNet.EfCore.Specifications`.
- **`PagedResponse<T>`** — a shared paging envelope (`PageNumber`, `PageSize`, `PageCount`,
  `TotalItemCount`, `Items`, `HasNextPage`, `HasPreviousPage`) used by every paged endpoint.
- **Result/ProblemDetails conversion** — `Response()`/`Response<T>()` turn a `FluentResults`
  outcome into the right minimal-API `IResult`; `ToProblemDetails()` does the same for a
  `ModelStateDictionary`.

## Quick Start

Register the pieces you need and let `UseEndpointConfigs` do the mapping:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthorization();
builder.Services.AddApiVersioning();
builder.Services.AddContextualRequestPopulation(); // powers [FromClaim] and friends

var app = builder.Build();
app.UseEndpointConfigs(); // discovers every IEndpointConfig across the loaded assemblies
app.Run();
```

```csharp
using System.Security.Claims;
using DKNet.AspCore.Extensions;
using DKNet.SlimBus.Extensions;

public sealed record CreateProductCommand : Fluents.Requests.IWitResponse<ProductModel>
{
    public string Name { get; init; } = string.Empty;

    [FromClaim(ClaimTypes.NameIdentifier)]
    public string? CreatedBy { get; set; } // always resolved from the caller's claim, never from the body
}

public sealed class ProductsEndpointConfig : IEndpointConfig
{
    public string GroupEndpoint => "/products";

    public void Map(RouteGroupBuilder group) =>
        group.MapPost<CreateProductCommand, ProductModel>("/");
}
```

`POST /v1/products` now dispatches `CreateProductCommand` through `IMessageBus`, resolves
`CreatedBy` from the caller's claim before the handler runs, and returns 201 Created (the mapper
infers "Created" from the command's type name) with a FluentResults-derived `ProblemDetails` body
on failure.

## Full Documentation

See the [full feature guide](https://github.com/baoduy/DKNet/blob/main/docs/AspNetCore/DKNet.AspCore.Extensions.md)
for contextual-source resolvers, `EndpointRegistrationOptions`, all fluent mappers, paging
details, and gotchas.

## License

This project is licensed under the MIT License - see the [LICENSE](https://opensource.org/licenses/MIT) file for
details.

## About

Developed by [Steven Hoang](https://drunkcoding.net).
