# DKNet.AspCore.Extensions

| Field | Value |
|---|---|
| Area | AspNetCore |
| Install | `dotnet add package DKNet.AspCore.Extensions` |
| NuGet | https://www.nuget.org/packages/DKNet.AspCore.Extensions |
| Docs | https://github.com/baoduy/DKNet/blob/dev/docs/AspNetCore/DKNet.AspCore.Extensions.md |
| Source | https://github.com/baoduy/DKNet/tree/dev/src/AspNet/DKNet.AspCore.Extensions |
| Depends on (DKNet) | `DKNet.SlimBus.Extensions`, `DKNet.EfCore.Specifications` |
| Depends on (3rd party) | `FluentResults`, `Asp.Versioning.Http`, `SharpGrip.FluentValidation.AutoValidation.Endpoints`, `Microsoft.AspNetCore.OpenApi` |
| Target framework | net10.0 |

## Purpose

The minimal-API glue layer for DKNet hosts: it turns a SlimBus command/query, or a bare
`IEntity<TKey>`, into a routed, versioned, authorized, OpenAPI-documented HTTP endpoint with almost
no repeated code. It also owns the one place a host shapes every failure response (`AddErrorResponses`)
and the one place a request property is populated from the caller's context instead of their payload
(`[FromClaim]`/`[FromRequestHeader]`).

It is NOT a routing framework replacement, a validation library, or a persistence layer — it composes
`Microsoft.AspNetCore.Routing` minimal APIs, `SlimMessageBus`/`DKNet.SlimBus.Extensions`,
`DKNet.EfCore.Specifications`' `IRepositorySpec`, and `FluentValidation`'s auto-validation. Do not use
it to build a non-minimal-API (MVC controller) surface, and do not use its generic entity mappers as a
substitute for `DKNet.EfCore.Specifications` when a query needs a shape the generic mappers don't cover.

## Entry points

| Call | Exact signature | Called on | Notes (lifetime, ordering, prerequisites) |
|---|---|---|---|
| `UseEndpointConfigs` | `IReadOnlyList<RouteGroupBuilder> UseEndpointConfigs(this WebApplication app, Action<EndpointRegistrationOptions>? configureOptions = null, params Assembly[] assemblies)` | `WebApplication` | Discovers `IEndpointConfig` via `Activator.CreateInstance` (needs a public parameterless ctor). With no `assemblies` supplied, scans **every currently loaded assembly** (`AppDomain.CurrentDomain.GetAssemblies()`); pass explicit assemblies to scope the scan. Throws `InvalidOperationException` before discovery if `EnableVersioning` (default `true`) is set but `AddApiVersioning()` was never called — even with 0 configs found. |
| `AddContextualRequestPopulation` | `IServiceCollection AddContextualRequestPopulation(this IServiceCollection services)` | `IServiceCollection` | Registers `ClaimValueResolver` + `RequestHeaderValueResolver` as `IContextualValueResolver` (scoped) and `ContextualRequestPopulationService` (scoped), plus two `IOpenApiSchemaTransformer`/`IOpenApiOperationTransformer` via `ConfigureAll<OpenApiOptions>`. Call before registering a custom `IContextualValueResolver` you want to *add*; call it after a custom resolver you want to *replace* the built-in one with, for the same attribute type. |
| `AddErrorResponses` | `IServiceCollection AddErrorResponses(this IServiceCollection services, Action<ErrorResponseOptions>? configure = null)` | `IServiceCollection` | Registers `ErrorResponseOptions` as singleton (`TryAddSingleton`), swaps in `ErrorResponseValidationResultFactory` for SharpGrip's auto-validation, calls `AddProblemDetails()`, registers `ErrorResponseExceptionHandler` and inserts `UseExceptionHandler()` via an `IStartupFilter`. Idempotent — a second call's `configure` is discarded. |
| `AddListQueryOptions` | `IServiceCollection AddListQueryOptions(this IServiceCollection services, Action<ListQueryOptions>? configure = null)` | `IServiceCollection` | Registers `ListQueryOptions` with `ValidateDataAnnotations().ValidateOnStart()`. Optional — not calling it, and not binding `DKNet:ListQuery`, still resolves the defaults. |
| `MapGet`/`MapGetPage`/`MapPost`/`MapPut`/`MapPatch`/`MapDelete`/`MapPutById`/`MapActionById`/`MapParameterlessActionById` | extension methods on `RouteGroupBuilder` (see Usage patterns) | `RouteGroupBuilder` | Dispatch through `IMessageBus`; every one calls `.ProducesCommons()`. Command-type constraints vary per mapper — see the `FluentsEndpointMapperExtensions` row in Public surface below for the full overload list. |
| `MapGetById`/`MapGetList`/`MapDeleteById` | extension methods on `RouteGroupBuilder` | `RouteGroupBuilder` | Go straight to `IRepositorySpec` — no command/handler. Require `services.AddSpecRepo<TDbContext>()` from `DKNet.EfCore.Specifications`. |
| `ProducesCommons` | `RouteHandlerBuilder ProducesCommons(this RouteHandlerBuilder routeBuilder)` | `RouteHandlerBuilder` | Public; call on a hand-written endpoint to add the shared 400/401/403/404/409/429/500 metadata and the unhandled-exception endpoint filter, putting it on the same footing as the mapped endpoints. |
| `[FromClaim(claimType)]` | property attribute, 1 required ctor arg | property (must have `set`/`init`) | Property with no setter throws `InvalidOperationException` at the first endpoint-build scan of its declaring type. |
| `[FromRequestHeader(headerName)]` | property attribute, 1 required ctor arg (`HeaderName`) | property (must have `set`/`init`) | Same setter requirement. Published as `in: header`; `[FromClaim]` members are hidden. |
| `[EndpointGroupScope(scope, params string[] httpMethods)]` | class attribute, `AllowMultiple = true` | class implementing `IEndpointConfig` | Only read when `EndpointRegistrationOptions.RequireAuthorization` is `true`. An uncovered served method fails `app.StartAsync()`. |

## Public surface

### `DKNet.AspCore.Extensions`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `IEndpointConfig` | interface | Declares one versioned group of endpoints, discovered by `UseEndpointConfigs`. | `string GroupEndpoint { get; }` (required); `void Map(RouteGroupBuilder group)` (required); `string Tag => GroupEndpoint.Replace("/","-").TrimStart('-')` (default); `int Version => 1` (default). |

### `DKNet.AspCore.Extensions.ModelBinding`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `IContextualSource` | interface (marker) | Opts an attribute into the population mechanism. | No members. |
| `FromClaimAttribute` | attribute (sealed) | Populates a property from a named claim on the authenticated caller. | `FromClaimAttribute(string claimType)`; `string ClaimType { get; }`. `[AttributeUsage(Property, AllowMultiple=false, Inherited=false)]`. |
| `FromRequestHeaderAttribute` | attribute (sealed) | Populates a property from a named HTTP request header. | `FromRequestHeaderAttribute(string headerName)`; `string HeaderName { get; }`. Same `[AttributeUsage]` as above. |
| `IContextualValueResolver` | interface | Resolves an `IContextualSource` declaration to a raw string. | `bool CanResolve(IContextualSource source)`; `string? Resolve(IContextualSource source, HttpContext httpContext)`. |
| `ClaimValueResolver` | class (internal) | Built-in resolver for `FromClaimAttribute`. | `CanResolve`/`Resolve` read `httpContext.User.FindFirst(...)`. |
| `RequestHeaderValueResolver` | class (internal) | Built-in resolver for `FromRequestHeaderAttribute`. | Reads `httpContext.Request.Headers`, first value on a duplicate. |
| `ContextualRequestPopulationServiceCollectionExtensions` | static class | DI registration entry point. | `AddContextualRequestPopulation(this IServiceCollection)`. |
| `IContextualRequestPopulationService` (internal), `ContextualRequestPopulationService` (internal) | interface/class | Applies resolvers to a bound request instance. | `void Populate(object request, HttpContext httpContext)`. Converts via a cached `TypeConverter`; unconvertible/absent → the property's type default, never a thrown error. |
| `ContextualMemberScanner` (internal), `ContextualMember` (internal readonly record struct) | static class/struct | Discovers + caches `IContextualSource`-declared properties per type; throws at first scan if a declared property has no setter. | `ContextualMember[] GetDeclaredMembers(Type type)`. |
| `ContextualSourceSchemaTransformer`, `ContextualSourceOperationTransformer` (both internal) | `IOpenApiSchemaTransformer`/`IOpenApiOperationTransformer` | Strip contextual-source members from the published body schema/operation parameters; add `[FromRequestHeader]` members back as `in: header` parameters. | `TransformAsync(...)`. |

### `DKNet.AspCore.Extensions.Endpoints`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `EndpointRegistrationOptions` | class (sealed) | Options for `UseEndpointConfigs`. | `RouteTemplate` (`Func<IEndpointConfig,string>?`, default `null`); `DefaultTag` (`string`, default `"Root"`); `RequireAuthorization` (`bool`, default `true`); `EnableVersioning` (`bool`, default `true`); `ConfigureGroup` (`Action<RouteGroupBuilder,IEndpointConfig>?`, default `null`). |
| `EndpointConfigExtensions` | static class | Hosts `UseEndpointConfigs` (extension on `WebApplication`, C# 14 `extension` block). | See Entry points. |
| `EndpointGroupScopeAttribute` | attribute (sealed) | Declares required authorization scope(s) per HTTP method, or a group default. | `EndpointGroupScopeAttribute(string scope, params string[] httpMethods)`; `string Scope { get; }`; `string[] HttpMethods { get; }`. `[AttributeUsage(Class, AllowMultiple=true)]`. |
| `EndpointHttpMethods` | static class | `const string` HTTP method names usable as attribute ctor args (unlike `Microsoft.AspNetCore.Http.HttpMethods`' `static readonly`). | `Get`, `Post`, `Put`, `Delete`, `Patch`, `Head`, `Options`. |
| `GroupScopeAuthorization` (internal) | static class | Turns `[EndpointGroupScope]` declarations into per-endpoint `AuthorizeAttribute`s via a `Finally` convention; refuses an uncovered served method. | `DeclareGroupScope(RouteGroupBuilder, string scope, params string[] httpMethods)`. |
| `FluentsEndpointMapperExtensions` | static class | Verb-to-SlimBus mappers + `ProducesCommons()`. | `MapGet<TCommand,TResponse>`, `MapGetPage<TCommand,TResponse>`, `MapPost<TCommand,TResponse>`/`MapPost<TCommand>`, `MapPut<TCommand,TResponse>`/`MapPut<TCommand>`, `MapPatch<TCommand,TResponse>`/`MapPatch<TCommand>`, `MapDelete<TCommand,TResponse>`/`MapDelete<TCommand>`, `MapPutById<TCommand,TKey,TResponse>`, `MapActionById<TCommand,TKey,TResponse>`, `MapParameterlessActionById<TCommand,TKey,TResponse>`. |
| `FluentsEntityEndpointMapperExtensions` | static class | Generic entity read/list/delete mappers backed by `IRepositorySpec`. | `MapGetById<TEntity,TKey,TModel>`/`MapGetById<TEntity,TModel>`, `MapDeleteById<TEntity,TKey>`/`MapDeleteById<TEntity>`/`MapDeleteById<TEntity,TKey,TRequest>`, `MapGetList<TEntity,TKey,TModel>`/`MapGetList<TEntity,TModel>`. |
| `EntityByIdSpecification<TEntity,TKey,TModel>`, `EntityByIdSpecification<TEntity,TKey>`, `EntityListSpecification<TEntity,TKey,TModel>` (all internal) | classes | Specifications backing the entity mappers above. | Constructors take the id or a `ListQuery<TEntity>`. |
| `ListFilter` | readonly record struct (public), implements `IParsable<ListFilter>` | One `field:operation:value` condition. | `ListFilter(string Field, Ops Operation, string Value)`; `static Parse`/`TryParse`; `ToString()` renders back to `field:operation:value`. `[JsonConverter(typeof(ListFilterJsonConverter))]`. `Ops` is the `DKNet.EfCore.Specifications` dynamic-predicate operator enum. |
| `ListFilterJsonConverter` | class (sealed) | JSON (de)serialization of `ListFilter` as its textual form. | `Read`/`Write` overrides. |
| `ListQueryOptions` | class (sealed) | Host-configurable page-size/window defaults. | `ConfigSectionName = "DKNet:ListQuery"`; `DefaultPageSize` (`int`, `[Range(1,MaxValue)]`, default `1000`); `MaxPageSize` (same range, default `1000`); `DefaultActivityWindowMonths` (`int`, `[Range(0,MaxValue)]`, default `3`). |
| `ListQueryOptionsServiceCollectionExtensions` | static class | DI registration. | `AddListQueryOptions(this IServiceCollection, Action<ListQueryOptions>?)`. |
| `ListQueryRequest` | sealed record | The `[AsParameters]`-bound query-string contract for `MapGetList`. | `PageNumber`, `PageSize` (both `int?`); `Filter` (`ListFilter[]?`); `Search` (`string?`); `OrderBy` (`string?`); `Desc` (`bool?`); `FromDate`/`ToDate` (`DateTimeOffset?`). |
| `ListQuery<TEntity>` (internal sealed record), `ListQuery` (internal static class) | record/class | Validated filter/order inputs + the validator (`TryValidate`) and predicate builders. | `TryValidate<TEntity,TModel>(ListQueryRequest, ListQueryOptions, out ListQuery<TEntity>?, out string?)`; constants `MaxFilterCount = 20`, `MinSearchLength = 2`. |
| `CrudOp` | enum (public) | The operation kinds a generated `Map{Entity}Crud` registers. | `GetById, GetList, Create, Update, Delete, Action`, in that declaration order. Source assigns no explicit numeric literals — the ordinals (`0`–`5`) are implicit from declaration order, and a regression test (`CrudMapOptionsTests`) pins all six values. |
| `CrudMapOptions` | class (sealed) | The only knob on a `DKNet.SlimBus.Generators`-emitted `Map{Entity}Crud` method. | `Exclude(params CrudOp[])`; `Exclude(params string[] routeNames)`; `IsExcluded(CrudOp)`/`IsExcluded(string)`; `Configure(CrudOp, Action<RouteHandlerBuilder>)`; `Configure(string, Action<RouteHandlerBuilder>)`; `ValidateRouteNames(string entityName, params string[] knownRouteNames)` (throws `ArgumentException` on an unknown name); `Apply(CrudOp, string routeName, RouteHandlerBuilder)`. |

### `DKNet.AspCore.Extensions.Responses`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `ErrorSource` | enum | Which failure path produced an `ErrorResponseContext`. | `Command`, `Validation`, `Unhandled`. |
| `ErrorItem` | sealed record | One error entry in the standard body. | `ErrorItem(string Message, string? Code = null, string? Field = null)`. |
| `ErrorResponseContext` | class (sealed) | What an `ErrorResponseOptions` callback sees. Carries no `HttpContext`/path/method by design. | `required ErrorSource Source`; `required IReadOnlyList<ErrorItem> Errors`; `Exception? Exception` (non-null only for `Unhandled`). |
| `ErrorResponseOptions` | class (sealed) | The one host-wide error-response setting. | `StatusCode` (`Func<ErrorResponseContext,int?>?`, default `null`); `Customize` (`Action<ProblemDetails,ErrorResponseContext>?`, default `null`); `UnhandledError` (`Func<ErrorResponseContext,ProblemDetails?>?`, default `null`). |
| `ErrorResponseServiceCollectionExtensions` | static class | `AddErrorResponses` DI entry point. | See Entry points. |
| `ErrorProblemFactory` (internal), `UnhandledErrorProblemFactory` (internal) | static classes | Build/finalize the standard `ProblemDetails` body; recompute `Type` from the final status; run `Customize` last. | `Create`, `Finalize`. |
| `ErrorResponseExceptionHandler` | class (not sealed) | `IExceptionHandler` registered by `AddErrorResponses`; open for inheritance (override `CreateProblemDetails`). | `ErrorResponseExceptionHandler(ErrorResponseOptions options)`; `TryHandleAsync(...)`; `protected virtual ProblemDetails CreateProblemDetails(HttpContext, Exception)`. |
| `ErrorResponseValidationResultFactory` (internal) | class (sealed) | Replaces SharpGrip's default validation-failure result factory. | `IResult CreateResult(EndpointFilterInvocationContext, ValidationResult)`. |
| `PagedResponse<TResult>` | sealed record | The envelope `MapGetList`/`MapGetPage` return. | `PagedResponse()` (empty); `PagedResponse(IPagedList<TResult> list)`; `Items`, `PageCount`, `PageNumber`, `PageSize`, `TotalItemCount`, `HasNextPage`, `HasPreviousPage`. |
| `ProblemDetailsExtensions` | static class | `ToProblemDetails` (internal) — the only status-choosing conversion path from a failed `IResultBase`. | `internal ToProblemDetails(this IResultBase, ErrorResponseOptions? = null)`; `internal ToErrorResponseContext(this IResultBase)`. |
| `ResultResponseExtensions` | static class | The public path from a `FluentResults` result to `IResult`. | `IResult Response<TObject>(this IResult<TObject>, bool isCreated = false)`; `IResult Response(this IResultBase, bool isCreated = false)`. |
| `ErrorResponseResult` (internal sealed) | class | Deferred `IResult` — resolves the registered `ErrorResponseOptions` from `HttpContext.RequestServices` when it executes. | `Task ExecuteAsync(HttpContext)`. |

## Options & defaults

| Option | Type | Default | Effect | Where set |
|---|---|---|---|---|
| `EndpointRegistrationOptions.RouteTemplate` | `Func<IEndpointConfig,string>?` | `null` | `null` → `/v{version:apiVersion}{GroupEndpoint}` (versioning on) or `{GroupEndpoint}` (off). | `UseEndpointConfigs(o => o.RouteTemplate = ...)` |
| `EndpointRegistrationOptions.DefaultTag` | `string` | `"Root"` | OpenAPI tag when `IEndpointConfig.Tag` resolves empty. | same |
| `EndpointRegistrationOptions.RequireAuthorization` | `bool` | `true` | Applies `RequireAuthorization()` + reads `[EndpointGroupScope]`. `false` makes the attribute inert. | same |
| `EndpointRegistrationOptions.EnableVersioning` | `bool` | `true` | Requires `AddApiVersioning()` or throws at startup. | same |
| `EndpointRegistrationOptions.ConfigureGroup` | `Action<RouteGroupBuilder,IEndpointConfig>?` | `null` | Host setup per group; runs after tags/mapping, before authorization and `IEndpointConfig.Map`. | same |
| `ErrorResponseOptions.StatusCode` | `Func<ErrorResponseContext,int?>?` | `null` | `null`/returning `null` keeps the default status (400, 404 for `NotFoundError`, 500 unhandled). | `AddErrorResponses(o => o.StatusCode = ...)` |
| `ErrorResponseOptions.Customize` | `Action<ProblemDetails,ErrorResponseContext>?` | `null` | Runs last, for all three `ErrorSource` kinds, on every route. | same |
| `ErrorResponseOptions.UnhandledError` | `Func<ErrorResponseContext,ProblemDetails?>?` | `null` | Replaces the built-in unhandled-exception body only; `null`/returning `null` keeps the built-in body. | same |
| `ListQueryOptions.DefaultPageSize` | `int` | `1000` | Page size when `pageSize` absent/`null`/`<1`. | `AddListQueryOptions` or bind `DKNet:ListQuery` |
| `ListQueryOptions.MaxPageSize` | `int` | `1000` | Hard ceiling, default included (`min(DefaultPageSize, MaxPageSize)`). | same |
| `ListQueryOptions.DefaultActivityWindowMonths` | `int` | `3` | Recent-activity window on a bare `MapGetList` request for audited types; `0` disables it. | same |

## Usage patterns

### Shared types used below

Every example in this file reuses these types, declared once:

```csharp
using DKNet.EfCore.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;

public sealed class Product : IEntity<Guid>
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal Price { get; init; }
}

public sealed record ProductModel(Guid Id, string Name, decimal Price);

public sealed record OrderModel(Guid Id, string Status);

public sealed class OrderLine
{
    public Guid Id { get; init; }
    public Guid ProductId { get; init; }
}

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
}
```

### Minimum host wiring

**When**: Any DKNet host that maps `IEndpointConfig` groups.

```csharp
using DKNet.AspCore.Extensions.Endpoints;
using DKNet.AspCore.Extensions.ModelBinding;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthorization();
builder.Services.AddApiVersioning();
builder.Services.AddContextualRequestPopulation();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseEndpointConfigs();
app.MapOpenApi();
app.Run();
```

**Notes**: `AddApiVersioning()` is required whenever `EnableVersioning` stays `true` (the default) —
`UseEndpointConfigs` throws before discovery even runs otherwise, even with zero `IEndpointConfig`
implementations found.

### An endpoint group with claim- and header-sourced request members

**When**: A command must carry the caller's identity or an idempotency key without trusting the body.

```csharp
using System.Security.Claims;
using DKNet.AspCore.Extensions;
using DKNet.AspCore.Extensions.Endpoints;
using DKNet.AspCore.Extensions.ModelBinding;
using DKNet.SlimBus.Extensions;
using Microsoft.AspNetCore.Routing;

public sealed record CreateProductCommand : Fluents.Requests.IWitResponse<ProductModel>
{
    public string Name { get; init; } = string.Empty;

    [FromClaim(ClaimTypes.NameIdentifier)]
    public string? CreatedBy { get; set; }

    [FromRequestHeader("Idempotency-Key")]
    public string? IdempotencyKey { get; set; }
}

public sealed class ProductsEndpointConfig : IEndpointConfig
{
    public string GroupEndpoint => "/products";

    public void Map(RouteGroupBuilder group) =>
        group.MapPost<CreateProductCommand, ProductModel>("/");
}
```

**Notes**: `CreatedBy` is always overwritten — even to `null` when the claim is missing — and hidden
from the published OpenAPI body. `IdempotencyKey` is filled from the header (case-insensitive match,
first value on a duplicate), published as an `in: header` parameter, and a missing header is *not* a
refusal — pair with a validator if it must be present. `POST` returns `201` because the type name
contains "Create".

### Scoped authorization per HTTP method

**When**: Different HTTP methods on one group need different authorization scopes.

```csharp
using DKNet.AspCore.Extensions;
using DKNet.AspCore.Extensions.Endpoints;
using Microsoft.AspNetCore.Routing;

[EndpointGroupScope("accounts.read")]
[EndpointGroupScope("accounts.write", EndpointHttpMethods.Post, EndpointHttpMethods.Put, EndpointHttpMethods.Delete)]
public sealed class AccountsEndpointConfig : IEndpointConfig
{
    public string GroupEndpoint => "/accounts";

    public void Map(RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", (Guid id) => Results.Ok());
        group.MapPost("/", () => Results.Ok());
        group.MapPut("/{id:guid}", (Guid id) => Results.Ok());
        group.MapDelete("/{id:guid}", (Guid id) => Results.Ok());
    }
}
```

**Notes**: `GET` falls through to the `accounts.read` default; `POST`/`PUT`/`DELETE` each need
`accounts.write`. A served method with no per-method declaration, no scope-only default, and no
route-level `RequireAuthorization`/`AllowAnonymous` fails `app.StartAsync()` — never a runtime `403`.

### Generic entity read/list/delete without a command or handler

**When**: An entity needs plain CRUD reads/deletes and no business rule beyond persistence.

```csharp
using DKNet.AspCore.Extensions;
using DKNet.AspCore.Extensions.Endpoints;
using Microsoft.AspNetCore.Routing;

public sealed class ProductsQueryEndpointConfig : IEndpointConfig
{
    public string GroupEndpoint => "/products";

    public void Map(RouteGroupBuilder group)
    {
        group.MapGetById<Product, ProductModel>("/{id:guid}");
        group.MapGetList<Product, ProductModel>("/");
        group.MapDeleteById<Product>("/{id:guid}");
    }
}
```

**Notes**: Requires `services.AddSpecRepo<TDbContext>()` (`DKNet.EfCore.Specifications`).
`MapGetList` serves a bare request with up to `ListQueryOptions.DefaultPageSize` (1,000) rows and,
if `Product` carries audit timestamps, only the last `DefaultActivityWindowMonths` (3) months.
`MapDeleteById` hard-deletes through `repo.Delete`/`SaveChangesAsync`, answering `204`/`404`/`409`
(`409` on `DbUpdateException`), with no ownership check of its own — authorization is inherited from
the group.

### Attaching a validation rule to a generic delete

**When**: A delete must be refusable (e.g. a foreign-row check) without accepting a request body.

```csharp
using DKNet.AspCore.Extensions;
using DKNet.AspCore.Extensions.Endpoints;
using DKNet.SlimBus.Extensions;
using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

public sealed record DeleteProductRequest : Fluents.Requests.IWithKey<Guid>
{
    public Guid Id { get; set; }
}

public sealed class DeleteProductRequestValidator : AbstractValidator<DeleteProductRequest>
{
    public DeleteProductRequestValidator(AppDbContext db) =>
        RuleFor(x => x.Id)
            .MustAsync(async (id, ct) => !await db.OrderLines.AnyAsync(l => l.ProductId == id, ct))
            .WithMessage("Product is still on an order and cannot be deleted.");
}

public sealed class ProductsDeleteEndpointConfig : IEndpointConfig
{
    public string GroupEndpoint => "/products";

    public void Map(RouteGroupBuilder group) =>
        group.MapDeleteById<Product, Guid, DeleteProductRequest>();
}
```

**Notes**: `TRequest` is bound `[AsParameters]` so a group-level `AddFluentValidationAutoValidation()`
filter (wired via `ConfigureGroup`) sees a validatable argument even though the caller sends no body.
A refused delete never reaches `SaveChangesAsync` — the row survives, no audit entry/domain event fires.

### Route key bound into a SlimBus command, plus a bodyless action

**When**: The command's key comes from the route, and one action takes no other input.

```csharp
using DKNet.AspCore.Extensions;
using DKNet.AspCore.Extensions.Endpoints;
using DKNet.SlimBus.Extensions;
using Microsoft.AspNetCore.Routing;

public sealed record ApproveOrderCommand
    : Fluents.Requests.IWitResponse<OrderModel>, Fluents.Requests.IWithKey<Guid>
{
    public Guid Id { get; set; }
    public string Approver { get; init; } = string.Empty;
}

public sealed record ArchiveOrderCommand
    : Fluents.Requests.IWitResponse<OrderModel>, Fluents.Requests.IWithKey<Guid>
{
    public Guid Id { get; set; }
}

public sealed class OrdersEndpointConfig : IEndpointConfig
{
    public string GroupEndpoint => "/orders";

    public void Map(RouteGroupBuilder group)
    {
        group.MapActionById<ApproveOrderCommand, Guid, OrderModel>("{id}/approval", "POST");
        group.MapParameterlessActionById<ArchiveOrderCommand, Guid, OrderModel>("{id}/archive", "PATCH");
    }
}
```

**Notes**: `MapActionById` binds `TCommand` from the body (a call with no body is `400` before the
handler runs). `MapParameterlessActionById` requires `new()` on `TCommand`, binds nothing from the
body, and dispatches on a call with no body at all — use it only when `Id` is the command's sole
member.

### One host-wide error-response setting

**When**: Mapping a failure code (or exception) to a specific status/body across the whole host.

```csharp
using DKNet.AspCore.Extensions.Responses;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddErrorResponses(o =>
{
    o.StatusCode = ctx => ctx.Errors.Any(e => e.Code == "precondition") ? 409 : null;
    o.Customize = (problem, ctx) => problem.Extensions["error-code"] = ctx.Errors.FirstOrDefault()?.Code;
    o.UnhandledError = ctx => new ProblemDetails
    {
        Status = StatusCodes.Status503ServiceUnavailable,
        Title = "Error",
        Extensions = { ["retryDelaySeconds"] = 30 }
    };
});
```

**Notes**: One call, host-wide, covers a failed command handler, refused validation input, and an
unhandled exception alike. Do **not** also call `UseExceptionHandler()` or `AddProblemDetails()` —
`AddErrorResponses` does both. `Customize` runs after `StatusCode`, for every failure kind — anything
it adds appears on every error response the host returns.

## Runtime behaviour

**`UseEndpointConfigs`, in order** (per group): resolve the route template → `app.MapGroup(...)` →
apply the shared `ApiVersionSet` (if `EnableVersioning`) → set display name/group name/tags →
`group.AddEndpointFilterFactory(...)` registering the contextual-population filter **first**, before
`ConfigureGroup`, so a host filter can never precede or defeat the unconditional member overwrite →
`options.ConfigureGroup?.Invoke(group, config)` → if `RequireAuthorization`: `group.RequireAuthorization()`
then read every `[EndpointGroupScope]` on `config`'s type and call `GroupScopeAuthorization.DeclareGroupScope`
→ `config.Map(group)`. The population filter itself, at request time, resolves scope
`IContextualRequestPopulationService` from `HttpContext.RequestServices` and calls `Populate` on every
non-null argument — but only for endpoints whose parameter types actually declare a contextual source
(checked once, at endpoint-build time, so a plain endpoint pays nothing per request).

**`GroupScopeAuthorization`, per built endpoint**: skip if the endpoint already has its own
`IAuthorizeData.Policy` or `IAllowAnonymous`; else union every `IHttpMethodMetadata.HttpMethods` entry
the endpoint carries (a route with none is treated as `"*"`); for each served method, resolve its
scope from the group's per-method map, else its `DefaultScope`, else throw `InvalidOperationException`
naming the route pattern and method; add one `AuthorizeAttribute` per distinct resolved scope.

**A `MapGetList` request**: bind `ListQueryRequest` `[AsParameters]` → `ListQuery.TryValidate<TEntity,TModel>`
(refuse `fromDate > toDate`; refuse >20 filters; build/AND each `ListFilter` via
`DynamicPredicateExtensions.TryBuildPredicate`; AND a cached, parameterized free-text search predicate
if `search` given; AND a cached, parameterized activity-window predicate if `TEntity : IAuditedProperties`
and either a bound was given or `DefaultActivityWindowMonths > 0`; validate `orderBy` against both
`TModel` and `TEntity`) → on failure, `Results.Problem(error, 400)` → on success, build
`EntityListSpecification<TEntity,TKey,TModel>` (default order: `CreatedOn` desc + `Id` desc tie-break
for `IAuditedEntity<TKey>`, else `Id` desc alone) → `repo.ToPagedListAsync(spec, pageNumber, pageSize)`
→ `Results.Ok(new PagedResponse<TModel>(page))`.

**`MapDeleteById`**: `repo.FirstOrDefaultAsync(EntityByIdSpecification)` → `404` if null → `repo.Delete(entity)`
→ `repo.SaveChangesAsync()` → `204`, or `409` on `DbUpdateException` (row survives, no audit/domain
event).

**A failure through `Response()`/`Response<T>()`**: status `400`, promoted to `404` if any error is a
`NotFoundError`; build `ErrorResponseContext` (`Source = Command`); resolve the registered
`ErrorResponseOptions` from `HttpContext.RequestServices` *when the `IResult` executes*; apply
`StatusCode` then recompute `Type` from the final status; apply `Customize` last; stamp `traceId` from
`Activity.Current?.Id` or `HttpContext.TraceIdentifier`.

**An unhandled exception**: inside a fluent-mapper-registered endpoint, caught by the
`ProducesCommons()` filter — closer to the endpoint than ASP.NET Core's Development-only exception
page, so it wins there. Everywhere else, caught by `ErrorResponseExceptionHandler` (registered by
`AddErrorResponses`). Both build the body through `UnhandledErrorProblemFactory.Create`, which reads
`IWebHostEnvironment.IsDevelopment()` to decide whether the single `ErrorItem` carries the exception's
real message or a fixed generic one. Neither handler answers an already-started response or an
aborted request.

## Diagnostics & exceptions

| Exception type | Severity | When | Fix |
|---|---|---|---|
| `InvalidOperationException` | startup | `EnableVersioning=true` but `AddApiVersioning()` never registered. | Call `builder.Services.AddApiVersioning()`, or set `EnableVersioning = false`. |
| `InvalidOperationException` | startup (endpoint-build) | A mapped endpoint's parameter type declares an `IContextualSource` member but `AddContextualRequestPopulation()` was never registered. | Register it, or remove the `[FromClaim]`/`[FromRequestHeader]` declaration. |
| `InvalidOperationException` | startup (first type scan) | A `[FromClaim]`/`[FromRequestHeader]`-declared property has no setter. | Add `set` or `init`. |
| `InvalidOperationException` | startup (`app.StartAsync()`) | A route serves an HTTP method with no scope from any `[EndpointGroupScope]` declaration and no route-level `RequireAuthorization`/`AllowAnonymous`. Message names the route pattern and method (or `*` for a route with no `IHttpMethodMetadata`). | Add a covering `[EndpointGroupScope]`, give the route its own auth rule, or stop serving the method. |
| `ArgumentException` | registration (`Map{Entity}Crud` call) | `CrudMapOptions.Exclude(string)`/`Configure(string, ...)` names a route the entity does not have. | Fix the spelling — check the entity's `[CrudUpdate]`/`[CrudAction]` method names. |
| `FormatException` | parse time | `ListFilter.Parse` given text not matching `field:operation:value` (or an unknown `Ops`). | Use `TryParse`, or fix the query string. |
| `400 BadRequest` (`ProblemDetails`) | request | `ListQuery.TryValidate` failure: too many filters (>20), unfilterable/unsortable field, search text < 2 chars, `fromDate > toDate`. | Fix the query string; only fields on `TModel` (and `TEntity`) are usable. |
| `404 NotFound` | request | `MapGetById`/`MapDeleteById` found no row; or a command failure carries a `NotFoundError`. | Expected — not a bug. |
| `409 Conflict` | request | `MapDeleteById`'s `SaveChangesAsync` threw `DbUpdateException`. | A referencing row blocks the delete — resolve the FK relationship or add a validation rule. |

## Gotchas

- **`IEndpointConfig` implementations have no DI.** `Activator.CreateInstance` requires a public
  parameterless constructor; inject services into the `Map` handlers, never into the config type.
  (`UseEndpointConfigs` discovers types via `Activator.CreateInstance`.)
- **Population only runs on groups mapped by `UseEndpointConfigs`.** A hand-written `app.MapPost(...)`
  outside a discovered group never gets the contextual-population filter, so a `[FromClaim]` property
  there keeps whatever the caller sent. (The population filter is added per-group, inside
  `UseEndpointConfigs`'s own mapping loop — never globally.)
- **Population is not validation.** An unconvertible claim/header value silently becomes the
  property's type default rather than rejecting the request. (`ContextualRequestPopulationService.Populate`'s
  conversion step catches `FormatException`/`NotSupportedException`/`ArgumentException` and falls back
  to the type's default.)
- **A custom `IContextualValueResolver` registered ahead of `AddContextualRequestPopulation()`
  *replaces* the built-in resolver for that attribute type, for every member, every request** —
  `CanResolve` keys on attribute type and only the first matching resolver is consulted
  (`resolvers.FirstOrDefault(r => r.CanResolve(...))`, in DI registration order).
- **`pageSize` is clamped, never rejected.** Asking for 5,000 rows silently returns
  `ListQueryOptions.MaxPageSize` (1,000 by default) with no signal the request was trimmed.
  (`ListQueryRequest.GetPageSize`.)
- **The default activity window narrows a bare `MapGetList` request without saying so.** A record
  older than `DefaultActivityWindowMonths` (3) is absent from both the page and `TotalItemCount`.
  Naming `fromDate`/`toDate` *replaces* the window rather than intersecting it. (`ListQuery`'s window
  builder.)
- **The activity window and the newest-first default ordering key off different interfaces.** The
  window applies to any `IAuditedProperties`; the default ordering only to `IAuditedEntity<TKey>`. A
  type with the timestamps but not the entity interface is windowed but not reordered.
- **A model with no `string` property cannot match `search`.** The predicate becomes `_ => false`
  (an empty page), not an error. Search also only walks 2 property hops (`ModelSearch`'s max depth).
- **`orderBy` is validated against both `TModel` and `TEntity`, but the 400 message names only the
  model.** A field on `TEntity` but absent from `TModel` is rejected the same as a nonexistent field.
- **A route serving several HTTP methods requires ALL scopes those methods declare** (one
  `AuthorizeAttribute` per distinct method-resolved scope, and ASP.NET Core AND-combines policies on
  one endpoint) — a route mapped for both `GET` and `POST` under two different per-method scopes
  refuses a caller holding only one.
- **`MapDeleteById` performs a hard delete with no ownership/tenancy check of its own** —
  authorization is whatever the enclosing route group requires.

## Anti-patterns & hallucination traps

- `IEndpointConfig.AuthPolicy` — **removed**. Do not write `public string? AuthPolicy => "..."`; use
  `[EndpointGroupScope("...")]` above the class instead.
- `ProblemDetailsExtensions.ToProblemDetails(this IResultBase, HttpStatusCode)` and
  `ToProblemDetails(this ModelStateDictionary)` — **retired**; the surviving overload is `internal`.
  The only public conversion path is `ResultResponseExtensions.Response()`/`Response<T>()`.
  `ResultResponseExtensions.Response(ErrorResponseOptions?, ...)` (an overload letting a caller name
  its own setting) is also retired.
- Do not hand-write a `Map{Entity}Crud` extension method — `DKNet.SlimBus.Generators` emits it from
  `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]`; hand-writing one produces a duplicate-type build error.
- Do not construct `CrudMapOptions` route-name strings from the kebab-cased URL segment — route names
  for `Configure(string, ...)`/`Exclude(string, ...)` are the C# method names (`"UpdatePrice"`), not
  `"update-price"`.
- Do not assume `[FromRequestHeader]` behaves like `[FromClaim]` for OpenAPI: a claim-declared member
  is hidden entirely; a header-declared one is published as an `in: header` parameter (the member
  itself still leaves the body schema).
- Do not treat a `[FromRequestHeader]` value as an authorization signal — it is caller-supplied and
  carries no identity guarantee, unlike a claim.
- Do not expect `MapGetList`'s default `pageSize` to be 20 — it is `ListQueryOptions.DefaultPageSize`
  (1,000); set it explicitly if a host relies on a smaller default.
- Do not call `services.AddProblemDetails()` or `app.UseExceptionHandler()` yourself alongside
  `AddErrorResponses()` — it registers both already; a second registration is redundant, not additive.
- `TKey` on the entity mappers is constrained to `IEquatable<TKey>`, not `IParsable<TSelf>` — do not
  assume a custom key type needs `IParsable` to be usable; it needs whatever minimal-API route binding
  already supports plus `IEquatable<TKey>`.

## Composes with

| Package | Rule |
|---|---|
| `DKNet.SlimBus.Extensions` | Reach for it for the `Fluents.Requests`/`Fluents.Queries` contracts the fluent mappers dispatch through `IMessageBus`, and the `FluentResults` result types `ResultResponseExtensions` converts. |
| `DKNet.SlimBus.Generators` | Emits `Map{Entity}Crud`, which composes this package's mappers and consults `CrudMapOptions`. Never hand-write what it emits — see `dknet-codegen`. |
| `DKNet.EfCore.Specifications` | Required (`AddSpecRepo<TDbContext>()`) for `MapGetById`/`MapGetList`/`MapDeleteById`; also the source of `Ops`/`DynamicPredicateExtensions` behind `ListFilter`. Reach for it directly when a query needs a shape the generic mappers don't cover. |
| `DKNet.EfCore.Abstractions` | Source of `IEntity<TKey>`/`IAuditedEntity<TKey>`/`IAuditedProperties` the entity mappers constrain on, and of the `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]` markers the generated endpoints come from. |
| `DKNet.AspCore.Idempotency` | Reach for it when a mapped endpoint must be safe for a client to retry (this package does not itself provide idempotency). |
| `DKNet.AspCore.Tasks` | Reach for it when the work runs once at application start-up rather than per request. |
| `SharpGrip.FluentValidation.AutoValidation.Endpoints` | `AddErrorResponses` swaps in its own result factory for this library's default; add validators via `AddValidatorsFromAssemblyContaining<T>()` as usual. |
| `Asp.Versioning.Http` | Backs `EnableVersioning`/`RouteTemplate`; must be registered (`AddApiVersioning()`) whenever versioning stays on. |

## Testing notes

- **Test through a real host, not builder-type assertions.** Build a `WebApplicationFactory`/
  `TestServer`, register a real (in-memory) `IMessageBus` provider, and either a real database or EF
  Core's `UseInMemoryDatabase` behind `IRepositorySpec` — these tests exercise endpoint wiring and
  dispatch, not SQL translation, so `InMemory` is appropriate here (unlike specification/SQL-translation
  tests, which must never use it).
- **Startup-time behaviour needs its own host per test.** Registration, authorization-scope, and
  versioning failures surface only when `UseEndpointConfigs()`/`app.StartAsync()` runs — build a fresh
  `WebApplicationBuilder` per test rather than sharing one host across scenarios.
- **Assert on the HTTP response, not the `IResult` object.** Verify the generic list/read mappers
  end-to-end through `HttpClient` + the JSON body (e.g. a filtered `MapGetList` call), matching how a
  real caller sees them.
