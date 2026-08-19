# DKNet.AspCore.Extensions

[![NuGet](https://img.shields.io/nuget/v/DKNet.AspCore.Extensions)](https://www.nuget.org/packages/DKNet.AspCore.Extensions/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DKNet.AspCore.Extensions)](https://www.nuget.org/packages/DKNet.AspCore.Extensions/)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](../../../../LICENSE)

Minimal API integration helpers for [SlimMessageBus](https://github.com/zarusz/SlimMessageBus) + `FluentResults`,
enabling terse, consistent, and well-documented REST endpoints over a CQRS-ish message bus abstraction.

This package focuses on the ASP.NET Core surface (endpoint mapping & HTTP translation). Core CQRS contracts and EF Core
behaviors live in `DKNet.SlimBus.Extensions`.

## Key Features

- ✳️ **One-Liner Endpoint Mapping** for commands, queries, paged queries, and void commands
- 📦 **Automatic Response Shaping** using `FluentResults` + `ProblemDetails`
- 🧾 **Standardized Problem Responses** via `ProducesCommons()` contract
- 📄 **Pagination Wrapper** (`PagedResult<T>`) for `IPagedList<T>` → stable JSON shape
- 🔄 **Uniform HTTP Semantics** (POST = create vs update; null query response → 404)
- 🧪 **Test-Friendly** pure extension methods (logic inside bus + results)

## Installation

```bash
dotnet add package DKNet.AspCore.Extensions
```

Also install (if not already):

```bash
dotnet add package DKNet.SlimBus.Extensions
```

## When To Use

| Scenario                                                                     | Use This Extension |
|------------------------------------------------------------------------------|--------------------|
| You have SlimMessageBus handlers and want quick HTTP Minimal API exposure    | ✅                  |
| You use `FluentResults` for domain outcomes                                  | ✅                  |
| You want consistent OpenAPI metadata for common error codes                  | ✅                  |
| You need multi-endpoint CRUD for a resource without hand-writing boilerplate | ✅                  |

## Quick Start

```csharp
var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

// SlimBus + handlers (from DKNet.SlimBus.Extensions)
services.AddSlimBusForEfCore(b =>
{
    b.WithProviderMemory()
     .AutoDeclareFrom(typeof(CreateProductHandler).Assembly)
     .AddJsonSerializer();
});

var app = builder.Build();
var products = app.MapGroup("/products");

products
    .MapGet<GetProduct, ProductResult>("/{id}")
    .WithName("GetProduct");

products
    .MapGetPage<GetProductsPage, ProductResult>("/")
    .WithName("GetProductsPage");

products
    .MapPost<CreateProduct, ProductResult>("/")
    .WithName("CreateProduct");

products
    .MapPut<UpdateProduct, ProductResult>("/{id}")
    .WithName("UpdateProduct");

products
    .MapDelete<DeleteProduct>("/{id}")
    .WithName("DeleteProduct");

app.Run();
```

## Endpoint Mapping API

All methods extend `RouteGroupBuilder` and infer OpenAPI metadata.

| Method                          | Generic Constraints                                   | Behavior                                             |
|---------------------------------|-------------------------------------------------------|------------------------------------------------------|
| `MapGet<TQuery,TResponse>`      | `TQuery : Fluents.Queries.IWitResponse<TResponse>`    | Sends query, returns 200 + body or 404 if null       |
| `MapGetPage<TQuery,TItem>`      | `TQuery : Fluents.Queries.IWitPageResponse<TItem>`    | Wraps `IPagedList<TItem>` in `PagedResult<TItem>`    |
| `MapPost<TCommand,TResponse>`   | `TCommand : Fluents.Requests.IWitResponse<TResponse>` | Sends command; 201 Created when `TCommand`'s name contains "Create", else 200 Ok |
| `MapPost<TCommand>`             | `TCommand : Fluents.Requests.INoResponse`             | Same created-vs-ok rule, no body                     |
| `MapPut<TCommand,TResponse>`    | `TCommand : Fluents.Requests.IWitResponse<TResponse>` | 200 OK with value or Problem                         |
| `MapPut<TCommand>`              | `TCommand : Fluents.Requests.INoResponse`             | 200 OK / Problem                                     |
| `MapPatch<TCommand,TResponse>`  | same as Put                                           | Partial update semantics                             |
| `MapPatch<TCommand>`            | same as Put                                           | Partial update no response                           |
| `MapDelete<TCommand,TResponse>` | `TCommand : Fluents.Requests.IWitResponse<TResponse>` | 200 OK with value / Problem                          |
| `MapDelete<TCommand>`           | `TCommand : Fluents.Requests.INoResponse`             | 200 OK / Problem                                     |
| `MapGetById<TEntity,TModel>`    | `TEntity : IEntity<Guid>`                             | Fetches by id via `IRepositorySpec`, 200 with body or 404 |
| `MapGetList<TEntity,TModel>`    | `TEntity : IEntity<Guid>`                             | Paged, newest-first list via `IRepositorySpec`, wraps in `PagedResponse<TModel>` |

All return a `RouteHandlerBuilder` enabling further customization (e.g., `.RequireAuthorization()`).

## Options-Driven Endpoint Group Registration

Implement `IEndpointConfig` per feature and let `UseEndpointConfigs()` discover and map every implementation —
including ones defined in the consuming application, not just this package:

```csharp
public sealed class ProductsEndpoint : IEndpointConfig
{
    public string GroupEndpoint => "/products";
    public int Version => 1;

    public void Map(RouteGroupBuilder group)
    {
        group.MapGet<GetProduct, ProductDto>("/{id}");
        group.MapPost<CreateProduct, ProductDto>("/");
    }
}

app.UseEndpointConfigs();
```

With no options supplied, a group gets: route `/v{version:apiVersion}{GroupEndpoint}`, tag `"Root"` (or
`GroupEndpoint` with slashes replaced by dashes), authorization required, FluentValidation auto-validation enabled,
and a filter that stamps `RequestBase.ByUser` from the authenticated user name. Override any of it per host:

```csharp
app.UseEndpointConfigs(o =>
{
    o.RequireAuthorization = false;      // relax auth; ByUser falls back to o.SystemAccountName
    o.EnableRequestValidation = false;   // handle validation yourself
    o.SystemAccountName = "svc-account";
});
```

## Common Response Metadata

Call `.ProducesCommons()` automatically (already applied internally by the mapping helpers) to register standardized
error codes:

- 400 (ProblemDetails) validation / domain errors
- 401 / 403 auth/authz
- 404 not found (for null query result)
- 409 conflict
- 429 rate limiting
- 500 server error

## FluentResults → HTTP Translation

`ResultResponseExtensions` define:

```csharp
public static IResult Response<T>(this IResult<T> result, bool isCreated = false);
public static IResult Response(this IResultBase result, bool isCreated = false);
```

Rules:

- Failure → `ProblemDetails` (400 by default)
- Success + `isCreated` → `201 Created` with payload
- Success + null value → `200 Ok` (no body)
- Success + value → JSON body

## ProblemDetails Extensions

```csharp
result.ToProblemDetails();              // From IResultBase
modelState.ToProblemDetails();          // From ModelStateDictionary
```

Aggregates distinct error messages into `extensions.errors`.

Example output:

```json
{
  "status": 400,
  "type": "BadRequest",
  "title": "Error",
  "detail": "Name is required",
  "errors": ["Name is required", "Price must be positive"]
}
```

## Pagination Wrapper

`PagedResult<T>` converts an `IPagedList<T>` into a transport-friendly DTO:

```json
{
  "pageNumber": 1,
  "pageSize": 20,
  "pageCount": 7,
  "totalItemCount": 123,
  "items": [ { /* ... */ } ]
}
```

Returned automatically by `MapGetPage` and `MapGetList`.

## Acting-User Requests

`RequestBase` (in `DKNet.SlimBus.Extensions`) is the base record for requests that need the acting user's identity.
`UseEndpointConfigs()`'s default filter populates `RequestBase.ByUser` before the request reaches its handler:

```csharp
public record CreateProduct : RequestBase, Fluents.Requests.IWitResponse<ProductDto>
{
    public string Name { get; init; } = "";
}
```

## Defining Requests & Handlers (from DKNet.SlimBus.Extensions)

```csharp
public record GetProduct : Fluents.Queries.IWitResponse<ProductDto>
{
    public Guid Id { get; init; }
}

public class GetProductHandler : Fluents.Queries.IHandler<GetProduct, ProductDto>
{
    private readonly AppDbContext _db; private readonly IMapper _m;
    public GetProductHandler(AppDbContext db, IMapper m) { _db = db; _m = m; }

    public async Task<ProductDto?> Handle(GetProduct q, CancellationToken ct) =>
        await _db.Products.Where(p => p.Id == q.Id)
            .Select(p => _m.Map<ProductDto>(p))
            .FirstOrDefaultAsync(ct);
}
```

## Binding With `[AsParameters]`

For GET endpoints the query object is bound via `[AsParameters]` automatically when using the helpers, enabling clean
record definitions:

```csharp
public record GetProductsPage(int PageIndex = 0, int PageSize = 20) : Fluents.Queries.IWitPageResponse<ProductDto>;
```

## Composing Policies / Filters

```csharp
products
  .MapPost<CreateProduct, ProductResult>("/")
  .RequireAuthorization("ProductsWrite")
  .AddEndpointFilter(new LoggingFilter());
```

## Error Scenarios & Status Codes

| Situation                   | Result Pattern                    | HTTP | Body                  |
|-----------------------------|-----------------------------------|------|-----------------------|
| Domain validation fails     | `Result.Fail("msg")`              | 400  | ProblemDetails        |
| Query returns null          | `null`                            | 404  | ProblemDetails (none) |
| Command succeeds (create)   | `Result.Ok(value)` + created flag | 201  | JSON                  |
| Command succeeds (no value) | `Result.Ok()`                     | 200  | (empty)               |
| Unhandled exception         | n/a                               | 500  | (standard)            |

## Extending

Add your own composite methods:

```csharp
public static class ProductEndpointGroup
{
    public static RouteGroupBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/products");
        g.MapGet<GetProduct, ProductDto>("/{id}");
        g.MapPost<CreateProduct, ProductDto>("/");
        return g;
    }
}
```

## Testing Patterns

Because the endpoint methods only orchestrate bus + translation, prefer unit testing handlers and a minimal integration
test asserting mapping correctness:

```csharp
// Arrange: host with in-memory bus stub
// Act: HTTP call
// Assert: status + payload shape
```

## Versioning & Compatibility

| Package                  | Purpose                              |
|--------------------------|--------------------------------------|
| DKNet.AspCore.Extensions | HTTP layer & endpoint helpers        |
| DKNet.SlimBus.Extensions | Core CQRS abstractions, EF behaviors |

Targets .NET 9+. Uses ASP.NET Core Minimal APIs.

## Roadmap / Ideas

- Optional Location URI delegate for Created responses
- Customizable default problem title / type mapping
- OpenTelemetry activity tagging
- Inline endpoint filter helpers (validation, caching)

## Contributing

PRs welcome. Keep helpers minimal and side-effect free. Larger concerns (transactions, persistence, events) belong in
the core extensions library.

## License

MIT © DKNet

## See Also

- `DKNet.SlimBus.Extensions` (core CQRS + EF integration)
- [SlimMessageBus](https://github.com/zarusz/SlimMessageBus)
- [FluentResults](https://github.com/altmann/FluentResults)

