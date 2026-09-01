# DKNet.SlimBus.Generators

[![NuGet](https://img.shields.io/nuget/v/DKNet.SlimBus.Generators)](https://www.nuget.org/packages/DKNet.SlimBus.Generators/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DKNet.SlimBus.Generators)](https://www.nuget.org/packages/DKNet.SlimBus.Generators/)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](https://github.com/baoduy/DKNet/blob/main/LICENSE)

A Roslyn incremental source generator that emits a full CRUD vertical slice — request records, SlimBus
handlers, and (optionally) minimal-API endpoint registration — from
`[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]`-attributed entity members. No hand-written command/handler/endpoint
boilerplate.

## Install

```xml
<ItemGroup>
  <PackageReference Include="DKNet.SlimBus.Generators" Version="{latest}" PrivateAssets="all" OutputItemType="Analyzer" />
</ItemGroup>
```

The compiling project also needs, for the generated code to compile and run:

- `DKNet.SlimBus.Extensions` — request/handler contracts (`IWitResponse<TDto>`, `IWithKey<TKey>`, `IHandler<,>`),
  `NotFoundError`, and the LazyMapper (`LazyMapExtensions.ResultOf`) the generated handlers call.
- `DKNet.EfCore.Specifications` — `IRepositorySpec` (persistence) and the by-id specification base type
  generated update handlers derive from.
- `DKNet.AspCore.Extensions` — only if you want the endpoint-registration file; see
  [Endpoint emission is opt-in](#endpoint-emission-is-opt-in) below.
- `Mapster` — the `IMapper` implementation (namespace `MapsterMapper`) generated handlers inject.

Generated files call extension members that `DKNet.AspCore.Extensions` and `DKNet.EfCore.Specifications`
declare with C# 14 `extension(...)` blocks. *Calling* them does not require C# 14 — a consumer at
`LangVersion` 13 compiles the generated files fine; only declaring such a block does.

## Quick start

```csharp
// Domain project — the ONLY hand-written feature code
public class Product : Entity
{
    [CrudCreate]
    public Product(string name, decimal price) { ... AddEvent<ProductCreated>(); }

    [CrudUpdate]
    public void UpdatePrice([Range(0, 1_000_000)] decimal price) { ... AddEvent<PriceChanged>(); }
}

// API project
[GenerateDto(typeof(Product))]
public partial record ProductDto;                             // existing generator

app.MapGroup("/products").MapProductCrud();                   // generated extension
```

This emits, into the compiling (API) project:

- `ProductCrudRequests.g.cs` — `CreateProductRequest` (`IWitResponse<ProductDto>`) and
  `UpdatePriceProductRequest` (`IWitResponse<ProductDto>` + `IWithKey<TKey>`).
- `ProductCrudHandlers.g.cs` — an `internal sealed` `IHandler<TRequest, ProductDto>` per request. The create
  handler invokes the `[CrudCreate]` constructor and `IRepositorySpec.AddAsync`; the update handler fetches
  the entity by id (404 via `NotFoundError` when missing) and invokes the `[CrudUpdate]` method. Both return
  `mapper.ResultOf<ProductDto>(entity)` — persistence happens afterward via the SlimBus EF Core auto-save
  interceptor, so handlers never call `SaveChanges`.
- `ProductCrudEndpoints.g.cs` — `MapProductCrud(this RouteGroupBuilder, Action<CrudMapOptions>? configure = null)`,
  composing the existing `FluentsEntityEndpointMapperExtensions`/`FluentsEndpointMapperExtensions` mappers.

### Minimum consumer wiring

```csharp
using DKNet.EfCore.Specifications;   // AddSpecRepo
using Mapster;                       // TypeAdapterConfig
using MapsterMapper;                 // IMapper, Mapper

builder.Services.AddSingleton<IMapper>(new Mapper(new TypeAdapterConfig()));
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString));
builder.Services.AddScoped<DbContext>(p => p.GetRequiredService<AppDbContext>());
builder.Services.AddSpecRepo<AppDbContext>();                 // DKNet.EfCore.Specifications

builder.Services
    .AddSlimBusEfCoreInterceptor<AppDbContext>()               // auto-save after a successful write
    .AddSlimMessageBus(mbb => mbb
        .AddJsonSerializer()
        .AddServicesFromAssembly(typeof(Program).Assembly)     // discovers generated + hand-written handlers
        .AddChildBus("Memory", mb => mb.WithProviderMemory().AutoDeclareFrom(typeof(Program).Assembly)));
```

## Attributes

| Attribute | Valid on | Members | Effect |
|---|---|---|---|
| `[CrudCreate]` | a public constructor or method, at most one per entity | `Name` (`string?`, default `null`) | Emits `Create{Entity}Request` (constructor) or `{Method}{Entity}Request` (method) plus a handler that calls the entity's **constructor** with the marked member's parameter list — a marked factory method's body is never executed, so prefer marking the constructor. `Name` overrides the request type name. |
| `[CrudUpdate]` | any public instance method | `Name` (`string?`, default `null`) | Emits `{Method}{Entity}Request` implementing `IWithKey<TKey>` plus a fetch-by-id handler. |
| `[CrudAction]` | any public instance method | `Route` (positional `string?`), `Verb` (`CrudActionVerb`, default `Post`), `Name` (`string?`) | Same shape as `[CrudUpdate]`, but always at its own `{id}/{segment}` route. |

Delete needs no attribute — `MapDeleteById` covers it generically. All three attributes live in
`DKNet.EfCore.Abstractions`, so the domain layer takes on no messaging dependency.

## Routes

`Map{Entity}Crud` registers, on the group it's called on:

| Operation | Route | Status |
|---|---|---|
| GET by id | `{id}` | 200 / 404 |
| GET list (paged) | `/` | 200 |
| POST create | `/` | 201 + DTO body (`Location` header currently a placeholder, `/`) |
| PUT — first `[CrudUpdate]` | `{id}` | 200 + DTO body, or 404 |
| PUT — additional `[CrudUpdate]`s | `{id}/{kebab-case-method-name}` | 200 + DTO body, or 404 |
| DELETE by id | `{id}` | 200 / 404 |

Additional `[CrudUpdate]` methods are routed in declaration order; the first one keeps the plain `{id}` PUT,
every one after gets its method name kebab-cased onto the route (e.g. `UpdatePrice` → `{id}/update-price`).
Kebab-casing inserts a `-` before every upper-case character after the first, so `ExportXML` becomes
`export-x-m-l`. Registration order inside `Map{Entity}Crud` is fixed: `GetById`, `GetList`, `Delete`, `Create`,
each `[CrudUpdate]` in declaration order, then each `[CrudAction]` in declaration order.

### Generated names

| Thing | Rule |
|---|---|
| Namespace | `{AssemblyName}.Crud`, or `Generated.Crud` when the compilation has no assembly name |
| Requests file | `{Entity}CrudRequests.g.cs` |
| Handlers file | `{Entity}CrudHandlers.g.cs`, omitted when every member has a hand-written handler |
| Endpoints file | `{Entity}CrudEndpoints.g.cs`, emitted only when the project references `DKNet.AspCore.Extensions` |
| Create request | `Create{Entity}Request` for a constructor, `{Method}{Entity}Request` for a method |
| Update / action request | `{Method}{Entity}Request` |
| Handler | the request name with a trailing `Request` replaced by `Handler` |
| By-id specification | `{Entity}ByIdCrudSpec`, `file`-scoped, one per handlers file |
| Endpoint extension | `{Entity}CrudEndpointExtensions.Map{Entity}Crud` |

`Name` on any of the three attributes overrides the request type name (and therefore the handler name).

### Domain actions — `[CrudAction]`

A method marked `[CrudAction]` is a named operation on the entity, published at its own `{id}/{segment}`
route — it never claims the plain `{id}` route, whatever verb it uses:

```csharp
[CrudAction("approval")]
public void Approve([Required] string approver) { ... AddEvent<OrderApproved>(); }

[CrudAction(Verb = CrudActionVerb.Patch)]
public void Archive() { ... }
```

| Constructor arg / property | Meaning | Default |
|---|---|---|
| `Route` (positional) | The route segment appended after `{id}/`. | Kebab-cased method name (e.g. `Archive` → `archive`). |
| `Verb` | The registered HTTP verb — `Post`, `Put`, or `Patch`. `Delete` is not supported. | `Post` |
| `Name` | Overrides the generated request type name. | `{Method}{Entity}Request` |

| Operation | Route | Status |
|---|---|---|
| `[CrudAction]` (default `Post`) | `{id}/{segment}` | 200 + DTO body, or 404 |
| `[CrudAction(Verb = Put)]` | `{id}/{segment}` | 200 + DTO body, or 404 |
| `[CrudAction(Verb = Patch)]` | `{id}/{segment}` | 200 + DTO body, or 404 |

`[CrudAction]` vs. `[CrudUpdate]` with `Verb = Put`: an update replaces state and, positionally, may claim the
plain `{id}` route; an action is always a named operation at its own segment and is never positional — it
never lands on `{id}` regardless of declaration order or verb. `Verb = Patch` only changes the advertised HTTP
method — there is no partial-update or merge semantics behind it.

### Endpoint emission is opt-in

`ProductCrudEndpoints.g.cs` is only emitted when the compiling project references `DKNet.AspCore.Extensions`.
A project that only wants the requests/handlers (no ASP.NET Core dependency) gets exactly that — no unresolved
types from a forced endpoint file.

### Excluding operations

```csharp
app.MapGroup("/products").MapProductCrud(o => o.Exclude(CrudOp.Delete));
```

`CrudMapOptions.Exclude` takes any number of `CrudOp` values (`GetById`, `GetList`, `Create`, `Update`,
`Delete`, `Action`); excluded operations are skipped entirely, not just hidden.

## Validation attributes are metadata, not enforcement

`System.ComponentModel.DataAnnotations` attributes on a `[CrudCreate]`/`[CrudUpdate]` member's parameters are
copied verbatim onto the generated request's properties (e.g. `[Range(0, 1_000_000)]` above lands on
`UpdatePriceProductRequest.Price`). They are **not** automatically enforced at the HTTP boundary — minimal API
does not run DataAnnotations validation on its own. If you need wire-level 400s for invalid input, add your own
validation (e.g. a FluentValidation validator plus `SharpGrip.FluentValidation.AutoValidation`, or a minimal-API
endpoint filter) against the generated request type.

## Overrides — hand-written handlers win

If the compiling project already declares a type implementing `IHandler<TRequest, TDto>` for a generated
request, the generator skips that request's handler and reports `DKCRUDGEN005` (Info) at the hand-written
type. The request record itself is still generated. Matching is by the **request type's name** — the
hand-written type's declared response/DTO type argument isn't cross-checked, so a hand-written handler with the
wrong second type argument still silently wins; that's on you to get right.

To keep the generated record but supply your own logic, write:

```csharp
internal sealed class UpdatePriceProductHandler(IRepositorySpec repo, IMapper mapper)
    : IHandler<UpdatePriceProductRequest, ProductDto>
{
    public async Task<IResult<ProductDto>> OnHandle(UpdatePriceProductRequest request, CancellationToken ct)
    {
        // custom logic instead of the generated one
    }
}
```

Anything beyond CRUD is a normal hand-written SlimBus feature — generated and hand-written handlers are the
same shape, so mixing them is seamless.

## Diagnostics

| Id | Severity | Meaning |
|---|---|---|
| `DKCRUDGEN001` | Error | Entity has `[CrudCreate]`/`[CrudUpdate]` members but no `[GenerateDto(typeof(Entity))]` DTO was found in the compiling project. |
| `DKCRUDGEN002` | Error | More than one `[GenerateDto(typeof(Entity))]` DTO was found for the entity — designate exactly one. |
| `DKCRUDGEN003` | Error | More than one member on the entity is marked `[CrudCreate]`; only one is allowed. |
| `DKCRUDGEN004` | Error | A member marked `[CrudCreate]`/`[CrudUpdate]` is not public. |
| `DKCRUDGEN005` | Info | A hand-written `IHandler<TRequest, ...>` was found for a generated request; the generated handler was skipped. |
| `DKCRUDGEN006` | Error | The entity does not implement `DKNet.EfCore.Abstractions.Entities.IEntity<TKey>`. |
| `DKCRUDGEN007` | Error | A member is marked both `[CrudUpdate]` and `[CrudAction]`; keep exactly one — the member is emitted as neither. |
| `DKCRUDGEN008` | Error | Two members on the entity resolve to the same route segment; give one an explicit distinct segment. |

## Cross-assembly discovery

Entities may live in a referenced assembly (e.g. a `Domain` project) — the generator walks both the current
compilation and its referenced assembly symbols for `[CrudCreate]`/`[CrudUpdate]` members, so the API project
never needs to redeclare or re-annotate anything.

## Documentation

Full feature reference, the declaration-versus-emitted-code walkthrough, the compile-time flow diagram, and the
naming and routing conventions in full:
https://github.com/baoduy/DKNet/blob/main/docs/Messaging/DKNet.SlimBus.Generators.md

## Out of scope (by design)

- FluentValidation integration (see [Validation attributes are metadata](#validation-attributes-are-metadata-not-enforcement)).
- Generated domain events, event handlers, or Mapster configs.
- `dotnet new` scaffolding templates — nothing editable is emitted, so nothing drifts.
- PATCH/partial-update semantics.
