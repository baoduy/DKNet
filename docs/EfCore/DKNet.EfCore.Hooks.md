# DKNet.EfCore.Hooks

A pluggable before/after-`SaveChanges` interceptor pipeline for EF Core — one shared interceptor per `DbContext` type
plus a small pair of interfaces you implement.

## ✨ Why use it?

- **One interceptor instead of a pile of them** — audit logging, domain events, ownership stamping, and your own
  concerns all register as hooks on the same `HookRunnerInterceptor` rather than each re-implementing "walk the change
  tracker, filter by state, do work".
- **Cross-cutting logic stays out of the domain** — hooks live in the infrastructure layer; neither your `DbContext`
  nor your entities reference them.
- **Both phases, with the right guarantees** — `IBeforeSaveHookAsync` runs while you can still mutate tracked
  entities and abort the save; `IAfterSaveHookAsync` runs only once the write has committed.
- **Registered per `DbContext` type, inheritance included** — a hook added against a base `DbContext` applies to
  derived contexts, and a hook never runs for a context it was not registered against.
- **One switch to suppress everything** — `dbContext.DisableHooks()` turns off audit, events, and ownership stamping
  together for a seeding or migration scope, with no per-package opt-out to remember.

Reach for this package when you need code to run around change-tracked entities on save. If you only need to intercept
SQL statements or connections, use EF Core's `SaveChangesInterceptor`/`DbCommandInterceptor` directly.

## 🚀 Quick Start

```bash
dotnet add package DKNet.EfCore.Hooks
```

The package depends on `DKNet.EfCore.Extensions` for `SnapshotContext` (see [DKNet.EfCore.Extensions](./DKNet.EfCore.Extensions.md)) and on `Microsoft.EntityFrameworkCore`.

Minimum wiring — register your `DbContext` through `AddDbContextWithHook` instead of `AddDbContext`, then register hook implementations with `AddHook`:

```csharp
using DKNet.EfCore.Hooks;
using Microsoft.Extensions.DependencyInjection;

services.AddDbContextWithHook<AppDbContext>((provider, options) =>
    options.UseSqlServer(connectionString));

services.AddHook<AppDbContext, MyAuditHook>();
```

`AddDbContextWithHook<TDbContext>` has two overloads — one taking `Action<IServiceProvider, DbContextOptionsBuilder>`, one taking `Action<DbContextOptionsBuilder<TDbContext>>` — both mirroring the standard `AddDbContext` overloads and internally calling `AddHookRunner<TDbContext>()` plus `options.UseHooks<TDbContext>(provider)` for you. If you must register the `DbContext` yourself (e.g. a base class already calls `AddDbContext`), call `UseHooks<TDbContext>(provider)` explicitly inside your own options delegate instead:

```csharp
services.AddHookRunner<AppDbContext>(); // internal-only: normally implied by AddDbContextWithHook/AddHook
services.AddDbContext<AppDbContext>((provider, options) =>
{
    options.UseSqlServer(connectionString);
    options.UseHooks<AppDbContext>(provider);
});
```

`AddHookRunner<TDbContext>` is `internal` — you will not call it directly from application code; `AddDbContextWithHook` and `AddHook` both call it for you, idempotently, so registration order between them does not matter.

Hooks with no registered `HookRunnerInterceptor` for their `DbContext` type are silently never invoked — always register the `DbContext` via `AddDbContextWithHook` (or call `UseHooks<TDbContext>` yourself) or your `AddHook<TDbContext, THook>()` calls will have no effect.

## 🧩 Features

### The hook interfaces

All hook contracts live in `DKNet.EfCore.Hooks.IHook.cs` and operate on `SnapshotContext` from `DKNet.EfCore.Extensions.Snapshots` (see [DKNet.EfCore.Extensions](./DKNet.EfCore.Extensions.md#capture-a-save-time-snapshot-for-hooks)):

```csharp
public interface IHookBaseAsync; // marker, implement a more specific interface below

public interface IBeforeSaveHookAsync : IHookBaseAsync
{
    Task BeforeSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default);
}

public interface IAfterSaveHookAsync : IHookBaseAsync
{
    Task AfterSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default);
}

public interface IHookAsync : IBeforeSaveHookAsync, IAfterSaveHookAsync;

public abstract class HookAsync : IHookAsync
{
    public virtual Task BeforeSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public virtual Task AfterSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
```

Implement `IBeforeSaveHookAsync` for a before-only hook, `IAfterSaveHookAsync` for an after-only hook, `IHookAsync` (or inherit `HookAsync` and override only what you need) for both. `SnapshotContext.Entities` (an `IReadOnlyCollection<SnapshotEntityEntry>`) exposes `Entity`, `Entry` (the underlying EF Core `EntityEntry`) and `OriginalState` for every entry that was `Added`, `Modified`, or `Deleted` at the moment the snapshot was captured — the same snapshot instance is shared by every hook registered on the `DbContext`, captured once before the before-save hooks run.

Example — a before-save audit stamp hook and an after-save event-publishing hook:

```csharp
using DKNet.EfCore.Hooks;
using DKNet.EfCore.Extensions.Snapshots;
using Microsoft.EntityFrameworkCore;

public sealed class AuditStampHook(ICurrentUserService currentUser) : IBeforeSaveHookAsync
{
    public Task BeforeSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in context.Entities)
        {
            if (entry.Entity is not IAuditedProperties audited) continue;

            if (entry.OriginalState == EntityState.Added)
                audited.CreatedBy = currentUser.UserId;
            if (entry.OriginalState is EntityState.Added or EntityState.Modified)
                audited.UpdatedOn = now;
        }

        return Task.CompletedTask;
    }
}

public sealed class DomainEventPublishingHook(IEventPublisher publisher) : IAfterSaveHookAsync
{
    public async Task AfterSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        foreach (var entry in context.Entities)
        {
            if (entry.Entity is not IEventEntity eventEntity) continue;

            foreach (var domainEvent in eventEntity.GetEvents())
                await publisher.PublishAsync(domainEvent, cancellationToken);

            eventEntity.ClearEvents();
        }
    }
}
```

Register each with the `DbContext` type it should run for:

```csharp
services.AddHook<AppDbContext, AuditStampHook>();
services.AddHook<AppDbContext, DomainEventPublishingHook>();
```

`AddHook<TDbContext, THook>()` registers `THook` as `AddKeyedScoped`, keyed by `typeof(TDbContext).FullName`, and calling it twice for the same `(TDbContext, THook)` pair is a no-op (it checks for an existing keyed registration first) — safe to call from multiple independent DI-setup methods. A hook registered for `TDbContext` also runs for any `DbContext` subclass of `TDbContext`, because `HookFactory` walks the runtime type's base-type chain when resolving keyed hooks — so hooks registered against a shared base `DbContext` are inherited by every derived context.

There is no built-in hook ordering: `AddHook` registers into the DI container's keyed-service collection, and hooks run in registration order for a given phase. If two hooks must run in a specific relative order, register them in that order (or fold them into a single hook).

### Disabling hooks — `HookDisablingContext`

Data seeding, bulk migrations, or fixups often need to bypass every hook (audit stamping, ownership assignment, event publishing) for a batch of saves. `DbContext.DisableHooks()` returns an `IHookDisablingContext` — dispose it (sync or async) to re-enable hooks:

```csharp
using DKNet.EfCore.Hooks;

await using (db.DisableHooks())
{
    db.Set<Product>().Add(new Product { Name = "Seed data" });
    await db.SaveChangesAsync(); // no hooks run for this save
}

// hooks run normally again from here
```

The disabling is reference-counted per `DbContext` CLR type (keyed by `Type.FullName`), so nested `using`/`await using` scopes are safe — hooks stay disabled until the outermost scope disposes. Disabling is scoped by *type*, not by `DbContext` instance: while a scope is active, hooks are suppressed for **every** instance of that `DbContext` type currently saving, not just the instance the scope was created from — keep disabling scopes short-lived and don't rely on it for per-instance isolation under concurrent access.

### How it runs — `HookFactory` and `HookRunnerInterceptor`

You don't call these directly, but knowing the mechanics helps when hooks don't seem to fire:

- `HookRunnerInterceptor` is a keyed `SaveChangesInterceptor` (one singleton per `DbContext` type, keyed by `Type.FullName`) added to `DbContextOptionsBuilder` by `UseHooks<TDbContext>`. On `SavingChangesAsync` it runs all `IBeforeSaveHookAsync` hooks; on `SavedChangesAsync` (after a successful save) it runs all `IAfterSaveHookAsync` hooks; on `SaveChangesFailedAsync` it discards the pending hook context without running after-save hooks.
- `HookFactory.LoadHooks(dbContext)` resolves hooks from the **application** service provider (the one your `DbContext` was registered with via `AddDbContextWithHook`/`AddDbContext`), not a detached scope — this is why a `DbContext` must be registered through this package's DI extensions (or manually call `UseHooks`) for its hooks to resolve at all; otherwise `HookRunnerInterceptor` throws `InvalidOperationException` when it can't find an application service provider on the context.
- Hooks are looked up keyed by every type name in the `DbContext`'s inheritance chain, which is what makes hook registrations on a base `DbContext` apply to derived contexts too.
- If a save produces no tracked `Added`/`Modified`/`Deleted` entries, no hooks run for that save (the snapshot is empty and both phases short-circuit).
- Exceptions thrown from a before-save hook abort the save (the exception propagates out of `SaveChangesAsync`); exceptions from an after-save hook propagate too — the database write has already committed by that point, so an after-save hook failure does **not** roll back the save. Wrap risky after-save work in your own try/catch if a downstream failure (e.g. a flaky event publisher) shouldn't surface as a `SaveChangesAsync` exception.

## ⚙️ Configuration reference

There is no options object for this package — behavior is controlled entirely through what you register:

| Aspect | Default | How to change it |
|---|---|---|
| Which `DbContext` types run hooks | None, until registered | `AddDbContextWithHook<TDbContext>(...)` or `options.UseHooks<TDbContext>(provider)` |
| Which hooks run for a `DbContext` | None | `AddHook<TDbContext, THook>()`, once per `(TDbContext, THook)` pair |
| Hook execution order | DI registration order, before-hooks then after-hooks per phase | Register hooks in the order you need |
| Hook lifetime | Scoped (`AddKeyedScoped`) | Not configurable — hooks are always scoped to the owning `DbContext`'s DI scope |
| `HookRunnerInterceptor` lifetime | Singleton, keyed per `DbContext` type | Not configurable |
| Disabling hooks | Enabled | `dbContext.DisableHooks()` around a `using`/`await using` scope |

## 🧱 Where it fits

`DKNet.EfCore.Events`, `DKNet.EfCore.AuditLogs`, and `DKNet.EfCore.DataAuthorization` are all built as hooks on top of this package, sharing the same `HookRunnerInterceptor` pipeline and the same `SnapshotContext` type — verified directly against their internal hook classes:

- **`DKNet.EfCore.Events`** — `EventHook : HookAsync` (`DKNet.EfCore.Events/Internals/EventHook.cs`) captures which `[RaisesEvent]`-declared events qualify for the save in `BeforeSaveAsync` (state and modified-property checks can only be evaluated before the save), then publishes collected domain events in `AfterSaveAsync` once the save has succeeded. See [DKNet.EfCore.Events](./DKNet.EfCore.Events.md).
- **`DKNet.EfCore.AuditLogs`** — `EfCoreAuditHook : HookAsync` (`DKNet.EfCore.AuditLogs/Internals/EfCoreAuditHook.cs`) builds audit log entries from `context.Entities` in `BeforeSaveAsync`, caches them per `DbContext` instance ID, and publishes them via registered `IAuditLogPublisher`s in `AfterSaveAsync`. See [DKNet.EfCore.AuditLogs](./DKNet.EfCore.AuditLogs.md).
- **`DKNet.EfCore.DataAuthorization`** — `DataOwnerHook : IBeforeSaveHookAsync` (`DKNet.EfCore.DataAuthorization/Internals/DataOwnerHook.cs`) stamps ownership on newly added entities and guards modified entities against cross-tenant `OwnedBy` reassignment, entirely in `BeforeSaveAsync`.

Because all three register through the same `AddHook<TDbContext, THook>()` extension against your `DbContext`, they compose automatically: register your `DbContext` once with `AddDbContextWithHook`, then add whichever of `AddEventPublisher<TDbContext, TPublisher>()`, `AddEfCoreAuditLogs<TDbContext, TPublisher>()`, and `AddDataOwnerProvider<TDbContext, TProvider>()` your application needs — they run side by side without needing to know about each other. A single `dbContext.DisableHooks()` scope suppresses all of them at once, which is exactly why it's the recommended way to bypass audit/event/ownership stamping during seeding.

## ⚠️ Gotchas & limits

- **Registering the `DbContext` the wrong way silently drops hooks.** If you keep using plain `AddDbContext` without also calling `options.UseHooks<TDbContext>(provider)`, `AddHook` registrations exist in DI but the interceptor that would invoke them is never attached — no exception, hooks just never run. Prefer `AddDbContextWithHook`.
- **No application service provider → hard failure, not silent skip.** If a `DbContext` *is* intercepted by `HookRunnerInterceptor` but was constructed in a way that has no `ApplicationServiceProvider` set (e.g. hand-built `DbContextOptions` bypassing DI), resolving hooks throws `InvalidOperationException` at save time.
- **`SnapshotContext` only ever contains `Added`/`Modified`/`Deleted` entries**, captured once per save via `ChangeTracker.DetectChanges()`. Mutating entity state from *inside* a before-save hook (adding new entities, changing state) is not re-snapshotted for hooks running later in the same phase — the snapshot is fixed for the whole `BeforeSaveAsync`/`AfterSaveAsync` run.
- **After-save hook failures don't roll back the save.** The write has already committed once `SavedChangesAsync` runs; treat after-save hook errors as "best effort, log and move on" unless you specifically want a failed publish to surface as an exception from `SaveChangesAsync`.
- **No built-in hook priority/ordering mechanism.** Order is purely DI registration order; there's no `[Order]` attribute or similar. Split a hook that must run first from ones that depend on it, and register them explicitly in the required order.
- **`DisableHooks()` suppresses by `DbContext` type, not instance.** In a process with multiple concurrently-open instances of the same `DbContext` type, a disabling scope opened for suppressing seeding on one instance also suppresses hooks for every other instance of that type saving concurrently, until disposed.
- **Hooks are scoped, not singleton** — a hook with per-save mutable state (like `EfCoreAuditHook`'s per-`ContextId` cache) needs to key any cross-phase state by `context.DbContext.ContextId.InstanceId` if the same hook instance could conceivably see multiple `DbContext` instances' saves (it normally won't, since hooks are scoped alongside the `DbContext`, but avoid assuming a 1:1 lifetime you haven't verified for your own DI setup).

## 🔗 Related packages

- [DKNet.EfCore.Events](./DKNet.EfCore.Events.md) – dispatches domain events as a hook on this pipeline. Reach for it
  instead of writing your own after-save publishing hook.
- [DKNet.EfCore.AuditLogs](./DKNet.EfCore.AuditLogs.md) – field-level change capture as a hook on this pipeline. Reach
  for it instead of writing your own change-diffing hook.
- [DKNet.EfCore.DataAuthorization](./DKNet.EfCore.DataAuthorization.md) – ownership stamping and reassignment guarding
  as a before-save hook. Reach for it for row-level multi-tenant isolation.
- [DKNet.EfCore.Extensions](./DKNet.EfCore.Extensions.md) – owns `SnapshotContext` and the model-building/wiring layer
  the hooks read. Reach for it for automatic entity configuration, global query filters, and seeding.
- [DKNet.EfCore.Abstractions](./DKNet.EfCore.Abstractions.md) – the entity base classes and attributes hooks act on.
  Reach for it when modelling the entities themselves.
