# DKNet.EfCore.Abstractions

The shared, persistence-agnostic vocabulary every other `DKNet.EfCore.*` package builds on — entity base classes,
domain-event contracts, and the attributes that steer audit, sequence, and mapping behaviour.

## ✨ Why use it?

- **One entity contract instead of eight** — Events, Hooks, AuditLogs, DataAuthorization, Extensions, DtoGenerator,
  and the retired Repos packages all read the same `IEntity<TKey>` / `IAuditedProperties` / `IEventEntity` /
  `IConcurrencyEntity<T>` / `ISoftDeletableEntity` contracts, so they interoperate on the same model instead of each
  inventing its own marker interface.
- **The domain layer stays free of EF Core** — the package references
  `Microsoft.EntityFrameworkCore.Abstractions`, `System.ComponentModel.Annotations`, and `FluentResults`, **not**
  `Microsoft.EntityFrameworkCore`. A domain project can define entities, raise events, and declare audit rules
  without pulling in the EF Core runtime.
- **Events queue on the aggregate, not on infrastructure** — `AddEvent(...)` records a business fact on the entity;
  what dispatches it is a separate package's problem, so the domain method has no messaging dependency.
- **Cross-cutting policy is declared next to the model** — `[AuditLog]`, `[IgnoreAuditLog]`, `[SensitiveData]`,
  `[RaisesEvent]`, `[Sequence]`, `[SqlSequence]`, and `[IgnoreEntity]` put audit, event, and mapping rules on the
  type they describe rather than in configuration elsewhere.
- **Concurrency and soft delete come pre-shaped** — implement the interface and `DKNet.EfCore.Extensions` configures
  the `RowVersion` token or the audit columns for you.

Reach for this package first when modelling a new domain entity in a DKNet-based solution.

## 🚀 Quick Start

```bash
dotnet add package DKNet.EfCore.Abstractions
```

There is no DI registration for this package — it is pure types (base classes, interfaces, attributes) referenced
at compile time by your domain project and by the DKNet packages that implement runtime behavior against them.
Minimum usage is simply deriving your entity from `Entity` (or `Entity<TKey>`):

```csharp
using DKNet.EfCore.Abstractions.Entities;

public class Product : Entity // Entity<Guid>
{
    private Product() { } // EF Core

    public Product(string name, decimal price) : base(Guid.NewGuid())
    {
        Name = name;
        Price = price;
    }

    public string Name { get; private set; } = null!;
    public decimal Price { get; private set; }
}
```

Runtime behavior — actually dispatching events, writing audit logs, redacting sensitive fields, enforcing row
ownership, encrypting columns — comes from the sibling packages described in [Where it fits](#-where-it-fits) below; this package only
supplies the shapes they agree on.

## 🧩 Features

### Entity identity — `IEntity<TKey>`, `Entity<TKey>` / `Entity`

`IEntity<out TKey>` is the minimal contract: a single `TKey Id { get; }`. `Entity<TKey>` is the base class you
actually derive from — it implements `IEntity<TKey>` and `IEventEntity` (see below), exposes `Id` with a
private setter, and overrides `ToString()` as `"{TypeName} '{Id}'"`. `Entity` is a convenience specialization with
`TKey = Guid`, the recommended default for distributed systems (globally unique, no DB round-trip needed to
generate).

```csharp
public class Category : Entity<int>
{
    private Category() { }
    public Category(int id, string name) : base(id) => Name = name;
    public string Name { get; private set; } = null!;
}
```

Both constructors that take an `id` exist mainly for EF Core data-seeding scenarios (per their own XML docs) —
day-to-day creation typically leaves EF Core / a value generator to assign `Id`.

### Domain events queued on the entity — `IEventEntity`, `Entity<TKey>`

`IEventEntity` lets an entity queue up domain events during a business operation, to be drained and published once
the surrounding `SaveChanges` succeeds:

- `AddEvent(object eventObj)` — queue an already-built event instance.
- `AddEvent<TEvent>()` — queue a *type*; the concrete event is produced later by mapping the entity itself onto
  `TEvent`. This overload requires an `IMapper` to be registered wherever the queue is drained — `DKNet.EfCore.Events`
  throws `EventException` at dispatch time if none is registered.
- `GetEvents()` — returns `(object[] Events, Type[] EventTypes)` for both queues (used by the dispatcher, not
  usually called from domain code).
- `ClearEvents()` — empties both queues.

`Entity<TKey>` already implements `IEventEntity` for you via two internal `Collection<>` fields, so every entity
deriving from `Entity`/`Entity<TKey>` gets event-queuing for free:

```csharp
public class Order : Entity
{
    public void Place()
    {
        // ... business logic ...
        AddEvent(new OrderPlacedEvent(Id));
    }
}
```

Queuing an event here does nothing by itself — see "Composition" for how `DKNet.EfCore.Events` drains and publishes
the queue.

### Declarative events — `[RaisesEvent]`, `EventOperations`

`[RaisesEvent]` is an alternative (or complement) to hand-calling `AddEvent(...)`: apply it to the entity class to
declare that a persistence operation should raise a payload automatically, without touching the entity's method
bodies. It is repeatable (`AllowMultiple = true`) — apply once per event the entity raises.

> **Breaking change:** the string-form argument used to be the generated record's name *verbatim*. It is now only
> the optional **label** segment of a name composed by fixed convention — every existing string-form declaration
> produces a differently-named record after upgrading, and each one must be revisited. Before:
> `[RaisesEvent("CustomerTouched", EventOperations.Created)]` generated `CustomerTouched`. After, the same
> declaration generates `CustomerTouchedCreatedEvent` (entity name + label + operation + `Event`).

Three forms:

```csharp
// Type-naming form — names an existing [GenerateDto] payload record (DKNet.EfCore.DtoGenerator)
[GenerateDto(typeof(Order), Exclude = new[] { "InternalNote" })]
public partial record OrderPlacedEvent;

[RaisesEvent(typeof(OrderPlacedEvent), EventOperations.Created)]
[RaisesEvent(typeof(OrderStatusChangedEvent), EventOperations.Updated, nameof(Order.Status))]
public class Order : Entity
{
    public string Status { get; set; } = string.Empty;
}

// Label-less convention form — no hand-written payload record; the build generates and names it by convention
[RaisesEvent(EventOperations.Created)]
public class Customer : Entity
{
}
// generates CustomerCreatedEvent

// Label convention form — the label is composed into the generated name
[RaisesEvent("Touched", EventOperations.Created)]
public class Product : Entity
{
}
// generates ProductTouchedCreatedEvent
```

`EventOperations` is a `[Flags]` enum with `Created = 1`, `Updated = 2`, `Deleted = 4` — combine flags
(`Created | Updated`) to raise the same event type for more than one lifecycle transition. For `Updated` rules, the
optional trailing `properties` (`nameof(...)`-checked) narrow the rule to fire only when at least one listed
*direct* property changed; an empty list fires on any change. Nested/owned-value changes never satisfy the
narrowing — only direct properties of the carrying entity are observed.

#### Convention-form naming

Both convention forms (label and label-less) name their generated record by a fixed, non-configurable formula —
never a literal name — composed in this order:

1. The carrying entity's simple name.
2. The label, when one is given (absent entirely for the label-less form).
3. The narrowing properties, de-duplicated and sorted ordinally (culture-independent) — declaration order and
   machine locale never affect the result.
4. The declared operations, always emitted in the canonical order **Created, Updated, Deleted**, regardless of the
   order the flags were combined in.
5. The literal suffix `Event`.

For example, `[RaisesEvent(EventOperations.Updated, nameof(Customer.Tier))]` on `Customer` composes
`CustomerTierUpdatedEvent`; `[RaisesEvent(EventOperations.Created | EventOperations.Updated)]` composes
`CustomerCreatedUpdatedEvent`. A declaration with no operation named (`Operations == 0`) is a build error
(`DKRAISEVT007`) — it can never raise anything. A composed name that isn't a single valid C# identifier (e.g. a
label containing whitespace or punctuation) is a build error (`DKRAISEVT005`) and generates no record. Two
declarations composing to the identical name are always an error, never silently merged: on the same entity that's
`DKRAISEVT008`, across different entities in the same namespace it's `DKRAISEVT006`.

#### Payload filters for composed records

Either convention form can also narrow the shape of its **composed** payload record with `Exclude`/`Include`
named arguments — they never affect the composed *name*, only the record's properties:

```csharp
[RaisesEvent(EventOperations.Created, Exclude = new[] { "InternalNote" })]
[RaisesEvent("Touched", EventOperations.Updated, Include = new[] { nameof(Customer.Name), nameof(Customer.Email) })]
public class Customer : Entity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string InternalNote { get; set; } = string.Empty;
}
```

- `Exclude` and `Include` are mutually exclusive on one declaration (`DKRAISEVT009`); a non-empty `Include` is the
  whole truth for the payload shape and **overrides** the project-wide `DtoGeneratorExclusions` MSBuild property
  (see `DKNet.EfCore.DtoGenerator`'s docs) for that declaration.
- Every named property must be a direct property of the entity — an unresolvable or nested name is a build error
  (`DKRAISEVT010`).
- Navigation/complex-type properties are omitted from the composed payload unconditionally, even when a non-empty
  `Include` names one by name — `Include` narrows which of the entity's own scalar properties ship, it never pulls
  a navigation property in.
- Neither filter is valid on the type-naming form (`DKRAISEVT011`) — that form's named payload record already
  owns its own shape via its own `[GenerateDto]` `Exclude`/`Include`.
- The project-wide `DtoGeneratorExclusions` list now also applies to composed convention-form payloads that set
  neither filter (or only `Exclude`), narrowing them the same way it narrows hand-written `[GenerateDto]` DTOs.

This attribute alone raises nothing at runtime: `DKNet.EfCore.DtoGenerator` validates the rule at build time (the
named type must be a `[GenerateDto]` payload generated from the *same* entity for the type-naming form; the
composed name must be a valid, non-colliding identifier for the convention forms), and `DKNet.EfCore.Events`' save
hook reads `[RaisesEvent]` via reflection to actually raise the payload after a successful save, composing the same
name from the same `EventNameComposer` source the build uses — the two can never disagree. A project that
references only `DKNet.EfCore.Abstractions` and `DKNet.EfCore.DtoGenerator` builds cleanly with rules declared and
simply never raises them until the application also registers `DKNet.EfCore.Events`.

### Audit tracking — `IAuditedProperties`, `IAuditedEntity<TKey>`, `AuditedEntity<TKey>` / `AuditedEntity`

`IAuditedProperties` declares the four audit fields every audited entity needs: `CreatedOn`, `CreatedBy`,
`UpdatedOn`, `UpdatedBy` (all decorated `[IgnoreAuditLog]` on the interface itself — see feature 6). `CreatedBy`
and `UpdatedBy` also carry `[MaxLength(500)]`. `IAuditedEntity<TKey>` combines `IEntity<TKey>` and
`IAuditedProperties` into one contract.

`AuditedEntity<TKey>` / `AuditedEntity` (Guid-keyed) is the base class you actually derive from. It implements the
four properties with private setters plus two derived, `[NotMapped]` conveniences — `LastModifiedBy` (falls back to
`CreatedBy` when never updated) and `LastModifiedOn` (falls back to `CreatedOn`) — and two `protected` mutators:

```csharp
public class Invoice : AuditedEntity
{
    private Invoice() { }

    public static Invoice Create(string createdBy)
    {
        var invoice = new Invoice();
        invoice.SetCreatedBy(createdBy);       // no-ops if CreatedBy is already set
        return invoice;
    }

    public void MarkPaid(string updatedBy) => SetUpdatedBy(updatedBy);
}
```

- `SetCreatedBy(userName, createdOn = null)` — a no-op once `CreatedBy` is already non-empty (first write wins);
  throws `ArgumentException` (via `ArgumentException.ThrowIfNullOrWhiteSpace`) for a null/blank `userName`.
  Defaults `createdOn` to `DateTimeOffset.UtcNow`.
- `SetUpdatedBy(userName, updatedOn = null)` — silently ignored if the supplied `updatedOn` is *older* than the
  currently stored `UpdatedOn` (out-of-order update guard); otherwise validates `userName` the same way and
  defaults `updatedOn` to UTC now.

Merely deriving from `AuditedEntity` does not populate these fields for you on every save — that stamping is
performed by whichever hook or interceptor your application wires up (DKNet ships this behavior in
`DKNet.EfCore.Hooks`/`DKNet.EfCore.DataAuthorization`, which detect `IAuditedProperties` and set `CreatedBy` on
`Added` entries — see "Composition"). Implementing `IAuditedProperties` is also the switch that turns an entity
into a candidate for audit *logging* (feature 6) — an entity that doesn't implement it is invisible to
`DKNet.EfCore.AuditLogs` regardless of any attributes you put on it.

### Optimistic concurrency — `IConcurrencyEntity<TType>`

Declares a nullable `RowVersion` (typed `TType`, e.g. `byte[]`), pre-annotated `[Timestamp]` and
`[Column(Order = 1000)]` so the concurrency token consistently sorts last in generated schemas, plus
`SetRowVersion(TType rowVersion)` to update it. Implement this on an entity to opt into EF Core's row-version
concurrency check:

```csharp
public class Account : Entity, IConcurrencyEntity<byte[]>
{
    public byte[]? RowVersion { get; private set; }
    public void SetRowVersion(byte[] rowVersion) => RowVersion = rowVersion;
}
```

`DKNet.EfCore.Extensions`' `DefaultEntityTypeConfiguration<TEntity>` detects `IConcurrencyEntity<>` by reflection and
automatically configures the `RowVersion` property as a concurrency token / row-version column with
`ValueGeneratedOnAddOrUpdate()` — you do not need to configure it yourself in `OnModelCreating` if you use that base
configuration.

### Soft deletion — `ISoftDeletableEntity`

Declares `IsDeleted`, `DeletedOn`, `DeletedBy` (`[MaxLength(250)]`) plus a `Delete(byUser, deletedOn = null)` method
that returns `FluentResults.IResultBase` so implementers can fail the delete (e.g. business-rule violation) without
throwing:

```csharp
public class Document : Entity, ISoftDeletableEntity
{
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

**Gotcha**: this is a contract only. No shipped DKNet package currently implements the query-filter or interceptor
side (translating "call `Delete`" into "actually filter it out of default queries" or "turn a hard delete into a
soft one automatically"). Implementing `ISoftDeletableEntity` gives you a consistent shape to code your own global
query filter (`modelBuilder.Entity<T>().HasQueryFilter(e => !e.IsDeleted)`) against — it does not wire one up for
you.

### Sequential values — `[Sequence]`

Applied to a `field` (not a property) to request database-generated sequential values. Constructor takes an
optional `Type` (defaults to `int`; only `byte`, `short`, `int`, `long` are supported — anything else throws
`NotSupportedException` at attribute-construction time). Settable options: `Cyclic` (default `true`),
`FormatString`, `IncrementsBy` (default `-1`), `Max` (default `-1`), `Min` (default `-1`), `StartAt` (default `-1`).
`DKNet.EfCore.Extensions`' `SequenceExtensions`/`EfCoreExtensions` read this attribute to register the sequence
against the model when the provider is SQL Server or Npgsql.

### Enum-backed SQL sequences — `[SqlSequence]`

Applied to an `enum` to associate it with a database sequence schema, e.g. `[SqlSequence("billing")] public enum
InvoiceKind { ... }`. Single constructor parameter `schema`, defaulting to `"seq"`, exposed as the read-only
`Schema` property.

### Audit-log opt-in and redaction — `[AuditLog]`, `[IgnoreAuditLog]`, `[SensitiveDataAttribute]`

These three attributes are pure markers read by `DKNet.EfCore.AuditLogs` (there is zero behavior in this package
itself) but they only make sense in terms of that consumer, so they're covered together:

- `[AuditLog]` on a **class**: with `AuditLogBehaviour.OnlyAttributedAuditedEntities`, marks the entity type as
  one that should be captured (entities are otherwise skipped under that behaviour unless attributed).
- `[AuditLog]` on a **property**: dual meaning depending on the active `AuditPropertyPolicy` — under the default
  `RedactSensitive` policy it forces plaintext capture even if the property name matches a built-in
  sensitive-name pattern; under the strict `OnlyAttributedProperties` policy it allow-lists the property for
  capture at all (properties not attributed are skipped entirely under that policy).
- `[IgnoreAuditLog]` on a class or property: unconditionally excludes it from audit logging, regardless of
  behaviour/policy. It's why `IAuditedProperties.CreatedOn/CreatedBy/UpdatedOn/UpdatedBy` (and
  `IConcurrencyEntity<TType>.RowVersion`) are pre-decorated with it in this package — the audit trail's own
  bookkeeping fields never audit-log themselves.
- `[SensitiveDataAttribute]` on a property: always redacts the value in the audit log, even when `[AuditLog]` is
  also present on the same property (redaction wins over the allow-list).

```csharp
[AuditLog] // only needed under OnlyAttributedAuditedEntities behaviour
public class Customer : AuditedEntity
{
    public string Email { get; private set; } = null!;

    [SensitiveDataAttribute] // always redacted in the audit log
    public string NationalId { get; private set; } = null!;
}
```

**Requires `IAuditedProperties`.** Verified against `DKNet.EfCore.AuditLogs`' `AuditLogExtensions.BuildAuditLog`:
an entity that doesn't implement `IAuditedProperties` is skipped before any of these attributes are even inspected
— attaching `[AuditLog]` to a plain `Entity` does nothing.

### Excluding a class from automatic mapping — `[IgnoreEntity]`

A class-level marker documented as excluding a type "from the automatic entity mapper", intended for delivered
(non-EF-mapped) types. **Verified caveat**: as of this writing, no shipped DKNet package (`Extensions`,
`DtoGenerator`, etc.) actually inspects this attribute — its only current references in the solution are its own
definition and its own unit tests asserting attribute metadata (`AttributeUsage`, sealed, etc.). Treat it as a
declared-but-not-yet-wired extension point rather than something that changes runtime EF Core discovery today;
don't rely on it to keep a type out of your model until you've confirmed the specific mapper/generator you're using
reads it.

### Publishing contract — `IEventPublisher`, `DefaultEventPublisher`, `IEventItem` / `EventItem`

`IEventPublisher` is the sink domain events are handed to: `PublishAsync(object, CancellationToken)` for one event,
`PublishAsync(IEnumerable<object>, CancellationToken)` for a batch. `DefaultEventPublisher` is an `abstract` base
that implements the batch overload as a sequential `foreach` over the single-event abstract method — you only
override `PublishAsync(object, ...)` to get a working batch implementation. Register one or more `IEventPublisher`
implementations in DI; `DKNet.EfCore.Events` fans every dispatched event out to all of them.

`IEventItem` is an optional shape for the event payload itself: `AdditionalData` (`[JsonIgnore]`, an
`IDictionary<string,string>` meant for message-header routing/filtering data that should not appear in the
serialized event body) and `EventType` (a string type-tag). `EventItem` is the matching abstract `record` base,
defaulting `AdditionalData` to an ordinal-case-insensitive dictionary and `EventType` to `GetType().FullName`.
`DKNet.EfCore.Events`' dispatcher stamps `AdditionalData[nameof(sourceType)]` (i.e. key `"sourceType"`) with the
originating entity's full type name for every dispatched event that implements `IEventItem`, whether or not you
derive from `EventItem` yourself.

## 🧱 Where it fits

This package is deliberately inert — every other EfCore package supplies the runtime behavior against the types
declared here. Concretely (all verified by reading the consuming source, not assumed):

- **`DKNet.EfCore.Events`** is the direct consumer of `IEventEntity`/`IEventPublisher`/`IEventItem`/
  `RaisesEventAttribute`/`EventOperations`. Its `EventContext` scans tracked entries for `IEventEntity`, calls
  `GetEvents()`/`ClearEvents()` on each, and (for `AddEvent<TEvent>()` queue entries) maps the entity onto `TEvent`
  via a registered `IMapper`. Its `EventHook` reads `[RaisesEvent]` off each changed entity's type via reflection,
  evaluates `EventOperations`/property narrowing against the save's `EntityState`, and hands the resulting event
  instances to every registered `IEventPublisher`.
- **`DKNet.EfCore.Hooks`** supplies the `IBeforeSaveHookAsync`/`IAfterSaveHookAsync`/`IHookAsync` pipeline that
  `DKNet.EfCore.Events`' `EventHook` (and `DKNet.EfCore.DataAuthorization`'s `DataOwnerHook`) plug into — this
  package defines no hook types itself, only the entity-side contracts hooks act on.
  `Entity<TKey>` doesn't reference `DKNet.EfCore.Hooks` at all; the hook pipeline reaches in from the outside via
  the `IEventEntity`/`IAuditedProperties` interfaces.
- **`DKNet.EfCore.AuditLogs`** is the sole consumer of `[AuditLog]`, `[IgnoreAuditLog]`, and
  `[SensitiveDataAttribute]`, and it gates everything on `IAuditedProperties` first (see *Audit-log opt-in and redaction* below). It also reads
  `IAuditedProperties.CreatedBy`/`UpdatedBy` directly for the "who" side of each audit entry.
- **`DKNet.EfCore.DataAuthorization`** stamps `IAuditedProperties.CreatedBy` on newly added entities (when empty)
  as part of assigning row ownership, and generally targets entities exposing the abstractions in this package
  (`DataOwnerHook` inspects `IAuditedProperties`, and works alongside `IConcurrencyEntity`/`Entity<TKey>`-shaped
  models).
- **`DKNet.EfCore.Encryption`** does **not** reuse `[SensitiveDataAttribute]` — it defines its own,
  narrower-purpose `[Encrypted]` attribute (`DKNet.EfCore.Encryption.Attributes.EncryptedAttribute`) for
  column-level encryption via a value converter. Don't conflate the two: `[SensitiveDataAttribute]` only affects
  what `AuditLogs` writes to the audit trail; it has no effect on how a value is stored.
- **`DKNet.EfCore.DtoGenerator`** validates `[RaisesEvent]` rules at build time (`RaisesEventValidator`) — that the
  named type-form payload was generated from the same entity, and that narrowing properties are direct existing
  properties — and, for the string form, source-generates the default-shape payload record. `[GenerateDto]`
  payload records referenced by the type-naming form of `[RaisesEvent]` are a DtoGenerator concept, not part of
  this package.
- **`DKNet.EfCore.Extensions`** reads `IEntity<TKey>.Id` (by convention, `nameof`) to configure the primary key and
  its value generator (including a `Guid` v7 generator), detects `IAuditedProperties` to configure the four audit
  columns, and detects `IConcurrencyEntity<>` to configure `RowVersion` as a row-version concurrency token — all in
  `DefaultEntityTypeConfiguration<TEntity>`. It also reads `[Sequence]` to register database sequences.
- **`DKNet.EfCore.Repos` / `DKNet.EfCore.Repos.Abstractions`** are generic over `TEntity : class` — they do **not**
  require `IEntity<TKey>` or `Entity<TKey>` at the interface level, but the DKNet convention (and the worked
  examples across the docs) is to back repositories with `Entity`/`Entity<TKey>`-derived aggregates so the rest of
  the stack (events, audit, concurrency) applies uniformly.

## ⚠️ Gotchas & limits

- **Implementing an interface is not enough by itself.** `IAuditedProperties`, `IConcurrencyEntity<>`,
  `ISoftDeletableEntity`, and `IEventEntity` are pure contracts — none of them stamp values, filter queries, or
  dispatch anything on their own. Each needs its matching runtime package registered (`Extensions` for concurrency
  column config, `Hooks`+`Events`/`DataAuthorization` for stamping and dispatch, your own query filter for soft
  delete).
- **`AddEvent<TEvent>()` requires an `IMapper`.** Only the object-instance overload (`AddEvent(object)`) is
  mapper-free; the generic overload throws `EventException` at dispatch time (not at call time) if no `IMapper` is
  registered — a domain project referencing only `DKNet.EfCore.Abstractions` will compile fine and only fail at
  runtime once `DKNet.EfCore.Events` tries to drain the queue.
- **`[RaisesEvent]` is inert without `DKNet.EfCore.Events`.** A project referencing `Abstractions` +
  `DtoGenerator` builds and packs cleanly with rules declared, and nothing ever raises until the consuming
  application also registers `DKNet.EfCore.Events`' save hook.
- **`[RaisesEvent]` narrowing is shallow.** Only direct properties of the carrying entity qualify for `Updated`
  narrowing; a change confined to a nested owned value never satisfies it. Narrowing a rule whose `operations` has
  no `Updated` flag is accepted by the compiler but is a no-op reported as a build warning by `DtoGenerator`.
- **`[IgnoreEntity]` currently has no consumer in this repo** (see *Excluding a class from automatic mapping* above) — don't treat it as a working
  exclusion mechanism without confirming the specific tool you're using reads it.
- **`SetCreatedBy`/`SetUpdatedBy` are first-write-wins / monotonic, not "always overwrite".** `SetCreatedBy` is a
  no-op once `CreatedBy` is set; `SetUpdatedBy` silently ignores a supplied `updatedOn` older than the current
  `UpdatedOn`. Both still validate `userName` even when they're about to no-op on the timestamp check.
- **`[SensitiveDataAttribute]` only affects the audit log**, not storage, serialization, or logging elsewhere in
  your application — it has nothing to do with `DKNet.EfCore.Encryption`'s `[Encrypted]`.
- **No dependency on `Microsoft.EntityFrameworkCore`.** This is intentional (keeps the domain layer persistence-
  technology-agnostic) but means nothing in this package can validate itself against a real `DbContext` — mistakes
  (e.g. a `[Sequence]` on an unsupported type) surface as an attribute-construction `NotSupportedException`, not an
  EF Core model-building error.

## 🔗 Related packages

- [DKNet.EfCore.Extensions](./DKNet.EfCore.Extensions.md) – turns these contracts into model configuration: primary
  keys and GUID v7 generators, audit columns, `RowVersion` concurrency tokens, `[Sequence]` registration. Reach for it
  to make the declarations here take effect.
- [DKNet.EfCore.Hooks](./DKNet.EfCore.Hooks.md) – the `SaveChanges` pipeline the runtime packages plug into. Reach for
  it when you need a custom before/after-save hook.
- [DKNet.EfCore.Events](./DKNet.EfCore.Events.md) – dispatches the events queued with `AddEvent` and raised by
  `[RaisesEvent]`. Reach for it to actually publish them.
- [DKNet.EfCore.AuditLogs](./DKNet.EfCore.AuditLogs.md) – the sole consumer of `[AuditLog]`, `[IgnoreAuditLog]`, and
  `[SensitiveData]`. Reach for it for a field-level change trail.
- [DKNet.EfCore.DataAuthorization](./DKNet.EfCore.DataAuthorization.md) – row-level ownership on top of these
  entities. Reach for it for multi-tenant isolation.
- [DKNet.EfCore.DtoGenerator](./DKNet.EfCore.DtoGenerator.md) – compile-time validation of `[RaisesEvent]` and
  generation of its payload records. Reach for it when a declared event will not resolve.
- [DKNet.EfCore.Specifications](./DKNet.EfCore.Specifications.md) – the supported way to query and persist these
  entities. Reach for it for reusable filter/include/order-by objects.
- [DKNet.EfCore.Encryption](./DKNet.EfCore.Encryption.md) – column-level encryption via its own `[Encrypted]`
  attribute. Reach for it to protect a value at rest; `[SensitiveData]` here only affects the audit trail.
