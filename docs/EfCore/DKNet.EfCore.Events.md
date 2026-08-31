# DKNet.EfCore.Events

Dispatches domain events raised by entities during `SaveChanges` — the runtime half of DKNet's domain-events feature.

## ✨ Why use it?

- **The domain method stops depending on messaging** — a business method queues a plain object with `AddEvent(...)`;
  it never references a publisher, a bus, or this package.
- **No call site has to remember to dispatch** — the hook notices the queued events during that entity's next
  `SaveChanges` instead of every write path calling a mediator right after `SaveChangesAsync`.
- **Events fire only after the save actually committed** — dispatch happens in the after-save phase, so a rolled-back
  transaction cannot publish "an order was placed" for an order that does not exist.
- **Declarative events for the common cases** — `[RaisesEvent]` on the entity raises a generated payload record on
  create/update/delete, with the payload type produced and validated at compile time by
  [DKNet.EfCore.DtoGenerator](./DKNet.EfCore.DtoGenerator.md).
- **You own the transport** — implement `IEventPublisher` to hand events to SlimMessageBus, MassTransit, an outbox
  table, or anything else; several publishers can be registered and all of them run.

Reach for it when you model rich aggregates and want cross-aggregate or cross-cutting side effects triggered by
business facts ("an order was placed") rather than by call sites remembering to trigger them. Skip it for plain CRUD
with no such side effects — there is nothing to dispatch.

`DKNet.EfCore.Abstractions` defines the contracts entities and publishers implement; this package collects and fires
the events.

## 🚀 Quick Start

```bash
dotnet add package DKNet.EfCore.Events
```

This pulls in `DKNet.EfCore.Abstractions` (the entity/event contracts) and `DKNet.EfCore.Hooks` (the save
pipeline it plugs into) as project references.

The package ships exactly one DI extension, `EventSetup.AddEventPublisher`:

```csharp
public static IServiceCollection AddEventPublisher<TDbContext, TImplementation>(this IServiceCollection services)
    where TImplementation : class, IEventPublisher
    where TDbContext : DbContext
```

Minimum end-to-end wiring — the DbContext **must** be registered through `DKNet.EfCore.Hooks`'s
`AddDbContextWithHook` (or `UseHooks<TDbContext>` manually) or nothing below ever runs, since that's what installs
the interceptor `AddEventPublisher` hooks into:

```csharp
services.AddDbContextWithHook<AppDbContext>((_, o) => o.UseSqlServer(connectionString));
services.AddEventPublisher<AppDbContext, MyEventPublisher>();
```

`AddEventPublisher` does two things: registers `TImplementation` as a scoped `IEventPublisher` (skipped if that
exact implementation type is already registered), and calls `DKNet.EfCore.Hooks`'s
`AddHook<TDbContext, EventHook>()` to add the package's internal `EventHook` to the save pipeline for
`TDbContext`.

## 🧩 Features

### The type you write: an event-raising entity

You don't implement `IEventEntity` yourself in the common case — derive from `Entity<TKey>` (or `Entity`, its
`Guid`-keyed convenience base) in `DKNet.EfCore.Abstractions`, which already implements all four `IEventEntity`
members (`AddEvent(object)`, `AddEvent<TEvent>()`, `GetEvents()`, `ClearEvents()`) backed by two private,
`[NotMapped]` collections. A business method just calls `AddEvent(...)`:

```csharp
public record OrderPlacedEvent(Guid OrderId, decimal Total);
public record OrderCompletedEvent(Guid OrderId);

public class Order : Entity<Guid>
{
    private Order() { } // EF Core

    public Order(Guid id, decimal total) : base(id)
    {
        Total = total;
        AddEvent(new OrderPlacedEvent(id, total)); // queued, not dispatched yet
    }

    public decimal Total { get; private set; }
    public string Status { get; private set; } = "Pending";

    public void Complete()
    {
        Status = "Completed";
        AddEvent(new OrderCompletedEvent(Id));
    }
}
```

`AddEvent(object)` just queues the instance you pass — no mapping, no `IMapper` needed. A second overload,
`AddEvent<TEvent>()`, queues the *type* instead: at dispatch time the entity itself is mapped onto `TEvent` via
the registered `IMapper` (see [Configuration reference](#️-configuration-reference)) — useful when the event should mirror current entity state at save time rather
than at call time.

### The publisher you write

```csharp
public sealed class LoggingEventPublisher(ILogger<LoggingEventPublisher> logger) : DefaultEventPublisher
{
    public override Task PublishAsync(object eventObj, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Publishing {EventType}", eventObj.GetType().Name);
        return Task.CompletedTask;
    }
}
```

`DefaultEventPublisher` (from `DKNet.EfCore.Abstractions`) implements the bulk overload by looping the single-item
one, so you only need to override `PublishAsync(object, CancellationToken)`. Implementing `IEventPublisher`
directly instead requires both overloads yourself.

### What happens on `SaveChangesAsync`

```csharp
db.Orders.Add(order);
await db.SaveChangesAsync(); // OrderPlacedEvent reaches LoggingEventPublisher only after this commits
```

1. **Before the save** (`EventHook.BeforeSaveAsync`, an `IBeforeSaveHookAsync`): for every tracked entity, the
   hook checks its `[RaisesEvent]` declarations (see *Declared events* below) against the pending operation and records which ones
   qualify. This has to happen here, before EF Core writes anything, because
   `EntityEntry.Property(...).IsModified` is meaningless once the save completes. Hand-raised events
   (`AddEvent(...)`) are untouched at this point — they stay queued on the entity.
2. **EF Core performs the actual INSERT/UPDATE/DELETE.** If it throws, `SaveChangesFailedAsync` tears down the
   hook's per-save state and **no event is ever published** — hand-raised or declared.
3. **After a successful save** (`EventHook.AfterSaveAsync`, an `IAfterSaveHookAsync`): the hook builds an
   `EventContext` over the same save's `SnapshotContext`. `EventContext.GetEvents()` walks every tracked entity
   that is `IEventEntity`, reads its queued `(object[] Events, Type[] EventTypes)` via `GetEvents()`, keeps the
   object instances as-is, and maps any `TEvent`-only entries onto their type via the registered `IMapper` —
   throwing `EventException` if none is registered (see [Gotchas & limits](#️-gotchas--limits)). It also stamps a `sourceType` entry (the entity's
   full type name) into `AdditionalData` for any event implementing `IEventItem`.
4. Declared events captured in step 1 are now mapped from their entity onto their declared payload type (again
   via `IMapper`, again `EventException` if missing) and merged with the hand-raised ones from the same save —
   both reach subscribers as distinct objects.
5. **Every registered `IEventPublisher`** — there can be more than one — receives the full combined list via
   `PublishAsync(IEnumerable<object>, ct)`. Each publisher runs in its own `try`/`catch`: a throwing publisher is
   logged and skipped, it does not stop the remaining publishers and does not undo the already-committed save.
6. **Regardless of publish outcome**, `EventContext.ClearEvents()` clears every event from every entity's queue.
   The next `SaveChanges` starts with empty queues.

![Sequence diagram of one SaveChangesAsync call: HookRunnerInterceptor runs EventHook.BeforeSaveAsync to capture declared events before the write, then EventHook.AfterSaveAsync to map and publish them only once the write has succeeded.](../diagrams/efcore-events-savechanges.svg)

The split across the write is the part worth internalising: declared-event capture has to happen before EF Core writes,
publishing can only happen after it succeeds, and both halves run inside the one `HookRunnerInterceptor` that
`DKNet.EfCore.Hooks` installs — which is also why a failed save publishes nothing at all.

### `EventException`

`EventException(IResultBase status)` (from `FluentResults`) is thrown in exactly two situations, both about a
missing `IMapper` for a mapping-based event source, and both at dispatch time (inside step 3/4 above), never at
`AddEvent()` call time:

- an entity queued a type-based event via `AddEvent<TEvent>()` with no `IMapper` registered;
- an entity qualifies for a `[RaisesEvent]` declared event with no `IMapper` registered.

A third case is specific to `[RaisesEvent]`'s convention forms (see *Declared events* below): the composed event name doesn't resolve to a
generated payload type in the entity's own assembly/namespace (typically because `DKNet.EfCore.DtoGenerator`
wasn't referenced, or the project didn't rebuild) — also thrown at dispatch time, never silently dropped.

### Declared events (`[RaisesEvent]`)

Besides hand-raising events from code, an entity can *declare* them instead — no `IEventEntity`, no `Entity<TKey>`
base class required. Declaring is two steps: shape the payload as a
[DtoGenerator](./DKNet.EfCore.DtoGenerator.md)-generated record via
`[GenerateDto]`, then apply the repeatable
`DKNet.EfCore.Abstractions.Events.RaisesEventAttribute` naming that payload, the persistence operation(s)
(`EventOperations.Created | Updated | Deleted`), and — for `Updated` — an optional narrowing property list:

```csharp
using DKNet.EfCore.Abstractions.Events;

[GenerateDto(typeof(Order))]
public partial record OrderPlacedEvent;

[GenerateDto(typeof(Order))]
public partial record OrderStatusChangedEvent;

[RaisesEvent(typeof(OrderPlacedEvent), EventOperations.Created)]
[RaisesEvent(typeof(OrderStatusChangedEvent), EventOperations.Updated, nameof(Order.Status))]
[RaisesEvent(typeof(OrderPlacedEvent), EventOperations.Deleted)]
public class Order
{
    public Guid Id { get; set; }
    public string Status { get; set; } = string.Empty;
}
```

`DKNet.EfCore.DtoGenerator` validates these rules at build time (payload/entity match, narrowing property names)
but emits no runtime code for them — **this package** is what reads `[RaisesEvent]` via reflection (cached per
entity type, and per entity-type+composed-name for the convention forms) and raises them at save time, exactly per
the lifecycle described above. A rule naming the same payload for the same operation twice on one entity raises it once.

An entity may combine `[RaisesEvent]` declarations with hand-raised `AddEvent(...)` calls in the same class —
both are published from the same save.

**Convention forms**: `[RaisesEvent("Touched", EventOperations.Created)]` (or, label-less,
`[RaisesEvent(EventOperations.Created)]`) skips the hand-written `[GenerateDto]` payload —
`DKNet.EfCore.DtoGenerator` generates a default-shape `public partial record` for it in the entity's own namespace,
named by fixed convention: entity name + label (if any) + sorted narrowing properties + operations (canonical
order Created, Updated, Deleted) + `Event` — e.g. `CustomerTouchedCreatedEvent`. At runtime, this package composes
the identical name via the same `EventNameComposer` the build uses, then resolves that generated type by
reflection from the entity's own assembly and namespace; if it isn't found, the first save that would raise it
throws `EventException` naming the composed event it looked for.

## ⚙️ Configuration reference

There is no options object — every knob is a DI registration:

| Registration | Lifetime | Required? | Effect |
|---|---|---|---|
| `AddEventPublisher<TDbContext, TPublisher>()` | scoped publisher | required | Registers `TPublisher` as an `IEventPublisher` and `EventHook` as a hook for `TDbContext`. |
| `MapsterMapper.IMapper` | your choice | only for `AddEvent<TEvent>()` / `[RaisesEvent]` | Maps the entity onto the event payload type. `EventHook` uses `mappers.FirstOrDefault()`. |
| `ILogger<EventHook>` | your choice | optional | One informational entry per `AfterSaveAsync`, one error entry per failed publisher. |
| `AddDbContextWithHook<TDbContext>()` / `options.UseHooks<TDbContext>(provider)` | — | required | Installs `HookRunnerInterceptor`. Without it `EventHook` is in DI but never invoked. |

Behaviour worth knowing beyond the table:

- **Publisher lifetime**: scoped, added via `AddScoped<IEventPublisher, TImplementation>()`. Calling
  `AddEventPublisher` twice with the *same* `TImplementation` is a no-op (guarded via
  `IsRegisteredWithImplementation`); calling it with *different* implementations registers all of them, and
  **every one runs on every save** (`IEnumerable<IEventPublisher>` — not first-match).
- **`IMapper`**: optional unless an entity uses `AddEvent<TEvent>()` or `[RaisesEvent]`. `EventHook` resolves
  `mappers.FirstOrDefault()` from `IEnumerable<MapsterMapper.IMapper>` — if you register more than one `IMapper`
  implementation, only the first one DI resolves is used for event mapping. Register one alongside
  `AddEventPublisher`:
  ```csharp
  services.AddSingleton<IMapper, Mapper>(); // Mapster's IMapper, or your own implementation
  services.AddEventPublisher<AppDbContext, MyEventPublisher>();
  ```
- **Logging**: `ILogger<EventHook>` is optional (nullable constructor parameter) — an informational entry on
  `AfterSaveAsync`, an error entry per failed publisher. No log is emitted for individual successful publishes.
- **No retry, no ordering knob, no dead-lettering.** These aren't configurable because the package doesn't
  implement them at all — see [Gotchas & limits](#️-gotchas--limits).

## 🧱 Where it fits

This is the part worth understanding before adopting the package: **Events doesn't define entities, event bases,
or the save pipeline — it only consumes contracts `DKNet.EfCore.Abstractions` defines, running inside a pipeline
`DKNet.EfCore.Hooks` owns.**

**With `DKNet.EfCore.Abstractions`:**

- `IEventEntity` is the contract an entity satisfies to participate; `Entity<TKey>`/`Entity` are the
  batteries-included base classes that already implement it, so most domain code never touches the interface
  directly.
- `IEventItem`/`EventItem` is an optional richer event base (adds `AdditionalData` for message-header-style
  metadata and an `EventType` string) — plain records/classes work fine as events too, they just don't get the
  `sourceType` stamping `EventContext` adds for `IEventItem` events.
  `IEventPublisher`/`DefaultEventPublisher` is the contract you implement; `EventOperations` and
  `RaisesEventAttribute` are the declared-event vocabulary.
- The dependency only points one way: `DKNet.EfCore.Events` project-references `DKNet.EfCore.Abstractions`, never
  the reverse. A domain project can reference *only* `DKNet.EfCore.Abstractions` (plus `DKNet.EfCore.DtoGenerator`
  for `[RaisesEvent]` payloads) and compile cleanly with events declared/raised in domain code — nothing actually
  dispatches until the application also references and registers `DKNet.EfCore.Events`. This is the dependency
  inversion the DDD/Onion split is built on: domain layer depends on abstractions only, infrastructure wires the
  concrete dispatch.

**With `DKNet.EfCore.Hooks`:**

- `EventHook` *is* a hook: `internal sealed class EventHook(...) : HookAsync`, and `HookAsync` /
  `IHookAsync` / `IBeforeSaveHookAsync` / `IAfterSaveHookAsync` all come from `DKNet.EfCore.Hooks`. It's
  registered through the exact same `IServiceCollection.AddHook<TDbContext, THook>()` extension any other hook
  uses — there's no event-specific pipeline; it's a first-class citizen of the general hook mechanism.
- Concretely, that means `EventHook` shares one `HookRunnerInterceptor` (an EF Core `SaveChangesInterceptor`,
  cached per `DbContext.ContextId.InstanceId`) with every other hook registered for the same `TDbContext` — audit
  hooks, validation hooks, whatever else you add. `HookRunnerInterceptor.SavingChangesAsync` runs all
  `BeforeSaveHooks` (including `EventHook.BeforeSaveAsync`) before EF Core's actual save;
  `HookRunnerInterceptor.SavedChangesAsync` runs all `AfterSaveHooks` (including `EventHook.AfterSaveAsync`) —
  and is only reached once the save has truly succeeded. `SaveChangesFailedAsync` just disposes the cached hook
  state; `AfterSaveHooks` never run on a failed save, so `EventHook` never publishes for one.
- The DbContext must be registered via `AddDbContextWithHook<TDbContext>` (or `UseHooks<TDbContext>` on a
  manually-built `DbContextOptionsBuilder`) for the interceptor to be installed at all —
  `AddEventPublisher` only registers `EventHook` into DI as a hook candidate, it does not wire the interceptor
  into `DbContextOptions` by itself.
- Because it's just another hook, `DKNet.EfCore.Hooks`'s `dbContext.DisableHooks()` (used for data
  seeding/migrations) also suppresses event dispatch for that scope — there's no separate "disable events only"
  switch.

## ⚠️ Gotchas & limits

- **Events are cleared unconditionally after `AfterSaveAsync`**, whether every publisher succeeded or not. A
  publisher exception is logged, not rethrown — it neither undoes the already-committed save nor requeues the
  event for a retry. If you need at-least-once delivery guarantees (outbox pattern, dead-lettering), build that
  into your `IEventPublisher` implementation; this package doesn't provide it.
- **No ordering guarantee.** Events across multiple entities in one save, and multiple registered publishers, run
  in whatever order the underlying collections/DI enumerate them — don't depend on sequence for correctness.
- **`AddEvent(object)` never needs an `IMapper`; `AddEvent<TEvent>()` and `[RaisesEvent]` always do.** Forgetting
  the mapper doesn't fail fast at startup or at the `AddEvent` call site — it throws `EventException` only at the
  next `SaveChanges` that actually needs to map something.
- **Delete events mirror pre-removal entity state**, not nulled-out fields — EF Core doesn't clear an entity's
  in-memory properties when it's deleted, so a declared `Deleted` event captures the entity exactly as it was
  right before removal. This is expected, not a bug.
- **A change confined to a nested owned value (`OwnsOne`/`[Owned]`) does not raise the owner's `Updated` rule** —
  EF Core doesn't report the owner itself as `Modified` when only an owned value changed. Narrow `[RaisesEvent]`
  properties to the owner's own direct properties only.
- **`[RaisesEvent]` compiles and validates without this package.** A project referencing only
  `DKNet.EfCore.Abstractions` + `DKNet.EfCore.DtoGenerator` builds fine with declarations in place — nothing
  raises until the application also references `DKNet.EfCore.Events` and registers the hook.
- **A declared event mirrors the entity's properties by default**, same rule as `[GenerateDto]` DTOs generally —
  sensitive fields need explicit `Exclude` on the payload's `[GenerateDto]` declaration or they leak into the
  published event.
- **`DisableHooks()` silently disables event dispatch too**, since `EventHook` is just another hook on the shared
  pipeline — don't be surprised when events stop firing during a seeding/migration scope that disabled hooks for
  an unrelated reason.

## 🔗 Related packages

- [DKNet.EfCore.Abstractions](./DKNet.EfCore.Abstractions.md) – defines `IEventEntity`, `AddEvent`, `IEventPublisher`,
  `[RaisesEvent]`, and `EventOperations`. Reach for it to make an entity raise events in the first place.
- [DKNet.EfCore.Hooks](./DKNet.EfCore.Hooks.md) – the `SaveChanges` pipeline `EventHook` runs in, and the owner of
  `DisableHooks()`. Reach for it when you need a custom hook or need to suppress the pipeline for seeding.
- [DKNet.EfCore.DtoGenerator](./DKNet.EfCore.DtoGenerator.md) – generates and compile-time-validates the payload
  records for `[RaisesEvent]`. Reach for it when a declared event will not resolve or a payload shape is wrong.
- [DKNet.EfCore.AuditLogs](./DKNet.EfCore.AuditLogs.md) – records changes on the same pipeline. Reach for it when the
  requirement is a change trail rather than a reaction.
- [DKNet.SlimBus.Extensions](../Messaging/DKNet.SlimBus.Extensions.md) – a ready-made transport to publish the
  dispatched events onto. Reach for it when your `IEventPublisher` should hand off to SlimMessageBus.
