# DKNet.SlimBus.Generators

A Roslyn incremental source generator that emits a full CRUD vertical slice — request records, SlimBus
handlers, and (optionally) minimal-API endpoint registration — from `[CrudCreate]`/`[CrudUpdate]`-attributed
entity members. No hand-written command/handler/endpoint boilerplate.

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

Generated files call the C# 14 `extension(RouteGroupBuilder)` members declared by `DKNet.AspCore.Extensions`/
`DKNet.EfCore.Specifications`, so the project's `LangVersion` must be `14` or later — this repo's
`Directory.Build.props` already sets `LangVersion=latest`.

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
using MapsterMapper;

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

### Endpoint emission is opt-in

`ProductCrudEndpoints.g.cs` is only emitted when the compiling project references `DKNet.AspCore.Extensions`.
A project that only wants the requests/handlers (no ASP.NET Core dependency) gets exactly that — no unresolved
types from a forced endpoint file.

### Excluding operations

```csharp
app.MapGroup("/products").MapProductCrud(o => o.Exclude(CrudOp.Delete));
```

`CrudMapOptions.Exclude` takes any number of `CrudOp` values (`GetById`, `GetList`, `Create`, `Update`,
`Delete`); excluded operations are skipped entirely, not just hidden.

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

## Cross-assembly discovery

Entities may live in a referenced assembly (e.g. a `Domain` project) — the generator walks both the current
compilation and its referenced assembly symbols for `[CrudCreate]`/`[CrudUpdate]` members, so the API project
never needs to redeclare or re-annotate anything.

## Out of scope (by design)

- FluentValidation integration (see [Validation attributes are metadata](#validation-attributes-are-metadata-not-enforcement)).
- Generated domain events, event handlers, or Mapster configs.
- `dotnet new` scaffolding templates — nothing editable is emitted, so nothing drifts.
- PATCH/partial-update semantics.
