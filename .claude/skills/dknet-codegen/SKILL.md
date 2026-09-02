---
name: dknet-codegen
description: Use when working with DKNet's Roslyn source generators — [CrudCreate], [CrudUpdate], [CrudAction], [GenerateDto], [RaisesEvent], [FromClaim] — when a DKCRUDGEN/DKDTOGEN/DKRAISEVT diagnostic appears, when adding a CRUD endpoint to a DKNet API, or when a generated request/handler/endpoint/DTO is missing, duplicated, or wrong.
---

# DKNet source generators

Two incremental generators, plus one runtime attribute that is often confused with them.

**The cardinal rule: never hand-write a type the generator emits.** Doing so produces duplicate-type build errors. Read the generated output (`obj/**/generated/**/*.g.cs`, or set `<EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>`) before assuming a type is missing.

Reference docs: `docs/Messaging/DKNet.SlimBus.Generators.md`, `docs/EfCore/DKNet.EfCore.DtoGenerator.md`, `docs/EfCore/GLOBAL_EXCLUSIONS_GUIDE.md`.

---

## 1. CRUD vertical slice — `DKNet.SlimBus.Generators`

Attributes live in `DKNet.EfCore.Abstractions.Attributes`:

| Attribute | Targets | Notes |
|---|---|---|
| `[CrudCreate]` | constructor, method | **At most one per entity.** Named prop `Name`. |
| `[CrudUpdate]` | method | Named prop `Name`. |
| `[CrudAction]` | method | `CrudActionAttribute(string? route = null)`; props `Route`, `Verb` (`CrudActionVerb.Post`/`Put`/`Patch`, default `Post`), `Name`. |

An entity is discovered *because* it carries one of these — there is no separate "mark this aggregate" attribute.

### What you write

```csharp
// Domain project
public sealed class Gadget : Entity            // must implement IEntity<TKey>
{
    private Gadget() { }                       // EF

    [CrudCreate]
    public Gadget([Required, MaxLength(100)] string name, decimal price) { ... }

    [CrudUpdate] public void UpdatePrice([Range(0, 1_000_000)] decimal price) => Price = price;
    [CrudAction] public void Approve()      => IsApproved = true;
    [CrudAction] public void Discontinue()  => IsDiscontinued = true;
}

// API project (the compiling project — this is where the DTO must live)
[GenerateDto(typeof(Gadget))]
public partial record GadgetDto;
```

Members must be **public**. `System.ComponentModel.DataAnnotations` attributes on parameters are copied verbatim onto the generated request properties.

### What you get

Three files per entity in namespace `{AssemblyName}.Crud`:

| File | Contents |
|---|---|
| `{Entity}CrudRequests.g.cs` | one `public sealed partial record` per CRUD member |
| `{Entity}CrudHandlers.g.cs` | `internal sealed` handlers + a `file sealed class {Entity}ByIdCrudSpec`. **Not emitted at all** if every member has a hand-written handler |
| `{Entity}CrudEndpoints.g.cs` | `Map{Entity}Crud(...)`. **Only** when the compilation references `DKNet.AspCore.Extensions` |

Naming:

- Create request → `Create{Entity}Request`; update/action → `{MethodName}{Entity}Request` (`UpdatePriceGadgetRequest`, `ApproveGadgetRequest`)
- Handler → request name with trailing `Request` swapped for `Handler`
- Requests are `partial` — add extra members in your own partial declaration rather than editing generated code

Routes: `[CrudAction]` segment is the explicit `Route` arg, else the kebab-cased method name (`UpdatePrice` → `update-price`), mapped under `{id}/{segment}`. The **first** `[CrudUpdate]` claims the bare `{id}`; later ones get `{id}/{kebab-name}`. Actions never claim bare `{id}`.

### Overriding a generated handler

Declare a type whose base list names `IHandler<TheRequestName, ...>`. The generator detects this **by syntax on the request's simple name** (the request symbol doesn't exist yet), skips that handler, and reports `DKCRUDGEN005` (Info).

### Diagnostics

| ID | Sev | Meaning / fix |
|---|---|---|
| `DKCRUDGEN001` | Error | No `[GenerateDto(typeof(Entity))]` DTO in the **compiling project**. Add one there — a DTO in another assembly is not found. |
| `DKCRUDGEN002` | Error | More than one `[GenerateDto]` DTO for that entity. Keep one. |
| `DKCRUDGEN003` | Error | More than one `[CrudCreate]` member. Keep one. |
| `DKCRUDGEN004` | Error | CRUD member is not `public`. |
| `DKCRUDGEN005` | Info | Hand-written handler took over — expected when overriding. |
| `DKCRUDGEN006` | Error | Entity does not implement `DKNet.EfCore.Abstractions.Entities.IEntity<TKey>`. |
| `DKCRUDGEN007` | Error | Member marked both `[CrudUpdate]` and `[CrudAction]`. Keep exactly one. |
| `DKCRUDGEN008` | Error | Two members resolve to the same route segment. Give one an explicit `Route`. |

`DKCRUDGEN001` is by far the most common: the DTO must be in the assembly being compiled, not the domain project.

---

## 2. DTOs — `DKNet.EfCore.DtoGenerator`

`GenerateDtoAttribute` lives in namespace **`DKNet.EfCore.DtoGenerator`** (not `Abstractions`) and is `internal` by design — its source file is packed as `contentFiles` and compiled into each consumer.

```csharp
GenerateDtoAttribute(Type entityType)
string[] Exclude { get; set; }      // property names to drop
string[] Include { get; set; }      // allow-list; mutually exclusive with Exclude
bool IgnoreComplexType { get; set; } // default true — navigation properties excluded
```

The generator **fills in the partial type you declared** — it does not invent a type name. Declare `partial class` / `partial record` / `partial record struct` and it matches your kind. Properties you declare by hand are skipped, so hand-declaring is how you override one. No mapping methods are emitted; Mapster maps at runtime.

```csharp
[GenerateDto(typeof(Customer), IgnoreComplexType = true, Exclude = ["Email"])]
public partial record CustomerWithExcludeDto;

[GenerateDto(typeof(Customer), Include = ["CustomerId", "Name", "Orders"])]
public partial record CustomerWithIncludeDto;
```

Reference the package as an analyzer (`PrivateAssets="all"`); it emits no runtime assembly.

**Solution-wide knobs** (MSBuild, see `GLOBAL_EXCLUSIONS_GUIDE.md`): `DtoGeneratorExclusions` (comma-separated, combines with local `Exclude`) and `DtoGeneratorIgnoreComplexType`. Precedence for `IgnoreComplexType`: attribute → MSBuild property → built-in `true`. `Include` **overrides** global exclusions.

### Diagnostics

| ID | Sev | Meaning |
|---|---|---|
| `DKDTOGEN001` | Warning | Generic generation failure |
| `DKDTOGEN002` | Warning | 0 properties found — entity type usually didn't resolve |
| `DKDTOGEN003` | Info | Reports which properties were filtered |
| `DKDTOGEN004` | — | `Include` and `Exclude` both set; use one |
| `DKDTOGEN005` | — | `Include` is bypassing global exclusions (informational) |

---

## 3. Domain event payloads — `[RaisesEvent]`

Validated and generated by the same analyzer (`RaisesEventValidator.cs` in `DKNet.EfCore.DtoGenerator`). Diagnostics `DKRAISEVT001`–`DKRAISEVT011` cover: invalid narrowing property (001), payload/entity mismatch (002), narrowing ignored (003), event name collides with an existing type (004), label not a compile-time constant (005), duplicate event name in namespace (006), no operation declared (007), duplicate composed name on the entity (008), `Include`+`Exclude` together (009), invalid payload filter property (010), payload filters on the type-naming form (011).

Most of these mean the event name or payload shape is ambiguous — resolve by naming the event explicitly rather than by deleting the attribute.

---

## 4. `[FromClaim]` — runtime, not a generator

Common confusion: `FromClaimAttribute` is **not** source-generated. It lives in `DKNet.AspCore.Extensions.ModelBinding` and is resolved at request time.

```csharp
public sealed record CreateOrderRequest
{
    [FromClaim(ClaimTypes.NameIdentifier)] public string UserId { get; init; } = default!;
}
```

Behaviour that matters:

- The property is **always overwritten** before validation and before the handler runs — including with `default` when the claim is absent or the caller is unauthenticated. A client cannot forge it through the payload. Do not add a "only set if empty" guard.
- Inert unless `services.AddContextualRequestPopulation(...)` is registered; applied by `UseEndpointConfigs`.
- Stripped from the OpenAPI schema automatically.
- `ContextualPopulationOptions.SystemAccountFallback` supplies a value only when the host's `EndpointRegistrationOptions.RequireAuthorization` is `false`.
- To add another source, implement `IContextualSource` + `IContextualValueResolver` — the mechanism needs no change.

It supersedes the obsolete member on `RequestBase` in `DKNet.SlimBus.Extensions`.

---

## Debugging checklist

1. Read the actual generated file before concluding anything is missing.
2. `DKCRUDGEN001`? The `[GenerateDto]` DTO is in the wrong assembly.
3. No endpoints generated? The project doesn't reference `DKNet.AspCore.Extensions`.
4. Duplicate type errors? Something hand-written collides with generated output — delete the hand-written copy, or override deliberately via the `IHandler<...>` route.
5. Generator changes not taking effect? Roslyn caches aggressively — `dotnet build --no-incremental`, or restart the IDE's language server.
6. Changing generator behaviour means updating `SlimBus.Generators.Tests` (`RequestEmissionTests`, `HandlerEmissionTests`, `EndpointEmissionTests`, `DiagnosticTests`), which assert on exact emitted strings.
