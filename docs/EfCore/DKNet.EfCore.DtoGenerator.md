# DKNet.EfCore.DtoGenerator

**A Roslyn incremental source generator that emits DTO properties from an entity type at compile time — no hand-written mapping boilerplate, no runtime reflection.**

## What problem this solves

Hand-written DTOs drift: an entity gains a property, the DTO doesn't, and nobody notices until a client complains about a missing field — or the reverse, a DTO leaks an entity's internal field because someone forgot to update an exclusion list. `DKNet.EfCore.DtoGenerator` removes the copy-paste step entirely: you declare an empty `partial` shell, point it at an entity type with `[GenerateDto]`, and the generator mirrors the entity's public readable properties onto the DTO every time you build.

It works at compile time only — the generator itself is a `netstandard2.0` analyzer package (`DevelopmentDependency`, `PrivateAssets="all"`) that runs inside the compiler process. Nothing it produces takes a runtime dependency on this package or any other DKNet package: a generated DTO is a plain `record`/`class` with `init`-only properties.

Reach for it when:
- You want read-model / API-response DTOs that always mirror an entity's shape (or a filtered subset of it) without maintaining the property list by hand.
- You're shaping event payloads for `[RaisesEvent]` (see [DKNet.EfCore.Events](./DKNet.EfCore.Events.md)) and want the payload record generated from the same entity, guaranteed to match.
- You want per-DTO or project-wide control over which properties (or whole categories, like navigation properties) are ever allowed onto a DTO.

It does **not** generate mapping methods (no `FromEntity`/`ToEntity`/`FromEntities`) — mapping between the entity and the generated DTO is left entirely to you (e.g. Mapster, a manual assignment, or a projection expression). This keeps the generator's only output "plain data".

## Install and minimum usage

```xml
<ItemGroup>
  <PackageReference Include="DKNet.EfCore.DtoGenerator" Version="{latest}" PrivateAssets="all" OutputItemType="Analyzer" />
</ItemGroup>
```

`GenerateDtoAttribute.cs` is packed as a compiled source file (`contentFiles/cs/any` + `content`, `BuildAction=Compile`), so it lands directly in your project's own compilation — no extra `using` beyond the namespace it declares, and no additional runtime assembly reference.

```csharp
public class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

using DKNet.EfCore.DtoGenerator;

[GenerateDto(typeof(Product))]
public partial record ProductDto;
```

This emits `ProductDto.g.cs` with `Id`, `required string Name`, and `Price` as `init`-only properties. The DTO shell must be `partial` — a `record`, `record struct`, or `class` all work; the generator infers the matching partial declaration ("partial record", "partial record struct", or "partial class") from what you wrote.

## Features

### `[GenerateDto(Type entityType)]` — the attribute

`[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]` — apply once per DTO shell.

| Parameter | Type | Default | Behavior |
|---|---|---|---|
| `entityType` (ctor arg) | `Type` | required | The entity to mirror. Passed as `typeof(Entity)`. |
| `Exclude` | `string[]` | `[]` | Property names to omit. Mutually exclusive with `Include`. |
| `Include` | `string[]` | `[]` | When non-empty, **only** these properties are generated — every other filter (local `Exclude`, global exclusions, `IgnoreComplexType`) is bypassed. |
| `IgnoreComplexType` | `bool` | unset (falls through to project-wide, then `true`) | When effectively `true`, navigation-style properties (see below) are dropped automatically. |

```csharp
// Exclude specific properties
[GenerateDto(typeof(Product), Exclude = new[] { "Price" })]
public partial record ProductNoPriceDto;

// Include only specific properties (Exclude/global exclusions/IgnoreComplexType are ignored)
[GenerateDto(typeof(Product), Include = new[] { nameof(Product.Id), nameof(Product.Name) })]
public partial record ProductNameDto;

// Opt in to navigation properties for one DTO
[GenerateDto(typeof(Order), IgnoreComplexType = false)]
public partial record OrderWithCustomerDto;
```

Both `Exclude` and `Include` accept `nameof(...)` expressions, string literals, or a collection expression (`["A", "B"]`) — all three syntaxes are read at compile time via the semantic model, not by string-parsing the attribute usage.

**Property selection rule** (applies before any filter): every `public` instance property with a `public` getter and no index parameters, walked up the entity's base-type chain (stopping at `object`/`ValueType`) and de-duplicated by name — so **entity inheritance is followed automatically**; you do not need a separate DTO per level of a base/derived entity hierarchy. Static properties and indexers are never candidates.

### Global exclusions (project-wide `Exclude`)

A `DtoGeneratorExclusions` MSBuild property lets you exclude the same property names from every `[GenerateDto]` in the project (e.g. audit columns) without repeating `Exclude = [...]` everywhere. Local `Exclude` and global exclusions are combined; `Include` bypasses both. This is a big enough feature to have its own deep-dive — see **[Global Exclusions Guide](./GLOBAL_EXCLUSIONS_GUIDE.md)** for the full configuration steps, behavior matrix, and troubleshooting. The shape:

```xml
<PropertyGroup>
  <DtoGeneratorExclusions>CreatedBy,UpdatedBy,CreatedAt,UpdatedAt</DtoGeneratorExclusions>
</PropertyGroup>
```

Using `Include` on a DTO while global exclusions are configured produces an informational diagnostic (`DKDTOGEN005`) reminding you the global list was skipped for that DTO.

### `GlobalDtoConfiguration`

`DKNet.EfCore.DtoGenerator.GlobalDtoConfiguration` is a documentation-only marker class living in the generator project — it is **not** shipped into consuming projects (unlike `GenerateDtoAttribute.cs`, it isn't packed as a content file), so you won't see or reference it from your own code. Its XML-doc remarks are the canonical description of the two project-wide MSBuild switches (`DtoGeneratorExclusions`, `DtoGeneratorIgnoreComplexType`) and their precedence rules; treat it as an in-source pointer to the Global Exclusions Guide and to the `IgnoreComplexType` precedence description below, not as an API surface.

### `IgnoreComplexType` — flattening navigation properties

Default effective value is `true` (flat DTOs) unless overridden. Precedence when resolving the effective value for a given DTO: **per-DTO `IgnoreComplexType` attribute argument** (if explicitly set) → **project-wide `DtoGeneratorIgnoreComplexType` MSBuild property** (if set) → **built-in default `true`**.

```xml
<PropertyGroup>
  <DtoGeneratorIgnoreComplexType>false</DtoGeneratorIgnoreComplexType>
</PropertyGroup>
```

A property counts as an excluded "navigation" when, after unwrapping arrays/`List<T>`/`IList<T>`/`ICollection<T>`/`IEnumerable<T>` to their element type, that type is:
- a reference type, not `string`, not another BCL special type,
- a `class` (not a `record`, not a `struct`) — records and structs are assumed to be value objects, not entities,
- not carrying an attribute named `Owned`/`OwnedAttribute` (checked by short name, any namespace),
- not in the `System.*` or `Microsoft.*` namespace (so `Uri`, `Version`, etc. are always kept as scalars).

`Include` always wins over `IgnoreComplexType` — naming a navigation property in `Include` generates it regardless of the flag.

### `[RaisesEvent]` validation (`RaisesEventValidator`)

`RaisesEventValidator` is a second `IIncrementalGenerator` in this same package. It does not shape DTOs itself; it validates every `DKNet.EfCore.Abstractions.Events.RaisesEventAttribute` declaration on an entity at build time, and — for the attribute's string form only — generates the payload record. See [`RaisesEventAttribute`](../../src/EfCore/DKNet.EfCore.Abstractions/Events/RaisesEventAttribute.cs) for the attribute itself; `DKNet.EfCore.Events` is what actually raises the event at runtime (via reflection, after `SaveChanges`) — see [DKNet.EfCore.Events](./DKNet.EfCore.Events.md).

Declaring a domain event is two separate steps:

1. Shape the payload as an ordinary `[GenerateDto]` record — every feature above (`Include`/`Exclude`/`IgnoreComplexType`) applies to it exactly like any other DTO.
2. Declare a raise rule on the entity with `[RaisesEvent]`, naming that payload, the persistence operation(s), and — for `Updated` — an optional narrowing property list.

```csharp
using DKNet.EfCore.Abstractions.Events;
using DKNet.EfCore.DtoGenerator;

[GenerateDto(typeof(Product))]
public partial record ProductCreatedEvent;

[GenerateDto(typeof(Product), Exclude = new[] { "InternalCost" })]
public partial record ProductPriceChangedEvent;

[RaisesEvent(typeof(ProductCreatedEvent), EventOperations.Created)]
[RaisesEvent(typeof(ProductPriceChangedEvent), EventOperations.Updated, nameof(Product.Price))]
public class Product
{
    public Guid Id { get; set; }
    public decimal Price { get; set; }
    public decimal InternalCost { get; set; }
}
```

`RaisesEventValidator` checks, at build time:
- the named payload type carries `[GenerateDto]` **and** was generated from the *same* entity carrying the `[RaisesEvent]` rule (`DKRAISEVT002` otherwise);
- every narrowing property (the trailing `params string[] properties`) is a direct property of the entity, not a nested path (`DKRAISEVT001`);
- narrowing set on a rule with no `Updated` flag is pointless and warned about (`DKRAISEVT003`), since it has no runtime effect.

**String form** — name an event that has no hand-written `[GenerateDto]` record at all, and this generator emits the payload for you, with the same default shape as `[GenerateDto(typeof(Entity))]` (no `Exclude`/`Include`/`IgnoreComplexType` knobs available on this form):

```csharp
[RaisesEvent("CustomerTouched", EventOperations.Created)]
public class Customer
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

// Optional — extend the generated record from your own file; it is public partial record:
public partial record CustomerTouched
{
    public string Greeting => $"Hello, {Name}";
}
```

The string form has its own diagnostics: `DKRAISEVT004` (name already resolves to an existing, incompatible type — a hand-authored `partial record` stub with no `[GenerateDto]` is *not* a collision, it merges), `DKRAISEVT005` (name isn't a compile-time constant string, or isn't a single valid C# identifier), `DKRAISEVT006` (two different entities in the same namespace declare the same string-form event name — never merged into one record).

### Diagnostics reference

All diagnostics use category `DKNet.EfCore.DtoGenerator`.

| Code | Severity | Emitted by | Meaning |
|---|---|---|---|
| `DKDTOGEN001` | Warning | `DtoGenerator` | Generation for a DTO threw an exception; the message is included, generation for that target is skipped, other targets are unaffected. |
| `DKDTOGEN002` | Warning | `DtoGenerator` | The resolved entity has zero eligible properties — usually means the entity type wasn't resolved correctly. |
| `DKDTOGEN003` | Info | `DtoGenerator` | Informational: N properties were filtered in/out of the DTO (echoes the effective Include/Exclude list). |
| `DKDTOGEN004` | Warning | `DtoGenerator` | `Include` and `Exclude` were both specified on the same DTO — mutually exclusive; **generation is skipped entirely for that DTO** (no `.g.cs` emitted). |
| `DKDTOGEN005` | Info | `DtoGenerator` | `Include` was used while global exclusions were configured — the global list was ignored for this DTO. |
| `DKRAISEVT001` | Error | `RaisesEventValidator` | A narrowing property is a nested path, or isn't a property of the entity. |
| `DKRAISEVT002` | Error | `RaisesEventValidator` | The named event type carries no `[GenerateDto]`, or was generated from a different entity than the one declaring the rule. |
| `DKRAISEVT003` | Warning | `RaisesEventValidator` | Narrowing properties set on a rule with no `Updated` operation flag — ignored at runtime. |
| `DKRAISEVT004` | Error | `RaisesEventValidator` | String-form event name already resolves to an existing, incompatible type. |
| `DKRAISEVT005` | Error | `RaisesEventValidator` | String-form event name isn't a compile-time constant, or isn't a single valid C# identifier. |
| `DKRAISEVT006` | Error | `RaisesEventValidator` | Two different entities in the same namespace declared the same string-form event name. |

## Configuration options and defaults

| Setting | Where | Default | Notes |
|---|---|---|---|
| `Exclude` | `[GenerateDto]` argument | `[]` | Mutually exclusive with `Include`; combined with global exclusions. |
| `Include` | `[GenerateDto]` argument | `[]` | Overrides `Exclude`, global exclusions, and `IgnoreComplexType` when non-empty. |
| `IgnoreComplexType` | `[GenerateDto]` argument | unset (falls through) | Per-DTO override; see precedence above. |
| `DtoGeneratorExclusions` | MSBuild property (`.csproj`) | unset | Comma/semicolon-separated property names excluded project-wide. Already exposed to the compiler by this package's `buildTransitive` props — no manual `CompilerVisibleProperty` needed. See the Global Exclusions Guide. |
| `DtoGeneratorIgnoreComplexType` | MSBuild property (`.csproj`) | unset (built-in default `true` applies) | Project-wide default for `IgnoreComplexType` when a DTO doesn't set it explicitly. |
| `EmitCompilerGeneratedFiles` / `CompilerGeneratedFilesOutputPath` | MSBuild properties | unset | Standard Roslyn generator switches (not specific to this package) to write `.g.cs` files to disk, e.g. under `obj/Generated`, for inspection/debugging. |

## How it composes with other DKNet packages

- **Depends on nothing at runtime.** The generator assembly targets `netstandard2.0`, is packed as an `analyzer` with `DevelopmentDependency=true` and `IncludeBuildOutput=false` — it runs inside the compiler process only. A project referencing it needs no other DKNet package to compile DTOs.
- **Generated DTOs are plain data types.** No base class, no interface, no attribute left on the output — a generated DTO has zero coupling to DKNet or to this generator once compiled. Project it, serialize it, return it from an API — it's an ordinary `record`/`class`.
- **Cross-reference with `DKNet.EfCore.Abstractions`:** `RaisesEventValidator` validates against `RaisesEventAttribute` (declared in `DKNet.EfCore.Abstractions.Events`) purely by attribute *name* and constant shape — it does not reference the `DKNet.EfCore.Abstractions` assembly at all (it even mirrors `EventOperations.Updated`'s numeric value as a local constant rather than referencing the real enum). A domain project can declare `[RaisesEvent]` rules and reference only `DKNet.EfCore.Abstractions` + this generator, and it builds and packs cleanly — the rules simply never raise until `DKNet.EfCore.Events` is wired up.
- **`DKNet.EfCore.Events`** is the runtime counterpart: it reads `[RaisesEvent]` via reflection after a successful `SaveChanges` and raises the named payload, mapping the entity onto it through a registered `IMapper` (e.g. Mapster). See [DKNet.EfCore.Events](./DKNet.EfCore.Events.md#declared-domain-events-raisesevent).
- **No built-in Mapster/AutoMapper integration.** Unlike some earlier iterations of this generator, no mapping helper methods are emitted — you choose your own mapping approach (e.g. Mapster's `Adapt`/`ProjectToType`, or manual assignment) for entity ↔ DTO conversion.

## Gotchas and limits

- **`Include` and `Exclude` are mutually exclusive on the same DTO.** Specifying both doesn't merge them — it produces `DKDTOGEN004` and **skips generation entirely** for that DTO (not even the unfiltered property set is emitted).
- **The DTO shell must be `partial`.** A non-`partial` `record`/`class`/`struct` can't receive the generator's emitted partial declaration; the C# compiler itself will report a conflicting-partial-modifier error, not this generator.
- **Hand-written properties always win.** If you declare a property with the same name in your own (non-generated) source, the generator skips emitting that name — this is the supported override/extension point, not an error.
- **Entity inheritance IS followed** — properties from base entity types are included automatically (deduplicated by name, walking up to but not including `object`/`ValueType`). You do not need to flatten a base/derived hierarchy by hand.
- **Records and value-object-style properties are never treated as navigation**, even with `IgnoreComplexType = true` — the navigation check only fires for `class` types that are not `record`. Model value objects as `record` (or mark them `[Owned]`) if you want them to survive the default flat-DTO filtering.
- **Generic entities have limited support** — the DTO shell itself must be non-generic; there is no generated generic DTO shape for a generic entity.
- **Nullability drives the generated shape, not just the type name:** non-nullable `string` → `required`; non-nullable collection → initialized with `= [];`; a non-nullable complex reference type that *is* generated (e.g. `IgnoreComplexType = false`, or an `[Owned]`/record/BCL type) → initialized with `= null!;` to satisfy nullable-reference-type analysis. Nullable variants get no initializer.
- **Only `System.ComponentModel.DataAnnotations` attributes are copied** from entity property to DTO property (best-effort re-serialization of the attribute's constructor/named arguments) — custom validation attributes and attributes from other namespaces are not copied.
- **Diagnostics don't all stop the build.** `DKDTOGEN00x` codes are Warning/Info only — a mistake there degrades the generated DTO shape but still compiles. `DKRAISEVT00x` codes mix Warning (`003`) and Error (`001`, `002`, `004`, `005`, `006`) — the Error-level ones fail the build.
