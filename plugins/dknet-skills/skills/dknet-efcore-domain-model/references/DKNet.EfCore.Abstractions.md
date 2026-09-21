# DKNet.EfCore.Abstractions

| Field | Value |
|---|---|
| Area | EfCore |
| Install | `dotnet add package DKNet.EfCore.Abstractions` |
| NuGet | https://www.nuget.org/packages/DKNet.EfCore.Abstractions |
| Docs | https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/DKNet.EfCore.Abstractions.md |
| Source | https://github.com/baoduy/DKNet/tree/dev/src/EfCore/DKNet.EfCore.Abstractions |
| Depends on (DKNet) | none |
| Depends on (3rd party) | `Microsoft.EntityFrameworkCore.Abstractions`, `System.ComponentModel.Annotations`, `FluentResults` |
| Target framework | `net10.0` |

## Purpose

The shared, persistence-agnostic vocabulary every other `DKNet.EfCore.*` package builds on: entity identity/base
classes (`Entity<TKey>`/`Entity`), domain-event contracts (`IEventEntity`, `IEventPublisher`, `IEventItem`,
`[RaisesEvent]`), audit/concurrency/soft-delete interfaces, and the attributes that steer audit-log, sequence and
CRUD-generator behaviour. It deliberately has **no** dependency on `Microsoft.EntityFrameworkCore` (only its
`.Abstractions` slice), so a domain project can model entities and raise events without pulling in the EF Core
runtime. It is NOT where any of that behaviour actually runs — no DI registration exists in this package; every
interface/attribute here is inert until a sibling package (`DKNet.EfCore.Extensions`, `.Hooks`, `.Events`,
`.AuditLogs`, `.DataAuthorization`, `DKNet.SlimBus.Generators`) reads it at model-build time, save time, or
compile time.

## Entry points

There is no DI, `ModelBuilder`, or `DbContextOptionsBuilder` extension method in this package. The only "entry
points" a consumer touches are the base classes to derive from and the attributes to apply.

| Call | Exact signature | Called on | Notes |
|---|---|---|---|
| Derive from `Entity<TKey>` / `Entity` | `public abstract class Entity<TKey> : IEntity<TKey>, IEventEntity` / `public abstract class Entity : Entity<Guid>` | your entity class declaration | `Id` has a `private set`; two `protected` constructors (`Entity()`, `Entity(TKey id)` — the id-ctor is "primarily for EF Core data seeding" per its own XML doc). |
| Derive from `AuditedEntity<TKey>` / `AuditedEntity` | `public abstract class AuditedEntity<TKey> : Entity<TKey>, IAuditedEntity<TKey>` / `public abstract class AuditedEntity : AuditedEntity<Guid>` | your entity class declaration | Adds `CreatedBy`/`CreatedOn`/`UpdatedBy`/`UpdatedOn` + `protected SetCreatedBy`/`SetUpdatedBy`. Nothing stamps these automatically — see Runtime behaviour. |
| Implement `IConcurrencyEntity<TType>` | `public interface IConcurrencyEntity<TType> { TType? RowVersion { get; } void SetRowVersion(TType rowVersion); }` | your entity class | Only takes effect once `DKNet.EfCore.Extensions`' `DefaultEntityTypeConfiguration<TEntity>` configures the column (reflection-detected). |
| Implement `ISoftDeletableEntity` | `public interface ISoftDeletableEntity { bool IsDeleted { get; } DateTimeOffset? DeletedOn { get; } string? DeletedBy { get; } IResultBase Delete(string byUser, DateTimeOffset? deletedOn = null); }` | your entity class | No shipped package wires a query filter for this — contract only (see Gotchas). |
| `[RaisesEvent(...)]` | `[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]` — three ctor overloads (see Public surface) | entity class | Inert without `DKNet.EfCore.Events`' save hook registered; validated at build time by `DKNet.EfCore.DtoGenerator`. |
| `[Sequence(...)]` | `[AttributeUsage(AttributeTargets.Field)]` | enum member (not an entity property) | Read by `DKNet.EfCore.Extensions` during `UseAutoConfigModel`, SQL Server/Npgsql only. |
| `[SqlSequence(schema)]` | `[AttributeUsage(AttributeTargets.Enum)]` | enum declaration | Names the DB schema for every `[Sequence]` member of that enum; default `"seq"`. |
| `[AuditLog]` / `[IgnoreAuditLog]` | `[AttributeUsage(AttributeTargets.Class \| AttributeTargets.Property, Inherited = false)]` | entity class or property | Zero behaviour here — read entirely by `DKNet.EfCore.AuditLogs`, gated on the type implementing `IAuditedProperties`. |
| `[SensitiveData(params roles)]` | `[AttributeUsage(AttributeTargets.Property, Inherited = false)]` | entity property | Read by `DKNet.EfCore.AuditLogs` (redaction, unconditional) and `DKNet.EfCore.Extensions` (API response filtering, opt-in per host). |
| `[IgnoreEntity]` | `[AttributeUsage(AttributeTargets.Class, Inherited = false)]` | entity class | **No shipped consumer** — declared but not wired to anything. |
| `[CrudCreate]` / `[CrudUpdate]` / `[CrudAction]` | see Public surface | constructor/method | Consumed entirely by `DKNet.SlimBus.Generators` — see the `dknet-codegen` skill; this package only supplies the attribute types. |

## Public surface

### `DKNet.EfCore.Abstractions.Entities`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `IEntity<out TKey>` | interface | Minimal identity contract | `TKey Id { get; }` |
| `Entity<TKey>` | abstract class | Base entity; implements `IEntity<TKey>`, `IEventEntity` | `protected Entity()`; `protected Entity(TKey id)`; `virtual TKey Id { get; private set; }`; `void AddEvent(object)`; `void AddEvent<TEvent>() where TEvent : class`; `void ClearEvents()`; `(object[] Events, Type[] EventTypes) GetEvents()`; `override string ToString()` → `"{TypeName} '{Id}'"` |
| `Entity` | abstract class | `Entity<Guid>` specialization | `protected Entity()`; `protected Entity(Guid id)` |
| `IAuditedProperties` | interface | The 4 audit fields | `[IgnoreAuditLog] DateTimeOffset CreatedOn { get; }`; `[IgnoreAuditLog] DateTimeOffset? UpdatedOn { get; }`; `[IgnoreAuditLog][MaxLength(500)] string CreatedBy { get; }`; `[MaxLength(500)][IgnoreAuditLog] string? UpdatedBy { get; }` |
| `IAuditedEntity<out TKey>` | interface | `IEntity<TKey>` + `IAuditedProperties` combined | no members of its own |
| `AuditedEntity<TKey>` | abstract class | Base audited entity; `Entity<TKey>` + `IAuditedEntity<TKey>` | `protected AuditedEntity()`; `protected AuditedEntity(TKey id)`; `[MaxLength(500)] string CreatedBy { get; private set; }`; `DateTimeOffset CreatedOn { get; private set; }`; `[NotMapped] string LastModifiedBy => UpdatedBy ?? CreatedBy`; `[NotMapped] DateTimeOffset LastModifiedOn => UpdatedOn ?? CreatedOn`; `[MaxLength(500)] string? UpdatedBy { get; private set; }`; `DateTimeOffset? UpdatedOn { get; private set; }`; `protected void SetCreatedBy(string userName, DateTimeOffset? createdOn = null)`; `protected void SetUpdatedBy(string userName, DateTimeOffset? updatedOn = null)` |
| `AuditedEntity` | abstract class | `AuditedEntity<Guid>` specialization | `protected AuditedEntity()`; `protected AuditedEntity(Guid id)` |
| `IConcurrencyEntity<TType>` | interface | Optimistic concurrency contract | `[IgnoreAuditLog][Column(Order = 1000)][Timestamp] TType? RowVersion { get; }`; `void SetRowVersion(TType rowVersion)` |
| `ISoftDeletableEntity` | interface | Soft-delete contract | `bool IsDeleted { get; }`; `DateTimeOffset? DeletedOn { get; }`; `[MaxLength(250)] string? DeletedBy { get; }`; `IResultBase Delete(string byUser, DateTimeOffset? deletedOn = null)` |

### `DKNet.EfCore.Abstractions.Events`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `IEventEntity` | interface | Per-entity domain-event queue | `void AddEvent(object eventObj)`; `void AddEvent<TEvent>() where TEvent : class`; `(object[] Events, Type[] EventTypes) GetEvents()`; `void ClearEvents()` |
| `EventOperations` | `[Flags]` enum | Lifecycle operation bitmask | `Created = 1`, `Updated = 2`, `Deleted = 4` |
| `RaisesEventAttribute` | sealed attribute (class, repeatable, not inherited) | Declares an auto-raised event | `RaisesEventAttribute(Type eventType, EventOperations operations, params string[] properties)`; `RaisesEventAttribute(string label, EventOperations operations, params string[] properties)`; `RaisesEventAttribute(EventOperations operations, params string[] properties)`; `Type? EventType { get; }`; `string? Label { get; }`; `EventOperations Operations { get; }`; `IReadOnlyList<string> Properties { get; }`; `string[] Exclude { get; set; }` (default `[]`); `string[] Include { get; set; }` (default `[]`) |
| `EventNameComposer` | static class | Fixed-convention name composer, linked into `DKNet.EfCore.DtoGenerator` too | `static string Compose(string entityName, string? label, IReadOnlyList<string>? properties, int operations)` — order: entity name → label (if non-empty) → properties (`Distinct(Ordinal).OrderBy(Ordinal)`) → operations always emitted `Created`,`Updated`,`Deleted` in that fixed order regardless of flag combination order → suffix `"Event"` |
| `IEventPublisher` | interface | Event-publishing sink | `Task PublishAsync(object eventObj, CancellationToken cancellationToken = default)`; `Task PublishAsync(IEnumerable<object> eventList, CancellationToken cancellationToken = default)` |
| `DefaultEventPublisher` | abstract class | Batch-for-free base | `abstract Task PublishAsync(object, CancellationToken)`; `virtual Task PublishAsync(IEnumerable<object>, CancellationToken)` — sequential `foreach` awaiting the single-event method |
| `IEventItem` | interface | Optional event-payload shape | `[JsonIgnore] IDictionary<string,string> AdditionalData { get; }`; `string EventType { get; }` |
| `EventItem` | abstract record, implements `IEventItem` | Base payload record | `virtual IDictionary<string,string> AdditionalData { get; }` defaults to `new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)`; `virtual string EventType => GetType().FullName ?? nameof(EventItem)` |

### `DKNet.EfCore.Abstractions.Attributes`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `SequenceAttribute` | sealed attribute (`AttributeTargets.Field`) | Describes one DB sequence on an enum member | `SequenceAttribute(Type? type = null)` — throws `NotSupportedException` unless `type` ∈ {`byte`,`short`,`int`,`long`} (defaults `typeof(int)`); `bool Cyclic { get; set; } = true`; `string? FormatString { get; set; }`; `int IncrementsBy { get; set; } = -1`; `long Max { get; set; } = -1`; `long Min { get; set; } = -1`; `long StartAt { get; set; } = -1`; `Type Type { get; }` (read-only, set by ctor) |
| `SqlSequenceAttribute` | sealed attribute (`AttributeTargets.Enum`) | Names the DB schema for an enum's sequences | `SqlSequenceAttribute(string schema = "seq")`; `string Schema { get; }` |
| `AuditLogAttribute` | sealed attribute (`Class \| Property`, not inherited) | Audit opt-in/allow-list marker, no members | — |
| `IgnoreAuditLogAttribute` | sealed attribute (`Class \| Property`, not inherited) | Unconditional audit exclusion marker, no members | — |
| `SensitiveDataAttribute` | sealed attribute (`Property`, not inherited) | Redaction + role-gated response filtering | `SensitiveDataAttribute(params string[] roles)`; `SensitiveDataAttribute()` (explicit parameterless ctor kept for binary compat, delegates to `this([])`); `IReadOnlyList<string> Roles { get; }` — never null; a `null` array argument collapses to empty |
| `IgnoreEntityAttribute` | sealed attribute (`Class`, not inherited) | Marker to exclude a type from "automatic entity mapping" | no members; **no shipped consumer** |
| `CrudCreateAttribute` | sealed attribute (`Constructor \| Method`, `AllowMultiple = false`) | Marks the Create member for `DKNet.SlimBus.Generators` | `string? Name { get; set; }` (default `null`) |
| `CrudUpdateAttribute` | sealed attribute (`Method`, `AllowMultiple = false`) | Marks an Update method | `string? Name { get; set; }` (default `null`) |
| `CrudActionAttribute` | sealed attribute (`Method`, `AllowMultiple = false`) | Marks a named domain-action endpoint | `CrudActionAttribute(string? route = null)`; `string? Route { get; }`; `CrudActionVerb Verb { get; set; } = CrudActionVerb.Post`; `string? Name { get; set; }` |
| `CrudActionVerb` | enum | HTTP verb for `[CrudAction]` | `Post = 0` (default), `Put = 1`, `Patch = 2` — underlying int values are load-bearing (`CrudModelBuilder.ResolveActionHttpMethod` in `DKNet.SlimBus.Generators` switches on the raw int). Never reorder. |

The last four rows (`[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]`/`CrudActionVerb`) are inventory only — how to
apply them and what gets generated is `dknet-codegen`'s job, not this skill's.

## Options & defaults

No options object and no DI registration exists in this package. Its entire customisation surface is attribute
constructor arguments/properties, tabulated above under Public surface. The only values worth calling out as
"defaults with runtime meaning":

| Option | Type | Default | Effect | Where set |
|---|---|---|---|---|
| `SequenceAttribute.Type` | `Type` | `typeof(int)` | Sequence's SQL data type; only `byte`/`short`/`int`/`long` accepted | ctor arg |
| `SequenceAttribute.StartAt`/`IncrementsBy`/`Min`/`Max` | `long`/`int`/`long`/`long` | `-1` | `-1` ("unset") is read by `DKNet.EfCore.Extensions` as "leave it to the database"; only values `> 0` are applied | property setters |
| `SequenceAttribute.Cyclic` | `bool` | `true` | Always applied (no "unset" sentinel) | property setter |
| `SqlSequenceAttribute.Schema` | `string` | `"seq"` | DB schema for every `[Sequence]` member of the enum | ctor arg |
| `CrudActionAttribute.Verb` | `CrudActionVerb` | `Post` | HTTP verb the generated endpoint registers | property setter |
| `SensitiveDataAttribute.Roles` | `IReadOnlyList<string>` | empty (never null) | Empty = any *authenticated* caller, not "everyone" | ctor `params` arg |

## Usage patterns

### Modelling a plain aggregate with domain events

```csharp
using DKNet.EfCore.Abstractions.Entities;

public class Order : Entity // Entity<Guid>
{
    private Order() { } // EF Core

    public Order(string customer) : base(Guid.NewGuid())
    {
        Customer = customer;
        AddEvent(new OrderPlacedEvent(Id));
    }

    public string Customer { get; private set; } = null!;
}

public record OrderPlacedEvent(Guid OrderId);
```

**Notes**: `AddEvent` only queues the event on the entity — nothing is dispatched until `DKNet.EfCore.Events`'
save hook drains the queue during a successful `SaveChangesAsync`. Referencing only this package, the code above
compiles and runs, but `OrderPlacedEvent` is never published.

### Audit-tracked entity with first-write-wins semantics

```csharp
using DKNet.EfCore.Abstractions.Entities;

public class Invoice : AuditedEntity
{
    private Invoice() { }

    public static Invoice Create(string createdBy)
    {
        var invoice = new Invoice();
        invoice.SetCreatedBy(createdBy); // no-op if CreatedBy already set
        return invoice;
    }

    public void MarkPaid(string updatedBy) => SetUpdatedBy(updatedBy);
}
```

**Notes**: `SetCreatedBy` validates `userName` via `ArgumentException.ThrowIfNullOrWhiteSpace`, which throws
`ArgumentNullException` for a null `userName` and the base `ArgumentException` for an empty/whitespace one.
`SetUpdatedBy` checks its timestamp guard *before* validating `userName` — when the supplied `updatedOn` is
older than the currently stored `UpdatedOn`, it returns immediately and `userName` is never validated on that
path, even a null/blank one. `userName` is validated only when the guard does not trigger.

### Optimistic concurrency

```csharp
using DKNet.EfCore.Abstractions.Entities;

public class Account : Entity, IConcurrencyEntity<byte[]>
{
    private Account() { }
    public Account(decimal balance) : base(Guid.NewGuid()) => Balance = balance;

    public decimal Balance { get; private set; }
    public byte[]? RowVersion { get; private set; }
    public void SetRowVersion(byte[] rowVersion) => RowVersion = rowVersion;
}
```

**Notes**: implementing the interface alone changes nothing in the DB — `DKNet.EfCore.Extensions`'
`DefaultEntityTypeConfiguration<TEntity>` must detect it (by reflection) and configure `RowVersion` as a
`[Timestamp]`/`ValueGeneratedOnAddOrUpdate()` concurrency token.

### Declarative event via convention naming

```csharp
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.Abstractions.Events;

[RaisesEvent(EventOperations.Created)]                                   // -> CustomerCreatedEvent
[RaisesEvent("Touched", EventOperations.Updated, nameof(Customer.Tier))] // -> CustomerTouchedTierUpdatedEvent
public class Customer : Entity
{
    private Customer() { }
    public Customer(string tier) : base(Guid.NewGuid()) => Tier = tier;
    public string Tier { get; set; } = string.Empty;
}
```

**Notes**: no hand-written payload record is needed — `DKNet.EfCore.DtoGenerator` generates it by the fixed
`EventNameComposer` convention. `Operations == 0` is a build error (`DKRAISEVT007`); narrowing `Updated` to
`nameof(Customer.Tier)` only fires when that direct property changes — nested/owned-value changes never satisfy
it. This attribute alone raises nothing until the application also registers `DKNet.EfCore.Events`.

### Soft-delete contract (you own the query filter)

```csharp
using DKNet.EfCore.Abstractions.Entities;
using FluentResults;

public class Document : Entity, ISoftDeletableEntity
{
    private Document() { }
    public Document(string name) : base(Guid.NewGuid()) => Name = name;

    public string Name { get; private set; } = null!;
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedOn { get; private set; }
    public string? DeletedBy { get; private set; }

    public IResultBase Delete(string byUser, DateTimeOffset? deletedOn = null)
    {
        IsDeleted = true;
        DeletedBy = byUser;
        DeletedOn = deletedOn ?? DateTimeOffset.UtcNow;
        return Result.Ok();
    }
}
```

**Notes**: this package ships no query filter or interceptor. You must add a `GlobalQueryFilter` (or a direct
`modelBuilder.Entity<Document>().HasQueryFilter(...)` call) yourself in your `DbContext` — see the sibling
[DKNet.EfCore.Extensions reference](DKNet.EfCore.Extensions.md) for the `GlobalQueryFilter` pattern; nothing
here does it for you.

### CRUD vertical-slice markers (consumed elsewhere)

`[CrudCreate]`, `[CrudUpdate]` and `[CrudAction]` mark a constructor/method for `DKNet.SlimBus.Generators` to
turn into a generated request/handler/minimal-API endpoint. This package only supplies the attribute types and
enforces (via its own `NetArchTest`-based architecture tests) that this assembly never depends on
`Microsoft.AspNetCore` or `DKNet.AspCore.Extensions` — a project referencing only `DKNet.EfCore.Abstractions`
compiles cleanly and produces no endpoints. `[CrudUpdate]` and `[CrudAction]` on the same method is a build
error. See the `dknet-codegen` skill for how to apply these attributes and what gets generated.

## Runtime behaviour

This package itself executes nothing at runtime — it is pure types. What actually happens, verified against its
own XML docs and the docs page's description of the consuming packages:

1. Domain code calls `AddEvent(object)` or `AddEvent<TEvent>()` on an `Entity<TKey>` — this appends to one of two
   private `Collection<>` fields on the entity instance. Nothing else happens yet.
2. Separately, `SaveChangesAsync` runs. `DKNet.EfCore.Events`' `EventContext`/`EventHook` (outside this package)
   scans tracked entries for `IEventEntity` and calls `GetEvents()` on each — a pure `ToArray()` read that does
   **not** clear the queue. The queue is only cleared afterwards by a separate `ClearEvents()` call, made once
   every publisher has already run. `EventContext` maps any type-only entries through a registered `IMapper`;
   `EventHook` separately reads `[RaisesEvent]` via reflection in `BeforeSaveAsync` (evaluating
   `EntityState`/property-narrowing *before* the save happens, since `IsModified`/original state are meaningless
   afterwards), and in `AfterSaveAsync` — after the save has already committed — hands every resulting event
   object to each registered `IEventPublisher.PublishAsync`, only then calling `ClearEvents()`.
3. `AuditedEntity`'s `SetCreatedBy`/`SetUpdatedBy` are plain in-memory setters called from your own domain code —
   nothing in this package calls them automatically. `DKNet.EfCore.AuditLogs`' `EfCoreAuditHook` and
   `DKNet.EfCore.DataAuthorization`'s `DataOwnerHook` (both plugging into `DKNet.EfCore.Hooks`' save pipeline)
   stamp `CreatedBy`/`UpdatedBy` from a signed-in user or an ownership key — but they do it by writing the
   tracked EF property value directly (`EntityEntry.Property(...).CurrentValue`, with a reflection fallback),
   **never** by calling `SetCreatedBy`/`SetUpdatedBy`. `EfCoreAuditHook` takes priority whenever a registered
   `ICurrentUserProvider` returns a non-empty value for that save; `DataOwnerHook` only stamps those same audit
   fields itself (from the ownership key) when no current-user value was available, and always skips a field
   your own domain code already set.
4. `[Sequence]`/`[SqlSequence]` do nothing at model-build time from this package alone; `DKNet.EfCore.Extensions`
   reads them during `UseAutoConfigModel` (SQL Server/Npgsql only) to register a database sequence named
   `Seq_{enumMemberName}`.

## Diagnostics & exceptions

This package defines no `DiagnosticDescriptor`s or analyzers of its own (the diagnostics named below are emitted
by `DKNet.EfCore.DtoGenerator` at build time against the attributes declared here; listed because an agent using
`[RaisesEvent]` will hit them).

| ID or exception type | Severity | When | Fix |
|---|---|---|---|
| `NotSupportedException` | Runtime (attribute construction) | `[Sequence(typeof(T))]` where `T` is not `byte`/`short`/`int`/`long` | Use one of the four supported numeric types, or omit the argument for the `int` default. |
| `ArgumentNullException` (null) / `ArgumentException` (empty or whitespace) — both via `ArgumentException.ThrowIfNullOrWhiteSpace` | Runtime | `SetCreatedBy`/`SetUpdatedBy` called with a null/empty/whitespace `userName`; on `SetUpdatedBy` only reached when its timestamp guard doesn't already no-op first | Pass a non-blank user name. |
| `DKRAISEVT005` (build error) | Build | Either the `[RaisesEvent]` label isn't a compile-time constant string, or the composed name isn't a single valid C# identifier | Pass a literal string label; use a label that is a valid identifier fragment. |
| `DKRAISEVT006` (build error) | Build | Two `[RaisesEvent]` declarations on different entities in the same namespace compose to the same name | Rename via a distinguishing label. |
| `DKRAISEVT007` (build error) | Build | `[RaisesEvent]` declared with `Operations == 0` | Name at least one of `Created`/`Updated`/`Deleted`. |
| `DKRAISEVT008` (build error) | Build | Two `[RaisesEvent]` declarations on the *same* entity compose to the same name | Rename via a distinguishing label. |
| `DKRAISEVT009` (build error) | Build | `Exclude` and `Include` both set on one `[RaisesEvent]` declaration | Use only one. |
| `DKRAISEVT010` (build error) | Build | `Exclude`/`Include` names a property that isn't a direct property of the entity | Name only direct, existing scalar properties. |
| `DKRAISEVT011` (build error) | Build | `Exclude`/`Include` used on the type-naming form of `[RaisesEvent]` | Shape the payload via the named record's own `[GenerateDto]` `Exclude`/`Include` instead. |
| `DKRAISEVT003` (build warning) | Build (Warning) | `Properties` narrowing given on a `[RaisesEvent]` rule whose `Operations` has no `Updated` flag | Remove the narrowing list, or add `Updated` to `Operations`, since it is otherwise a no-op. |

## Gotchas (source-verified)

- **Implementing an interface is not enough by itself.** `IAuditedProperties`, `IConcurrencyEntity<>`,
  `ISoftDeletableEntity`, `IEventEntity` are pure contracts — none stamp, filter, or dispatch anything on their
  own.
- **`AddEvent<TEvent>()` requires an `IMapper` — but only at dispatch time.** `Entity<TKey>.AddEvent<TEvent>()`
  just adds `typeof(TEvent)` to a list; a domain project referencing only this package compiles and runs fine,
  and only fails with `EventException` once `DKNet.EfCore.Events` tries to drain the queue without a registered
  `IMapper`. `AddEvent(object)` has no such dependency.
- **`SetUpdatedBy` is monotonic, not "always overwrite".** `AuditedEntity.SetUpdatedBy`'s `if (updatedOn <
  UpdatedOn) return;` guard silently ignores an out-of-order timestamp *before* validating `userName` — read the
  two-line body carefully before assuming either "always validates" or "always writes".
- **`SetCreatedBy` is first-write-wins.** `AuditedEntity.SetCreatedBy` — a call after `CreatedBy` is already
  non-empty is a silent no-op (no exception, no update), even if you intended to correct it.
- **`[Sequence]` targets the enum *field*, not an entity property** (`SequenceAttribute`'s
  `[AttributeUsage(AttributeTargets.Field)]`). Putting it on an entity property will not compile against this
  attribute's usage restriction.
- **`[Sequence]`'s numeric defaults are `-1`, meaning "database default", not "zero".** `StartAt`/`IncrementsBy`/
  `Min`/`Max` all default to `-1`; only values `> 0` are read as "apply this" by the consuming package — a `0`
  is silently treated the same as unset, not rejected. `Cyclic` has no such sentinel and is always applied
  (default `true`).
- **`[IgnoreEntity]` has zero consumers in this repo today.** Only its own definition and its own unit tests
  reference it. Do not rely on it to exclude a type from anything until you've confirmed the specific tool
  you're using reads it — `DKNet.EfCore.Extensions`' `UseAutoConfigModel` does not (see that package's own
  gotchas).
- **`SensitiveDataAttribute()` has an explicit parameterless constructor**, not just a `params` array called
  with zero arguments — kept deliberately for binary compatibility with assemblies compiled against the
  marker-only shape before `Roles` existed. Functionally identical to `new SensitiveDataAttribute([])`, but
  don't "simplify" it away in a refactor without checking that comment.
- **`CrudActionVerb`'s underlying int values (`Post=0`, `Put=1`, `Patch=2`) are load-bearing** —
  `DKNet.SlimBus.Generators`' `CrudModelBuilder.ResolveActionHttpMethod` switches on the raw int. Never reorder
  the enum members.
- **Two independent architecture tests enforce the ASP.NET Core boundary**: this assembly must never reference
  `Microsoft.AspNetCore` or `DKNet.AspCore.Extensions`. A new `PackageReference` that transitively pulls either
  in fails these tests, not just "looks wrong" in review.

## Anti-patterns & hallucination traps

- **`AggregateRoot` does not exist in this package.** No such type is declared anywhere in
  `DKNet.EfCore.Abstractions`. The actual base class to derive an aggregate from is `Entity<TKey>` / `Entity`.
  Do not write `class Order : AggregateRoot` — it will not compile.
- **`Entity<TKey>.Id` has a `private` setter.** There is no public `SetId`/`WithId` method — the only way to set
  `Id` after construction is via the `protected Entity(TKey id)` constructor, called from a derived type's own
  constructor.
- **`RaisesEventAttribute`'s string-form argument is a *label*, not a literal event name.**
  `[RaisesEvent("CustomerTouched", EventOperations.Created)]` does **not** generate a type named
  `CustomerTouched` — it composes `CustomerTouchedCreatedEvent`. Don't assume the string argument is used
  verbatim.
- **`[SensitiveData]` does not encrypt anything.** It only affects audit-log redaction and (opt-in) API response
  filtering. Column-level encryption at rest is `DKNet.EfCore.Encryption`'s separate `[Encrypted]` attribute —
  the two are unrelated and not interchangeable.
- **`[SensitiveData("someRole")]` alone changes nothing about API responses.** The role-gated filtering only
  activates once the *host* opts in via `DKNet.EfCore.Extensions`' `UseRoleAwareSensitiveData` on
  `JsonSerializerOptions`. Without that call, the property serializes in full to every caller regardless of
  role.
- **Don't hand-write the request/handler/endpoint for a `[CrudAction]`/`[CrudCreate]`/`[CrudUpdate]`-marked
  member.** `DKNet.SlimBus.Generators` emits it; a hand-written duplicate is a build error (duplicate type).
- **`DKNet.EfCore.Repos`/`DKNet.EfCore.Repos.Abstractions` are gone** — don't suggest a repository pattern built
  on interfaces from this package; the DKNet convention is `DKNet.EfCore.Specifications` (see
  `dknet-efcore-specifications`), which does not actually require `IEntity<TKey>`/`Entity<TKey>` at the
  interface level but is conventionally used with them.
- **This package has no `Microsoft.EntityFrameworkCore` reference**, only `.Abstractions`. Don't import
  `Microsoft.EntityFrameworkCore.DbContext`/`DbSet<>` etc. expecting it to resolve from this package alone.

## Composes with

| Package | Rule |
|---|---|
| `DKNet.EfCore.Extensions` | Reach for it to make `IEntity<TKey>`, `IAuditedProperties`, `IConcurrencyEntity<>`, and `[Sequence]` take effect in the EF Core model (`DefaultEntityTypeConfiguration<TEntity>`, `UseAutoConfigModel`). Also the host opt-in point for `[SensitiveData]` role filtering. |
| `DKNet.EfCore.Hooks` | Reach for it when you need a custom before/after-save hook; it supplies the pipeline the other packages plug into. This package defines no hook types itself. |
| `DKNet.EfCore.Events` | Reach for it to actually dispatch events queued via `AddEvent`/declared via `[RaisesEvent]`. Without it, events queue and are silently dropped on `ClearEvents()`/never drained. |
| `DKNet.EfCore.AuditLogs` | Reach for it for a field-level audit trail driven by `[AuditLog]`/`[IgnoreAuditLog]`/`[SensitiveData]`; gates everything on `IAuditedProperties` first. |
| `DKNet.EfCore.DataAuthorization` | Reach for it for row-level multi-tenant ownership; stamps `CreatedBy`/`UpdatedBy` from the ownership key only when no current-user value was available for that save. |
| `DKNet.EfCore.DtoGenerator` | Reach for it when a declared `[RaisesEvent]` rule won't resolve — it validates the rule at build time and generates the convention-form payload record. |
| `DKNet.EfCore.Specifications` | The supported way to query/persist entities built on this package's base classes; not a hard dependency, but the DKNet convention. |
| `DKNet.EfCore.Encryption` | Reach for it to protect a value at rest via its own `[Encrypted]` attribute — do not conflate with `[SensitiveData]` here, which only affects the audit trail and API responses. |
| `DKNet.SlimBus.Generators` | The only consumer of `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]`; reach for it to turn those markers into actual generated endpoints. |

## Testing notes

- Test project: xUnit + Shouldly, per repo convention; no database or containers needed — this package has no
  EF Core runtime dependency to test against.
- Pattern: minimal concrete test doubles per base type (`TestEntity : Entity<int>, IConcurrencyEntity<byte[]>`,
  `TestGuidEntity : Entity`, `TestAuditedEntity : AuditedEntity<int>`, `TestAuditedGuidEntity : AuditedEntity`)
  declared at the top of the corresponding test file, then asserted against directly — no mocking framework.
- Attribute tests assert `[AttributeUsage]` metadata via reflection (`GetCustomAttribute<AttributeUsageAttribute>()`)
  rather than only behavioural defaults. Follow this pattern for a new attribute: assert `ValidOn`,
  `AllowMultiple`, `Inherited` explicitly. A naming-convention test suite for `EventNameComposer` is the single
  source both the build-time generator and the save-time runtime share — add a case there for a new
  naming-convention scenario.
- An architecture-boundary test suite uses `NetArchTest.Rules` (`Types.InAssembly(...).Should()
  .NotHaveDependencyOn(...)`) to enforce the ASP.NET Core boundary at the assembly level — add a similar test
  rather than a comment if you introduce a new boundary invariant for this package.
