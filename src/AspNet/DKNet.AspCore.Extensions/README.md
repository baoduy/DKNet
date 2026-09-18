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
- **Header-sourced request members** — `[FromRequestHeader("Idempotency-Key")]` fills a request
  property from a named HTTP request header through that same mechanism and the same
  `AddContextualRequestPopulation()` registration. The header is published as an `in: header`
  operation parameter (a claim-filled member is hidden instead), a missing header is never a
  refusal, and — unlike a claim — a header is supplied by the caller, so it is a binding
  convenience, never an authorization signal.
- **Endpoint group discovery** — implement `IEndpointConfig` per feature area and let
  `UseEndpointConfigs()` discover, version, tag, and authorize every group across your assemblies.
- **Fluent minimal-API mappers** — `MapPost`/`MapPut`/`MapPatch`/`MapDelete`/`MapGet`/`MapGetPage`
  wire a verb straight onto a SlimMessageBus fluent command/query, plus generic `MapGetById`/
  `MapGetList`/`MapDeleteById` backed by `DKNet.EfCore.Specifications`.
- **`PagedResponse<T>`** — a shared paging envelope (`PageNumber`, `PageSize`, `PageCount`,
  `TotalItemCount`, `Items`, `HasNextPage`, `HasPreviousPage`) used by every paged endpoint.
- **Result/ProblemDetails conversion** — `Response()`/`Response<T>()` turn a `FluentResults`
  outcome into the right minimal-API `IResult`, and are the only public path onto the standard error
  body. They resolve the host's registered error-response setting themselves, so an endpoint never
  has to name it — and cannot skip it.
- **One error-response setting** — `AddErrorResponses()` registers a single `ErrorResponseOptions`
  that shapes all three ways a request can fail: a failed command handler, input a validator refused,
  and an unhandled exception. One body shape for all three, one status rule set, and no second
  registration — the call wires the exception handling for you.

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

    [FromRequestHeader("Idempotency-Key")]
    public string? IdempotencyKey { get; set; } // always resolved from the request header, never from the body
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

`IdempotencyKey` is filled the same way, from the `Idempotency-Key` request header — the same
registration, no route-level code. Header-name matching is case-insensitive, and a header sent
more than once fills the member with the first value sent. Three things to know before you rely
on it:

- **A missing header is not a refusal.** The member holds its type's default and the request is
  still dispatched. Requiring a header stays the job of a filter or a validator.
- **A header-filled member takes `SystemAccountFallback`** wherever a claim-filled one would — a
  fallback is configured and the group's `RequireAuthorization` is `false`. There, an absent header
  leaves a *constant* value rather than an empty one, which matters if you use the member as an
  idempotency key.
- **A header is never an authorization signal.** Unlike a claim, a header is supplied by the
  caller. The declaration is a binding convenience; a service that treated it as proof of identity
  would be trusting the caller.

Unlike `[FromClaim]`, the header is advertised in the published OpenAPI operation as an
`in: header` parameter — the caller has to know to send it — while the member itself stays absent
from the published request body.

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

`FromRequestHeaderAttribute` is new in this release rather than moved, and ships in
`DKNet.AspCore.Extensions.ModelBinding` alongside `FromClaimAttribute`.

## Customisation reference

Four options types, plus the type parameters and attributes that make up the rest of the public
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
| `RequireAuthorization` | `bool` | `true` | Applies `RequireAuthorization(config.AuthPolicy)` to every group. Turning it off is also what enables `SystemAccountFallback`, and makes `[EndpointGroupScope]` (below) inert — no attribute read, no scope enforced, no startup refusal. |
| `EnableVersioning` | `bool` | `true` | Adds the version prefix and API-version metadata. Requires `AddApiVersioning()`, or `UseEndpointConfigs` throws at startup — even with zero discovered configs. |
| `ConfigureGroup` | `Action<RouteGroupBuilder, IEndpointConfig>?` | `null` | Host setup per group. Runs after tags/version metadata, before authorization and before `IEndpointConfig.Map`. |
| `assemblies` (method parameter) | `params Assembly[]` | empty → every currently loaded assembly | Assemblies scanned for `IEndpointConfig` implementations. |

`ErrorResponseOptions` — `services.AddErrorResponses(Action<ErrorResponseOptions>?)`. The one place a
host shapes error responses, covering all three failure kinds: a failed command handler, refused
validation input, and an unhandled exception. It is the host's only step — do **not** add a
`UseExceptionHandler()` line or an `AddProblemDetails()` call of your own; `AddErrorResponses` makes
both for you, and calling it twice is a no-op (the first call's `configure` wins). Every knob is
host-wide, applying to every route:

| Knob | Type | Default | Effect |
|---|---|---|---|
| `StatusCode` | `Func<ErrorResponseContext, int?>?` | `null` | Chooses the status from the failure's own errors. The context carries no `HttpContext`, path or HTTP method, so the route cannot influence it. Returning `null`, or leaving this unset, keeps the status the failure would have had anyway — `400`, `404` when it carries a `NotFoundError`, or `500` for an unhandled exception. Wins over the status `UnhandledError` set when both return non-null. |
| `Customize` | `Action<ProblemDetails, ErrorResponseContext>?` | `null` | Adds members to the `ProblemDetails` after its status is chosen, for `ErrorSource.Command`, `ErrorSource.Validation` and `ErrorSource.Unhandled` alike — a member added here is never on one failure kind only, and appears on every error response the host returns. Name each member you add; nothing is added for you. |
| `UnhandledError` | `Func<ErrorResponseContext, ProblemDetails?>?` | `null` | Supplies the body for an unhandled exception in place of the library's own. Leaving it `null`, or returning `null` from it, keeps the library's body. Has no effect on a command or validation failure. |
| `configure` (method parameter) | `Action<ErrorResponseOptions>?` | `null` | Leave `null` to keep every knob unset — each unset knob is a no-op, so the standard body and its default statuses apply as-is. Skipping the call entirely leaves the setting unregistered; the mappers resolve it as an optional service, and nothing then handles an unhandled exception. |

Each callback receives an `ErrorResponseContext`: `Source` (`Command`, `Validation` or `Unhandled`),
`Errors` — a list of `ErrorItem(Message, Code?, Field?)` — and `Exception`, the raised exception for
`Unhandled` only. `Code` is the FluentResults error's `"Code"` metadata entry for a command failure and
the validation failure's `ErrorCode` for refused input; `Field` names the refused input member and is
always `null` otherwise.

All three failure kinds answer with the same body:

```json
{
  "title": "Error",
  "status": 409,
  "type": "Conflict",
  "traceId": "00-...-00",
  "errors": [ { "message": "...", "code": "precondition", "field": null } ]
}
```

`type` is always the final response status' name, recomputed after `StatusCode` runs — it never names
an exception type. There is no `detail` member. Outside the `Development` environment an unhandled
exception's `errors` carries one fixed message and nothing the exception itself carried; inside
`Development` that entry carries the exception's own message.

An exception raised inside an endpoint one of the fluent mappers registered is caught by the endpoint
filter `ProducesCommons()` adds; every other unhandled exception is caught by the `IExceptionHandler`
`AddErrorResponses` registers. An endpoint registered without `ProducesCommons()` is covered in
`Production`, but in `Development` still answers with ASP.NET Core's developer exception page — chain
`.ProducesCommons()` onto it to put it on the same path.

Mapping a failure code to a status, and supplying your own body for an unhandled exception:

```csharp
using DKNet.AspCore.Extensions.Responses;
using Microsoft.AspNetCore.Mvc;

builder.Services.AddErrorResponses(o =>
{
    o.StatusCode = ctx => ctx.Errors.Any(e => e.Code == "precondition") ? 409 : null;

    o.UnhandledError = ctx => new ProblemDetails
    {
        Status = StatusCodes.Status503ServiceUnavailable,
        Title = "Error",
        Extensions = { ["retryDelaySeconds"] = 30 }
    };
});
```

`IEndpointConfig` — what each implementation supplies:

| Member | Default | Effect |
|---|---|---|
| `GroupEndpoint` | none — required | Route segment after the version prefix, e.g. `"/products"`. |
| `Map(RouteGroupBuilder)` | none — required | Where the group's endpoints are registered. |
| `AuthPolicy` | `null` | Policy name for the group; null/empty means authentication with no policy. Ignored when `RequireAuthorization` is `false`. |
| `Tag` | `GroupEndpoint` with `/` → `-`, leading `-` trimmed | OpenAPI tag. Empty falls back to `DefaultTag`. |
| `Version` | `1` | API version, and the `v{n}` in the route. |

`ListQueryOptions` — `services.AddListQueryOptions(Action<ListQueryOptions>?)`, or bind the
`DKNet:ListQuery` configuration section with `services.Configure<ListQueryOptions>(…)`. Global to the
host, not per endpoint:

| Knob | Type | Default | Effect |
|---|---|---|---|
| `DefaultPageSize` | `int` | `1000` | Page size used when `pageSize` is absent, null or below 1. |
| `MaxPageSize` | `int` | `1000` | Ceiling every page is subject to, `DefaultPageSize` included; an oversized request is served trimmed, never rejected. |
| `DefaultActivityWindowMonths` | `int` | `3` | How many months back a listing of audited records reaches when the caller names neither `fromDate` nor `toDate`. `0` switches the default window off, restoring an unbounded listing. Ignored for listed types that carry no audit timestamps. |
| `ConfigSectionName` | `const string` | `"DKNet:ListQuery"` | Section the options are meant to bind from. |

`AddListQueryOptions` binds no configuration itself — it adds
`ValidateDataAnnotations().ValidateOnStart()`, so a value below 1 fails the host at start-up. Both
calls are optional: with neither, the defaults above apply.

`ListQueryRequest` — the query string every `MapGetList` endpoint accepts:

| Parameter | Type | Default | Effect |
|---|---|---|---|
| `pageNumber` | `int?` | page 1 | One-based; anything below 1 is the first page. |
| `pageSize` | `int?` | `1000` | `ListQueryOptions.DefaultPageSize` when absent, null or below 1; clamped to `ListQueryOptions.MaxPageSize` (1,000 by default), silently. |
| `filter` | `ListFilter[]?` | none | Repeatable `field:operation:value`, AND-combined, at most 20 per request. Operations: `Equal`, `NotEqual`, `GreaterThan`, `GreaterThanOrEqual`, `LessThan`, `LessThanOrEqual`, `Contains`, `NotContains`, `StartsWith`, `EndsWith`, `In`, `NotIn`, `IsNull`, `IsNotNull`. `In`/`NotIn` take a comma-separated list; `IsNull`/`IsNotNull` take no value. |
| `search` | `string?` | none | Free-text match across the returned model's text fields, minimum 2 characters. |
| `orderBy` | `string?` | endpoint default | Field on the returned model to sort by. |
| `desc` | `bool?` | `false` | Sort descending; ignored without `orderBy`. |
| `fromDate` | `DateTimeOffset?` | last 3 months | Lower bound on when a record was last active. |
| `toDate` | `DateTimeOffset?` | open-ended | Upper bound on when a record was last active. |

Only fields the returned model declares can be filtered, searched or sorted; anything else is
rejected with `400` rather than silently ignored.

`fromDate`/`toDate` bound the listing by activity: a record is in range when either its `CreatedOn`
or its `UpdatedOn` moment falls inside the bounds, so a record edited recently stays listed however
old it is, and a record never updated is matched on `CreatedOn` alone. Naming either bound replaces
the default window entirely and is open-ended on the side left out — `fromDate=0001-01-01T00:00:00Z`
is how a caller asks for all history. `fromDate` later than `toDate` is a `400`. Listed types that
carry no audit timestamps ignore both bounds rather than rejecting them.

Attributes and other extension points:

| Knob | Kind | Default | Effect |
|---|---|---|---|
| `[EndpointGroupScope(scope, params httpMethods)]` | class attribute above an `IEndpointConfig`, `AllowMultiple` | none | Requires `scope` as the authorization policy for every route the group serves under `httpMethods` (constants on `EndpointHttpMethods`, e.g. `EndpointHttpMethods.Get`). Stack one attribute per scope. A route's own `RequireAuthorization(...)` or `AllowAnonymous()` wins over the group's declaration. A served method with no declaration and no route-level rule fails the host at startup, naming the route and the method. No effect when `RequireAuthorization` is `false`. |
| `[FromClaim(claimType)]` | property attribute, one required ctor argument | none | Populates the property from that claim before validation and before the handler; always overwrites the caller's value, and is removed from the published OpenAPI description. The property needs a `set` or `init` or startup throws. |
| `[FromRequestHeader(headerName)]` | property attribute, one required ctor argument (`HeaderName`) | none | Populates the property from that HTTP request header before validation and before the handler; always overwrites the caller's body value. Header-name matching is case-insensitive, and a header sent more than once fills the member with the first value. A missing header is not a refusal — the member holds its type's default, or `SystemAccountFallback` where that applies. The header *is* published as an `in: header` operation parameter (the member stays out of the published body), and it is never an authorization signal — the caller supplies it. The property needs a `set` or `init` or startup throws. |
| `IContextualSource` | marker interface on your own attribute | — | Opts a new source kind into the same mechanism. |
| `IContextualValueResolver` | interface you register in DI | `ClaimValueResolver` for `[FromClaim]` | `CanResolve` selects the resolver; the mechanism never switches on a concrete attribute type. |
| `CrudMapOptions.Exclude(params CrudOp[])` | builder method | nothing excluded | Skips operations in the generated `Map{Entity}Crud`. `CrudOp` is `GetById`, `GetList`, `Create`, `Update`, `Delete`, `Action`. |
| `CrudMapOptions.Configure(CrudOp, Action<RouteHandlerBuilder>)` | builder method | no settings | Applies a `RouteHandlerBuilder` setting (e.g. `RequireAuthorization`) to every generated route of that operation kind. Additive and chainable. |
| `CrudMapOptions.Configure(string, Action<RouteHandlerBuilder>)` | builder method | no settings | Same, for the one route carrying that name. Operation-kind settings run first, name settings after; an unknown name throws `ArgumentException` at registration. Route names are the generator's — see [DKNet.SlimBus.Generators](https://github.com/baoduy/DKNet/blob/main/docs/Messaging/DKNet.SlimBus.Generators.md#naming-and-routing-conventions). |

## Full Documentation

See the [full feature guide](https://github.com/baoduy/DKNet/blob/main/docs/AspNetCore/DKNet.AspCore.Extensions.md)
for contextual-source resolvers, `EndpointRegistrationOptions`, all fluent mappers, paging
details, and gotchas.

## License

MIT — see [LICENSE](https://github.com/baoduy/DKNet/blob/main/LICENSE).

## About

Developed by [Steven Hoang](https://drunkcoding.net).
