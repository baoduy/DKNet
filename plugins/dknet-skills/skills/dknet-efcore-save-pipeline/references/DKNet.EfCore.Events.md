# DKNet.EfCore.Events

| Field | Value |
|---|---|
| Area | EfCore |
| NuGet | `dotnet add package DKNet.EfCore.Events` |
| Docs | https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/DKNet.EfCore.Events.md |
| Source | https://github.com/baoduy/DKNet/tree/dev/src/EfCore/DKNet.EfCore.Events |
| Depends on (DKNet) | `DKNet.EfCore.Abstractions` (entity/event contracts), `DKNet.EfCore.Hooks` (the save pipeline it plugs into) |
| Depends on (3rd party) | `FluentResults` (backs `EventException`'s `IResultBase`); `Mapster` + `Mapster.DependencyInjection` (`MapsterMapper.IMapper`/`ServiceMapper`, used for `AddEvent<TEvent>()` and `[RaisesEvent]` mapping) |
| Target framework | net10.0 |

## Purpose

Dispatches domain events that entities queue during `DbContext.SaveChangesAsync`. An entity calls `AddEvent(...)` from a business method (no messaging dependency at the call site); this package's `EventHook` — a hook plugged into `DKNet.EfCore.Hooks`'s shared interceptor — captures declared (`[RaisesEvent]`) events before the write, and after a *successful* save, maps/collects every queued event and hands the full batch to every registered `IEventPublisher`.

**Not for**: plain CRUD with no cross-cutting side effect to react to; at-least-once/guaranteed delivery (build a transactional outbox instead — see Gotchas); ordering-sensitive event consumers (no ordering guarantee across entities or publishers).

## Entry points

| Call | Signature | Notes |
|---|---|---|
| `AddEventPublisher<TDbContext, TImplementation>` | `IServiceCollection AddEventPublisher<TDbContext, TImplementation>(this IServiceCollection services) where TImplementation : class, IEventPublisher where TDbContext : DbContext` | Registers `TImplementation` as a **scoped** `IEventPublisher` (no-op if that exact implementation type is already registered) and calls `DKNet.EfCore.Hooks`'s `AddHook<TDbContext, EventHook>()`. Call again with a different implementation to register more — all run per save. Requires the `DbContext` to also be registered via `AddDbContextWithHook<TDbContext>()` or `options.UseHooks<TDbContext>(provider)`, otherwise `EventHook` sits in DI unused. |

That is the package's entire public DI entry surface — no `ModelBuilder` extension, no attribute defined in this package (`[RaisesEvent]` lives in `DKNet.EfCore.Abstractions`), no MSBuild target. `AddEventPublisher` lives on `EventSetup`, deliberately declared in the ambient namespace `Microsoft.Extensions.DependencyInjection` — it resolves with no extra `using` beyond what an ASP.NET Core project already has implicitly.

## Public surface

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `EventException` | sealed class : `Exception` | Thrown when a mapping-based event source has no `IMapper`, at true dispatch time (inside `AfterSaveAsync`) — **or** when a `[RaisesEvent]` convention-form payload can't be resolved by reflection, which throws earlier, inside the before-save run, before EF Core issues any SQL. | ctor `(IResultBase status)`; `IResultBase Status { get; }` (from `FluentResults`). Message is the first `status.Errors[0].Message` if any, else a generic fallback. |

Ambient contracts consumed from `DKNet.EfCore.Abstractions` (not defined here, but essential to using this package):

| Type | Kind | Namespace | Purpose | Key members |
|---|---|---|---|---|
| `IEventEntity` | interface | `DKNet.EfCore.Abstractions.Events` | Contract an entity satisfies to hand-raise events. `Entity<TKey>`/`Entity` already implement it. | `void AddEvent(object)`; `void AddEvent<TEvent>() where TEvent : class`; `(object[] Events, Type[] EventTypes) GetEvents()`; `void ClearEvents()` |
| `IEventPublisher` | interface | `DKNet.EfCore.Abstractions.Events` | The transport contract you implement. Every registered instance runs on every save. | `Task PublishAsync(object, CancellationToken = default)`; `Task PublishAsync(IEnumerable<object>, CancellationToken = default)` |
| `DefaultEventPublisher` | abstract class : `IEventPublisher` | `DKNet.EfCore.Abstractions.Events` | Convenience base — implements the bulk overload by looping the single-item one. | `abstract Task PublishAsync(object, CancellationToken = default)` (override this); `virtual Task PublishAsync(IEnumerable<object>, CancellationToken = default)` |
| `RaisesEventAttribute` | sealed class : `Attribute`, repeatable, `Class`-only, `Inherited = false` | `DKNet.EfCore.Abstractions.Events` | Declares an event without hand-raising it. Three constructors = three forms (see Usage patterns). | `RaisesEventAttribute(Type eventType, EventOperations operations, params string[] properties)`; `RaisesEventAttribute(string label, EventOperations operations, params string[] properties)`; `RaisesEventAttribute(EventOperations operations, params string[] properties)`; properties `Type? EventType`, `string? Label`, `EventOperations Operations`, `IReadOnlyList<string> Properties`, `string[] Exclude { get; set; }`, `string[] Include { get; set; }` |
| `EventOperations` | `[Flags] enum` | `DKNet.EfCore.Abstractions.Events` | Identifies which entity lifecycle operation(s) a declared/generated event corresponds to; combine flags (e.g. `Created \| Updated`) to raise one event type for more than one operation. | `Created = 1`, `Updated = 2`, `Deleted = 4` |
| `IEventItem` / `EventItem` | interface / abstract record | `DKNet.EfCore.Abstractions.Events` | Optional richer event base — adds message-header-style metadata. | `IDictionary<string, string> AdditionalData { get; }` (`[JsonIgnore]`); `string EventType { get; }` |
| `EventNameComposer` | static class | `DKNet.EfCore.Abstractions.Events` | Composes the fixed convention-form payload name: entity name → label → sorted narrowing properties → operations (`Created`, `Updated`, `Deleted` order) → `Event`. Same algorithm the build-time generator and this package's runtime both use. | `static string Compose(string entityName, string? label, IReadOnlyList<string>? properties, int operations)` |
| `Entity<TKey>` / `Entity` | abstract class : `IEntity<TKey>`, `IEventEntity` | `DKNet.EfCore.Abstractions.Entities` (**not** `.Events`, unlike every other row above) | Batteries-included entity base implementing all four `IEventEntity` members. `Entity` is the `Guid`-keyed convenience form. | `TKey Id { get; }`; `AddEvent(object)`; `AddEvent<TEvent>()`; `ClearEvents()`; `(object[], Type[]) GetEvents()` |

`EventHook` and `EventContext` (the actual hook implementation and its per-save state walker) are `internal sealed` in `DKNet.EfCore.Events.Internals` — mentioned in Runtime behaviour below because they explain observable dispatch order, but not directly referenceable from application code.

## Options & defaults

There is no options object — every knob is a DI registration or constructor parameter:

| Registration / parameter | Default | Effect |
|---|---|---|
| `AddEventPublisher<TDbContext, TPublisher>()` | required, none registered by default | Registers `TPublisher` as scoped `IEventPublisher`; registers `EventHook` as a hook candidate for `TDbContext`. Repeating with the *same* implementation is a no-op; different implementations all register and **every one runs on every save** (`IEnumerable<IEventPublisher>`, not first-match). |
| `AddDbContextWithHook<TDbContext>()` / `options.UseHooks<TDbContext>(provider)` | required | Installs `HookRunnerInterceptor`. Without it, `EventHook` exists in DI but is never invoked — no exception, dispatch simply never fires. |
| `MapsterMapper.IMapper` (ctor param `mappers`) | none registered | `EventHook` uses `mappers.FirstOrDefault()` — if more than one `IMapper` is registered, only the first one DI resolves is used; the rest are silently ignored. Required only for `AddEvent<TEvent>()` and `[RaisesEvent]`. Missing it throws `EventException` at the *next* `SaveChanges` that needs to map something. |
| `ILogger<EventHook>?` | `null` (optional ctor param) | One informational entry per `AfterSaveAsync`; one error entry per failed publisher. No log for individual successful publishes. |

## Usage patterns

### Minimal wiring + a hand-raised event, and AddEvent&lt;TEvent&gt;() at the same time

**When**: an aggregate raises a plain event object from a business method, and also wants the event mapped from its own state at dispatch time.

```csharp
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.Abstractions.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public record OrderPlacedEvent(Guid OrderId, decimal Total);

public record OrderSnapshotEvent(Guid Id, decimal Total);

public class Order : Entity<Guid>
{
    private Order() { } // EF Core

    public Order(Guid id, decimal total) : base(id)
    {
        Total = total;
        AddEvent(new OrderPlacedEvent(id, total)); // queued, not dispatched yet -- never needs an IMapper
        AddEvent<OrderSnapshotEvent>(); // mapped from current state at dispatch time -- always needs an IMapper
    }

    public decimal Total { get; private set; }
}

public sealed class LoggingEventPublisher(ILogger<LoggingEventPublisher> logger) : DefaultEventPublisher
{
    public override Task PublishAsync(object eventObj, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Publishing {EventType}", eventObj.GetType().Name);
        return Task.CompletedTask;
    }
}

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
}
```

```csharp
using DKNet.EfCore.Hooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public static class Startup
{
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging();
        services.AddDbContextWithHook<AppDbContext>((_, o) => o.UseSqlite("DataSource=app.db"));
        services.AddEventPublisher<AppDbContext, LoggingEventPublisher>();
    }
}

public static class UsageDemo
{
    public static async Task RunAsync(AppDbContext db)
    {
        db.Orders.Add(new Order(Guid.NewGuid(), 42.00m));
        await db.SaveChangesAsync(); // both events reach LoggingEventPublisher only after this succeeds
    }
}
```

**Notes**: without `AddDbContextWithHook` (or a manual `UseHooks<AppDbContext>`), `Add`/`SaveChangesAsync` above compiles and runs but neither event is ever published — `EventHook` is registered in DI, not in the pipeline. `AddEvent(object)` never needs a mapper; `AddEvent<TEvent>()` always does (see next pattern) — missing one throws `EventException` naming `IMapper`, only at the `SaveChangesAsync` that actually needs to map, never at the `AddEvent<TEvent>()` call site itself.

### Register the IMapper that AddEvent&lt;TEvent&gt;() and [RaisesEvent] require

**When**: any entity in the `DbContext` calls `AddEvent<TEvent>()` or carries `[RaisesEvent]`.

```csharp
using Mapster;
using MapsterMapper;
using Microsoft.Extensions.DependencyInjection;

public static class MapperSetup
{
    public static void ConfigureServices(IServiceCollection services)
    {
        TypeAdapterConfig.GlobalSettings.NewConfig<Order, OrderSnapshotEvent>();
        services.AddSingleton(TypeAdapterConfig.GlobalSettings);
        services.AddScoped<IMapper, ServiceMapper>();
    }
}
```

**Notes**: `ServiceMapper` ships in the separate `Mapster.DependencyInjection` package — `dotnet add package Mapster.DependencyInjection` alongside `Mapster`. Registering more than one `IMapper` is not an error; `EventHook` just uses `mappers.FirstOrDefault()` and silently ignores the rest.

### [RaisesEvent] — convention form with narrowing

**When**: the default entity-name-based event name is fine, and only some property changes should raise it.

```csharp
using DKNet.EfCore.Abstractions.Events;

[RaisesEvent(EventOperations.Updated, nameof(Tier))] // generates LoyaltyMembershipTierUpdatedEvent
public class LoyaltyMembership
{
    public int Points { get; private set; }
    public string Tier { get; private set; } = string.Empty;

    public void ChangeTier(string tier) => Tier = tier;
}
```

**Notes**: a change to `Points` alone does **not** raise this rule — only `Tier`. Narrowing is evaluated in the before-save run via `EntityEntry.Property(name).IsModified`, because that flag is meaningless once the save completes — this is why declared-event qualification is captured *before* the write and publishing happens *after* it. No `IEventEntity`/`Entity<TKey>` needed for this form — declared events are read via reflection, not through `IEventEntity`.

The **type-naming form** — `[RaisesEvent(typeof(OrderPlacedEvent), EventOperations.Created)]` naming an existing `[GenerateDto]` payload record — and the **label form** — `[RaisesEvent("Touched", EventOperations.Created)]`, which composes a label into the generated name — exist too; both need `DKNet.EfCore.DtoGenerator` referenced to actually generate the payload, and its build-time diagnostics (`DKRAISEVT0xx`) are `dknet-codegen`'s territory, not this package's. Applying `[RaisesEvent]` in any form never fails a build by itself — a project that only references `DKNet.EfCore.Abstractions` and `DKNet.EfCore.DtoGenerator` builds cleanly with rules declared and simply never raises them until the runtime pieces above are wired up.

### Multiple publishers, one save

**When**: several independent side effects (log, outbox, message bus) should all see every event.

```csharp
using DKNet.EfCore.Abstractions.Events;
using Microsoft.Extensions.DependencyInjection;

public sealed class OutboxEventPublisher : DefaultEventPublisher
{
    public override Task PublishAsync(object eventObj, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

public static class MultiPublisherStartup
{
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddEventPublisher<AppDbContext, LoggingEventPublisher>();
        services.AddEventPublisher<AppDbContext, OutboxEventPublisher>(); // different implementation -> both run
    }
}
```

**Notes**: registering the **same** implementation type twice is a no-op; registering a *different* implementation adds it, and every registered publisher receives the full event list for that save, each inside its own try/catch — one throwing publisher is logged and skipped, the rest still run, and the already-committed write is never undone.

## Runtime behaviour

What happens, in order, for one `await db.SaveChangesAsync()` on a hook-aware `DbContext`:

1. The before-save run (installed by `DKNet.EfCore.Hooks`) runs every registered `IBeforeSaveHookAsync`, including `EventHook.BeforeSaveAsync`. For each tracked entity it maps the snapshot entry's captured original state (`Added`/`Modified`/`Deleted`) to an `EventOperations` value, looks up that entity type's `[RaisesEvent]` attributes (cached per entity type), and for each rule whose `Operations` flag matches and whose narrowing `Properties` (if any) include at least one modified property, resolves the payload `Type` (cached per `(entityType, composedName)`) and records `(entity, eventType)` — deduped, so two rules naming the same payload for the same operation raise once. Hand-raised (`AddEvent(...)`) events are untouched here.
2. EF Core performs the actual INSERT/UPDATE/DELETE. If it throws, the failure path tears down the hook's per-save state and **no event is ever published** — hand-raised or declared.
3. Once the write succeeds, the after-save run fires every `IAfterSaveHookAsync`, including `EventHook.AfterSaveAsync`. It walks every tracked `IEventEntity`, keeps hand-raised object instances as-is, maps `AddEvent<TEvent>()` entries via `IMapper`, stamps `sourceType` onto any `IEventItem`, then appends the declared events from step 1 (also mapped via the same `IMapper`).
4. Every registered `IEventPublisher` — in DI enumeration order, not a guaranteed order — receives the **full** combined list via `PublishAsync(IEnumerable<object>, ct)`, each call inside its own try/catch; a throwing publisher is logged (if a logger was provided) and skipped.
5. Regardless of publish outcome, every entity's event queue and the declared-events set are cleared unconditionally. The next `SaveChanges` starts with empty queues — a failed publish is never retried by this package.

## Diagnostics & exceptions

| Exception | When | Fix |
|---|---|---|
| `EventException` | (a) an entity queued a type-based event via `AddEvent<TEvent>()` with no `IMapper` registered; (b) an entity qualifies for a `[RaisesEvent]` declared event with no `IMapper` registered; (c) a `[RaisesEvent]` convention-form rule composes a payload name that doesn't resolve to a generated type in the entity's own assembly/namespace. | Register an `IMapper` (Mapster's `ServiceMapper` or your own) for (a)/(b); for (c), ensure `DKNet.EfCore.DtoGenerator` is referenced and the project rebuilt so the composed record actually exists. |

No analyzer `DiagnosticDescriptor`s are defined in this package — the `DKRAISEVT0xx` build-time diagnostics the docs mention belong to `DKNet.EfCore.DtoGenerator`, which validates `[RaisesEvent]` at compile time; this package only reads the attribute via reflection at save time.

## Gotchas

- **`AddEventPublisher` without `AddDbContextWithHook`/`UseHooks<TDbContext>` compiles and resolves `IEventPublisher` from DI just fine — but nothing ever dispatches.** `AddHook<TDbContext, EventHook>()` only adds the hook as a keyed DI candidate; the interceptor that invokes hooks is wired by `UseHooks<TDbContext>` inside `AddDbContextWithHook`.
- **A rolled-back *ambient* transaction does not un-publish events already dispatched.** Dispatch happens on a successful `SaveChangesAsync` return, not on an outer transaction's commit. An app that wraps `SaveChangesAsync` in its own `BeginTransactionAsync`/`RollbackAsync` still sees the event published even though the row it describes was rolled back.
- **Two structurally-equal hand-raised events on the same entity collapse into one published event.** Event collection de-duplicates per entity through a `HashSet<object>`, and `record` types use value equality.
- **Multiple `IMapper` registrations silently pick the first one, with no warning.** A second, differently-configured `IMapper` registered later in the container is simply never used for event mapping.
- **A publisher that always throws still runs, and still logs, on every single save that has events — forever.** There is no circuit breaker or backoff; each call is caught and logged individually with no state carried between saves.
- **`DisableHooks()` (owned by `DKNet.EfCore.Hooks`) silently disables event dispatch too**, since `EventHook` is just another hook on the shared pipeline.
- **Declared `Deleted` events mirror pre-removal entity state**, not nulled-out fields — EF Core doesn't clear an entity's in-memory properties on delete.
- **A change confined to an owned value (`OwnsOne`) does not raise the owner's `Updated` rule** — EF Core doesn't mark the owner itself `Modified` when only the owned value changed.

## Anti-patterns & hallucination traps

- `services.AddEventHandler<T>()`, `AddEventDispatcher<T>()`, `AddDomainEvents<T>()` — **do not exist**. The only DI entry point is `EventSetup.AddEventPublisher<TDbContext, TImplementation>()`.
- Calling `IEventPublisher.PublishAsync(...)` yourself from application/handler code to "dispatch now" — dispatch is automatic, driven by `EventHook` after `SaveChangesAsync` succeeds. Manually invoking a publisher bypasses the before/after-save split and the queue-clearing guarantee.
- Referencing `DKNet.EfCore.Events.Internals.EventHook` or `EventContext` directly — both are `internal sealed`; an ordinary consuming project cannot see them.
- Assuming `AddEvent<TEvent>()` behaves like `AddEvent(object)` and needs no mapper — it is the opposite: `object` never needs a mapper, `<TEvent>` always does.
- Hand-writing a payload record for a `[RaisesEvent]` convention form (e.g. authoring your own `CustomerCreatedEvent` class) instead of letting `DKNet.EfCore.DtoGenerator` generate it — the generated record is `partial`; add members to a `partial record` of the same name instead of replacing it, or the build conflicts.
- Assuming publisher order or entity-processing order is deterministic — there is **no ordering guarantee** across entities or across registered publishers.
- Expecting a retry, dead-letter, or at-least-once delivery knob — none exist. Build a transactional outbox (write to an outbox table from a `BeforeSaveHookAsync` inside the same save transaction, drain it separately) if that guarantee is required.
- Putting `Include`/`Exclude` on a type-naming `[RaisesEvent]` declaration — that form's payload already owns its shape via its own `[GenerateDto]` `Include`/`Exclude`; this is a `DKNet.EfCore.DtoGenerator` build error, not something this package validates at runtime.

## Composes with

| Package | Rule |
|---|---|
| `DKNet.EfCore.Abstractions` | Reach for it first — defines `IEventEntity`, `Entity<TKey>`/`Entity`, `AddEvent`, `IEventPublisher`, `DefaultEventPublisher`, `[RaisesEvent]`, `EventOperations`. `DKNet.EfCore.Events` alone gives an app nothing to raise events *with*. |
| `DKNet.EfCore.Hooks` | Required, not optional — `AddDbContextWithHook<TDbContext>()`/`UseHooks<TDbContext>()` installs the interceptor `EventHook` runs inside, and `DisableHooks()` is the shared (not events-specific) seeding/migration escape hatch. |
| `DKNet.EfCore.DtoGenerator` | Required whenever `[RaisesEvent]` is used — generates and compile-time-validates the convention-form payload records (and validates the type-naming form). Reach for it when a declared event won't resolve, or a payload shape is wrong. |
| `DKNet.EfCore.AuditLogs` | A sibling hook on the same pipeline. Reach for it when the requirement is a change *trail*, not a *reaction* — the two compose, they don't replace each other. |
| `DKNet.SlimBus.Extensions` | A ready-made transport. Reach for it when your `IEventPublisher` implementation should hand events to SlimMessageBus rather than log/no-op them. |
| Mapster (`MapsterMapper.IMapper`) | Required the moment any entity uses `AddEvent<TEvent>()` or `[RaisesEvent]`. Register `TypeAdapterConfig.GlobalSettings` + `IMapper` (Mapster's `ServiceMapper`, or a hand-written implementation) alongside `AddEventPublisher`. |

## Testing notes

- Tests drive a **real SQLite in-memory** `DbContext` (`Microsoft.Data.Sqlite`, `UseSqlite(sharedConnection)` + `UseAutoConfigModel()`), not EF Core InMemory and not TestContainers — this package's dispatch mechanics don't depend on SQL-Server-specific behaviour, so SQLite is enough to exercise real `SaveChangesAsync`/`IsModified` semantics.
- Every fixture wires `TypeAdapterConfig.GlobalSettings` + `AddScoped<IMapper, ServiceMapper>()` so `AddEvent<TEvent>()`/`[RaisesEvent]` mapping actually works; a mapper-omitted fixture variant exists specifically to exercise the `EventException` paths.
- Publisher test doubles expose a shared `IList<object>` of published events; tests clear it at the start of each test since the list is shared across tests within the same fixture/class.
- DI-shape assertions (hook actually registered, correct lifetime, separate hooks per `DbContext`) are made without a real save, via `serviceProvider.GetKeyedServices<IHookBaseAsync>(typeof(TDbContext).FullName)`.
- Generated-payload shape assertions (`Exclude`/`Include` actually removing a property) go through reflection on the generated record type, not just checking the captured value is blank.
