# DKNet.AspCore.Extensions

The minimal-API glue for DKNet-based web APIs: host-populated request members, discovered and
versioned endpoint groups, one-line verb-to-command mappers, generic list/read/delete endpoints, and
`FluentResults`-to-`IResult` conversion.

## ✨ Why use it?

`DKNet.AspCore.Extensions` covers five distinct jobs that show up in almost every DKNet host:

- populating request properties from the authenticated caller (claims today, anything else
  tomorrow) so they can never be forged through the request body or querystring;
- discovering and mapping versioned groups of endpoints (`IEndpointConfig`) without hand-wiring
  `MapGroup`/`WithApiVersionSet` boilerplate per feature;
- mapping a minimal-API verb straight onto a SlimMessageBus fluent command/query in one line,
  including the generic "get by id" / "list" / "delete by id" cases backed by
  `DKNet.EfCore.Specifications`;
- a paging, filtering, searching and ordering contract (`ListQueryRequest`, `ListFilter`,
  `PagedResponse<T>`) shared by every list endpoint; and
- converting a `FluentResults` result (or an ASP.NET Core `ModelStateDictionary`) into the
  `IResult`/`ProblemDetails` shape minimal APIs and OpenAPI both expect.

Reach for it whenever you are building minimal-API endpoints on top of DKNet's SlimBus/CQRS and
EF Core Specifications packages — it is what turns a command/query class, or a bare entity, into a
routed, versioned, documented HTTP endpoint with almost no repeated code.

## 🚀 Quick Start

```bash
dotnet add package DKNet.AspCore.Extensions
```

Minimum wiring in `Program.cs` — `AddApiVersioning()` is required because `UseEndpointConfigs`
defaults to versioned routes, and `AddContextualRequestPopulation()` is required the moment any
request declares a `[FromClaim]` (or other `IContextualSource`) member:

```csharp
using DKNet.AspCore.Extensions.Endpoints;   // UseEndpointConfigs
using DKNet.AspCore.Extensions.ModelBinding; // AddContextualRequestPopulation

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

### Namespaces at a glance

Every sample below assumes the `using` that owns the type it shows:

| Namespace | Types |
|---|---|
| `DKNet.AspCore.Extensions` | `IEndpointConfig` |
| `DKNet.AspCore.Extensions.ModelBinding` | `FromClaimAttribute`, `IContextualSource`, `IContextualValueResolver`, `ContextualPopulationOptions`, `AddContextualRequestPopulation()` |
| `DKNet.AspCore.Extensions.Endpoints` | `EndpointRegistrationOptions`, `UseEndpointConfigs()`, the `Map*` mappers, `ListQueryRequest`, `ListFilter`, `ListFilterJsonConverter`, `CrudMapOptions`, `CrudOp` |
| `DKNet.AspCore.Extensions.Responses` | `PagedResponse<T>`, `ResultResponseExtensions`, `ProblemDetailsExtensions` |

## 🧩 Features

### Contextual request binding — `[FromClaim]` and `IContextualSource`

A request DTO often needs a value the *caller* must never control — who created it, which tenant it
belongs to. `IContextualSource` marks a property as populated by the host instead of the caller;
`FromClaimAttribute` is the built-in implementation, resolving the property from a named claim on
the authenticated user.

`FromClaimAttribute` has exactly one form — a single required constructor argument, no named
properties, `AttributeTargets.Property`, `AllowMultiple = false`, `Inherited = false`. What you
write, and what the request instance actually carries by the time your handler sees it:

| You declare | The handler receives |
|---|---|
| `[FromClaim(ClaimTypes.NameIdentifier)]`<br>`public string? CreatedBy { get; set; }` | `CreatedBy = "8f0c…"` — the value of the caller's `nameidentifier` claim, whatever the request body said |
| same, caller authenticated but the claim is absent | `CreatedBy = null` — the property's type default, **never** the caller's value |
| same, on an anonymous group (`RequireAuthorization = false`) with `SystemAccountFallback = "system-account"` | `CreatedBy = "system-account"` |
| `[FromClaim("tenant_id")]`<br>`public Guid TenantId { get; set; }` | `TenantId = Guid.Parse(claim)` via `TypeDescriptor` conversion; an unconvertible claim yields `Guid.Empty`, not a 400 |
| `[FromClaim("tenant_id")]`<br>`public Guid TenantId { get; }` *(no setter)* | Nothing — `InvalidOperationException` at startup naming `'{Type}.{Property}' declares a contextual source but has no setter` |

```csharp
using System.Security.Claims;
using DKNet.AspCore.Extensions.ModelBinding;
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

That single call registers `ClaimValueResolver` and the population service as **scoped**, and adds
the two OpenAPI transformers through `ConfigureAll<OpenApiOptions>`. Endpoint groups mapped by
`UseEndpointConfigs` then populate declared members **before validation and before the handler
runs** — for both JSON-body binding and `[AsParameters]`/query binding — and the declared members
are removed from the published OpenAPI description (`ContextualSourceSchemaTransformer` for JSON
bodies, `ContextualSourceOperationTransformer` for query/`[AsParameters]` parameters), since the
caller can never actually supply them.

A new source kind needs only its own attribute plus a matching resolver — no change to this
package. The mechanism dispatches on `IContextualValueResolver.CanResolve`, never on a concrete
attribute type:

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

### Endpoint group discovery and mapping — `IEndpointConfig` / `UseEndpointConfigs`

Implement `IEndpointConfig` per feature area instead of wiring `MapGroup` calls by hand. Only
`GroupEndpoint` and `Map` are abstract; the other three members are default interface
implementations you override when the default is wrong:

| Member | Default | What it controls |
|---|---|---|
| `string GroupEndpoint { get; }` | none — you must supply it | Route segment appended after the version prefix, e.g. `"/products"`. |
| `void Map(RouteGroupBuilder group)` | none — you must supply it | Where the group's endpoints are registered. Runs last, after tags, filters and authorization. |
| `string? AuthPolicy { get; }` | `null` | Authorization policy name for the group. `null`/empty means `RequireAuthorization()` with no policy. Ignored entirely when `RequireAuthorization` is `false`. |
| `string Tag { get; }` | `GroupEndpoint` with `/` replaced by `-` and leading `-` trimmed — so `"/products"` becomes `"products"` | The OpenAPI tag. Resolving to an empty string falls back to `EndpointRegistrationOptions.DefaultTag`. |
| `int Version { get; }` | `1` | API version the group is mapped to, and the `v{n}` in the route and group name. |

```csharp
using DKNet.AspCore.Extensions;

public sealed class ProductsEndpointConfig : IEndpointConfig
{
    public string GroupEndpoint => "/products";          // Tag defaults to "products"
    public int Version => 1;                             // optional; defaults to 1
    public string? AuthPolicy => "products:write";       // optional; defaults to null

    public void Map(RouteGroupBuilder group)
    {
        group.MapGetById<Product, ProductModel>("/{id:guid}");
        group.MapGetList<Product, ProductModel>("/");
        group.MapPost<CreateProductCommand, ProductModel>("/");
    }
}
```

`app.UseEndpointConfigs(...)` scans the given assemblies (or every loaded assembly by default — so a
consuming application's own `IEndpointConfig` types are picked up automatically), builds one
versioned `RouteGroupBuilder` per config via `Asp.Versioning`, tags it, requires authorization by
default, and calls `Map`. It returns the created groups as an `IReadOnlyList<RouteGroupBuilder>`,
or an empty list when nothing was discovered. Every default reproduces the DKNet template's original
hardcoded behaviour — a caller who supplies no options gets that behaviour unchanged:

```csharp
app.UseEndpointConfigs(o =>
{
    o.EnableVersioning = false;          // drop the "/v{version}" prefix entirely
    o.RequireAuthorization = false;      // explicit host opt-out; the host owns this decision
    o.DefaultTag = "Root";               // used when a config resolves an empty Tag
    o.RouteTemplate = c => $"/api{c.GroupEndpoint}"; // override the generated route pattern
    o.ConfigureGroup = (group, config) =>
        group.AddEndpointFilter(async (ctx, next) => await next(ctx)); // per-group host setup
},
typeof(Program).Assembly);               // optional: restrict the scan to named assemblies
```

`ConfigureGroup` is the hook for host-specific setup that used to be built into this package —
request validation (e.g. `AddFluentValidationAutoValidation()`), custom filters, and so on. It
always runs after the contextual-population filter and before `RequireAuthorization`, so population
can never be bypassed by a host filter, while real ASP.NET Core authorization middleware still runs
ahead of every endpoint filter at request time.

### Fluent minimal-API mappers — verb to SlimMessageBus command

`FluentsEndpointMapperExtensions` maps an HTTP verb straight onto a SlimMessageBus fluent
request/query (from `DKNet.SlimBus.Extensions`), dispatching through `IMessageBus` and turning the
`FluentResults` outcome into the right `IResult` automatically. Every one of them is an extension on
`RouteGroupBuilder` and every one calls `.ProducesCommons()`:

| Mapper | Command constraint | Binding | Success status |
|---|---|---|---|
| `MapGet<TCommand, TResponse>(endpoint)` | `Fluents.Queries.IWitResponse<TResponse>` | `[AsParameters]` | `200`, or `404` when the query returns `null` |
| `MapGetPage<TCommand, TResponse>(endpoint)` | `Fluents.Queries.IWitPageResponse<TResponse>` | `[AsParameters]` | `200` with `PagedResponse<TResponse>` |
| `MapPost<TCommand, TResponse>(endpoint)` | `Fluents.Requests.IWitResponse<TResponse>` | inferred body | `201` when the command's type name contains `"Create"` (case-insensitive), otherwise `200` |
| `MapPost<TCommand>(endpoint)` | `Fluents.Requests.INoResponse` | inferred body | same `201`/`200` rule, no body |
| `MapPut<TCommand, TResponse>(endpoint)` / `MapPut<TCommand>(endpoint)` | `IWitResponse<TResponse>` / `INoResponse` | inferred body | `200` |
| `MapPatch<TCommand, TResponse>(endpoint)` / `MapPatch<TCommand>(endpoint)` | `IWitResponse<TResponse>` / `INoResponse` | inferred body | `200` |
| `MapDelete<TCommand, TResponse>(endpoint)` | `IWitResponse<TResponse>` | explicit `[FromBody]` | `200` |
| `MapDelete<TCommand>(endpoint)` | `INoResponse` | `[AsParameters]` | `200` |
| `MapPutById<TCommand, TKey, TResponse>(endpoint = "{id}")` | `IWitResponse<TResponse>` **and** `Fluents.Requests.IWithKey<TKey>` | route `id` + inferred body | `200` |
| `MapActionById<TCommand, TKey, TResponse>(endpoint, httpMethod)` | `IWitResponse<TResponse>` **and** `IWithKey<TKey>` | route `id` + inferred body | `200` |

```csharp
group.MapPost<CreateProductCommand, ProductModel>("/");    // 201 Created — type name contains "Create"
group.MapPost<RenameProductCommand, ProductModel>("/{id:guid}/rename"); // 200 Ok otherwise
group.MapPut<UpdateProductCommand, ProductModel>("/{id:guid}");
group.MapPutById<UpdateProductCommand, Guid, ProductModel>();           // binds route {id} into request.Id
group.MapActionById<ApproveOrderCommand, Guid, OrderModel>("{id}/approval", "POST");
group.MapPatch<AdjustStockCommand>("/{id:guid}/stock");    // INoResponse overload — 200/no body
group.MapDelete<DeactivateProductCommand>("/{id:guid}");   // INoResponse — [AsParameters] binding
group.MapGet<FindProductQuery, ProductModel>("/find");     // Fluents.Queries.IWitResponse<T> -> 200 or 404
group.MapGetPage<ListProductsPageQuery, ProductModel>("/page"); // Fluents.Queries.IWitPageResponse<T>
```

`MapPutById`/`MapActionById` assign the route key onto the command before dispatch
(`request.Id = id`), so the command never has to re-read it from the route:

```csharp
public sealed record ApproveOrderCommand
    : Fluents.Requests.IWitResponse<OrderModel>, Fluents.Requests.IWithKey<Guid>
{
    public Guid Id { get; set; }           // assigned from the route by the mapper
    public string Approver { get; init; } = string.Empty;
}
```

`ProducesCommons()` adds the shared `400`/`401`/`403`/`404`/`409`/`429`/`500` response metadata so
the published OpenAPI description is consistent across endpoints. It is public — call it yourself on
a hand-written `RouteHandlerBuilder` to match:

```csharp
app.MapGet("/health", () => "ok").ProducesCommons();
```

### Generic entity endpoints — read, list, delete without a handler

`FluentsEntityEndpointMapperExtensions` skips SlimMessageBus entirely and goes straight to a
`DKNet.EfCore.Specifications` `IRepositorySpec`. Each mapper comes in two shapes: an explicit-`TKey`
form for any key type, and a `Guid` shorthand that forwards to it.

| Mapper | Default route | Constraints | Result |
|---|---|---|---|
| `MapGetById<TEntity, TKey, TModel>(endpoint = "{id}")` | `{id}` | `TEntity : class, IEntity<TKey>`, `TKey : IEquatable<TKey>`, `TModel : class` | `200` with the projected model, `404` when no row matches |
| `MapGetById<TEntity, TModel>(endpoint = "{id}")` | `{id}` | `TEntity : class, IEntity<Guid>` | forwards to the `TKey` form with `TKey = Guid` |
| `MapDeleteById<TEntity, TKey>(endpoint = "{id}")` | `{id}` | `TEntity : class, IEntity<TKey>`, `TKey : IEquatable<TKey>` | `204`, `404` when no row matches, `409` when `SaveChangesAsync` throws `DbUpdateException` |
| `MapDeleteById<TEntity>(endpoint = "{id}")` | `{id}` | `TEntity : class, IEntity<Guid>` | forwards to the `TKey` form |
| `MapGetList<TEntity, TKey, TModel>(endpoint = "/")` | `/` | `TEntity : class, IEntity<TKey>`, `TKey : IEquatable<TKey>`, `TModel : class` | `200` with `PagedResponse<TModel>`, `400` on an unusable `filter`/`search`/`orderBy` |
| `MapGetList<TEntity, TModel>(endpoint = "/")` | `/` | `TEntity : class, IEntity<Guid>` | forwards to the `TKey` form |

```csharp
group.MapGetById<Product, ProductModel>("/{id:guid}");        // Guid-keyed shorthand
group.MapGetById<Sprocket, int, SprocketModel>("/{id}");      // int key
group.MapGetById<Coupon, string, CouponModel>("/{id}");       // string key
group.MapDeleteById<Sprocket, int>("/{id}");
group.MapGetList<Product, ProductModel>("/");
```

`TKey` is constrained to `IEquatable<TKey>` rather than `IParsable<TSelf>` on purpose: the looser
constraint keeps `string` keys usable, and minimal APIs bind those natively. The cost is that a key
type the framework cannot bind fails when the route is built rather than at compile time.

All three require `IRepositorySpec` to be registered (`services.AddSpecRepo<TDbContext>()`, from
`DKNet.EfCore.Specifications`). `MapDeleteById` hard-deletes through the repository's save pipeline,
so audit-log and domain-event hooks fire exactly as for any other removal.

Default ordering for `MapGetList`, when the caller supplies no `orderBy`: `CreatedOn` descending
with `Id` descending as tie-break when the entity implements `IAuditedEntity<TKey>`, or `Id`
descending alone otherwise. A caller-supplied `orderBy` replaces that default outright, and `Id`
descending is appended as a tie-break unless the caller already ordered by `Id`.

### The list-endpoint query contract — `ListQueryRequest` and `ListFilter`

`MapGetList` binds `ListQueryRequest` with `[AsParameters]`, so its properties are the endpoint's
query string. Every property is nullable, so an absent parameter is distinguishable from a supplied
one:

| Query parameter | Property | Type | Default | Effect |
|---|---|---|---|---|
| `pageNumber` | `PageNumber` | `int?` | `null` → page 1 | One-based page. `null` or any value below 1 is treated as the first page. |
| `pageSize` | `PageSize` | `int?` | `null` → 20 | Items per page. `null` or below 1 becomes 20; anything above 100 is clamped to 100. |
| `filter` | `Filter` | `ListFilter[]?` | `null` | Repeatable `field:operation:value` conditions, AND-combined. At most 20 per request. |
| `search` | `Search` | `string?` | `null` | Free-text `LIKE '%…%'` across the model's text fields, OR-combined, then AND-ed onto `filter`. Minimum 2 characters after trimming; blank is treated as absent. |
| `orderBy` | `OrderBy` | `string?` | `null` | Field to sort by, replacing the endpoint's default ordering. |
| `desc` | `Desc` | `bool?` | `null` → `false` | Sort descending. Ignored without `orderBy`. |

`ListFilter` is a `readonly record struct (string Field, Ops Operation, string Value)` implementing
`IParsable<ListFilter>`, which is what lets minimal APIs bind a repeated `?filter=…&filter=…`
straight into an array. Its textual form *is* its representation — `ListFilterJsonConverter` (public,
applied via `[JsonConverter]`) serialises it as the same colon-separated string, which is also why
OpenAPI describes `filter` as an array of strings rather than an object:

```text
GET /v1/products?filter=name:Contains:widget&filter=price:GreaterThan:100&orderBy=price&desc=true
GET /v1/products?filter=discontinuedOn:IsNull&search=blue&pageSize=50
```

```csharp
// Composing the same conditions in code rather than formatting strings:
var conditions = new[]
{
    new ListFilter("Name", Ops.Contains, "widget"),
    new ListFilter("Price", Ops.GreaterThan, "100"),
};
```

Rules the parser and validator enforce, all traceable to `ListFilter.TryParse` and
`ListQuery.TryValidate`:

- **Operations** come from `Ops` (`DKNet.EfCore.Specifications.Dynamics`): `Equal`, `NotEqual`,
  `GreaterThan`, `GreaterThanOrEqual`, `LessThan`, `LessThanOrEqual`, `Contains`, `NotContains`,
  `StartsWith`, `EndsWith`, `In`, `NotIn`, `IsNull`, `IsNotNull`. Matched case-insensitively.
- **`In`/`NotIn`** take a comma-separated value list; empty entries are dropped and entries trimmed.
- **`IsNull`/`IsNotNull`** take no value, so the two-part form `field:IsNull` is accepted. Every
  other operation requires the third segment.
- **Only the first two colons split** the string, so a value may contain colons of its own — an
  ISO-8601 timestamp being the case that matters.
- **Field names** are matched case-insensitively and `snake_case`/`kebab-case` spellings are
  accepted; they are normalised to PascalCase before lookup.
- **Only fields the returned model declares** can be filtered, searched or sorted. Anything else is
  rejected with `400`, never silently dropped — dropping a condition would answer a filtered query
  with unfiltered data.
- **Bounds**: more than 20 conditions, or a search shorter than 2 characters, is a `400`.

### Paged responses — `PagedResponse<T>`

`PagedResponse<TResult>` is the envelope `MapGetList`/`MapGetPage` return. It has a parameterless
constructor (empty page) and one taking an `X.PagedList.IPagedList<TResult>`; build one directly
when a handler already has a paged list (e.g. a `Fluents.Queries.IPageHandler<TQuery, TResponse>`
result):

```csharp
return Results.Ok(new PagedResponse<ProductModel>(pagedList));
```

| Property | Type | Populated from |
|---|---|---|
| `Items` | `IList<TResult>` | the page's items; `[]` from the parameterless constructor |
| `PageNumber` | `int` | `IPagedList.PageNumber` (1-based) |
| `PageSize` | `int` | `IPagedList.PageSize` |
| `PageCount` | `int` | `IPagedList.PageCount` |
| `TotalItemCount` | `int` | `IPagedList.TotalItemCount` |
| `HasNextPage` | `bool` | derived: `PageNumber < PageCount` |
| `HasPreviousPage` | `bool` | derived: `PageNumber > 1` |

### Result → `IResult` / `ProblemDetails` conversion

`ResultResponseExtensions.Response()`/`Response<T>()` convert a `FluentResults` `IResultBase`/
`IResult<T>` — the same result type DKNet's SlimBus handlers already return — into the right
minimal-API `IResult`. This is what the fluent mappers above call internally, and it is available
standalone for any hand-written endpoint:

```csharp
app.MapPost("/products", async (IMessageBus bus, CreateProductCommand cmd) =>
    (await bus.Send(cmd)).Response(isCreated: true));
```

| Input | `isCreated` | Output |
|---|---|---|
| `IResult<T>` success, non-null value | `false` | `TypedResults.Json(value)` |
| `IResult<T>` success, null value | `false` | `TypedResults.Ok()` |
| `IResult<T>` success | `true` | `TypedResults.Created("/", value)` — the location is a literal `"/"` placeholder |
| `IResultBase` success | `false` / `true` | `TypedResults.Ok()` / `TypedResults.Created()` |
| either, failure | any | `TypedResults.Problem(problemDetails)` |

`ProblemDetailsExtensions.ToProblemDetails()` builds the underlying `ProblemDetails` from either an
`IResultBase` or an ASP.NET Core `ModelStateDictionary`:

```csharp
if (!ModelState.IsValid)
    return Results.Problem(ModelState.ToProblemDetails()!);
```

| Overload | Default status | Notes |
|---|---|---|
| `ToProblemDetails(this IResultBase, HttpStatusCode statusCode = BadRequest)` | `400` | Promoted to `404` when any error is a `NotFoundError`. `Title` is always `"Error"`, `Type` is the status name, `Detail` is the first message. |
| `ToProblemDetails(this ModelStateDictionary)` | `400` | Not configurable. |

Both return `null` on success/valid input, and both collect distinct (case-insensitive), non-empty
error messages into the response's `errors` extension property.

### Generated CRUD endpoints — `CrudMapOptions` and `CrudOp`

`CrudMapOptions` and `CrudOp` are this package's half of the vertical-slice CRUD generator. You
never write a `Map{Entity}Crud` method: `DKNet.SlimBus.Generators` emits it from the
`[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]` markers on your entity (see
[DKNet.EfCore.Abstractions](../EfCore/DKNet.EfCore.Abstractions.md)), composing the mappers above.
The generated file is skipped entirely when the compilation does not reference this package.

What you write:

```csharp
using DKNet.EfCore.Abstractions.Attributes;
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.DtoGenerator;

public class Product : IEntity<Guid>
{
    [CrudCreate]
    public Product(string name, decimal price) { Name = name; Price = price; }

    [CrudUpdate] public void UpdatePrice(decimal price) => Price = price;
    [CrudUpdate] public void UpdateName(string name) => Name = name;
    [CrudAction("approval")] public void Approve(string approver) => Approver = approver;

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public string? Approver { get; private set; }
}

[GenerateDto(typeof(Product))]
public partial record ProductDto;
```

What the generator emits into `ProductCrudEndpoints.g.cs`, verbatim in shape:

```csharp
// <auto-generated by DKNet.SlimBus.Generators />
#nullable enable
using DKNet.AspCore.Extensions.Endpoints;
namespace MyApi;

/// <summary>Registers the generated CRUD endpoints for Product.</summary>
public static class ProductCrudEndpointExtensions
{
    public static global::Microsoft.AspNetCore.Routing.RouteGroupBuilder MapProductCrud(
        this global::Microsoft.AspNetCore.Routing.RouteGroupBuilder group,
        global::System.Action<global::DKNet.AspCore.Extensions.Endpoints.CrudMapOptions>? configure = null)
    {
        var options = new global::DKNet.AspCore.Extensions.Endpoints.CrudMapOptions();
        configure?.Invoke(options);
        if (!options.IsExcluded(global::DKNet.AspCore.Extensions.Endpoints.CrudOp.GetById))
            group.MapGetById<global::MyDomain.Product, global::System.Guid, global::MyApi.ProductDto>();
        if (!options.IsExcluded(global::DKNet.AspCore.Extensions.Endpoints.CrudOp.GetList))
            group.MapGetList<global::MyDomain.Product, global::System.Guid, global::MyApi.ProductDto>();
        if (!options.IsExcluded(global::DKNet.AspCore.Extensions.Endpoints.CrudOp.Delete))
            group.MapDeleteById<global::MyDomain.Product, global::System.Guid>();
        if (!options.IsExcluded(global::DKNet.AspCore.Extensions.Endpoints.CrudOp.Create))
            group.MapPost<CreateProductRequest, global::MyApi.ProductDto>("/");
        if (!options.IsExcluded(global::DKNet.AspCore.Extensions.Endpoints.CrudOp.Update))
            group.MapPutById<UpdatePriceProductRequest, global::System.Guid, global::MyApi.ProductDto>("{id}");
        if (!options.IsExcluded(global::DKNet.AspCore.Extensions.Endpoints.CrudOp.Update))
            group.MapPutById<UpdateNameProductRequest, global::System.Guid, global::MyApi.ProductDto>("{id}/update-name");
        if (!options.IsExcluded(global::DKNet.AspCore.Extensions.Endpoints.CrudOp.Action))
            group.MapActionById<ApproveProductRequest, global::System.Guid, global::MyApi.ProductDto>("{id}/approval", "POST");
        return group;
    }
}
```

Note the routing rules that fall out of that emission: registration order is GetById, GetList,
Delete, Create, then each `[CrudUpdate]` in declaration order, then each `[CrudAction]`. The **first**
`[CrudUpdate]` claims the plain `{id}` route and each additional one gets `{id}/{kebab-cased-method}`;
an action never claims the plain `{id}` route, whatever verb it uses, and defaults its segment to the
kebab-cased method name when `[CrudAction]` carries no explicit route.

`CrudMapOptions` is the only knob on the generated method — it excludes operations, nothing more:

```csharp
group.MapProductCrud(o => o.Exclude(CrudOp.Delete, CrudOp.Action));
```

| Member | Signature | Behaviour |
|---|---|---|
| `Exclude` | `CrudMapOptions Exclude(params CrudOp[] operations)` | Adds each operation to the exclusion set and returns `this` for chaining. Nothing is excluded by default. |
| `IsExcluded` | `bool IsExcluded(CrudOp operation)` | What the generated code calls per registration. |
| `CrudOp` | enum | `GetById`, `GetList`, `Create`, `Update`, `Delete`, `Action`. `Update` and `Action` are all-or-nothing — there is no per-method exclusion. |

## ⚙️ Configuration reference

`ContextualPopulationOptions` — via `AddContextualRequestPopulation(Action<ContextualPopulationOptions>?)`:

| Option | Type | Default | Effect |
|---|---|---|---|
| `SystemAccountFallback` | `string?` | `null` | Substituted only when a declared member cannot be resolved **and** the mapped group's `RequireAuthorization` is `false`. `null` disables the fallback entirely. An authenticated-but-unresolved member never receives it — it holds its type's default instead. |

`EndpointRegistrationOptions` — via `UseEndpointConfigs(Action<EndpointRegistrationOptions>?, params Assembly[])`:

| Option | Type | Default | Effect |
|---|---|---|---|
| `RouteTemplate` | `Func<IEndpointConfig, string>?` | `null` | `null` uses `/v{version:apiVersion}{GroupEndpoint}` when versioning is enabled, or `{GroupEndpoint}` otherwise. |
| `DefaultTag` | `string` | `"Root"` | Used when an `IEndpointConfig.Tag` resolves to an empty string. |
| `RequireAuthorization` | `bool` | `true` | When `true`, applies `RequireAuthorization(config.AuthPolicy)`, or `RequireAuthorization()` when the policy is null/empty. Disabling it is an explicit per-host opt-out, and it is also what enables `SystemAccountFallback`. |
| `EnableVersioning` | `bool` | `true` | Adds the version prefix and API-version metadata. Requires `AddApiVersioning()` to be registered, or `UseEndpointConfigs` throws at startup — even with zero discovered configs. |
| `ConfigureGroup` | `Action<RouteGroupBuilder, IEndpointConfig>?` | `null` | Runs after mapping/tags/version metadata, before authorization is applied and before `IEndpointConfig.Map`. |

`UseEndpointConfigs`'s own parameters:

| Parameter | Type | Default | Effect |
|---|---|---|---|
| `configureOptions` | `Action<EndpointRegistrationOptions>?` | `null` | Leave `null` to keep every default above. |
| `assemblies` | `params Assembly[]` | empty → `AppDomain.CurrentDomain.GetAssemblies()` | Assemblies scanned for `IEndpointConfig` implementations. |

`ListQueryRequest`'s query-string defaults and ceilings are listed under
[The list-endpoint query contract](#the-list-endpoint-query-contract--listqueryrequest-and-listfilter);
they are constants on the record, not configurable per endpoint.

## 🧱 Where it fits

![Workflow diagram of UseEndpointConfigs: a fail-fast versioning guard runs before discovery, discovered IEndpointConfig implementations each become a versioned route group, the contextual-population filter is registered first, then the host's ConfigureGroup callback, then authorization and finally the config's own Map. Two InvalidOperationException exits and an empty-list exit are shown.](../diagrams/aspcore-extensions-endpoint-mapping.svg)

The two early exits on the left of that diagram are the failures worth internalising: a missing
`AddApiVersioning()` throws before discovery even runs, and a request type that declares a
contextual source without `AddContextualRequestPopulation()` throws while endpoint metadata is
built. Neither is a runtime surprise — both happen at startup.

- **`DKNet.SlimBus.Extensions`** — `Fluents.Requests`/`Fluents.Queries` are the command/query
  contracts the fluent mappers dispatch through `IMessageBus`; handlers return `FluentResults`
  (`IResult<T>`/`IResultBase`), the same result type `ResultResponseExtensions` converts.
- **`DKNet.SlimBus.Generators`** — emits the `Map{Entity}Crud` extension that composes those mappers
  and consults this package's `CrudMapOptions`.
- **`DKNet.EfCore.Specifications`** — `MapGetById`/`MapGetList`/`MapDeleteById` run through
  `IRepositorySpec` and internal `ModelSpecification<TEntity,TModel>` types, and `ListFilter`'s
  `Ops` and dynamic predicate building come from its `Dynamics` namespace.
- **`DKNet.EfCore.Abstractions`** — the entity mappers constrain `TEntity` to `IEntity<TKey>`, and
  `MapGetList` special-cases `IAuditedEntity<TKey>` for its default ordering. The CRUD markers live
  there too.
- **Minimal APIs / `Microsoft.AspNetCore.OpenApi`** — `AddContextualRequestPopulation()` registers
  its schema/operation transformers through `ConfigureAll<OpenApiOptions>`, so they apply
  automatically to whatever `AddOpenApi()` document(s) the host already configures; versioned
  routing is `Asp.Versioning.Http`'s `IApiVersionParser`/`ApiVersionSet`.

## ⚠️ Gotchas & Limits

- **`EnableVersioning = true`** (the default) requires `AddApiVersioning()` on the service
  collection; `UseEndpointConfigs` fails fast on that check *before* discovery runs, so it throws
  even when zero `IEndpointConfig` implementations exist in the scanned assemblies.
- **`IEndpointConfig` implementations are instantiated with `Activator.CreateInstance`**, so each
  one needs a public parameterless constructor — there is no DI for the config object itself.
  Inject services into the endpoint handlers inside `Map`, not into the config.
- A **`[FromClaim]`-declared property with no setter** throws `InvalidOperationException` the first
  time its type is scanned — add a `set` or `init`.
- A request that **declares a contextual source but never registers**
  `AddContextualRequestPopulation()` throws `InvalidOperationException` at endpoint-build time
  (startup), naming the offending type — it does not silently pass the caller's value through.
- **Population only runs on groups mapped by `UseEndpointConfigs`.** A hand-written
  `app.MapPost(...)` outside a discovered group never gets the filter, so a `[FromClaim]` property
  there keeps whatever the caller sent.
- **`SystemAccountFallback` never crosses the `RequireAuthorization` boundary** — it only fires when
  the group allows anonymous access; an authenticated caller missing the claim always gets the
  property's type default, never the fallback.
- **Population is not validation.** A claim value that fails to convert to the property's type
  (e.g. a non-`Guid` string into a `Guid` property) silently becomes that type's default — it never
  rejects the request. Pair it with your own validator if a missing/unresolvable value must block
  the request.
- **`orderBy` is validated against the entity as well as the model**, but the `400` message only
  names the model. A field that exists on `TModel` and not on `TEntity` is rejected with
  *"no such field on `TModel`"*, which reads as wrong until you check the entity.
- **A model with no `string` property cannot match a `search`** — the predicate matches nothing and
  the endpoint answers with an empty page rather than an error. Search walks at most two property
  hops (`Name`, `Merchant.Name`; not `Merchant.Address.City`).
- **`pageSize` is silently clamped, not rejected.** Asking for 5,000 rows returns 100 without any
  indication that the request was trimmed.
- **`MapDeleteById` performs a hard delete** and does no ownership or tenancy check of its own;
  authorization is whatever the enclosing route group requires.
- Registration order is deliberate: the contextual-population filter is added *before*
  `ConfigureGroup` runs, so it can never be defeated by a host filter's registration order — but it
  still executes after ASP.NET Core's authorization middleware at request time.

## 🔗 Related Packages

- [DKNet.AspCore.Tasks](DKNet.AspCore.Tasks.md) — reach for it when the work runs once at
  application start-up rather than per request.
- [DKNet.AspCore.Idempotency](DKNet.AspCore.Idempotency.md) — reach for it when a mapped endpoint
  must be safe for a client to retry.
- [DKNet.EfCore.Specifications](../EfCore/DKNet.EfCore.Specifications.md) — reach for it directly
  when you need a query shape the generic list/read mappers do not cover.
- [DKNet.EfCore.Abstractions](../EfCore/DKNet.EfCore.Abstractions.md) — reach for it for the
  `IEntity<TKey>`/`IAuditedEntity<TKey>` contracts and the `[CrudCreate]`/`[CrudUpdate]`/
  `[CrudAction]` markers the generated endpoints are built from.
