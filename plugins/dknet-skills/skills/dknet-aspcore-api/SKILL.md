---
name: dknet-aspcore-api
description: Covers DKNet.AspCore.Extensions and DKNet.AspCore.Tasks. Use when writing an IEndpointConfig group, calling UseEndpointConfigs for minimal-API endpoint discovery and DKNet API versioning, mapping verbs with MapGet/MapPost/MapPut/MapPatch/MapDelete/MapPutById/MapActionById/MapParameterlessActionById, building a generic list endpoint with MapGetList/MapGetById/MapDeleteById plus PagedResponse and ListFilter, converting a FluentResults Result to ProblemDetails via .Response()/.Response<T>() or AddErrorResponses, scoping authorization per verb with [EndpointGroupScope], populating [FromClaim]/[FromRequestHeader] members via AddContextualRequestPopulation, or registering IBackgroundTask with AddBackgroundJob/AddBackgroundJobFrom to run work once at host start-up.
license: MIT
metadata:
  author: baoduy
  version: "0.1.0"
  packages: "DKNet.AspCore.Extensions,DKNet.AspCore.Tasks"
---

# DKNet minimal-API endpoint conventions and start-up tasks

This skill teaches the two packages that turn a DKNet host's minimal APIs into routed, versioned,
authorized endpoints, and give it a place to run one-shot start-up work: `DKNet.AspCore.Extensions`
(`IEndpointConfig` groups, the verb-to-SlimBus mappers, the generic entity mappers, `[FromClaim]`/
`[FromRequestHeader]`, and the host-wide error shape) and `DKNet.AspCore.Tasks` (`IBackgroundTask`).
Open `references/DKNet.AspCore.Extensions.md` or `references/DKNet.AspCore.Tasks.md` for the full
member tables, options, runtime order and gotchas behind any call below.

## Packages

| Package | Install | What it gives you | Depends on | Reference |
|---|---|---|---|---|
| `DKNet.AspCore.Extensions` | `dotnet add package DKNet.AspCore.Extensions` | `IEndpointConfig` discovery + versioning (`UseEndpointConfigs`), verb-to-SlimBus mappers, generic entity list/read/delete endpoints, `Result`→`ProblemDetails` (`.Response()`), `[FromClaim]`/`[FromRequestHeader]`, one host-wide error shape (`AddErrorResponses`) | `DKNet.SlimBus.Extensions`, `DKNet.EfCore.Specifications` | `references/DKNet.AspCore.Extensions.md` |
| `DKNet.AspCore.Tasks` | `dotnet add package DKNet.AspCore.Tasks` | Run-once-at-start-up jobs (`IBackgroundTask`, `AddBackgroundJob`/`AddBackgroundJobFrom`) | none | `references/DKNet.AspCore.Tasks.md` |

## Quick start

```csharp
using DKNet.AspCore.Extensions;
using DKNet.AspCore.Extensions.Endpoints;
using DKNet.EfCore.Abstractions.Entities;
using DKNet.SlimBus.Extensions;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

public sealed class Product : IEntity<Guid>
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal Price { get; init; }
}

public sealed record ProductModel(Guid Id, string Name, decimal Price);

public sealed record CreateProductCommand : Fluents.Requests.IWitResponse<ProductModel>
{
    public string Name { get; init; } = string.Empty;
    public decimal Price { get; init; }
}

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
}

public sealed class ProductsEndpointConfig : IEndpointConfig
{
    public string GroupEndpoint => "/products";

    public void Map(RouteGroupBuilder group)
    {
        group.MapPost<CreateProductCommand, ProductModel>("/");
        group.MapGetById<Product, ProductModel>("/{id:guid}");
        group.MapGetList<Product, ProductModel>("/");
        group.MapDeleteById<Product>("/{id:guid}");
    }
}
```

```csharp
using DKNet.AspCore.Extensions.Endpoints;
using DKNet.AspCore.Extensions.ModelBinding;
using DKNet.AspCore.Extensions.Responses;
using DKNet.EfCore.Specifications;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthorization();
builder.Services.AddApiVersioning();
builder.Services.AddContextualRequestPopulation();
builder.Services.AddErrorResponses();
builder.Services.AddSpecRepo<AppDbContext>();

var app = builder.Build();

app.UseEndpointConfigs();
app.Run();
```

`UseEndpointConfigs()` with no assembly argument scans every currently loaded assembly, finds
`ProductsEndpointConfig` by its public parameterless constructor, and maps `POST /v1/products`
(dispatched through SlimBus) plus the three generic entity routes (served straight from
`IRepositorySpec`, no handler involved) — versioned, tagged, and authorized in one call.

## Rules

1. **Route, then read.** Pick the fluent command mapper or the generic entity mapper before writing a
   handler by hand — see "How to" below and each package's reference for the full overload list.
2. `EnableVersioning` defaults to `true`. Call `AddApiVersioning()` before `UseEndpointConfigs()` runs,
   or startup throws `InvalidOperationException` — even with zero `IEndpointConfig` types discovered.
3. `IEndpointConfig` implementations get no DI — `Activator.CreateInstance` needs a public
   parameterless constructor. Inject services into `Map`'s lambda handlers, never into the config type.
4. A `[FromClaim]`/`[FromRequestHeader]` property must have a `set` or `init` accessor, or the first
   endpoint-build scan throws `InvalidOperationException`.
5. `[FromClaim]`/`[FromRequestHeader]` always overwrite the bound value — even to `null` when the
   source is missing — and this is not validation: an unconvertible value becomes the property's type
   default, never a rejected request.
6. The contextual-population filter only runs on groups mapped through `UseEndpointConfigs()`. A
   hand-written `app.MapPost(...)` outside a discovered group never populates those members.
7. `MapGetById`/`MapGetList`/`MapDeleteById` require `services.AddSpecRepo<TDbContext>()` from
   `DKNet.EfCore.Specifications` — they go straight to `IRepositorySpec`, no command or handler.
8. `MapGetList`'s default page size is `ListQueryOptions.DefaultPageSize` (1,000), not 20, and an
   oversized `pageSize` is silently clamped to `MaxPageSize`, never rejected.
9. Never hand-write a `Map{Entity}Crud` method, or the request/handler/endpoint behind a
   `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]` — `DKNet.SlimBus.Generators` emits it; a hand-written
   duplicate is a build error. See `dknet-codegen`.
10. Call `AddErrorResponses()` once, host-wide. Never also call `UseExceptionHandler()` or
    `AddProblemDetails()` yourself — `AddErrorResponses` already does both.
11. Every registered `IBackgroundTask` runs from **one shared** DI scope, **concurrently**, once per
    host start-up (every restart/redeploy re-runs it). Never rely on registration order, and open a
    private `IServiceScopeFactory` scope before touching a scoped, non-thread-safe dependency such as
    a `DbContext`.
12. A thrown `IBackgroundTask.RunAsync` is logged and swallowed — it never retries and never stops the
    host or the other tasks. Design `RunAsync` to be safe to re-run from a partial failure
    (existence-check/upsert), not a blind insert.
13. `AddBackgroundJobFrom` takes exactly `Assembly[]`, not `params Assembly[]`. Pass a collection
    expression: `AddBackgroundJobFrom([asm1, asm2])`.

## How to

### Discover and version endpoint groups with a scoped assembly scan

**When**: a host loads many assemblies and `UseEndpointConfigs()`'s default (scan everything loaded)
is wider than you want, or a group needs a non-default tag.

```csharp
using System.Reflection;
using DKNet.AspCore.Extensions.Endpoints;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApiVersioning();

var app = builder.Build();

app.UseEndpointConfigs(o =>
{
    o.DefaultTag = "Catalog";
    o.ConfigureGroup = (group, config) => group.WithTags(config.Tag);
}, Assembly.GetExecutingAssembly());

app.Run();
```

**Notes**: `ConfigureGroup` runs after tagging/mapping but before authorization and `Map(group)`, so
it can decorate every discovered group the same way without touching each `IEndpointConfig`.

### Scope authorization per HTTP verb on one group

**When**: different verbs on the same route group need different authorization scopes.

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

**Notes**: `GET` falls through to the scope-only default (`accounts.read`); the other three each
need `accounts.write`. A served method with no covering scope and no route-level
`RequireAuthorization`/`AllowAnonymous` fails `app.StartAsync()` — never a runtime `403`.

### Populate a request member from the caller's claim or a header

**When**: a command needs the caller's identity or an idempotency key without trusting the body.

```csharp
using System.Security.Claims;
using DKNet.AspCore.Extensions;
using DKNet.AspCore.Extensions.Endpoints;
using DKNet.AspCore.Extensions.ModelBinding;
using DKNet.SlimBus.Extensions;
using Microsoft.AspNetCore.Routing;

public sealed record OrderReceipt(Guid Id);

public sealed record CreateOrderCommand : Fluents.Requests.IWitResponse<OrderReceipt>
{
    [FromClaim(ClaimTypes.NameIdentifier)]
    public string? PlacedBy { get; set; }

    [FromRequestHeader("Idempotency-Key")]
    public string? IdempotencyKey { get; set; }
}

public sealed class OrdersCreateEndpointConfig : IEndpointConfig
{
    public string GroupEndpoint => "/orders";

    public void Map(RouteGroupBuilder group) =>
        group.MapPost<CreateOrderCommand, OrderReceipt>("/");
}
```

**Notes**: requires `AddContextualRequestPopulation()` registered (Rule 4/6), or endpoint-build throws.
`PlacedBy` is hidden from the OpenAPI body entirely; `IdempotencyKey` is published as an `in: header`
parameter, and a missing header does not by itself refuse the request — pair it with a validator.

### Hand-write an endpoint outside the fluent mappers, but keep the same response shape

**When**: a route's shape doesn't fit any fluent/entity mapper, but it must still fail the same way
every mapped endpoint does.

```csharp
using DKNet.AspCore.Extensions.Endpoints;
using DKNet.AspCore.Extensions.Responses;
using DKNet.SlimBus.Extensions;
using Microsoft.AspNetCore.Routing;
using SlimMessageBus;

public sealed record ProductSummary(Guid Id, string Name, int OrderCount);

public sealed record GetProductSummaryQuery(Guid Id) : Fluents.Requests.IWitResponse<ProductSummary>;

public static class ProductsSummaryEndpoint
{
    public static void MapProductSummary(this RouteGroupBuilder group) =>
        group.MapGet("/{id:guid}/summary", async (IMessageBus bus, Guid id) =>
            {
                var rs = await bus.Send(new GetProductSummaryQuery(id));
                return rs.Response();
            })
            .Produces<ProductSummary>()
            .ProducesCommons();
}
```

**Notes**: call `group.MapProductSummary()` from an `IEndpointConfig.Map`, same as any DKNet mapper.
The fluent/entity mappers already call `.Response()` and `.ProducesCommons()` internally — reach for
this pattern only when nothing else fits. Skipping `.ProducesCommons()` still answers correctly (via
`ErrorResponseExceptionHandler`) but drops the route's 400/401/403/404/409/429/500 `Produces` metadata.

### Set one host-wide error-response shape

**When**: mapping a failure code, or an unhandled exception, to a specific status/body across the
whole host — once, not per endpoint.

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
    };
});
```

**Notes**: covers a failed command handler, refused validation input, and an unhandled exception
alike (`ErrorSource.Command`/`Validation`/`Unhandled`). `Customize` runs last, for every kind, on
every route — do not also call `UseExceptionHandler()`/`AddProblemDetails()` (Rule 10).

### Page, filter and sort a generic list endpoint

**When**: tuning `MapGetList`'s defaults, or building the query string a caller sends to it.

```csharp
using DKNet.AspCore.Extensions.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddListQueryOptions(o =>
{
    o.DefaultPageSize = 50;
    o.MaxPageSize = 200;
    o.DefaultActivityWindowMonths = 0; // 0 disables the audited-entity activity window
});
```

```http
GET /v1/products?pageNumber=2&pageSize=50&filter=Price:GreaterThan:100&search=widget&orderBy=Name&desc=true
```

**Notes**: `filter` repeats as `field:operation:value`; more than 20 filters or a `search` under 2
characters is a `400`. The response body is `PagedResponse<TModel>`: `Items`, `PageCount`,
`PageNumber`, `PageSize`, `TotalItemCount`, `HasNextPage`, `HasPreviousPage`.

### Run one-shot work exactly once per host start-up

**When**: seeding reference data, warming a cache, or any job that must run once when the host boots.

```csharp
using DKNet.AspCore.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public sealed class SeedCatalogTask(ILogger<SeedCatalogTask> logger) : IBackgroundTask
{
    public Task RunAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Seeding catalog reference data");
        return Task.CompletedTask;
    }
}
```

```csharp
using DKNet.AspCore.Tasks;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBackgroundJob<SeedCatalogTask>();
```

**Notes**: runs concurrently with every other registered task, from one shared DI scope, on every
host start (Rule 11) — make `RunAsync` idempotent. A thrown exception is logged and swallowed; it
never stops the host or retries (Rule 12).

## Runtime behaviour

**Per group, when `UseEndpointConfigs()` runs**: the route template resolves → the group is mapped,
versioned and tagged → the contextual-population endpoint filter is attached *first* → your
`ConfigureGroup` callback runs → authorization is applied (`RequireAuthorization()` +
`[EndpointGroupScope]`) → `IEndpointConfig.Map(group)` runs last. At request time, `[FromClaim]`/
`[FromRequestHeader]` members are overwritten before the handler runs, unconditionally, but only for
parameter types that actually declare one.

**Per host start, for `DKNet.AspCore.Tasks`**: `BackgroundJobHost` opens one shared DI scope, resolves
every registered `IBackgroundTask`, and runs `RunAsync` on all of them concurrently via
`Task.WhenAll`. The host reports "started" as soon as this hits its first `await` — a request can be
served before every task finishes. A thrown exception is logged and swallowed, never rethrown.

## Gotchas

- **`UseEndpointConfigs` needs `AddApiVersioning()` even to map zero endpoints.** `EnableVersioning`
  defaults to `true`, and the check runs before discovery — an empty `IEndpointConfig` set still
  throws without it.
- **Population is not validation.** An unconvertible or missing `[FromClaim]`/`[FromRequestHeader]`
  value becomes the property's type default, never a rejected request — pair it with a validator if
  the value must be present.
- **Population only fires on groups mapped through `UseEndpointConfigs()`.** A hand-written
  `app.MapPost(...)` outside a discovered group never gets the filter.
- **`MapGetList`'s activity window silently narrows results.** Audited entities default to the last
  `DefaultActivityWindowMonths` (3 months) — a caller who doesn't know this gets a page that looks
  wrong, not an error. Naming `fromDate`/`toDate` replaces the window, it does not intersect it.
- **`BackgroundJobHost` shares one DI scope across every task, running them concurrently.** Two tasks
  that both inject a scoped `DbContext` directly get the *same instance* in parallel — open a private
  `IServiceScopeFactory` scope instead.
- **A registered `IBackgroundTask` re-runs on every host start, not once ever.** A blind-insert seed
  task duplicates rows on the next deploy — make `RunAsync` idempotent.
- **A failing `IBackgroundTask` is logged and swallowed, never rethrown.** Do not expect the host to
  retry, or a caller to see the failure.

## Do not

- `IEndpointConfig.AuthPolicy` — does not exist. Use `[EndpointGroupScope("...")]` above the class.
- `ProblemDetailsExtensions.ToProblemDetails(...)` has no public overload — the only public
  conversion is `ResultResponseExtensions.Response()`/`Response<T>()`.
- Do not hand-write a `Map{Entity}Crud` method — `DKNet.SlimBus.Generators` emits it from
  `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]` (Rule 9; see `dknet-codegen`):

  ```csharp
  // no-compile
  public sealed class ProductsEndpointConfig : IEndpointConfig
  {
      public string? AuthPolicy => "products.read"; // removed API — use [EndpointGroupScope] instead
      public string GroupEndpoint => "/products";
      public void Map(RouteGroupBuilder group) { }
  }
  ```

- `AddBackgroundJob<TJob>(options => ...)` and any `IConfiguration`-binding overload do not exist —
  `DKNet.AspCore.Tasks` ships no options type at all:

  ```csharp
  // no-compile
  builder.Services.AddBackgroundJob<SeedCatalogTask>(options =>
  {
      options.MaxDegreeOfParallelism = 4; // AddBackgroundJob takes no configuration delegate
  });
  ```

- `services.AddBackgroundJobs(...)` (plural) — the real names are `AddBackgroundJob<TJob>()` and
  `AddBackgroundJobFrom(Assembly[])`.
- `AddBackgroundJobFrom(asm1, asm2)` — the parameter is a single `Assembly[]`, not `params`; pass
  `AddBackgroundJobFrom([asm1, asm2])`.
- There is no `IBackgroundTaskScheduler`, `BackgroundJobOptions`, or `CronBackgroundTask` — this
  package has no scheduling concept, only "run once at start-up."
- Do not call `services.AddProblemDetails()`/`app.UseExceptionHandler()` alongside
  `AddErrorResponses()` — it already registers both; a second registration is redundant, not additive.
- Do not expect `MapGetList`'s default `pageSize` to be 20 — it is `ListQueryOptions.DefaultPageSize`
  (1,000).

## Related skills

- `dknet-packages` — start there for which DKNet package solves a scenario, and the install/setup
  order across the whole suite.
- `dknet-codegen` — go there for `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]` and the
  `Map{Entity}Crud` method they generate; never hand-write it here.
- `dknet-slimbus-cqrs` — go there to write the `IRequestHandler`/`Fluents.Requests.IHandler<...>`
  behind a command this skill's mappers dispatch.
- `dknet-efcore-specifications` — go there for `IRepositorySpec`/`AddSpecRepo<TDbContext>()` itself,
  and for a query shape the generic entity mappers don't cover.
- `dknet-efcore-domain-model` — go there for the `IEntity<TKey>`/`IAuditedEntity<TKey>`/
  `IAuditedProperties` base contracts the entity mappers constrain on.
- `dknet-idempotency` — go there when a mapped endpoint must be safe for a client to retry; this
  skill's packages don't provide that themselves.
- `dknet-testing` — go there for TestContainers fixtures and test conventions when testing inside the
  DKNet repo itself, rather than a consumer app.

## References

- `references/DKNet.AspCore.Extensions.md` — full member tables, options, runtime order, gotchas and
  anti-patterns for `IEndpointConfig`, the fluent/entity mappers, `ListQuery`/`PagedResponse`,
  `[FromClaim]`/`[FromRequestHeader]`, and `AddErrorResponses`.
  Docs: https://github.com/baoduy/DKNet/blob/dev/docs/AspNetCore/DKNet.AspCore.Extensions.md ·
  NuGet: https://www.nuget.org/packages/DKNet.AspCore.Extensions
- `references/DKNet.AspCore.Tasks.md` — full member tables, the `BackgroundJobHost` execution model,
  and the shared-scope/idempotent-rerun gotchas for `IBackgroundTask`.
  Docs: https://github.com/baoduy/DKNet/blob/dev/docs/AspNetCore/DKNet.AspCore.Tasks.md ·
  NuGet: https://www.nuget.org/packages/DKNet.AspCore.Tasks
- General DKNet docs: https://baoduy.github.io/DKNet/
