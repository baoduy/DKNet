# DKNet.AspCore.Extensions

## ✨ Why use it?

`DKNet.AspCore.Extensions` is the minimal-API glue for DKNet-based web APIs. Instead of one
grab-bag of helpers it covers five distinct jobs that show up in almost every DKNet host:

- populating request properties from the authenticated caller (claims today, anything else
  tomorrow) so they can never be forged through the request body or querystring;
- discovering and mapping versioned groups of endpoints (`IEndpointConfig`) without hand-wiring
  `MapGroup`/`WithApiVersionSet` boilerplate per feature;
- mapping a minimal-API verb straight onto a SlimMessageBus fluent command/query in one line,
  including the generic "get by id" / "list" cases backed by `DKNet.EfCore.Specifications`;
- a paging envelope (`PagedResponse<T>`) shared by every paged endpoint; and
- converting a `FluentResults` result (or an ASP.NET Core `ModelStateDictionary`) into the
  `IResult`/`ProblemDetails` shape minimal APIs and OpenAPI both expect.

Reach for it whenever you are building minimal-API endpoints on top of DKNet's SlimBus/CQRS and
EF Core Specifications packages — it is what turns a command/query class into a routed,
versioned, documented HTTP endpoint with almost no repeated code.

## 🚀 Quick Start

```bash
dotnet add package DKNet.AspCore.Extensions
```

Minimum wiring in `Program.cs` — `AddApiVersioning()` is required because `UseEndpointConfigs`
defaults to versioned routes, and `AddContextualRequestPopulation()` is required the moment any
request declares a `[FromClaim]` (or other `IContextualSource`) member:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthorization();
builder.Services.AddApiVersioning();
builder.Services.AddContextualRequestPopulation();
builder.Services.AddOpenApi(); // optional, needed for the published API description

var app = builder.Build();

app.UseEndpointConfigs();      // discovers every IEndpointConfig across the loaded assemblies
app.MapOpenApi();

app.Run();
```

## 🔐 Contextual Request Binding — `[FromClaim]` and `IContextualSource`

A request DTO often needs a value the *caller* must never control — who created it, which
tenant it belongs to. `IContextualSource` marks a property as populated by the host instead of
the caller; `FromClaimAttribute` is the built-in implementation, resolving the property from a
named claim on the authenticated user:

```csharp
using System.Security.Claims;
using DKNet.AspCore.Extensions;
using DKNet.SlimBus.Extensions;

public sealed record CreateProductCommand : Fluents.Requests.IWitResponse<ProductModel>
{
    public string Name { get; init; } = string.Empty;

    [FromClaim(ClaimTypes.NameIdentifier)]
    public string? CreatedBy { get; set; } // always overwritten — never trust a caller-supplied value here
}
```

Register the mechanism once:

```csharp
builder.Services.AddContextualRequestPopulation(o =>
{
    // Only applied when the group's RequireAuthorization is false AND the value could not be
    // resolved — an authenticated caller missing the claim gets the property's type default
    // instead, never this fallback.
    o.SystemAccountFallback = "system-account";
});
```

Once registered, every endpoint mapped by `UseEndpointConfigs` populates declared members
**before validation and before the handler runs** — for both JSON-body binding and
`[AsParameters]`/query binding — and the same registration also excludes those members from the
published OpenAPI schema/parameters (`ContextualSourceSchemaTransformer` for JSON bodies,
`ContextualSourceOperationTransformer` for query/`[AsParameters]`), since the caller can never
actually supply them.

A new source kind needs only its own attribute plus a matching resolver — no change to this
package:

```csharp
public sealed class FromTenantHeaderAttribute : Attribute, IContextualSource;

public sealed class TenantHeaderResolver : IContextualValueResolver
{
    public bool CanResolve(IContextualSource source) => source is FromTenantHeaderAttribute;

    public string? Resolve(IContextualSource source, HttpContext httpContext) =>
        httpContext.Request.Headers["X-Tenant-Id"];
}

// builder.Services.AddScoped<IContextualValueResolver, TenantHeaderResolver>();
```

## 🧩 Endpoint Group Discovery & Mapping — `IEndpointConfig` / `UseEndpointConfigs`

Implement `IEndpointConfig` per feature area instead of wiring `MapGroup` calls by hand:

```csharp
public sealed class ProductsEndpointConfig : IEndpointConfig
{
    public string GroupEndpoint => "/products";          // Tag defaults to "products"
    public int Version => 1;                              // optional; defaults to 1

    public void Map(RouteGroupBuilder group)
    {
        group.MapGetById<Product, ProductModel>("/{id:guid}");
        group.MapGetList<Product, ProductModel>("/");
        group.MapPost<CreateProductCommand, ProductModel>("/");
    }
}
```

`app.UseEndpointConfigs(...)` scans the given assemblies (or every loaded assembly by default —
so a consuming application's own `IEndpointConfig` types are picked up automatically), builds one
versioned `RouteGroupBuilder` per config via `Asp.Versioning`, tags it, requires authorization by
default, and calls `Map`. Every default reproduces the DKNet template's original hardcoded
behaviour — a caller who supplies no options gets that behaviour unchanged:

```csharp
app.UseEndpointConfigs(o =>
{
    o.EnableVersioning = false;          // drop the "/v{version}" prefix entirely
    o.RequireAuthorization = false;      // explicit host opt-out; the host owns this decision
    o.DefaultTag = "Root";               // used when a config resolves an empty Tag
    o.RouteTemplate = c => $"/api{c.GroupEndpoint}"; // override the generated route pattern
    o.ConfigureGroup = (group, config) =>
        group.AddEndpointFilter(async (ctx, next) => await next(ctx)); // per-group host setup
});
```

`ConfigureGroup` is the hook for host-specific setup that used to be built into this package —
request validation (e.g. `AddFluentValidationAutoValidation()`), custom filters, and so on. It
always runs after the contextual-population filter and before `RequireAuthorization`, so
population can never be bypassed by a host filter, while real ASP.NET Core authorization
middleware still runs ahead of every endpoint filter at request time.

## 🛣️ Fluent Minimal-API Endpoint Mapping Helpers

`FluentsEndpointMapperExtensions` maps an HTTP verb straight onto a SlimMessageBus fluent
request/query (from `DKNet.SlimBus.Extensions`), dispatching through `IMessageBus` and turning the
`FluentResults` outcome into the right `IResult` automatically:

```csharp
group.MapPost<CreateProductCommand, ProductModel>("/");   // 201 Created — type name contains "Create"
group.MapPost<RenameProductCommand, ProductModel>("/{id:guid}/rename"); // 200 Ok otherwise
group.MapPut<UpdateProductCommand, ProductModel>("/{id:guid}");
group.MapPatch<AdjustStockCommand>("/{id:guid}/stock");    // INoResponse overload — 200/no body
group.MapDelete<DeactivateProductCommand>("/{id:guid}");   // INoResponse only — see Gotchas
group.MapGet<FindProductQuery, ProductModel>("/find");     // Fluents.Queries.IWitResponse<T> -> 200 or 404
group.MapGetPage<ListProductsPageQuery, ProductModel>("/page"); // Fluents.Queries.IWitPageResponse<T>
```

```csharp
public sealed record CreateProductCommand : Fluents.Requests.IWitResponse<ProductModel>
{
    public string Name { get; init; } = string.Empty;
}

internal sealed class CreateProductHandler : Fluents.Requests.IHandler<CreateProductCommand, ProductModel>
{
    public Task<IResult<ProductModel>> OnHandle(CreateProductCommand request, CancellationToken ct) =>
        Task.FromResult<IResult<ProductModel>>(Result.Ok(new ProductModel { Name = request.Name }));
}
```

Two more mappers skip SlimMessageBus entirely and go straight to a `DKNet.EfCore.Specifications`
`IRepositorySpec` for the common "read model by id" / "list model" cases:

```csharp
group.MapGetById<Product, ProductModel>("/{id:guid}");   // 200 with the projected model, or 404
group.MapGetList<Product, ProductModel>("/");            // paged, newest-first (see "Paged Responses" below)
```

Both require `Product` to implement `IEntity<Guid>` and `IRepositorySpec` to be registered
(`services.AddSpecRepo<TDbContext>()`, from `DKNet.EfCore.Specifications`). `MapGetList` orders by
`CreatedOn` descending (tie-broken by `Id`) when the entity implements `IAuditedEntity<Guid>`, or
by `Id` descending alone otherwise.

Every mapper also calls `.ProducesCommons()`, adding the shared 400/401/403/404/409/429/500
response metadata so the published OpenAPI description is consistent across endpoints — call it
yourself on a hand-written `RouteHandlerBuilder` to match:

```csharp
app.MapGet("/health", () => "ok").ProducesCommons();
```

## 📄 Paged Responses — `PagedResponse<T>`

`PagedResponse<TResult>` is the response envelope `MapGetList`/`MapGetPage` return: it wraps an
`X.PagedList.IPagedList<T>` with `PageNumber`, `PageSize`, `PageCount`, `TotalItemCount`, `Items`,
and the derived `HasNextPage`/`HasPreviousPage` flags. Build one directly when a handler already
has an `IPagedList<T>` (e.g. a `Fluents.Queries.IPageHandler<TQuery, TResponse>` result):

```csharp
return Results.Ok(new PagedResponse<ProductModel>(pagedList));
```

`MapGetList<TEntity, TModel>` additionally clamps `pageNumber` to at least 1 and `pageSize` to
`[1, 100]` before querying, and documents the 100-row ceiling on the `pageSize` parameter's
published OpenAPI description.

## 🧯 Result → `IResult` / `ProblemDetails` Conversion

`ResultResponseExtensions.Response()`/`Response<T>()` convert a `FluentResults` `IResultBase`/
`IResult<T>` — the same result type DKNet's SlimBus handlers already return — into the right
minimal-API `IResult`: `Ok`/`Created` on success, `TypedResults.Problem(...)` on failure. This is
exactly what the fluent mappers above call internally, and it is available standalone for any
hand-written minimal-API endpoint:

```csharp
app.MapPost("/products", async (IMessageBus bus, CreateProductCommand cmd) =>
    (await bus.Send(cmd)).Response(isCreated: true));
```

`ProblemDetailsExtensions.ToProblemDetails()` builds the underlying `ProblemDetails` from either
an `IResultBase` (used by `Response()` on failure) or an ASP.NET Core `ModelStateDictionary` —
useful when you still validate through classic model binding instead of `[AsParameters]`/minimal
APIs:

```csharp
if (!ModelState.IsValid)
    return Results.Problem(ModelState.ToProblemDetails()!);
```

Both return `null` on success/valid input, and both collect distinct, non-empty error messages
into the response's `errors` extension property.

## ⚙️ Configuration Options

`ContextualPopulationOptions` (via `AddContextualRequestPopulation(Action<ContextualPopulationOptions>?)`):

| Option | Default | Notes |
|---|---|---|
| `SystemAccountFallback` | `null` | Substituted only when a member can't be resolved **and** the mapped group's `RequireAuthorization` is `false`. `null` disables the fallback entirely. An authenticated-but-unresolved member (e.g. authorization on, claim missing) never receives it — it holds its type's default instead. |

`EndpointRegistrationOptions` (via `UseEndpointConfigs(Action<EndpointRegistrationOptions>?, params Assembly[])`):

| Option | Default | Notes |
|---|---|---|
| `RouteTemplate` | `null` | `null` uses `/v{version:apiVersion}{GroupEndpoint}` when versioning is enabled, or `{GroupEndpoint}` otherwise. |
| `DefaultTag` | `"Root"` | Used when an `IEndpointConfig.Tag` resolves empty. |
| `RequireAuthorization` | `true` | Disabling it is an explicit, per-host opt-out — the host owns that decision. |
| `EnableVersioning` | `true` | Requires `AddApiVersioning()` to be registered, or `UseEndpointConfigs` throws at startup — even with zero discovered configs. |
| `ConfigureGroup` | `null` | Runs after mapping/tags/version metadata, before authorization is applied and before `IEndpointConfig.Map`. |

## 🧱 How It Composes With Other DKNet Packages

- **`DKNet.SlimBus.Extensions`** — `Fluents.Requests`/`Fluents.Queries` are the command/query
  contracts the fluent mappers dispatch through `IMessageBus`; handlers return `FluentResults`
  (`IResult<T>`/`IResultBase`), the same result type `ResultResponseExtensions` converts.
- **`DKNet.EfCore.Specifications`** — `MapGetById`/`MapGetList` run through `IRepositorySpec` and
  an internal `ModelSpecification<TEntity,TModel>`, so any entity already query-able through the
  specifications package works with no extra plumbing.
- **`DKNet.EfCore.Abstractions`** — `MapGetById`/`MapGetList` constrain `TEntity` to
  `IEntity<Guid>`, and `MapGetList` special-cases `IAuditedEntity<Guid>` for its default ordering.
- **Minimal APIs / `Microsoft.AspNetCore.OpenApi`** — `AddContextualRequestPopulation()` registers
  its schema/operation transformers through `ConfigureAll<OpenApiOptions>`, so they apply
  automatically to whatever `AddOpenApi()` document(s) the host already configures; versioned
  routing is `Asp.Versioning.Http`'s `IApiVersionParser`/`ApiVersionSet`.

## ⚠️ Gotchas & Limits

- **`MapDelete<TCommand, TResponse>`** (the "with response" overload) binds its command with no
  `[FromBody]`/`[AsParameters]` — plain `(IMessageBus bus, TCommand request)`. Minimal APIs'
  inferred-body binding does not support DELETE, so the endpoint throws `InvalidOperationException`
  the moment endpoint metadata is built (the first request the *host* handles, not just this
  route). Use the `INoResponse` overload for DELETE, or model the payload as route/query values.
- **`EnableVersioning = true`** (the default) requires `AddApiVersioning()` on the service
  collection; `UseEndpointConfigs` fails fast on that check *before* discovery runs, so it throws
  even when zero `IEndpointConfig` implementations exist in the scanned assemblies.
- A **`[FromClaim]`-declared property with no setter** throws `InvalidOperationException` the
  first time its type is scanned — add a `set` or `init`.
- A request that **declares a contextual source but never registers**
  `AddContextualRequestPopulation()` throws `InvalidOperationException` at endpoint-build time
  (startup), naming the offending type — it does not silently pass the caller's value through.
- **`SystemAccountFallback` never crosses the `RequireAuthorization` boundary** — it only fires
  when the group allows anonymous access; an authenticated caller missing the claim always gets
  the property's type default, never the fallback.
- **Population is not validation.** A claim value that fails to convert to the property's type
  (e.g. a non-`Guid` string into a `Guid` property) silently becomes that type's default — it
  never rejects the request. Pair it with your own validator if a missing/unresolvable value must
  block the request.
- **`MapGetById`/`MapGetList`** only support `Guid`-keyed entities (`IEntity<Guid>`).
- Registration order is deliberate: the contextual-population filter is added *before*
  `ConfigureGroup` runs, so it can never be defeated by a host filter's registration order — but
  it still executes after ASP.NET Core's authorization middleware at request time.

## 🔗 Related Packages

- [DKNet.AspCore.Tasks](DKNet.AspCore.Tasks.md) — background jobs at application start-up.
- [DKNet.AspCore.Idempotency](DKNet.AspCore.Idempotency.md) — endpoint middleware for safe request retries.
- `DKNet.SlimBus.Extensions` — the fluent request/query contracts mapped by this package.
- `DKNet.EfCore.Specifications` — backs the generic `MapGetById`/`MapGetList` helpers.
