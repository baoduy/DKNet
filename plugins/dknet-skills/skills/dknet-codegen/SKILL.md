---
name: dknet-codegen
description: Covers DKNet.EfCore.DtoGenerator's [GenerateDto(typeof(Entity))] generator (Include/Exclude/IgnoreComplexType, DtoGeneratorExclusions, DtoGeneratorIgnoreComplexType, DKDTOGEN001-005) plus its RaisesEventValidator for [RaisesEvent] (DKRAISEVT001-011); and DKNet.SlimBus.Generators' CrudGenerator, turning [CrudCreate]/[CrudUpdate]/[CrudAction] (DKNet.EfCore.Abstractions.Attributes) into request records, IHandler<,> implementations and Map{Entity}Crud endpoints (DKCRUDGEN001-010). Use when a source generator is not generating, a build reports a duplicate type generated, a generated request/handler/endpoint is missing, EmitCompilerGeneratedFiles is needed for .g.cs output, or before hand-writing what an attribute already emits. Also clarifies [FromClaim] is not a generator — it's runtime model binding in DKNet.AspCore.Extensions.ModelBinding (see dknet-aspcore-api).
license: MIT
metadata:
  author: baoduy
  version: "0.1.0"
  packages: "DKNet.EfCore.DtoGenerator,DKNet.SlimBus.Generators"
---

# DKNet source generators: DTOs and CRUD vertical slices

Two Roslyn generators, both compile-time only, both zero runtime footprint. `DKNet.EfCore.DtoGenerator` mirrors an entity into a flat DTO from `[GenerateDto]`, and separately validates/generates `[RaisesEvent]` payloads. `DKNet.SlimBus.Generators` turns `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]` into request records, SlimBus handlers, and an optional minimal-API endpoint file. Read `references/DKNet.EfCore.DtoGenerator.md` and `references/DKNet.SlimBus.Generators.md` for the full API surface, and `references/diagnostics.md` for a sorted index of every diagnostic ID either one can report.

## Packages

| Package | Install | What it gives you | Depends on | Reference |
|---|---|---|---|---|
| `DKNet.EfCore.DtoGenerator` | `dotnet add package DKNet.EfCore.DtoGenerator` — add `PrivateAssets="all" OutputItemType="Analyzer"` | `[GenerateDto(typeof(Entity))]` → flat DTO properties; `[RaisesEvent]` build-time validation, and payload generation for its convention forms | None at the DKNet level (source-links `EventNameComposer` instead of referencing `DKNet.EfCore.Abstractions`) | [references/DKNet.EfCore.DtoGenerator.md](references/DKNet.EfCore.DtoGenerator.md) |
| `DKNet.SlimBus.Generators` | `dotnet add package DKNet.SlimBus.Generators` — add `PrivateAssets="all" OutputItemType="Analyzer"` | `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]` → request records + `IHandler<,>` handlers + (optionally) `Map{Entity}Crud` | Zero project references of its own; its **emitted code** needs the consuming project to also reference `DKNet.EfCore.Abstractions`, `DKNet.EfCore.DtoGenerator`, `DKNet.EfCore.Specifications`, `DKNet.SlimBus.Extensions`, and optionally `DKNet.AspCore.Extensions` | [references/DKNet.SlimBus.Generators.md](references/DKNet.SlimBus.Generators.md) |

Both are analyzer-only packages: no `IServiceCollection` extension, no runtime DLL a consumer loads. `PrivateAssets="all"` keeps them from flowing to anyone who references your project; `OutputItemType="Analyzer"` is what makes MSBuild run them as generators instead of compiling them as ordinary references.

## Quick start

The smallest project that turns one entity into a working DTO and a full CRUD vertical slice — create, update, list/get/delete, all with zero hand-written request/handler/endpoint code:

```csharp
// Domain project
using System.ComponentModel.DataAnnotations;
using DKNet.EfCore.Abstractions.Attributes;
using DKNet.EfCore.Abstractions.Entities;

namespace Catalog;

// `partial` so the fuller vertical-slice example in references/DKNet.SlimBus.Generators.md can
// extend this same Product with two more [CrudAction] members without repeating the ctor/UpdatePrice.
public sealed partial class Product : Entity
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

    [CrudUpdate]
    public void UpdatePrice([Range(0, 1_000_000)] decimal price) => Price = price;
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

`ProductDto` gets `Id`, `required string Name`, `Price` — mirrored from `Product` at compile time. `[CrudCreate]` + `[CrudUpdate]` generate `CreateProductRequest`, `UpdatePriceProductRequest`, `Create`/`UpdatePriceProductHandler`, a `DeleteProductRequest` (no handler needed for delete), and — because `DKNet.AspCore.Extensions` is referenced — `MapProductCrud`, all in namespace `{AssemblyName}.Crud` (here `Generated.Crud`, the fallback for a project with no assembly name — use your own project's assembly name in a real app). Nothing above is hand-written except the entity and the empty DTO shell.

## Rules

1. **Never hand-write a type a generator already emits.** A `[GenerateDto]` DTO never gets `.ToEntity()`/`.Adapt()`/`.FromEntity()` — write your own mapping. A `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]` member's request/handler/endpoint comes from `DKNet.SlimBus.Generators` — a hand-written duplicate is a build error. The only supported override is a same-named hand-written `IHandler<TRequest, TDto>` (see *How to*).
2. Every CRUD-attributed entity needs **exactly one** `[GenerateDto(typeof(Entity))]` DTO in the same project (`DKCRUDGEN001`/`DKCRUDGEN002`), and every `[GenerateDto]` shell must be `partial` or the C# compiler — not the generator — rejects it.
3. `IgnoreComplexType`'s *effective* default is `true`, not the attribute property's CLR default of `false`. It is only `false` when written explicitly on the attribute or via the `DtoGeneratorIgnoreComplexType` MSBuild property.
4. `Include` and `Exclude` are mutually exclusive on one `[GenerateDto]` or convention-form `[RaisesEvent]`. Setting both (`DKDTOGEN004`) is a Warning, not an Error — the build stays green while that one DTO silently gets **zero** generated properties.
5. `[CrudCreate]` on a factory *method* still has the generated handler call the marked member's parameter list as a **constructor**; the method body never runs. Mark the constructor itself when it differs from the factory.
6. The generated handler-override point matches by the request's **simple name only**, never by the response type — `IHandler<UpdatePriceProductRequest, WrongDto>` silently wins over the generated handler and fails at dispatch, not at build.
7. `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]`/`CrudActionVerb` live in `DKNet.EfCore.Abstractions.Attributes`; `[GenerateDto]`/`[RaisesEvent]`'s validator live in `DKNet.EfCore.DtoGenerator`. Neither package has a DI registration — there is no `AddCrudGenerators()`/`AddDtoGenerator()`.
8. `Map{Entity}Crud` is only emitted when the compiling project also references `DKNet.AspCore.Extensions`. Omit that reference for a request/handler-only slice with no minimal-API surface.
9. Reference both packages as analyzers (`PrivateAssets="all" OutputItemType="Analyzer"`) — they ship `netstandard2.0` and produce no assembly a consumer should load normally.
10. `[FromClaim]`/`[FromRequestHeader]` are **not** part of either generator. They are runtime model binding (`DKNet.AspCore.Extensions.ModelBinding`) resolved per-request from `HttpContext` — see `dknet-aspcore-api`.

## How to...

### Shape a DTO with Include/Exclude

**When**: the DTO needs a narrower shape than the full entity.

```csharp
using DKNet.EfCore.DtoGenerator;

namespace Sales;

public class Customer
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string InternalNotes { get; set; } = string.Empty;
}

[GenerateDto(typeof(Customer), Exclude = [nameof(Customer.InternalNotes)])]
public partial record CustomerPublicDto;

[GenerateDto(typeof(Customer), Include = [nameof(Customer.Id), nameof(Customer.Name)])]
public partial record CustomerNameDto;
```

- `Include` bypasses `Exclude`, the project-wide exclusion list, and `IgnoreComplexType` entirely — it is the whole truth for that DTO's shape.
- Setting both on one DTO fires `DKDTOGEN004` and generates nothing for it — no partial success.

### Keep audit columns out of every DTO, project-wide

**When**: every entity in the project carries the same audit columns that should never reach a DTO.

```xml
<!-- consuming project's .csproj -->
<PropertyGroup>
  <DtoGeneratorExclusions>CreatedBy,UpdatedBy,CreatedAt,UpdatedAt</DtoGeneratorExclusions>
</PropertyGroup>
```

```csharp
using DKNet.EfCore.DtoGenerator;

namespace Billing;

public class Invoice
{
    public Guid Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

[GenerateDto(typeof(Invoice))]
public partial record InvoiceDto; // only Id, Number are generated
```

- Names are case-sensitive and comma/semicolon-separated; no `CompilerVisibleProperty` wiring is needed — the package's own `.props` file declares it.
- A DTO's local `Exclude` combines with this list; a non-empty `Include` bypasses it and reports `DKDTOGEN005` (Info) naming how many global exclusions were skipped.

### Raise a validated domain event without hand-writing the payload

**When**: an entity raises a domain event and the payload should track the entity's own shape automatically.

```csharp
using DKNet.EfCore.Abstractions.Events;

namespace Membership;

[RaisesEvent(EventOperations.Created)]
public class Member
{
    public Guid Id { get; set; }
    public string Tier { get; set; } = string.Empty;
}
```

- This composes and emits `MemberCreatedEvent` for you — no `[GenerateDto]` record to hand-write. `RaisesEventValidator` checks the rule at build time (`DKRAISEVT001`-`011`); `DKNet.EfCore.Events` is what actually publishes it at `SaveChanges` time — referencing only these two generator packages, the rule builds and validates but never raises anything at runtime.
- Narrowing an `Updated` rule to specific properties (`nameof(Member.Tier)`) only fires when that *direct* property changes; nested/owned-value changes never satisfy it.

### Turn generated routes off, or lock one down, without touching the entity

**When**: one deployment needs fewer CRUD routes, or per-route authorization.

```csharp
using Api;
using DKNet.AspCore.Extensions.Endpoints;
using Generated.Crud;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGroup("/products").MapProductCrud(o => o
    .Exclude(CrudOp.Delete)
    .Configure(CrudOp.Update, b => b.RequireAuthorization("product.write")));
```

- A route name passed to the `string` overloads of `Exclude`/`Configure` that the entity doesn't have throws `ArgumentException` at registration — a typo fails loudly, not silently.
- This is the *only* knob `DKNet.SlimBus.Generators`' emitted `Map{Entity}Crud` exposes — there is no other way to configure a generated route short of overriding its handler.

### Override one generated handler, keep the rest generated

**When**: the generated request shape is right but the logic needs to differ.

```csharp
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
        var entity = await repository.FirstOrDefaultAsync(new ProductByIdSpec(request.Id), cancellationToken);
        if (entity is null)
            return Result.Fail<ProductDto>(new NotFoundError($"Product '{request.Id}' was not found."));

        entity.UpdatePrice(request.Price);
        await repository.UpdateAsync(entity, cancellationToken);
        return Result.Ok(mapper.Map<ProductDto>(entity));
    }
}
```

- Matching is by `UpdatePriceProductRequest`'s simple name only, found via a syntax scan of base lists — the generator skips emitting *only* the handler (`DKCRUDGEN005`, Info); the request record is still generated regardless.
- The generated `{Entity}ByIdCrudSpec` this override would otherwise reuse is `file`-scoped to the generated handlers file, so a hand-written override declares its own specification (as above) instead of reaching for it.

### Inspect the code a generator emitted

**When**: a diagnostic doesn't explain the output, or you just want to read the `.g.cs` file.

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)Generated</CompilerGeneratedFilesOutputPath>
</PropertyGroup>
```

- After `dotnet build`, look under `obj/Generated/<GeneratorAssemblyName>/<GeneratorTypeFullName>/`, e.g. `obj/Generated/DKNet.EfCore.DtoGenerator/DKNet.EfCore.DtoGenerator.DtoGenerator/ProductDto.g.cs` or `.../DKNet.SlimBus.Generators/DKNet.SlimBus.Generators.CrudGenerator/Product.Requests.g.cs`.
- These two MSBuild properties are standard Roslyn switches, not specific to either DKNet package — they apply to any source generator in the project.

## Runtime behaviour

**`DtoGenerator` / `RaisesEventValidator`**: compile-time only. `[GenerateDto]` emits DTO properties; `[RaisesEvent]` is validated and, for its convention forms, its payload record is emitted the same way `DtoGenerator` shapes any DTO. Neither raises anything — `DKNet.EfCore.Events` reads `[RaisesEvent]` via reflection during `SaveChanges` to actually publish (see `dknet-efcore-save-pipeline`).

**`CrudGenerator`**: also compile-time only. At request time it's the code it emitted that runs, composed with other packages: Create dispatches through the generated handler → `repository.AddAsync(...)` → a **lazily** mapped DTO; Update/Action fetch the entity by id, call the marked method, return the same lazy DTO; Delete never touches SlimBus at all, going straight to `IRepositorySpec`. `SaveChanges` happens only after a handler returns successfully, via `AddSlimBusEfCoreInterceptor<TDbContext>()` — no generated handler ever calls it itself, so database-generated values (keys, timestamps) are only present in the response because the mapping is deferred until after save.

## Gotchas

- **`GenerateDtoAttribute` is `internal`, compiled fresh into every project via a content file.** You cannot reference the attribute type itself across a project boundary; each project applying `[GenerateDto]` gets its own private copy.
- **A hand-written property with the same name as a would-be-generated one silences the generator for that name, silently.** A wrong-*type* same-name override leaves the property incorrectly shaped with no diagnostic; a wrong-*case* one does not match at all (matching is exact-case), so both the generator's property and your extra one end up on the DTO.
- **Records and structs are never treated as navigation, even under `IgnoreComplexType = true`.** Only `class`-kind reference types that aren't records/BCL/`[Owned]` get dropped; a record-typed value object always survives.
- **Only `System.ComponentModel.DataAnnotations` attributes and `[SensitiveData]` are copied onto a generated DTO property.** Your own custom attributes, `[JsonPropertyName]`, etc. never carry over — re-declare them by hand (the generator won't fight a hand-written member of the same, correct name/type).
- **The CRUD generator has zero project references of its own** — it resolves `[CrudCreate]`/`IEntity<TKey>`/`[GenerateDto]` by fully-qualified string name, not by binding to real symbols from those assemblies.
- **An action's route never falls on the plain `{id}` route, ever.** Only `[CrudUpdate]`'s first member (by declaration order) gets `{id}`; every `[CrudAction]` is always `{id}/{segment}`, regardless of verb.
- **A parameterless `[CrudAction]` genuinely requires no body and no `Content-Type` at all**, while an action with even one nullable parameter still requires *a* body — just one with an optional field inside it.
- **Nullability, not the parameter's rendered type text, decides `required` on the generated request.** In a nullable-*disabled* compilation, every parameter is treated as required, even one that "looks" nullable by convention.
- **A DELETE name collision silently downgrades the generated route.** If a create/update/action member already claims the name `Delete{Entity}Request`, DELETE falls back from the 3-generic `MapDeleteById<TEntity, TKey, TRequest>()` to the request-less 2-generic overload — any validator registered against that request type for this entity simply never runs (`DKCRUDGEN010`, Info only).
- **The generated namespace is `{AssemblyName}.Crud`, derived from the *compiling* project's own assembly name** — not a fixed string, and not related to the entity's or DTO's namespace. A hand-written handler override or delete validator must import (or live inside) that exact namespace.

## Do not

- `[GenerateDto(typeof(Product))]`-generated DTO `.ToEntity()`, `.Adapt()`, `.FromEntity(...)` — **do not exist**. No mapping method is ever emitted.
- `services.AddDtoGenerator(...)`, `services.AddCrudGenerators(...)`, `CrudGeneratorOptions` — **no DI surface exists** for either package.
- Guessing the CRUD attributes live in `DKNet.SlimBus.Generators.Attributes` — they live in `DKNet.EfCore.Abstractions.Attributes`.
- Hand-writing `Create{Entity}Request`/`{Method}{Entity}Request`/`Delete{Entity}Request`/`Map{Entity}Crud` for an already-attributed entity — duplicate-type build error; there is no partial-merge story for these, only the named-handler override.
- Assuming `[Range]`/`[Required]` on a `[CrudAction]` parameter is *enforced* — it is copied onto the request as metadata only; minimal APIs run no automatic `DataAnnotations` validation by themselves.
- Treating `[FromClaim(...)]` as a generator attribute that produces a DTO or CRUD member — it is runtime `HttpContext` model binding from `DKNet.AspCore.Extensions.ModelBinding`, unrelated to `[GenerateDto]`/`[CrudAction]`.

```csharp
// no-compile
using DKNet.EfCore.DtoGenerator;

namespace WrongExamples;

// AggregateRoot does not exist anywhere in DKNet — the base is Entity<TKey> / Entity.
public class Product : AggregateRoot
{
}

// GlobalDtoConfiguration has exactly one member (ConfigurationDocumentation) and the class
// itself is never visible to a consuming project — it ships only in the analyzer's own DLL.
public class ExclusionReader
{
    public string[] Exclusions = GlobalDtoConfiguration.Exclusions;
}
```

## Related skills

- `dknet-packages` — confirm this is the right package before wiring anything; the router across the whole plugin.
- `dknet-efcore-domain-model` — `Entity<TKey>`/`AuditedEntity`, `UseAutoConfigModel`, and shaping the entity a `[GenerateDto]`/`[CrudAction]` attribute is applied to.
- `dknet-efcore-specifications` — `IRepositorySpec`/`Specification<TEntity>` themselves; this skill shows only the minimum needed inside a generated or overriding handler.
- `dknet-efcore-save-pipeline` — what actually dispatches a `[RaisesEvent]` payload (`DKNet.EfCore.Events`), and hooks/audit logs unrelated to generation.
- `dknet-efcore-data-security` — row-level authorization and column encryption; neither generator here is aware of either.
- `dknet-slimbus-cqrs` — hand-written SlimBus commands/handlers, `AddSlimMessageBus`/`AddSlimBusEfCoreInterceptor` in depth, anything that isn't a `[CrudAction]` vertical slice.
- `dknet-aspcore-api` — `[FromClaim]`/`[FromRequestHeader]`, `IEndpointConfig`, `ListQuery`/paging, error-response shaping; none of it is generated by either package here.
- `dknet-idempotency` — making a generated or hand-written endpoint safe to retry; generated endpoints carry no idempotency of their own.
- `dknet-blob-storage` — unrelated surface; no overlap.
- `dknet-services` — unrelated surface (encryption, PDF, template transformation); no overlap.
- `dknet-core-utilities` — unrelated surface (`Fw.Extensions`, `RandomCreator`); no overlap.
- `dknet-testing` — fixture patterns (TestContainers, in-memory hosts, ARM64 fallbacks) for testing code built on a generated CRUD slice or DTO.

## References

- [references/DKNet.EfCore.DtoGenerator.md](references/DKNet.EfCore.DtoGenerator.md) — full entry points, options, usage patterns, diagnostics and gotchas for `[GenerateDto]` and `[RaisesEvent]` validation. Docs: [DKNet.EfCore.DtoGenerator.md](https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/DKNet.EfCore.DtoGenerator.md), [GLOBAL_EXCLUSIONS_GUIDE.md](https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/GLOBAL_EXCLUSIONS_GUIDE.md).
- [references/DKNet.SlimBus.Generators.md](references/DKNet.SlimBus.Generators.md) — full entry points, naming/routing rules, usage patterns, diagnostics and gotchas for `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]`. Docs: [DKNet.SlimBus.Generators.md](https://github.com/baoduy/DKNet/blob/main/docs/Messaging/DKNet.SlimBus.Generators.md).
- [references/diagnostics.md](references/diagnostics.md) — one sorted index of every `DKDTOGEN`/`DKRAISEVT`/`DKCRUDGEN` ID across both generators, for fast triage from a build log.
- Package pages: [nuget.org/packages/DKNet.EfCore.DtoGenerator](https://www.nuget.org/packages/DKNet.EfCore.DtoGenerator), [nuget.org/packages/DKNet.SlimBus.Generators](https://www.nuget.org/packages/DKNet.SlimBus.Generators). Full docs site: [baoduy.github.io/DKNet](https://baoduy.github.io/DKNet/).
