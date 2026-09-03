# DKNet.AspCore.Extensions

[![NuGet](https://img.shields.io/nuget/v/DKNet.AspCore.Extensions.svg)](https://www.nuget.org/packages/DKNet.AspCore.Extensions/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/baoduy/DKNet/blob/main/LICENSE)

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
  `MapGetList`/`MapDeleteById` backed by `DKNet.EfCore.Specifications`.
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
using DKNet.AspCore.Extensions.ModelBinding;
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

`MapDeleteById<TEntity>` hard-deletes an `IEntity<Guid>` by id without a command or handler:

```csharp
group.MapDeleteById<ProductEntity>("/{id}");
```

`DELETE /v1/products/{id}` loads the entity via `IRepositorySpec`, removes it through
`Delete`/`SaveChangesAsync` (so audit-log and domain-event hooks fire as usual), and returns
204 No Content, 404 when no entity matches, or 409 when a referencing row blocks the removal.
Authorization is inherited from the route group the endpoint is registered into — the handler
performs no ownership or tenancy check of its own.

## Migration — namespace changes in this release

Root types were grouped into concern folders; the namespace of each moved type now ends
with its folder name. This is an import-only source break: no type was renamed, removed,
resignatured, or had its behaviour changed — update the `using` line and you're done.

| Type | Old namespace | New namespace |
|---|---|---|
| `IContextualSource`, `FromClaimAttribute`, `ContextualValueResolver`, `ContextualRequestPopulation` (+ its internal helpers), `ContextualSourceOpenApiTransformers` | `DKNet.AspCore.Extensions` | `DKNet.AspCore.Extensions.ModelBinding` |
| `EndpointConfigExtensions` (incl. `EndpointRegistrationOptions`, `UseEndpointConfigs`), `FluentEndpointMapperExtensions` | `DKNet.AspCore.Extensions` | `DKNet.AspCore.Extensions.Endpoints` |
| `PagedResponse<T>`, `ProblemDetailsExtensions`, `ResultResponseExtensions` | `DKNet.AspCore.Extensions` | `DKNet.AspCore.Extensions.Responses` |

`IEndpointConfig` — the package's entry surface — stays at `DKNet.AspCore.Extensions`.

## Customisation reference

Two options types, plus the type parameters and attributes that make up the rest of the public
surface. Defaults are the ones the code applies when you pass nothing.

`ContextualPopulationOptions` — `AddContextualRequestPopulation(Action<ContextualPopulationOptions>?)`:

| Knob | Type | Default | Effect |
|---|---|---|---|
| `SystemAccountFallback` | `string?` | `null` | Value substituted for a declared member the resolver could not resolve, and only when the group's `RequireAuthorization` is `false`. An authenticated caller missing the claim gets the property's type default instead. `null` disables the fallback. |

`EndpointRegistrationOptions` — `app.UseEndpointConfigs(Action<EndpointRegistrationOptions>?, params Assembly[])`:

| Knob | Type | Default | Effect |
|---|---|---|---|
| `RouteTemplate` | `Func<IEndpointConfig, string>?` | `null` | `null` uses `/v{version:apiVersion}{GroupEndpoint}` with versioning on, or `{GroupEndpoint}` with it off. |
| `DefaultTag` | `string` | `"Root"` | OpenAPI tag used when a config's `Tag` resolves to an empty string. |
| `RequireAuthorization` | `bool` | `true` | Applies `RequireAuthorization(config.AuthPolicy)` to every group. Turning it off is also what enables `SystemAccountFallback`. |
| `EnableVersioning` | `bool` | `true` | Adds the version prefix and API-version metadata. Requires `AddApiVersioning()`, or `UseEndpointConfigs` throws at startup — even with zero discovered configs. |
| `ConfigureGroup` | `Action<RouteGroupBuilder, IEndpointConfig>?` | `null` | Host setup per group. Runs after tags/version metadata, before authorization and before `IEndpointConfig.Map`. |
| `assemblies` (method parameter) | `params Assembly[]` | empty → every currently loaded assembly | Assemblies scanned for `IEndpointConfig` implementations. |

`IEndpointConfig` — what each implementation supplies:

| Member | Default | Effect |
|---|---|---|
| `GroupEndpoint` | none — required | Route segment after the version prefix, e.g. `"/products"`. |
| `Map(RouteGroupBuilder)` | none — required | Where the group's endpoints are registered. |
| `AuthPolicy` | `null` | Policy name for the group; null/empty means authentication with no policy. Ignored when `RequireAuthorization` is `false`. |
| `Tag` | `GroupEndpoint` with `/` → `-`, leading `-` trimmed | OpenAPI tag. Empty falls back to `DefaultTag`. |
| `Version` | `1` | API version, and the `v{n}` in the route. |

`ListQueryRequest` — the query string every `MapGetList` endpoint accepts:

| Parameter | Type | Default | Effect |
|---|---|---|---|
| `pageNumber` | `int?` | page 1 | One-based; anything below 1 is the first page. |
| `pageSize` | `int?` | `20` | Clamped to a maximum of 100, silently. |
| `filter` | `ListFilter[]?` | none | Repeatable `field:operation:value`, AND-combined, at most 20 per request. Operations: `Equal`, `NotEqual`, `GreaterThan`, `GreaterThanOrEqual`, `LessThan`, `LessThanOrEqual`, `Contains`, `NotContains`, `StartsWith`, `EndsWith`, `In`, `NotIn`, `IsNull`, `IsNotNull`. `In`/`NotIn` take a comma-separated list; `IsNull`/`IsNotNull` take no value. |
| `search` | `string?` | none | Free-text match across the returned model's text fields, minimum 2 characters. |
| `orderBy` | `string?` | endpoint default | Field on the returned model to sort by. |
| `desc` | `bool?` | `false` | Sort descending; ignored without `orderBy`. |

Only fields the returned model declares can be filtered, searched or sorted; anything else is
rejected with `400` rather than silently ignored.

Attributes and other extension points:

| Knob | Kind | Default | Effect |
|---|---|---|---|
| `[FromClaim(claimType)]` | property attribute, one required ctor argument | none | Populates the property from that claim before validation and before the handler; always overwrites the caller's value, and is removed from the published OpenAPI description. The property needs a `set` or `init` or startup throws. |
| `IContextualSource` | marker interface on your own attribute | — | Opts a new source kind into the same mechanism. |
| `IContextualValueResolver` | interface you register in DI | `ClaimValueResolver` for `[FromClaim]` | `CanResolve` selects the resolver; the mechanism never switches on a concrete attribute type. |
| `CrudMapOptions.Exclude(params CrudOp[])` | builder method | nothing excluded | Skips operations in the generated `Map{Entity}Crud`. `CrudOp` is `GetById`, `GetList`, `Create`, `Update`, `Delete`, `Action`. |

## Full Documentation

See the [full feature guide](https://github.com/baoduy/DKNet/blob/main/docs/AspNetCore/DKNet.AspCore.Extensions.md)
for contextual-source resolvers, `EndpointRegistrationOptions`, all fluent mappers, paging
details, and gotchas.

## License

MIT — see [LICENSE](https://github.com/baoduy/DKNet/blob/main/LICENSE).

## About

Developed by [Steven Hoang](https://drunkcoding.net).
