---
name: dknet-efcore-save-pipeline
description: "Covers DKNet.EfCore.Hooks, DKNet.EfCore.Events, and DKNet.EfCore.AuditLogs: the three SaveChangesAsync interceptors that stamp, dispatch, and audit around one EF Core write. APIs: AddDbContextWithHook, IBeforeSaveHookAsync/IAfterSaveHookAsync/IHookAsync/HookAsync, AddHook, DisableHooks, SnapshotContext (before/after SaveChanges hook, IHook wiring); AddEventPublisher, IEventPublisher, DefaultEventPublisher, AddEvent/AddEvent<TEvent>(), [RaisesEvent], EventOperations, EventException (domain events not firing); AddEfCoreAuditLogs, AddEfCoreAuditHook, AddCurrentUserProvider, ICurrentUserProvider, AuditLogEntry, AuditFieldChange, [SensitiveData], [IgnoreAuditLog], [AuditLog] (audit trail, redact sensitive fields). Use when wiring a SaveChanges hook, when events or the audit trail silently never fire, or EventException throws."
license: MIT
metadata:
  author: baoduy
  version: "0.1.0"
  packages: "DKNet.EfCore.Hooks, DKNet.EfCore.Events, DKNet.EfCore.AuditLogs"
---

# DKNet SaveChanges hooks, domain events and audit logs

This skill wires DKNet's `SaveChangesAsync` pipeline: `DKNet.EfCore.Hooks`' before/after-save interceptor, `DKNet.EfCore.Events`' domain-event dispatch, and `DKNet.EfCore.AuditLogs`' change-diffing audit trail — three independent hooks that share one `HookRunnerInterceptor` per `DbContext`. Open `references/DKNet.EfCore.Hooks.md` for the hook contracts and `SnapshotContext`, `references/DKNet.EfCore.Events.md` for `AddEvent`/`[RaisesEvent]` dispatch, and `references/DKNet.EfCore.AuditLogs.md` for `AuditLogEntry` and redaction — each carries the full public surface, options, diagnostics, and gotchas.

## Packages

| Package | Install | What it gives you | Depends on | Reference |
|---|---|---|---|---|
| `DKNet.EfCore.Hooks` | `dotnet add package DKNet.EfCore.Hooks` | The before/after-`SaveChanges` interceptor pipeline (`AddDbContextWithHook`, `IBeforeSaveHookAsync`/`IAfterSaveHookAsync`, `AddHook`, `DisableHooks`) every hook in this skill plugs into. | `DKNet.EfCore.Extensions` (owns `SnapshotContext`) | [references/DKNet.EfCore.Hooks.md](references/DKNet.EfCore.Hooks.md) |
| `DKNet.EfCore.Events` | `dotnet add package DKNet.EfCore.Events` | Domain-event dispatch after a successful save (`AddEventPublisher`, `IEventPublisher`, `AddEvent`/`AddEvent<TEvent>()`, `[RaisesEvent]`). | `DKNet.EfCore.Abstractions`, `DKNet.EfCore.Hooks` | [references/DKNet.EfCore.Events.md](references/DKNet.EfCore.Events.md) |
| `DKNet.EfCore.AuditLogs` | `dotnet add package DKNet.EfCore.AuditLogs` | A change-diffing audit trail with redaction (`AddEfCoreAuditLogs`, `AddCurrentUserProvider`, `AuditLogEntry`, `[SensitiveData]`). | `DKNet.EfCore.Abstractions`, `DKNet.EfCore.Hooks` | [references/DKNet.EfCore.AuditLogs.md](references/DKNet.EfCore.AuditLogs.md) |

## Quick start

Entities and publishers:

```csharp
using DKNet.EfCore.AuditLogs;
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.Abstractions.Events;
using Microsoft.EntityFrameworkCore;

public record OrderPlacedEvent(Guid OrderId, decimal Total);

public class Order : AuditedEntity<Guid>
{
    private Order() { } // EF Core

    public Order(Guid id, decimal total) : base(id)
    {
        Total = total;
        AddEvent(new OrderPlacedEvent(id, total)); // queued, not dispatched yet
    }

    public decimal Total { get; private set; }
}

public sealed class LoggingEventPublisher : DefaultEventPublisher
{
    public override Task PublishAsync(object eventObj, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Publishing {eventObj.GetType().Name}");
        return Task.CompletedTask;
    }
}

public sealed class ConsoleAuditLogPublisher : IAuditLogPublisher
{
    public Task PublishAsync(IEnumerable<AuditLogEntry> logs, CancellationToken cancellationToken = default)
    {
        foreach (var log in logs)
            Console.WriteLine($"[{log.Action}] {log.EntityName} by {log.CreatedBy}");
        return Task.CompletedTask;
    }
}

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
}
```

Wiring and one real use — one `AddDbContextWithHook` call attaches the interceptor; `AddEventPublisher` and `AddEfCoreAuditLogs` are two hooks registered on top of it:

```csharp
using DKNet.EfCore.AuditLogs;
using DKNet.EfCore.Hooks;
using Microsoft.EntityFrameworkCore;

public static class Startup
{
    public static void ConfigureServices(IServiceCollection services, string connectionString)
    {
        services.AddDbContextWithHook<AppDbContext>((_, o) => o.UseSqlite(connectionString));
        services.AddEventPublisher<AppDbContext, LoggingEventPublisher>();
        services.AddEfCoreAuditLogs<AppDbContext, ConsoleAuditLogPublisher>();
    }
}

public static class UsageDemo
{
    public static async Task<Guid> PlaceOrderAsync(AppDbContext db)
    {
        var order = new Order(Guid.NewGuid(), 42.00m);
        db.Orders.Add(order);
        await db.SaveChangesAsync(); // audit stamped -> row written -> event published -> audit published
        return order.Id;
    }
}
```

## Rules

1. **Every registration here is inert without `AddDbContextWithHook<TDbContext>`.** `AddHook`, `AddEventPublisher`, `AddEfCoreAuditHook`/`AddEfCoreAuditLogs`, and `AddCurrentUserProvider` only add a candidate hook to DI. Nothing invokes it until `HookRunnerInterceptor` is attached — via `AddDbContextWithHook<TDbContext>(...)` or a manual `options.UseHooks<TDbContext>(provider)`. No exception is thrown; the hook just never runs.
2. `AddHookRunner<TDbContext>` is `internal`. Never call it directly — use `AddDbContextWithHook`/`AddHook` instead.
3. There is no hook-ordering mechanism. Every `IBeforeSaveHookAsync` runs before the write, in DI registration order; EF Core writes; every `IAfterSaveHookAsync` runs after, also in DI order. No `[Order]`/priority attribute exists — register in the order you need, or fold logic into one hook.
4. `AddEvent(object)` never needs an `IMapper`. `AddEvent<TEvent>()` and every `[RaisesEvent]` declaration always do. A missing `IMapper` throws `EventException` at the *next* `SaveChangesAsync` that needs to map — never at the `AddEvent`/attribute call site.
5. Domain events and audit entries publish after `SaveChangesAsync` returns successfully — not after an app-managed *outer* transaction commits. A save wrapped in your own `BeginTransactionAsync`/`RollbackAsync` still publishes for a row that gets rolled back afterward.
6. Publishing is `await`ed inside `SaveChangesAsync`, and a publisher's exception is logged and swallowed *after the write already committed* — there is no rollback and no retry. One throwing `IEventPublisher`/`IAuditLogPublisher` is skipped; the rest still run.
7. `[SensitiveData]` and any attribute literally named `EncryptedAttribute` always redact in the audit trail, even when `[AuditLog]` is also applied to the same property — `[AuditLog]` never overrides them.
8. `IAuditedProperties`' own `[IgnoreAuditLog]` on `CreatedOn`/`UpdatedOn`/`CreatedBy`/`UpdatedBy` does **not** propagate to `AuditedEntity<TKey>`'s implementing properties — .NET attribute lookup doesn't cross an interface boundary. Expect those four fields in `Changes` unless you add `[IgnoreAuditLog]` on your own subclass or filter them in your publisher.
9. `DisableHooks()` suppresses hooks by `DbContext` CLR *type*, not by instance, scoped to the current async flow — it silently disables event dispatch and audit logging too, since both ride the same pipeline.
10. Don't hand-roll audit stamping or domain-event dispatch as a new hook. This pipeline already ships both; row-level ownership stamping is `DKNet.EfCore.DataAuthorization`'s job (see `dknet-efcore-data-security`).
11. A save with zero `Added`/`Modified`/`Deleted` entries short-circuits before any hook, event, or audit code runs — including one where you called `ChangeTracker.Clear()` first.
12. `AddEvent<TEvent>()`'s target type does not need `[GenerateDto]` — it can be any hand-written `class`/`record`. Only the `[RaisesEvent(typeof(X), ...)]` type-naming form requires `X` to be a `[GenerateDto]`-generated payload (see `dknet-codegen`).

## How to add a hook that reads or mutates tracked entities

**When**: cross-cutting logic (touch a column, log a count) must run inside the same `SaveChangesAsync` as everything else, both before and after the write.

```csharp
using DKNet.EfCore.Hooks;
using DKNet.EfCore.Extensions.Snapshots;
using Microsoft.EntityFrameworkCore;

public sealed class TouchTimestampHook : HookAsync
{
    public override Task BeforeSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        foreach (var entry in context.Entities)
        {
            if (entry.OriginalState is EntityState.Added or EntityState.Modified)
                entry.Entry.Property("RowVersionStampedAt").CurrentValue = DateTimeOffset.UtcNow;
        }

        return Task.CompletedTask;
    }

    public override Task AfterSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Saved {context.Entities.Count} entries.");
        return Task.CompletedTask;
    }
}
```

```csharp
using DKNet.EfCore.Hooks;

public static class Recipe1Setup
{
    public static void ConfigureServices(IServiceCollection services) =>
        services.AddHook<AppDbContext, TouchTimestampHook>();
}
```

- `HookAsync`'s default `BeforeSaveAsync`/`AfterSaveAsync` are no-ops — override only what you need, or implement `IBeforeSaveHookAsync`/`IAfterSaveHookAsync` directly for a single-phase hook.
- `entry.Entry.Property(name)` writes through the tracked `EntityEntry`, not the `SnapshotEntityEntry.Entity` reference, because the entity's own property may be get-only.
- Both phases read the *same* `SnapshotContext` instance; a before-hook that adds entities or flips states is not re-snapshotted for hooks running later in the same phase.

## How to suppress hooks during seeding or migration

**When**: a bulk fixup must bypass every hook — custom, event, and audit — for one save.

```csharp
using DKNet.EfCore.Hooks;

public static class SeedData
{
    public static async Task RunAsync(AppDbContext db)
    {
        await using (db.DisableHooks())
        {
            db.Orders.Add(new Order(Guid.NewGuid(), 10m));
            await db.SaveChangesAsync(); // no hook, event, or audit code runs for this save
        }
        // hooks run normally again from here
    }
}
```

- `DisableHooks()` is reference-counted and safe to nest; the outermost `await using` re-enables hooks.
- It suppresses by `DbContext` CLR type for the current async flow, not by instance — see Rule 9.

## How to share one hook registration across a DbContext hierarchy

**When**: several `DbContext` types share a base class and one hook registration should cover all of them.

```csharp
using Microsoft.EntityFrameworkCore;

public class BaseAppDbContext(DbContextOptions options) : DbContext(options);

public sealed class TenantDbContext(DbContextOptions<TenantDbContext> options) : BaseAppDbContext(options);
```

```csharp
using DKNet.EfCore.Hooks;
using Microsoft.EntityFrameworkCore;

public static class Recipe3Setup
{
    public static void ConfigureServices(IServiceCollection services, string tenantConnectionString)
    {
        services.AddHook<BaseAppDbContext, TouchTimestampHook>();
        // TouchTimestampHook also runs for TenantDbContext saves: HookFactory walks TenantDbContext's
        // base-type chain when resolving keyed hooks.
        services.AddDbContextWithHook<TenantDbContext>((_, o) => o.UseSqlite(tenantConnectionString));
    }
}
```

- `AddHook<BaseAppDbContext, _>` alone does not attach an interceptor to `TenantDbContext` — it still needs its own `AddDbContextWithHook<TenantDbContext>` (or `UseHooks<TenantDbContext>`) so `HookRunnerInterceptor` is actually present to invoke the inherited hook.

## How to map entity state onto an event at save time

**When**: the event should mirror entity state at dispatch time, not the call site's state — and the payload doesn't need to be a `[GenerateDto]` record.

```csharp
using DKNet.EfCore.Abstractions.Entities;

public record InvoiceSnapshotEvent(Guid Id, decimal Total);

public class Invoice : Entity<Guid>
{
    private Invoice() { } // EF Core

    public Invoice(Guid id, decimal total) : base(id)
    {
        Total = total;
        AddEvent<InvoiceSnapshotEvent>(); // mapped from current state at dispatch time, not call time
    }

    public decimal Total { get; private set; }
}
```

```csharp
using Mapster;
using MapsterMapper;

public static class Recipe4Setup
{
    public static void ConfigureServices(IServiceCollection services)
    {
        TypeAdapterConfig.GlobalSettings.NewConfig<Invoice, InvoiceSnapshotEvent>();
        services.AddSingleton(TypeAdapterConfig.GlobalSettings);
        services.AddScoped<IMapper, ServiceMapper>();
    }
}
```

- `ServiceMapper` ships in the `Mapster.DependencyInjection` package, not the base `Mapster` package — `dotnet add package Mapster.DependencyInjection` alongside `Mapster`.
- Without an `IMapper` registered, this throws `EventException` at the next save that raises an `Invoice` change — not at the `AddEvent<TEvent>()` call site.

## How to declare an event without hand-raising it

**When**: the default entity-name-based event is fine, and only some property changes should raise it.

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

- A change to `Points` alone does not raise this rule — only `Tier`. No `IEventEntity`/`Entity<TKey>` base is needed: `[RaisesEvent]` is read by reflection, not through `AddEvent`.
- This declares the rule and, once `DKNet.EfCore.DtoGenerator` is referenced, generates the payload record at build time. Applying the attribute never fails a build by itself; wiring `AddEventPublisher`/`AddDbContextWithHook` is still required to actually publish it (Rule 1). The type-naming form (`[RaisesEvent(typeof(X), ...)]`) and generator diagnostics belong to `dknet-codegen`.

## How to build an audit trail, stamp the current user, and redact sensitive fields

**When**: entities need a change trail with attribution, and some fields must never appear in plaintext.

```csharp
using DKNet.EfCore.Abstractions.Attributes;
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.AuditLogs;

public interface ICurrentPrincipal
{
    string? SubjectId { get; }
}

public sealed class SignedInUserProvider(ICurrentPrincipal principal) : ICurrentUserProvider
{
    public string? GetCurrentUser() => principal.SubjectId;
}

public sealed class ApiClient : AuditedEntity<Guid>
{
    public required string Name { get; set; }

    [AuditLog] // "Token" matches the built-in sensitive-name pattern; [AuditLog] forces plaintext capture
    public DateTimeOffset TokenExpiryUtc { get; set; }

    [AuditLog] // does NOT win over [SensitiveData] on the same property -- still redacted
    [SensitiveData]
    public string? InternalNotes { get; set; }

    [IgnoreAuditLog] // never appears in Changes at all, regardless of behaviour or policy
    public byte[]? Thumbnail { get; set; }
}
```

```csharp
using DKNet.EfCore.AuditLogs;

public static class Recipe6Setup
{
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddEfCoreAuditLogs<AppDbContext, ConsoleAuditLogPublisher>(); // defaults: IncludeAllAuditedEntities, RedactSensitive
        services.AddCurrentUserProvider<AppDbContext, SignedInUserProvider>();
    }
}
```

- `AddCurrentUserProvider` is application-wide, not per-`DbContext` — first registration wins, and it never overwrites a prior `AddEfCoreAuditLogs`/`AddEfCoreAuditHook` call's `behaviour`/`propertyPolicy`.
- To opt in instead of auditing everything, pass `behaviour: AuditLogBehaviour.OnlyAttributedAuditedEntities, propertyPolicy: AuditPropertyPolicy.OnlyAttributedProperties` to `AddEfCoreAuditLogs` and mark the class `[AuditLog]` too — see `references/DKNet.EfCore.AuditLogs.md`.

## Runtime behaviour

For one `await db.SaveChangesAsync()` on a `DbContext` registered via `AddDbContextWithHook`, with a custom hook, an event hook, and the audit hook all registered (any DI order):

1. `HookRunnerInterceptor.SavingChangesAsync` fires. It resolves one shared `SnapshotContext` for this save — `ChangeTracker.DetectChanges()`, then capture every `Added`/`Modified`/`Deleted` entry. Zero such entries short-circuits everything below.
2. Every registered `IBeforeSaveHookAsync.BeforeSaveAsync(snapshot, ct)` runs in DI registration order, against that same snapshot: `EfCoreAuditHook` stamps `CreatedBy`/`UpdatedBy` then builds and caches `AuditLogEntry` objects; `EventHook` reads `[RaisesEvent]` attributes and records which declared events qualify (narrowing is checked via `IsModified`, which is why this must happen before the write). A before-hook that throws aborts the save — nothing below runs.
3. EF Core executes the INSERT/UPDATE/DELETE.
4. On failure, `SaveChangesFailedAsync` tears down the cached hook state. No `IAfterSaveHookAsync` runs; no event or audit entry is ever published.
5. On success, `HookRunnerInterceptor.SavedChangesAsync` fires. Every `IAfterSaveHookAsync.AfterSaveAsync(snapshot, ct)` runs, same DI order: `EfCoreAuditHook` awaits every keyed `IAuditLogPublisher.PublishAsync(...)` for this `DbContext` instance's cached entries, each in its own try/catch; `EventHook` collects hand-raised plus declared events and awaits every registered `IEventPublisher.PublishAsync(...)` with the full batch, also each in its own try/catch.
6. Queues clear unconditionally — cached audit entries removed, `ClearEvents()` called — regardless of publish success. A failed publish is never retried by either package.

## Gotchas

- **Registering a hook is not the same as attaching the interceptor.** `AddHook`/`AddEventPublisher`/`AddEfCoreAuditHook`/`AddEfCoreAuditLogs`/`AddCurrentUserProvider` all register DI candidates only. Forgetting `AddDbContextWithHook<TDbContext>` (or a manual `options.UseHooks<TDbContext>(provider)`) means every one of them silently never runs — no exception, no log.
- **A rolled-back *outer* transaction does not un-publish events or audit entries already dispatched.** Both fire on a successful `SaveChangesAsync` return, not on the surrounding transaction's commit. An app that wraps several saves in its own `BeginTransactionAsync`/`RollbackAsync` still sees them published for rows that get rolled back afterward.
- **Multiple `IMapper` registrations silently pick the first one.** `EventHook` resolves `mappers.FirstOrDefault()` — a second, differently configured `IMapper` is simply never used for event mapping, with no warning.
- **The audit redaction sentinel erases whether a value even changed.** A redacted field's `OldValue` and `NewValue` both become the identical string `"***REDACTED***"` — a publisher can't tell from the entry alone whether the real value changed.
- **`CreatedBy`/`UpdatedBy`/`CreatedOn`/`UpdatedOn` show up in `Changes` by default** despite `IAuditedProperties` declaring them `[IgnoreAuditLog]` at the interface level — see Rule 8. Add `[IgnoreAuditLog]` on your own subclass's properties, or filter them in your `IAuditLogPublisher`, if you need them hidden.
- **A change confined to an owned value (`OwnsOne`) does not raise the owner's `Updated` `[RaisesEvent]` rule** — EF Core doesn't mark the owner itself `Modified` when only the owned value changed.
- **Declared `Deleted` events and audit entries mirror pre-removal state**, not nulled-out fields — EF Core doesn't clear an entity's in-memory properties on delete.
- **A publisher that always throws still runs, and still logs, on every save that has something to publish — forever.** There is no circuit breaker, backoff, or dead-letter queue in either package.
- **`HookFactory` walks the `DbContext`'s base-type chain, but each type still needs its own interceptor.** A hook registered on a base `DbContext` runs for derived types too, but each derived `DbContext` type still needs its own `AddDbContextWithHook`/`UseHooks` call.

## Do not

- There is no `IHook` interface and no non-`Async`-suffixed hook contract — only `IHookBaseAsync`, `IBeforeSaveHookAsync`, `IAfterSaveHookAsync`, `IHookAsync`.
- Do not call `AddHookRunner<TDbContext>()` — it is `internal` and will not compile from application code.
- No `[Order]`, `[Priority]`, or similar sequencing attribute exists for hooks. Order is pure DI registration order.
- `services.AddEventHandler<T>()`, `AddEventDispatcher<T>()`, `AddDomainEvents<T>()` do not exist. The only DI entry point for dispatch is `AddEventPublisher<TDbContext, TImplementation>()`.
- Do not call `IEventPublisher.PublishAsync(...)` yourself to "dispatch now" — dispatch is automatic, driven by `EventHook` after a successful save. Calling a publisher directly bypasses the before/after-save split and the queue-clearing guarantee.
- There is no public, mutable `AuditLogOptions` to inject or reconfigure post-startup, and no public `BuildAuditLog`/`EfCoreAuditHook` API — `services.Configure<AuditLogOptions>(...)` compiles against nothing public. The only knobs are the constructor-time parameters on `AddEfCoreAuditHook`/`AddEfCoreAuditLogs`.
- Do not assume `SnapshotContext` re-detects changes mid-phase — mutating the change tracker from inside a before-save hook is not picked up by other before-save hooks in the same phase.
- Do not treat `DisableHooks()` as instance-scoped isolation for concurrent saves of the same `DbContext` type on the same logical flow — it is type-scoped (Rule 9).
- Do not expect a retry, dead-letter, or at-least-once delivery knob for events or audit entries — none exists. Build a transactional outbox (write to an outbox table from a `BeforeSaveHookAsync` inside the same save transaction, drain it separately) if that guarantee is required.
- `SnapshotContext`/`SnapshotEntityEntry` live in `DKNet.EfCore.Extensions.Snapshots`, not in `DKNet.EfCore.Hooks` — don't guess a `DKNet.EfCore.Hooks.Snapshots` namespace.

## Related skills

- `dknet-packages` — package router; start here if you have not already picked `DKNet.EfCore.Hooks`/`Events`/`AuditLogs` for the job.
- `dknet-efcore-domain-model` — `Entity<TKey>`, `AuditedEntity<TKey>`, `IAuditedProperties`, and `UseAutoConfigModel` that this skill's entities build on.
- `dknet-efcore-specifications` — querying and repositories; unrelated to the save pipeline itself.
- `dknet-efcore-data-security` — `DataOwnerHook` (row-level ownership stamping, a sibling hook on the same pipeline) and column encryption (`[Encrypted]`, which this skill's audit redaction matches by attribute name only).
- `dknet-codegen` — `[GenerateDto]` and the `[RaisesEvent(typeof(X), ...)]` type-naming form's generator diagnostics (`DKRAISEVT0xx`); read this before hand-writing a `[RaisesEvent]` payload record.
- `dknet-slimbus-cqrs` — forwarding dispatched domain events onto SlimMessageBus from an `IEventPublisher` implementation.
- `dknet-testing` — SQLite-backed hook/event/audit test fixtures and patterns.

## References

- [references/DKNet.EfCore.Hooks.md](references/DKNet.EfCore.Hooks.md) — hook contracts, `AddDbContextWithHook`/`AddHook`/`DisableHooks`, `SnapshotContext`, runtime ordering, diagnostics. Docs: https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/DKNet.EfCore.Hooks.md · https://www.nuget.org/packages/DKNet.EfCore.Hooks
- [references/DKNet.EfCore.Events.md](references/DKNet.EfCore.Events.md) — `AddEventPublisher`, `AddEvent`/`AddEvent<TEvent>()`, `[RaisesEvent]`, `EventException`, publish semantics. Docs: https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/DKNet.EfCore.Events.md · https://www.nuget.org/packages/DKNet.EfCore.Events
- [references/DKNet.EfCore.AuditLogs.md](references/DKNet.EfCore.AuditLogs.md) — `AddEfCoreAuditLogs`, `AddCurrentUserProvider`, `AuditLogEntry`/`AuditFieldChange`, redaction attributes. Docs: https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/DKNet.EfCore.AuditLogs.md · https://www.nuget.org/packages/DKNet.EfCore.AuditLogs
- General: https://baoduy.github.io/DKNet/
