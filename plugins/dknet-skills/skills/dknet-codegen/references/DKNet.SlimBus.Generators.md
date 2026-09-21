# DKNet.SlimBus.Generators

| Field | Value |
|---|---|
| Area | Messaging |
| NuGet | `dotnet add package DKNet.SlimBus.Generators` — add `PrivateAssets="all" OutputItemType="Analyzer"` to the `<PackageReference>` (it is a Roslyn analyzer package, `IncludeBuildOutput=false`) |
| Docs | [DKNet.SlimBus.Generators.md](https://github.com/baoduy/DKNet/blob/main/docs/Messaging/DKNet.SlimBus.Generators.md) |
| Source | https://github.com/baoduy/DKNet/tree/dev/src/SlimBus/DKNet.SlimBus.Generators |
| Depends on (DKNet) | None via project reference — zero `<ProjectReference>`s in the project. Its **emitted code** references `DKNet.EfCore.Specifications`, `DKNet.SlimBus.Extensions`, and (conditionally) `DKNet.AspCore.Extensions` — these must be referenced by the **consuming** project. The generator's own logic separately resolves `DKNet.EfCore.Abstractions.Attributes.*`, `IEntity<TKey>`, and `DKNet.EfCore.DtoGenerator.GenerateDtoAttribute` purely by fully-qualified string name, with no real assembly reference needed. |
| Depends on (3rd party) | `Microsoft.CodeAnalysis.CSharp` (pinned to an older version than the rest of the solution on purpose — see Gotchas), `Microsoft.CodeAnalysis.Analyzers`. |
| Target framework | `netstandard2.0` (mandatory for a Roslyn generator; consuming projects target whatever they target, e.g. `net10.0`). |

## Purpose

A Roslyn incremental source generator (`[Generator]` on `CrudGenerator`) that scans a compilation (and its non-framework referenced assemblies) for entity members marked `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]`, resolves each entity against its `IEntity<TKey>` key and its `[GenerateDto]` DTO, and emits up to three C# files per entity: request records, SlimBus `IHandler<,>` implementations, and (only when `DKNet.AspCore.Extensions` is referenced) a `Map{Entity}Crud` minimal-API endpoint extension. It runs once per compilation; nothing is written to disk unless the consuming project sets `EmitCompilerGeneratedFiles`.

**NOT**: a runtime library (everything it emits calls into `DKNet.SlimBus.Extensions`/`DKNet.EfCore.Specifications`/`DKNet.AspCore.Extensions` — this package itself produces zero runtime behaviour), a validation framework, an event system, a Mapster-config generator, or a `dotnet new` scaffolder. Do not reach for it for multi-entity transactions, external calls, or branching logic — write that as an ordinary hand-written SlimBus feature instead (see `dknet-slimbus-cqrs`).

## Entry points

| Call | Signature | Applied to | Notes |
|---|---|---|---|
| `[CrudCreate]` | `[AttributeUsage(Constructor \| Method, AllowMultiple = false)] public sealed class CrudCreateAttribute : Attribute { public string? Name { get; set; } }` | public constructor or method | At most one per entity (`DKCRUDGEN003`). On a method, the generated handler still calls a **constructor** with the same parameter list — the method body never runs. |
| `[CrudUpdate]` | `[AttributeUsage(Method, AllowMultiple = false)] public sealed class CrudUpdateAttribute : Attribute { public string? Name { get; set; } }` | public instance method | One request per marked method. The first one in declaration order gets the plain `{id}` PUT. |
| `[CrudAction]` | `[AttributeUsage(Method, AllowMultiple = false)] public sealed class CrudActionAttribute(string? route = null) : Attribute { public string? Route { get; } public CrudActionVerb Verb { get; set; } = Post; public string? Name { get; set; } }` | public instance method | Always its own `{id}/{segment}` route, never the plain `{id}` route. Mutually exclusive with `[CrudUpdate]` on the same member (`DKCRUDGEN007`). |
| `[GenerateDto(typeof(TEntity))]` | from `DKNet.EfCore.DtoGenerator` — this generator only *looks it up* | `partial record` in the compiling project | Exactly one match required per entity (`DKCRUDGEN001`/`DKCRUDGEN002`). |
| `Map{Entity}Crud` | `public static RouteGroupBuilder Map{Entity}Crud(this RouteGroupBuilder group, Action<CrudMapOptions>? configure = null)` (generated, one per entity) | `RouteGroupBuilder` | Only emitted when the compiling project references `DKNet.AspCore.Extensions`. Call once per group; `configure` is optional. |
| `CrudMapOptions.Exclude` | `CrudMapOptions Exclude(params CrudOp[])` / `CrudMapOptions Exclude(params string[] routeNames)` | inside the `configure` delegate | Fluent. Nothing excluded by default. The name-form overload throws `ArgumentException` at registration if the entity has no such route. |
| `CrudMapOptions.Configure` | `CrudMapOptions Configure(CrudOp, Action<RouteHandlerBuilder>)` / `CrudMapOptions Configure(string, Action<RouteHandlerBuilder>)` | inside the `configure` delegate | Additive — multiple calls for one operation/name all run, op-kind settings first, then name settings. |

## Public surface

### `DKNet.SlimBus.Generators` (this package's own assembly — ships only as an analyzer)

| Type | Kind | Purpose |
|---|---|---|
| `CrudGenerator` | sealed class, `[Generator]`, `IIncrementalGenerator` | The generator entry point registered by the Roslyn host. |

Everything else in the assembly (`CrudDiagnostics`, `CrudModelBuilder`, `Emitter`, `CrudEntityModel`, `CrudMemberModel`, `CrudParamModel`, `CrudGenerationResult`) is `internal`, listed under Gotchas/Runtime behaviour instead because they explain what gets emitted.

### `DKNet.EfCore.Abstractions.Attributes` (a **different** package — the attribute surface a consumer actually writes against)

| Type | Kind | Key members |
|---|---|---|
| `CrudCreateAttribute` | sealed attribute | `string? Name { get; set; }` |
| `CrudUpdateAttribute` | sealed attribute | `string? Name { get; set; }` |
| `CrudActionAttribute` | sealed attribute | ctor `CrudActionAttribute(string? route = null)`; `string? Route { get; }`; `CrudActionVerb Verb { get; set; } = Post`; `string? Name { get; set; }` |
| `CrudActionVerb` | enum | `Post = 0` (default), `Put = 1`, `Patch = 2` — no `Delete` |

### Generated, per entity, into `{AssemblyName}.Crud` (or `Generated.Crud` when the compiling project has no assembly name)

| Type | Kind | Key members |
|---|---|---|
| `Create{Entity}Request` (or the `[CrudCreate].Name` override) | `sealed partial record` | Implements `Fluents.Requests.IWitResponse<TDto>`; one `required`/optional property per constructor parameter (PascalCased), with copied `DataAnnotations` attributes. |
| `{Method}{Entity}Request` (update) | `sealed partial record` | Implements `IWitResponse<TDto>` **and** `IWithKey<TKey>`; `Id` (route-bound) plus one property per method parameter. `Method` is the C# member name verbatim (`UpdatePrice` → `UpdatePriceProductRequest`). |
| `{Method}{Entity}Request` (action) | `sealed partial record` | Same shape as an update request; differs only in routing. |
| `Delete{Entity}Request` | `sealed partial record` | Implements `IWithKey<TKey>` only; `Id { get; set; }`. Emitted for every entity unless a name collision (`DKCRUDGEN010`) suppresses it. |
| `{Entity}ByIdCrudSpec` | `file`-scoped class | Derives from `DKNet.EfCore.Specifications.Definitions.Specification<TEntity>`; fetches by id for update/action handlers. Skipped when every update/action on the entity is hand-written-overridden. |
| `Create{Entity}Handler` / `{Method}{Entity}Handler` | `internal sealed class` | Ctor `(IRepositorySpec repository, IMapper mapper)`; implements `IHandler<TRequest, TDto>`; single method `Task<IResult<TDto>> OnHandle(TRequest, CancellationToken)`. Skipped per-member when a hand-written `IHandler<TRequest, ...>` already exists (`DKCRUDGEN005`, Info). |
| `{Entity}CrudEndpointExtensions` | `public static class` | Holds `Map{Entity}Crud`. Only emitted when `DKNet.AspCore.Extensions` is referenced. |

## Options & defaults

There is no options object for the generator itself — configuration is the three attributes' properties, plus the map-time `CrudMapOptions` (owned by `DKNet.AspCore.Extensions`, consulted by the generated `Map{Entity}Crud`):

| Option | Type | Default | Effect |
|---|---|---|---|
| `[CrudCreate].Name` / `[CrudUpdate].Name` / `[CrudAction].Name` | `string?` | `null` | Overrides the generated request type name (and therefore its handler name). |
| `[CrudAction].Route` (positional ctor arg) | `string?` | `null` → kebab-cased method name | Route segment appended after `{id}/`. |
| `[CrudAction].Verb` | `CrudActionVerb` | `Post` | Registered HTTP verb (`Post`/`Put`/`Patch`; no `Delete`). |
| `CrudMapOptions.Exclude(params CrudOp[])` | method | none excluded | Skips those operations entirely (not just hides them). |
| `CrudMapOptions.Exclude(params string[])` | method | none excluded | Skips the one named route; throws `ArgumentException` if the name doesn't exist on the entity. |
| `CrudMapOptions.Configure(CrudOp, Action<RouteHandlerBuilder>)` | method | no-op | Applies a setting to every route of that kind, additive. |
| `CrudMapOptions.Configure(string, Action<RouteHandlerBuilder>)` | method | no-op | Applies a setting to the one named route, additive. |

## Usage patterns

### A full create + update + action vertical slice, with zero hand-written request/handler/endpoint code

```csharp
// Domain project
using System.ComponentModel.DataAnnotations;
using DKNet.EfCore.Abstractions.Attributes;
using DKNet.EfCore.Abstractions.Entities;

namespace Catalog;

public sealed class Product : Entity
{
    private Product()
    {
    } // EF

    [CrudCreate]
    public Product([Required, MaxLength(100)] string name, decimal price) : base(Guid.NewGuid())
    {
        Name = name;
        Price = price;
    }

    public string Name { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public bool IsApproved { get; private set; }
    public bool IsArchived { get; private set; }

    [CrudUpdate]
    public void UpdatePrice([Range(0, 1_000_000)] decimal price) => Price = price;

    [CrudAction("approval")]
    public void Approve([Required] string approvedBy) => IsApproved = true;

    [CrudAction(Verb = CrudActionVerb.Patch)]
    public void Archive() => IsArchived = true;
}
```

```csharp
// API project
using Catalog;
using DKNet.EfCore.DtoGenerator;

namespace Api;

[GenerateDto(typeof(Product))]
public partial record ProductDto;

public sealed class AppDbContext(Microsoft.EntityFrameworkCore.DbContextOptions<AppDbContext> options)
    : Microsoft.EntityFrameworkCore.DbContext(options)
{
    public Microsoft.EntityFrameworkCore.DbSet<Product> Products => Set<Product>();
}
```

```csharp
// Program.cs
using Api;
using DKNet.EfCore.Specifications;
using Generated.Crud;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.Serialization.SystemTextJson;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IMapper>(new Mapper(new TypeAdapterConfig()));
builder.Services.AddDbContext<AppDbContext>();
builder.Services.AddScoped<DbContext>(p => p.GetRequiredService<AppDbContext>());
builder.Services.AddSpecRepo<AppDbContext>();

builder.Services
    .AddSlimBusEfCoreInterceptor<AppDbContext>()
    .AddSlimMessageBus(mbb => mbb
        .AddJsonSerializer()
        .AddServicesFromAssembly(typeof(Program).Assembly)
        .AddChildBus("Memory", mb => mb.WithProviderMemory().AutoDeclareFrom(typeof(Program).Assembly)));

var app = builder.Build();
app.MapGroup("/products").MapProductCrud();
app.Run();
```

`CreateProductRequest` gets `Name` (`required string`) and `Price` (`required decimal`). `UpdatePriceProductRequest` gets `Id` (route-bound) and `Price`; it maps `PUT {id}` because it is the first `[CrudUpdate]` member. `ApproveProductRequest` maps `POST {id}/approval` with a body (`ApprovedBy`); `Archive` takes no parameters, so `ArchiveProductRequest` maps via `MapParameterlessActionById` at `PATCH {id}/archive` and accepts a request with **no** body and no `Content-Type` at all — a body sent anyway is accepted and ignored. `DeleteProductRequest` is also emitted automatically, with no handler. All of this lands in namespace `{AssemblyName}.Crud` — substitute your own compiled assembly's name for `{AssemblyName}` (it falls back to `Generated.Crud` only when the project has none).

### Excluding and configuring routes at mapping time

```csharp
using Api;
using DKNet.AspCore.Extensions.Endpoints;
using Generated.Crud;
using Microsoft.AspNetCore.Builder;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Drop the DELETE route entirely.
app.MapGroup("/products-a").MapProductCrud(o => o.Exclude(CrudOp.Delete));

// Require a scope on just the UpdatePrice route, and a broader scope on every Update-kind route.
app.MapGroup("/products-b").MapProductCrud(o => o
    .Configure(CrudOp.Update, b => b.RequireAuthorization("product.write"))
    .Configure("UpdatePrice", b => b.RequireAuthorization("product.price")));
```

A route name passed to `Exclude(string[])`/`Configure(string, ...)` that the entity does not have throws `ArgumentException` at registration, so a typo fails loudly instead of silently doing nothing.

### Overriding a generated handler with hand-written logic

```csharp
// Same namespace the generator emits requests/handlers into for this project: {AssemblyName}.Crud
// (Generated.Crud here because this sample project has no assembly name — use your own project's
// assembly name followed by ".Crud" in a real app).
namespace Generated.Crud;

using System.Threading;
using Api;
using Catalog;
using DKNet.EfCore.Specifications.Definitions;
using DKNet.EfCore.Specifications.Extensions;
using DKNet.EfCore.Specifications.Repositories;
using DKNet.SlimBus.Extensions;
using FluentResults;
using MapsterMapper;

internal sealed class ProductByIdSpec : Specification<Product>
{
    public ProductByIdSpec(Guid id) => WithFilter(x => x.Id == id);
}

internal sealed class UpdatePriceProductHandler(IRepositorySpec repository, IMapper mapper)
    : Fluents.Requests.IHandler<UpdatePriceProductRequest, ProductDto>
{
    public async Task<IResult<ProductDto>> OnHandle(UpdatePriceProductRequest request, CancellationToken cancellationToken)
    {
        // Custom logic replaces the generated fetch-by-id + UpdatePrice(...) call; the request
        // record itself is still generated, only this handler is skipped by the generator.
        var entity = await repository.FirstOrDefaultAsync(new ProductByIdSpec(request.Id), cancellationToken);
        if (entity is null)
            return Result.Fail<ProductDto>(new NotFoundError($"Product '{request.Id}' was not found."));

        entity.UpdatePrice(request.Price);
        await repository.UpdateAsync(entity, cancellationToken);
        return Result.Ok(mapper.Map<ProductDto>(entity));
    }
}
```

Matching is by the **request type's simple name only** (found via a syntax scan of base lists, since the generated type doesn't exist yet as a symbol) — the hand-written handler's DTO type argument is never cross-checked against the generated one, so getting it wrong compiles but fails at dispatch. The request record itself is still generated regardless of the override; only the handler is skipped (`DKCRUDGEN005`, Info).

### Refusing a delete via validation on the generated `Delete{Entity}Request`

```csharp
using Api;
using FluentValidation;
using Generated.Crud;
using Microsoft.EntityFrameworkCore;

namespace Api.Validators;

public sealed class DeleteProductRequestValidator : AbstractValidator<DeleteProductRequest>
{
    public DeleteProductRequestValidator(AppDbContext db) =>
        RuleFor(x => x.Id)
            .MustAsync(async (id, ct) => !await db.Products.AnyAsync(p => p.Id == id && p.IsApproved, ct))
            .WithMessage("Approved products cannot be deleted.");
}
```

```csharp
using DKNet.AspCore.Extensions.Endpoints;
using FluentValidation;
using Generated.Crud;
using Microsoft.AspNetCore.Builder;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddValidatorsFromAssemblyContaining<Api.Validators.DeleteProductRequestValidator>();

var app = builder.Build();
var products = app.MapGroup("/products");
products.MapProductCrud();
products.AddFluentValidationAutoValidation(); // only groups that opt in consult the validator
```

The DELETE route never gets a SlimBus handler — it goes straight to `IRepositorySpec` via `MapDeleteById`. A refused delete never reaches `SaveChanges`, so no audit entry or domain event fires for it.

## Runtime behaviour

The generator itself runs at **compile time only**. What runs at request time is the code it emitted, composed with other packages:

1. **Create**: the generated `MapPost<TRequest, TDto>("/")` (from `DKNet.AspCore.Extensions`) binds the request body, dispatches over SlimBus to `Create{Entity}Handler.OnHandle`, which does `new {Entity}(request.…)` then `await repository.AddAsync(entity, cancellationToken)` and returns a **lazily** mapped `IResult<TDto>` — not yet materialized.
2. **Update/Action**: the generated `MapPutById`/`MapActionById`/`MapParameterlessActionById` binds `Id` from the route (and the body, for update/action-with-parameters), dispatches to `{Method}{Entity}Handler.OnHandle`, which does `await repository.FirstOrDefaultAsync(new {Entity}ByIdCrudSpec(request.Id), cancellationToken)`; a miss returns a `NotFoundError` (mapped to 404), a hit calls the marked entity method then returns the same lazy result.
3. **Persistence happens after the handler returns**: `AddSlimBusEfCoreInterceptor<TDbContext>()` calls `SaveChanges` once the handler completes successfully — the generated handler itself never calls `SaveChanges`.
4. Because the DTO mapping is lazy and runs after save, database-generated values (identity keys, timestamps) are present in the HTTP response.
5. **Domain events** raised inside the marked entity method (`AddEvent(...)`) are dispatched by `DKNet.EfCore.Events` during that same `SaveChanges`, and published to the bus via `AddSlimBusEventPublisher<TDbContext>()` — none of this is generated by this package; it is ordinary domain code reached through the save pipeline.
6. **Delete**: no SlimBus involvement at all — `MapDeleteById` calls `IRepositorySpec` directly; `204`/`404`/`409` as usual.

## Diagnostics & exceptions

| ID or exception | Severity | When | Fix |
|---|---|---|---|
| `DKCRUDGEN001` | Error | Entity has CRUD-attributed members but no `[GenerateDto(typeof(Entity))]` DTO in the compiling project. | Add exactly one `[GenerateDto(typeof(Entity))] public partial record EntityDto;`. |
| `DKCRUDGEN002` | Error | More than one `[GenerateDto(typeof(Entity))]` DTO for the entity. | Keep exactly one. |
| `DKCRUDGEN003` | Error | More than one member marked `[CrudCreate]` on one entity. | Keep exactly one `[CrudCreate]` member. |
| `DKCRUDGEN004` | Error | A CRUD-attributed member is not public. | Make the constructor/method `public`. |
| `DKCRUDGEN005` | Info | A hand-written `IHandler<TRequest, ...>` was found for a generated request; the generated handler was skipped. | Informational only — confirms the override took effect. |
| `DKCRUDGEN006` | Error | The entity does not implement `DKNet.EfCore.Abstractions.Entities.IEntity<TKey>`. | Implement `IEntity<TKey>` (directly, or via `Entity`/`AuditedEntity` — DKNet ships no separate `AggregateRoot` type). |
| `DKCRUDGEN007` | Error | A member is marked both `[CrudUpdate]` and `[CrudAction]`. | Keep exactly one attribute; the member is emitted as neither until fixed. |
| `DKCRUDGEN008` | Error | Two members resolve to the same route segment. | Give one an explicit distinct `[CrudAction("...")]` segment. |
| `DKCRUDGEN009` | Error | Two routes on the entity resolve to the same route name (including colliding with the reserved `GetById`/`GetList`/`Create`/`Delete`). | Rename one of the colliding members. |
| `DKCRUDGEN010` | Info | A CRUD member already claims the name `Delete{Entity}Request`. | Rename the member if a delete rule is needed for that entity; otherwise informational — DELETE still works via the 2-generic-argument `MapDeleteById<TEntity, TKey>()`. |
| `ArgumentException` (thrown at runtime, not a generator diagnostic) | n/a | `CrudMapOptions.ValidateRouteNames` finds a name passed to `Exclude(string[])`/`Configure(string, ...)` that the entity has no route for. | Fix the route name — check the entity's `[CrudUpdate]`/`[CrudAction]` method names. |

## Gotchas

- **`[CrudCreate]` on a method never runs that method's body.** `Emitter.AppendCreateHandler` always emits `new {Entity}(request.Param1, ...)` using the marked member's parameter list as a constructor call, regardless of whether the marked member is itself the constructor or a factory method. Mark the constructor, not a factory method.
- **Handler-override matching is by name only, not by type.** `CrudGenerator.FindHandWrittenHandlerOverrides` scans syntax for a base-list `IHandler<X, Y>` and keys purely on `X`'s simple name; `Y` (the DTO) is never checked. A hand-written handler with the wrong second type argument silently wins and fails at dispatch instead of at build.
- **The generator has zero project references** — it resolves `DKNet.EfCore.Abstractions.Attributes.CrudCreateAttribute` etc. purely by fully-qualified string name. If a consumer's project defines its own type with that exact full name (unlikely, but possible in a test harness), it is indistinguishable to the generator from the real attribute.
- **`Microsoft.CodeAnalysis.CSharp` is pinned below the solution-wide package version on purpose** — the generator must reference the *lowest* Roslyn it needs, because an IDE's `csc` host can be older than the SDK's (`CS9057` otherwise). Do not "fix" this during a routine package-version bump.
- **The assembly-skip filter (`CrudGenerator.ShouldScan`) is a name-prefix check, not a wildcard.** It skips an assembly only when its name equals, or is dot-prefixed by, one of `System`, `Microsoft`, `netstandard`, `mscorlib`, `FluentResults`, `Mapster`, `SlimMessageBus`, `Shouldly`, `xunit` — so `Systemic.Domain` or `MicrosoftX.Domain` are still scanned. This is intended, not a bug — worth knowing because a plausible-looking assembly named e.g. `System.Custom` would be silently skipped.
- **An action's route never falls on `{id}`, no matter its verb or declaration order** — only `[CrudUpdate]`'s *first* member (by declaration order) gets the plain `{id}` route; every action is always `{id}/{segment}`. Confusing an action for a same-named update produces a different route than expected.
- **A parameterless `[CrudAction]` genuinely requires no body and no `Content-Type`**, while an action with even one nullable parameter still requires *a* body — just an optional field inside it.
- **Nullability, not the rendered type text, decides `required`.** `CrudGenerator.BuildParamModel` checks `NullableAnnotation == NullableAnnotation.Annotated` (or `Nullable<T>`); in a **nullable-disabled** compilation context every parameter is treated as required, even a reference type that "looks" nullable by convention.
- **DELETE silently changes shape when a name collision occurs.** If any create/update/action member already resolves to `Delete{Entity}Request` as its own request name (e.g. `[CrudUpdate] public void Delete()`), the entity's DELETE route falls back from the 3-generic-argument `MapDeleteById<TEntity, TKey, TRequest>()` to the 2-argument `MapDeleteById<TEntity, TKey>()` — any delete-rule validator registered against `Delete{Entity}Request` for that entity simply never runs, with only an Info diagnostic (`DKCRUDGEN010`) marking it.

## Anti-patterns & hallucination traps

- Hand-writing `Create{Entity}Request`/`{Method}{Entity}Request`/`Delete{Entity}Request`, the `IHandler<...>` classes, or `Map{Entity}Crud` for an entity that already has the matching `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]` — produces duplicate-type build errors. There is no partial-merge story for these; only the handler-override mechanism (keyed by name) is supported.
- Calling `SaveChanges`/`SaveChangesAsync` inside a generated-shape handler override — the generated handlers never do this; persistence is the auto-save interceptor's job, which runs after `OnHandle` returns.
- Expecting `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]` to raise or wire domain events — they don't; `AddEvent(...)` inside the marked entity method is ordinary domain code, unrelated to this generator.
- Expecting a `CrudGeneratorOptions`, `AddCrudGenerators()`, or any DI/`IServiceCollection` registration for this package — there is none; the only "configuration" is the three attributes plus `CrudMapOptions` at map time. This package is a `PackageReference` with `OutputItemType="Analyzer"`, never something you `AddXyz()` into a container.
- Assuming `[Range]`/`[Required]`/other `DataAnnotations` on a marked parameter are enforced at the HTTP boundary — they are copied onto the generated request property as metadata only; minimal APIs run no automatic `DataAnnotations` validation by themselves (pair with FluentValidation's auto-validation for that).
- Assuming the create response's `Location` header points at the created resource — it is always the placeholder `/`.
- Assuming `[CrudAction(Verb = CrudActionVerb.Patch)]` implies partial-update/merge semantics — `Verb` only changes the advertised HTTP method; there is no PATCH semantics behind it.
- Guessing the CRUD attributes' namespace as `DKNet.SlimBus.Generators.Attributes` — they live in `DKNet.EfCore.Abstractions.Attributes` (kept there deliberately so the domain layer takes on no messaging dependency).
- Assuming `201 Created` always follows a POST create route — `MapPost` returns `201` only when the request type's name contains `"Create"` case-insensitively; overriding `Name` on `[CrudCreate]` to something without "Create" silently downgrades the response to `200`.

## Composes with

| Package | Rule |
|---|---|
| `DKNet.EfCore.Abstractions` | Always — home of `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]`/`CrudActionVerb` and `IEntity<TKey>`, which every attributed entity must implement. |
| `DKNet.EfCore.DtoGenerator` | Always, for the `[GenerateDto(typeof(Entity))]` DTO this generator resolves against — declare the DTO, never hand-write it. |
| `DKNet.EfCore.Specifications` | Always at runtime — supplies `IRepositorySpec` and `Specification<TEntity>`, which every generated handler and the by-id spec derive from/call. `AddSpecRepo<TDbContext>()` must be registered. |
| `DKNet.SlimBus.Extensions` | Always at runtime — the request/handler contracts (`Fluents.Requests.IWitResponse<TDto>`, `IWithKey<TKey>`, `IHandler<,>`), `NotFoundError`, the lazy-mapping helpers, and `AddSlimBusEfCoreInterceptor<TDbContext>()` (auto-save) / `AddSlimMessageBus(...)` (dispatch). Reach for it directly to hand-write any feature this generator doesn't cover, in the same shape. See `dknet-slimbus-cqrs`. |
| `DKNet.AspCore.Extensions` | Only if you want the generated `Map{Entity}Crud` endpoint file — supplies `MapGetById`/`MapGetList`/`MapDeleteById`/`MapPost`/`MapPutById`/`MapActionById`/`MapParameterlessActionById` and `CrudMapOptions`/`CrudOp`. Omit the reference for a request/handler-only slice with no ASP.NET Core dependency. See `dknet-aspcore-api`. |
| Mapster (`MapsterMapper.IMapper`) | Always at runtime — register `IMapper` for the generated handlers to inject. |

## Testing notes

Test a generated CRUD slice through real HTTP, not by mocking the generated pipeline: build a real `WebApplication`/`TestServer`, wire `AddSpecRepo<TDbContext>()`, `AddSlimBusEfCoreInterceptor<TDbContext>()`, and `AddSlimMessageBus(...)` with an in-memory transport (`WithProviderMemory()`), back the `DbContext` with a real (if lightweight) provider, and hit the mapped routes with `HttpClient`/`GetTestClient()`, asserting on status code and JSON body. Do not mock `IRepositorySpec` — the point of the generated slice is the whole pipeline (request → handler → repository → save → lazy DTO mapping), and a mock repository proves nothing about routing, model binding, or the auto-save interceptor. For fixture patterns (in-memory hosts, TestContainers, ARM64 fallbacks) shared across DKNet-based test suites, see the `dknet-testing` skill.
