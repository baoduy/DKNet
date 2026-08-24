# CRUD Vertical-Slice Generation — Design Spec

**Date:** 2026-08-24
**Status:** Approved design, pending implementation plan
**Goal:** Ship an end-to-end CRUD feature (entity → event → command/handler → DTO → endpoint) with the developer writing only the domain entity and its events. Everything else is generic runtime framework or compile-time generated glue.

## Context (current state)

- Reads + Delete already have a generic entity pipeline: `MapGetById` / `MapGetList` / `MapDeleteById` in `DKNet.AspCore.Extensions` (specifications + Mapster projection + `ListQueryRequest`).
- Writes have no generic path: every Create/Update needs a hand-written SlimBus command record + handler, then manual `MapPost`/`MapPut<TCommand>` wiring.
- `DKNet.EfCore.DtoGenerator` (Roslyn incremental generator) generates DTO properties only.
- No FluentValidation in the repo; validation is ad hoc.
- Domain events are done (recent PRs): entities call `AddEvent(...)`, dispatched on SaveChanges.

## Decisions (locked with user)

1. **Strategy: Hybrid** — generic runtime framework where generics reach; Roslyn source generation for per-entity glue (command types, handlers, endpoint registration).
2. **Business logic lives in entity methods** — generated handlers are dumb: they construct or fetch the aggregate and invoke the marked member. `AddEvent` stays in the aggregate.
3. **Writes go through SlimBus** — one uniform CQRS path; contextual claim population, `EfAutoSavePostInterceptor` auto-save, and cross-cutting behaviors keep working. Reads/Delete stay on the entity/specification pipeline.
4. **Discovery: attributes on entity members** — the method signature is the single source of truth for the command's shape.
5. **Generation target: the App/API layer project** hosts the generator and receives generated code. The generator scans **referenced assemblies** for marked entity members, so the domain project never references SlimBus.
6. **Naming: `Request` suffix** for generated types (`Create{Entity}Request`), all `sealed record`.
7. **Create and Update both return the DTO** — `Fluents.Requests.IWitResponse<TDto>` — via the LazyMapper `ResultOf<TDto>(entity)` pattern (see Component 3).

## Target developer experience

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

app.MapGroup("/products").MapProductCrud<ProductDto>();       // generated extension
```

## Components

### 1. Marker attributes → `DKNet.EfCore.Abstractions`

- `[CrudCreate]` — on a public constructor or static factory method. At most one per entity (analyzer diagnostic otherwise).
- `[CrudUpdate]` — on any public instance method; one command per marked method.
- Optional `Name` property on both for request/route naming overrides (default: `Create{Entity}Request`, `{MethodName}{Entity}Request`; update route defaults to `PUT {id}`, additional updates to `PUT {id}/{kebab-method-name}`).
- Delete requires no attribute — `MapDeleteById` already covers it generically.

Attributes live in EfCore.Abstractions because the domain layer already references it; no messaging dependency leaks into the domain.

### 2. LazyMapper → `DKNet.SlimBus.Extensions`

Port the LazyMapper trio from DKNet.Templates (`Minimal.AppServices/Extensions/LazyMapper/`): `ILazyMap<T>`/`LazyMap<T>`, `LazyResult<T>` (a FluentResults `IResult<T>` that maps the wrapped entity to the DTO lazily via Mapster `IMapper`), and the `LazyMapExtensions` (`mapper.LazyMap<T>(value)`, `mapper.ResultOf<T>(value)`).

**Home: `DKNet.SlimBus.Extensions`, not `DKNet.EfCore.Extensions`** (deviation from the original suggestion, for user confirmation): EfCore.Extensions references neither MapsterMapper nor FluentResults — hosting LazyMapper there drags two unrelated packages into a pure-EF library. SlimBus.Extensions already references FluentResults (its `Fluents` contracts are built on it) and `ResultOf<TDto>` is only meaningful inside `IResult`-returning handlers, which are SlimBus types. Requires adding the MapsterMapper package reference to SlimBus.Extensions (already transitively present via EfCore.Events → Mapster).

### 3. New Roslyn incremental generator → `DKNet.SlimBus.Generators`

Sibling project to `DKNet.EfCore.DtoGenerator`, same multi-targeting and analyzer packaging. Referenced by the API/application project. Pipeline: `CompilationProvider`-based scan of the current compilation **and referenced assembly symbols** for `[CrudCreate]`/`[CrudUpdate]` members.

**DTO resolution:** generated requests return the entity's DTO. The generator finds it by scanning the compiling project for the partial record marked `[GenerateDto(typeof(TEntity))]` (the existing DtoGenerator attribute). Exactly one match → use it; zero or multiple → error diagnostic telling the developer to designate one (naming the ambiguity), since the domain-side attribute cannot reference an API-layer type.

Per marked member it emits into the compiling project:

**a. Request record** (`sealed record`, `Request` suffix: `Create{Entity}Request`, `{MethodName}{Entity}Request`)
- Properties mirror the member's parameters (name → PascalCase, type preserved).
- Parameter-level DataAnnotations (`[Required]`, `[Range]`, `[MaxLength]`, …) are copied onto the generated properties and ride minimal-API validation. No FluentValidation dependency.
- `[CrudCreate]` → `Fluents.Requests.IWitResponse<TDto>`.
- `[CrudUpdate]` → `Fluents.Requests.IWitResponse<TDto>`, plus an `Id` property (entity's `TKey`) bound from route.

**b. Handler** (sealed, ~8 lines, injects repository + Mapster `IMapper`)
- Create: invoke the marked ctor/factory with request values → `repo.Add` → `return mapper.ResultOf<TDto>(entity);`. Persistence via existing auto-save interceptor.
- Update: fetch by `Id` via repository → not found → `Result.Fail` (404) → else invoke the marked method → `return mapper.ResultOf<TDto>(entity);`.
- The lazy mapping means the DTO materializes only when the HTTP layer reads the result — after the auto-save interceptor has persisted (so DB-generated values are present).

**c. Endpoint registration extension**
- `Map{Entity}Crud(this RouteGroupBuilder, Action<CrudMapOptions>? configure = null)` — non-generic (the DTO is already resolved at generation time; reads and writes use the same one). Pure composition over the **existing** mappers, no new mapping layer:
  - `FluentsEntityEndpointMapperExtensions`: `MapGetById<TEntity,TKey,TDto>` + `MapGetList<TEntity,TKey,TDto>` + `MapDeleteById<TEntity,TKey>` (entity pipeline)
  - `FluentsEndpointMapperExtensions`: `MapPost<TRequest,TDto>` / new `MapPutById<TRequest,TKey,TDto>` per generated request (bus pipeline)
- `CrudMapOptions` (runtime type in `DKNet.AspCore.Extensions`) allows excluding operations (e.g. `o => o.Exclude(CrudOp.Delete)`).

**Framework gaps closed alongside (small, reused by hand-written code too):**
- `Fluents.Requests.IWithKey<TKey>` (`TKey Id { get; set; }`) + `MapPutById<TCommand,TKey,TResponse>(endpoint = "{id}")` on `FluentsEndpointMapperExtensions` — binds the route id into the body-bound request before `bus.Send`; today's `MapPut` never sees the route value.
- `NotFoundError : Error` in `DKNet.SlimBus.Extensions`, mapped to HTTP 404 by `ProblemDetailsExtensions` — today every failed `IResult` becomes 400, so generated Update handlers couldn't express 404.

### 4. Override / escape hatches

- **Hand-written handler wins:** if the compilation already contains a handler for a generated command type, the generator skips its handler and reports an info diagnostic. Command record generation still applies (or the developer writes both by hand and drops the attribute).
- **Per-operation exclusion** at mapping time via `CrudMapOptions`.
- Anything beyond CRUD is a normal hand-written SlimBus feature — the generated and manual styles are the same style, so mixing is seamless.

## Out of scope (deliberate)

- FluentValidation integration (revisit only if DataAnnotation copying proves insufficient).
- Generated domain events, event handlers, or Mapster configs.
- Scaffolding/`dotnet new` templates (nothing editable is emitted, so nothing drifts).
- PATCH/partial-update semantics.

## Key risk

Cross-assembly symbol scanning is less cacheable than syntax-based incremental generation and is the trickiest part. **Spike first:** prove a `CompilationProvider` scan finds `[CrudUpdate]` on a referenced project's entity and regenerates reliably on rebuild, before building the full emitter.

## Testing

- **Generator snapshot tests** (mirror DtoGenerator's test project layout): given an annotated entity, assert generated command/handler/mapper source.
- **Analyzer diagnostic tests:** duplicate `[CrudCreate]`, non-public member, unsupported parameter type.
- **Integration tests:** a full generated slice (entity in a separate test domain project) over SQLite — the same relational provider the existing `AspCore.Extensions.Tests` endpoint tests use, runnable locally on ARM — POST → 201 + DTO body + event dispatched, PUT → 200 + DTO body / 404, generated validation → 400, plus existing read/delete behavior. Full solution verified via the remote x64 GitHub runner.
- Coverage target: EfCore/SlimBus library tier — 95%.

## Delivery phases

1. **Spike:** cross-assembly attribute scan in an incremental generator (throwaway).
2. Port LazyMapper into `DKNet.SlimBus.Extensions` (+ unit tests); marker attributes in `DKNet.EfCore.Abstractions` + analyzer diagnostics.
3. Generator: request records + handlers, snapshot tests.
4. Generator: endpoint-registration extension + `CrudMapOptions`.
5. Integration test slice + docs (`docs/` page + memory-bank routing entry).
