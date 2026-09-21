# DKNet.EfCore.DtoGenerator

| Field | Value |
|---|---|
| Area | EfCore |
| NuGet | `dotnet add package DKNet.EfCore.DtoGenerator` — install with `PrivateAssets="all" OutputItemType="Analyzer"` (it is a Roslyn analyzer package: `IncludeBuildOutput=false`, `DevelopmentDependency=true`) |
| Docs | [DKNet.EfCore.DtoGenerator.md](https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/DKNet.EfCore.DtoGenerator.md) · [GLOBAL_EXCLUSIONS_GUIDE.md](https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/GLOBAL_EXCLUSIONS_GUIDE.md) |
| Source | https://github.com/baoduy/DKNet/tree/dev/src/EfCore/DKNet.EfCore.DtoGenerator |
| Depends on (DKNet) | None via project/assembly reference. `EventNameComposer` is pulled in as a linked source file, specifically so this analyzer never depends on `DKNet.EfCore.Abstractions` or `DKNet.EfCore.Events` as assemblies. |
| Depends on (3rd party) | `Microsoft.CodeAnalysis.CSharp` (pinned to an older version than the rest of the solution on purpose — see Gotchas), `Microsoft.CodeAnalysis.Analyzers`. |
| Target framework | `netstandard2.0` (mandatory for a Roslyn source generator). |

## Purpose

A Roslyn incremental source generator (`[Generator]` + `IIncrementalGenerator`) that, given an empty `partial record`/`class`/`struct` decorated with `[GenerateDto(typeof(Entity))]`, emits `init`-only properties mirroring the entity's public readable properties into a `.g.cs` file at compile time. A second generator in the same package, `RaisesEventValidator`, validates `[RaisesEvent]` declarations (from `DKNet.EfCore.Abstractions.Events`) against their `[GenerateDto]` payloads and, for convention forms, generates the payload record itself. Both run inside the compiler process only — nothing from this package exists at runtime.

It is **not** a mapper: it never emits `FromEntity`/`ToEntity`/`Adapt` methods — entity↔DTO conversion is left to the consumer (Mapster, manual assignment, projection). Do not reach for it to generate request/handler/endpoint vertical slices — that is `DKNet.SlimBus.Generators` and `[CrudAction]`.

## Entry points

| Call | Signature | Applied to | Notes |
|---|---|---|---|
| `[GenerateDto(typeof(Entity))]` | `internal sealed class GenerateDtoAttribute : Attribute` — ctor `GenerateDtoAttribute(Type entityType)`; `Exclude`, `Include`: `string[]` (default `[]`); `IgnoreComplexType`: `bool` (CLR default `false`) | An empty `partial` DTO shell (`AttributeTargets.Class \| Struct`) | `AllowMultiple = false`, `Inherited = false`. The attribute type is `internal` — see Gotchas. Shipped as a content file, so it compiles directly into **each** consuming project (no assembly reference to this package at runtime). |
| `DtoGenerator` | `public sealed class DtoGenerator : IIncrementalGenerator` | Roslyn compiler pipeline (never called from consumer code) | Emits one `.g.cs` per `[GenerateDto]` target. |
| `RaisesEventValidator` | `public sealed class RaisesEventValidator : IIncrementalGenerator` | Roslyn compiler pipeline | Reuses the same global-exclusion extraction logic as `DtoGenerator` for convention-form payloads. |
| `DtoGeneratorExclusions` (MSBuild property) | comma/semicolon-separated string | `.csproj` `<PropertyGroup>` | Declared `CompilerVisibleProperty` by this package's own `.props` file (ships `buildTransitive/`) — no manual wiring needed. Names are case-sensitive, collected into an ordinal `HashSet<string>`. |
| `DtoGeneratorIgnoreComplexType` (MSBuild property) | `"true"`/`"false"` string | `.csproj` `<PropertyGroup>` | Also `CompilerVisibleProperty` via the same `.props` file. |
| `GlobalDtoConfiguration.ConfigurationDocumentation` | `public static class GlobalDtoConfiguration { public const string ConfigurationDocumentation = "..."; }` | n/a | Documentation marker only. **Not** shipped as a content file — it lives only in the analyzer's own DLL, so a consuming project never sees this class at all. |

## Public surface

### `DKNet.EfCore.DtoGenerator` (analyzer DLL — consumer-visible members only)

| Type | Kind | Purpose |
|---|---|---|
| `DtoGenerator` | sealed class, `[Generator]` | Emits DTO properties for `[GenerateDto]` targets. |
| `RaisesEventValidator` | sealed class, `[Generator]` | Validates/generates `[RaisesEvent]` payloads. |
| `GlobalDtoConfiguration` | static class | Documentation-only marker; never referenced from consumer code. |

### `DKNet.EfCore.DtoGenerator` (compiled directly into every consuming project via a content file)

| Type | Kind | Key members |
|---|---|---|
| `GenerateDtoAttribute` | attribute, **`internal sealed`** | `GenerateDtoAttribute(Type entityType)`; `Type EntityType { get; }`; `string[] Exclude { get; set; } = []`; `string[] Include { get; set; } = []`; `bool IgnoreComplexType { get; set; }` (CLR default `false` — see Gotchas for why the generator's *effective* default differs) |

### Internal types that shape observable output (not consumer-visible, documented because they explain generated code)

- `DtoGenerator.Target` — per-DTO extraction result: `DtoSymbol`, `EntitySymbol`, `ExcludedProperties`/`IncludedProperties` (ordinal `HashSet<string>`), `IgnoreComplexType` (`bool?` — `null` means "not written in source", distinct from the attribute property's CLR-default `false`).
- `DtoGenerator.PropertyInfo` — per-property render metadata: `Name`, `TypeName`, `IsNonNullableString`, `IsCollection`, `IsComplexReferenceType`, `NullableAnnotation`, `ValidationAttributes`.
- `RaisesEventValidator.RaisesEventDeclaration` (record) — per-`[RaisesEvent]` metadata: `EntitySymbol`, `IsStringForm`, `EventTypeSymbol`, `LabelIsConstantString`, `Label`, `ComposedName`, `Operations` (int bitmask mirroring `EventOperations`), `Properties`, `ExcludeFilter`/`IncludeFilter`, `Location`.

## Options & defaults

No runtime options object — everything is compile-time (attribute arguments + MSBuild properties).

| Option | Type | Default | Effect |
|---|---|---|---|
| `[GenerateDto]` ctor arg `entityType` | `Type` | required | Entity to mirror, as `typeof(Entity)`. |
| `[GenerateDto].Exclude` | `string[]` | `[]` | Property names omitted. Combines with `DtoGeneratorExclusions`. Mutually exclusive with `Include` (`DKDTOGEN004` if both set — generation is skipped entirely for that DTO). |
| `[GenerateDto].Include` | `string[]` | `[]` | Non-empty ⇒ only these are generated; bypasses `Exclude`, the global list, and `IgnoreComplexType`. |
| `[GenerateDto].IgnoreComplexType` | `bool` (attribute-syntax presence checked, not the CLR default) | unset → falls through | Effective value = per-DTO attribute arg (if explicitly written) → `DtoGeneratorIgnoreComplexType` MSBuild property (if set) → built-in `true`. |
| `DtoGeneratorExclusions` | MSBuild property | unset | Comma/semicolon-separated property names excluded from every `[GenerateDto]` DTO and convention-form `[RaisesEvent]` payload in the project (case-sensitive). |
| `DtoGeneratorIgnoreComplexType` | MSBuild property | unset (built-in `true` applies) | Project-wide default for `IgnoreComplexType` when a DTO doesn't set it. |
| `EmitCompilerGeneratedFiles` / `CompilerGeneratedFilesOutputPath` | standard Roslyn MSBuild properties | unset | Write generated `.g.cs` to disk (e.g. `obj/Generated`) for inspection — not specific to this package. |

## Usage patterns

### Basic DTO from an entity

```csharp
using DKNet.EfCore.DtoGenerator;

namespace MyApp.Catalog;

public class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

[GenerateDto(typeof(Product))]
public partial record ProductDto;
```

Emits `ProductDto.g.cs` with `Id` (`Guid`), `required string Name`, `Price` (`decimal`) as `init`-only properties. The shell must be `partial` or the C# compiler (not this generator) reports a conflicting-partial-modifier error.

### Excluding or including specific properties

```csharp
using DKNet.EfCore.DtoGenerator;

namespace MyApp.Sales;

public class Order
{
    public Guid Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public decimal InternalCost { get; set; }
}

[GenerateDto(typeof(Order), Exclude = [nameof(Order.InternalCost)])]
public partial record OrderPublicDto;

[GenerateDto(typeof(Order), Include = [nameof(Order.Id), nameof(Order.Number)])]
public partial record OrderNumberDto;
```

`Exclude`/`Include` accept `nameof(...)`, string literals, or collection expressions. Specifying both on the same DTO produces `DKDTOGEN004` (Warning) and generation is skipped entirely for that DTO — no `.g.cs` at all, not even the unfiltered set.

### Project-wide audit-column exclusions

```xml
<!-- consuming project's .csproj -->
<PropertyGroup>
  <DtoGeneratorExclusions>CreatedBy,UpdatedBy,CreatedAt,UpdatedAt</DtoGeneratorExclusions>
</PropertyGroup>
```

```csharp
using DKNet.EfCore.DtoGenerator;

namespace MyApp.Billing;

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

Local `Exclude` combines with the global list. `Include` bypasses both and produces an informational `DKDTOGEN005` diagnostic naming how many global exclusions were skipped for that DTO.

### Opting a DTO into navigation properties

```csharp
using DKNet.EfCore.DtoGenerator;

namespace MyApp.Sales;

public class Address
{
    public string City { get; set; } = string.Empty;
}

public class Customer
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Address? PrimaryAddress { get; set; }
}

[GenerateDto(typeof(Customer), IgnoreComplexType = false)]
public partial record CustomerWithAddressDto;
```

Default effective `IgnoreComplexType` is `true`, which drops non-`record`, non-BCL reference-type properties (and their collection forms) unless marked `[Owned]`. Setting it `false` (per-DTO or via `DtoGeneratorIgnoreComplexType`) generates `Address? PrimaryAddress` on the DTO too — using `Address`'s own CLR shape, not a recursively generated, separately filtered DTO type.

### Declaring a domain event payload with `[RaisesEvent]` (type-naming form)

```csharp
using DKNet.EfCore.Abstractions.Events;
using DKNet.EfCore.DtoGenerator;

namespace MyApp.Catalog;

[GenerateDto(typeof(Widget))]
public partial record WidgetCreatedEvent;

[RaisesEvent(typeof(WidgetCreatedEvent), EventOperations.Created)]
public class Widget
{
    public Guid Id { get; set; }
    public decimal Price { get; set; }
}
```

`RaisesEventValidator` checks at build time that `WidgetCreatedEvent` carries `[GenerateDto]` for the *same* entity (`DKRAISEVT002` otherwise). This generator only validates/shapes the payload type — it never raises anything; `DKNet.EfCore.Events` does that via reflection after `SaveChanges`.

### Convention-form event payload (no hand-written record)

```csharp
using DKNet.EfCore.Abstractions.Events;

namespace MyApp.Membership;

[RaisesEvent(EventOperations.Created)]                                     // -> MemberCreatedEvent
[RaisesEvent("Touched", EventOperations.Updated, nameof(Member.Tier))]     // -> MemberTouchedTierUpdatedEvent
public class Member
{
    public Guid Id { get; set; }
    public string Tier { get; set; } = string.Empty;
}
```

Name = entity name + optional label + sorted narrowing properties + operations (canonical order `Created`, `Updated`, `Deleted`) + `Event`, composed by `EventNameComposer` — the same source file is linked into both this generator and `DKNet.EfCore.Abstractions`, so build-time and runtime names can never disagree. `IgnoreComplexType` is not configurable on convention forms — navigation properties are always omitted. `Exclude`/`Include` on the type-naming form (`[RaisesEvent(typeof(X), ...)]`) is a build error (`DKRAISEVT011`).

## Runtime behaviour

There is none — this package has zero runtime footprint. What happens is at **compile time**, in order:

1. `DtoGenerator.Initialize` registers a syntax provider matching any attributed type declaration; it resolves each `[GenerateDto]` usage's entity `Type` argument, `Exclude`/`Include` lists, and whether `IgnoreComplexType` was explicitly written in source (not just its CLR default).
2. It combines every target with `DtoGeneratorExclusions`/`DtoGeneratorIgnoreComplexType` from the project's MSBuild properties, then generates source per target.
3. Generation walks the entity's public instance readable properties up its base-type chain (de-duplicated by name, stopping at `object`/`ValueType`), reports `DKDTOGEN002` if none found, skips the DTO entirely with `DKDTOGEN004` if `Include`+`Exclude` are both set, reports `DKDTOGEN005` if `Include` is used alongside a non-empty global-exclusion list, resolves the effective `IgnoreComplexType` (`target.IgnoreComplexType ?? projectWideIgnoreComplexType ?? true`), filters the property set, reports `DKDTOGEN003` when anything was filtered, then emits the `.g.cs` source.
4. Any unhandled exception during a single target's generation is caught and reported as `DKDTOGEN001` — that target's DTO is skipped, other targets are unaffected.
5. `RaisesEventValidator` runs a separate pipeline: it collects every `[RaisesEvent]` declaration per type, cross-checks each against the compilation (payload/entity match, narrowing-property validity, name-collision checks against every other declaration in the compilation), and, for convention forms, emits the composed payload record using the same property-filtering/shaping logic `DtoGenerator` uses.

## Diagnostics & exceptions

| ID | Severity | When | Fix |
|---|---|---|---|
| `DKDTOGEN001` | Warning | Generation for one `[GenerateDto]` target threw an exception (caught per-target). | Fix the shell/entity per the included exception message; other DTOs still generate. |
| `DKDTOGEN002` | Warning | Resolved entity has zero eligible properties. | Check the `typeof(...)` reference and that the entity has public instance properties. |
| `DKDTOGEN003` | Info | N properties were filtered in/out — echoes the effective Include/Exclude list. | Informational only. |
| `DKDTOGEN004` | Warning | Both `Include` and `Exclude` set on the same `[GenerateDto]`. | Use one or the other. **No `.g.cs` is emitted at all** while this fires. |
| `DKDTOGEN005` | Info | `Include` used while `DtoGeneratorExclusions` is non-empty. | Informational — the global list was ignored for that DTO. |
| `DKRAISEVT001` | Error | A narrowing property (trailing `params string[]`) is a nested path or not a property of the entity. | Name a direct, non-nested property of the entity. |
| `DKRAISEVT002` | Error | Named payload type has no `[GenerateDto]`, or was generated from a different entity. | Point `[RaisesEvent(typeof(X), ...)]` at a `[GenerateDto(typeof(SameEntity))]` record. |
| `DKRAISEVT003` | Warning | Narrowing properties set on a rule without `EventOperations.Updated`. | Remove the narrowing list, or add `Updated` — it has no runtime effect otherwise. |
| `DKRAISEVT004` | Error | Composed convention-form name collides with an existing, incompatible type. | Rename via a `label`, or resolve the collision (a hand-authored stub `partial record` with no `[GenerateDto]` merges instead of colliding). |
| `DKRAISEVT005` | Error | Label isn't a compile-time constant string, or the composed name isn't a single valid C# identifier. | Use a `const`/literal label that produces a valid identifier. |
| `DKRAISEVT006` | Error | Two different entities in the same namespace compose the same event name. | Add a distinguishing `label`. |
| `DKRAISEVT007` | Error | A declaration names no `EventOperations` — can never raise anything. | Add at least one operation flag. |
| `DKRAISEVT008` | Error | Two declarations on the SAME entity compose the same event name. | Add a distinguishing `label` or narrowing set. |
| `DKRAISEVT009` | Error | Convention-form declaration sets both `Exclude` and `Include`. | Use one or the other; no record emitted otherwise. |
| `DKRAISEVT010` | Error | `Exclude`/`Include` filter names a property that isn't a direct property of the entity. | Name a direct property. |
| `DKRAISEVT011` | Error | `Exclude`/`Include` supplied on the type-naming form (`[RaisesEvent(typeof(Payload), ...)]`). | Shape the payload via its own `[GenerateDto]` filters instead. |

## Gotchas

- **`GenerateDtoAttribute` is `internal`, not `public`.** Because it's compiled directly into each consuming project as a content file, that's fine within one project, but you cannot reference the attribute type itself across a project boundary (e.g. a shared helper library that wants to build `[GenerateDto]` metadata reflectively from another assembly). Every project applying `[GenerateDto]` gets its own private copy of the attribute type.
- **The attribute's `IgnoreComplexType` CLR default (`false`) is NOT the generator's effective default (`true`).** The attribute property is a plain `bool`, but the generator inspects the attribute **syntax** for an explicit named argument and only treats it as `false` if you wrote `IgnoreComplexType = false` in source. Reading the attribute via reflection at runtime would show `false` even when the generator treated it as unset — don't infer generator behaviour by reflecting the attribute instance.
- **`Include`+`Exclude` together silently produces zero output, not a build error.** `DKDTOGEN004` is a Warning, so the build still succeeds green while the DTO shell is left with no generated properties at all.
- **Records and structs are never treated as navigation, even under `IgnoreComplexType = true`.** The navigation check only excludes `class`-kind reference types that aren't records/BCL/`[Owned]`. A `record`-typed "value object" property always survives flattening regardless of the flag.
- **Only `System.ComponentModel.DataAnnotations` attributes and `[SensitiveData]` are copied onto the generated property**, matched by short type name + namespace string, not by type reference. Your own custom validation attributes, `[JsonPropertyName]`, etc. are never carried — re-declare them by hand if needed (the generator won't stomp on a hand-written property of the same name — see next).
- **Hand-writing a property with the same name as a would-be-generated one silences the generator for that name.** This is the documented override point, not an error — but it means a wrong-*type* hand-written member (same exact name, wrong type) can quietly leave the property incorrectly shaped with no diagnostic. The match is a case-sensitive, exact-name comparison — a wrong-*case* typo (e.g. `name` vs `Name`) does **not** match, so the generator still emits the correctly-named, correctly-typed property alongside your differently-cased extra member; nothing is silenced in that case.
- **`RaisesEventValidator` never references `DKNet.EfCore.Abstractions` as an assembly**, even though it uses the `DKNet.EfCore.Abstractions.Events` namespace — that namespace resolves only because `EventNameComposer` is source-linked into this project. `EventOperations.Updated`'s value is hard-mirrored as a local constant rather than referencing the real enum. A domain project can declare `[RaisesEvent]` with only this generator + `DKNet.EfCore.Abstractions` referenced and it builds — the rule just never *raises* until `DKNet.EfCore.Events` is wired in.
- **A DTO carrying a `[SensitiveData]`-copied property makes that DTO's compiled `.g.cs` reference `DKNet.EfCore.Abstractions`** — the one exception to "generated DTOs have zero runtime coupling."
- **`Microsoft.CodeAnalysis.CSharp` is pinned below the solution-wide package version on purpose** — the generator must reference the *lowest* Roslyn version it needs, because an IDE's `csc` host can lag the SDK's compiler (`CS9057` otherwise). Don't "fix" this during a routine package-version bump.

## Anti-patterns & hallucination traps

- `[GenerateDto(typeof(Product))]`-generated DTO `.ToEntity()`, `.Adapt()`, `.FromEntity(...)` — **do not exist**. No mapping method is emitted; use Mapster/manual assignment.
- `DKNet.EfCore.DtoGenerator.GlobalDtoConfiguration.Exclusions` or any instance/static member beyond `ConfigurationDocumentation` — the class has exactly one member, and the class itself is never visible in a consuming project's compilation (not packed as content).
- `services.AddDtoGenerator(...)` or any DI registration — there is none; this is a compile-time-only analyzer with no `IServiceCollection` surface.
- Referencing `DKNet.EfCore.DtoGenerator.GenerateDtoAttribute` from a *different* project than where `[GenerateDto]` is applied and expecting it to resolve — it's `internal`, freshly compiled per-project, not a shared type.
- Assuming `IgnoreComplexType` defaults to `false` because the attribute property's CLR default is `false` — the generator's effective default (when unset in source) is `true`.
- Hand-writing the `[RaisesEvent]`-declared payload record's properties directly instead of applying `[GenerateDto]` to it, or hand-writing the composed convention-form record — the validator/generator does this for you. A hand-written non-`[GenerateDto]` stub is fine (it *merges*), but duplicating properties on it fights the generator.
- Expecting recursive DTO generation for a navigation property when `IgnoreComplexType = false` — the navigation property is emitted with its **original CLR type**, not a nested, separately-filtered DTO.

## Composes with

| Package | Rule |
|---|---|
| `DKNet.EfCore.Events` | The runtime counterpart — reads `[RaisesEvent]` via reflection after `SaveChanges` and raises the named payload (mapped via Mapster). Reach for it to actually raise the events this generator validates. |
| `DKNet.EfCore.Abstractions` | Declares `[RaisesEvent]`, `EventOperations`, `SensitiveDataAttribute`, and the entity base classes DTOs mirror. Needed only at the consuming project's own reference level — this generator itself never references it as an assembly. |
| `DKNet.EfCore.Specifications` | A model-projection specification projects an entity onto a model type inside the SQL query itself. Reach for that instead of this generator when you want the projection pushed into SQL rather than a hand-mapped DTO type. |
| `DKNet.EfCore.AuditLogs` | Consumes the `[SensitiveData]` attribute carried onto generated DTO properties for audit-log redaction. |
| `DKNet.SlimBus.Generators` | Resolves the `[GenerateDto(typeof(Entity))]` DTO for a `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]`-attributed entity — every CRUD-generated entity needs exactly one. |

## Testing notes

This package has no runtime, so testing a generated DTO needs no database, no container, and no generator internals — plain reflection against the compiled type is enough:

```csharp
using System.Reflection;

namespace MyApp.Catalog.Tests;

public class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
```

```csharp
using DKNet.EfCore.DtoGenerator;

namespace MyApp.Catalog.Tests;

[GenerateDto(typeof(Product))]
public partial record ProductDto;
```

```csharp
using System.Reflection;
using MyApp.Catalog.Tests;

PropertyInfo? nameProperty = typeof(ProductDto).GetProperty(nameof(Product.Name));
PropertyInfo? idProperty = typeof(ProductDto).GetProperty(nameof(Product.Id));
Console.WriteLine($"{nameProperty?.PropertyType} {idProperty?.PropertyType}");
```

Assert directly against `PropertyInfo`s the way the example above does — `GetProperty`/`GetProperties` — rather than mocking anything. If the DTO is also consumed through Mapster, round-trip a real entity through `mapper.Map<ProductDto>(entity)` (or `.Adapt<ProductDto>()`) to confirm property-name/type compatibility in the same test. For fixture patterns shared across DKNet-based test suites, see the `dknet-testing` skill.
