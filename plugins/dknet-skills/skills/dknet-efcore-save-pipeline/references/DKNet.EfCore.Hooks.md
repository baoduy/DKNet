# DKNet.EfCore.Hooks

| Field | Value |
|---|---|
| Area | EfCore |
| NuGet | `dotnet add package DKNet.EfCore.Hooks` |
| Docs | https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/DKNet.EfCore.Hooks.md |
| Source | https://github.com/baoduy/DKNet/tree/dev/src/EfCore/DKNet.EfCore.Hooks |
| Depends on (DKNet) | `DKNet.Fw.Extensions` (Core), `DKNet.EfCore.Extensions` (owns `SnapshotContext`/`SnapshotEntityEntry`) |
| Depends on (3rd party) | `Microsoft.EntityFrameworkCore` (version centrally pinned) |
| Target framework | net10.0 |

## Purpose

A pluggable before/after-`SaveChanges` interceptor pipeline for EF Core: one keyed `HookRunnerInterceptor` singleton per `DbContext` type runs any number of registered `IBeforeSaveHookAsync`/`IAfterSaveHookAsync` implementations against a shared `SnapshotContext` captured once per save. It lets independent cross-cutting concerns — audit stamping, domain-event dispatch, ownership stamping — each implement a small interface instead of overriding `SaveChangesAsync`, and share the same `DbContext` without knowing about each other.

It is **not** a SQL/connection interceptor (use EF Core's own `SaveChangesInterceptor`/`DbCommandInterceptor` for that), it has no built-in hook ordering/priority mechanism, and it ships no audit/event/ownership logic itself — those live in `DKNet.EfCore.AuditLogs`, `DKNet.EfCore.Events`, and `DKNet.EfCore.DataAuthorization`, each built as a hook on top of this package.

## Entry points

| Call | Signature | Notes |
|---|---|---|
| `AddDbContextWithHook<TDbContext>` (provider overload) | `IServiceCollection AddDbContextWithHook<TDbContext>(this IServiceCollection services, Action<IServiceProvider, DbContextOptionsBuilder> builder, ServiceLifetime contextLifetime = ServiceLifetime.Scoped, ServiceLifetime optionLifetime = ServiceLifetime.Scoped) where TDbContext : DbContext` | Registers `TDbContext` via `AddDbContext` and wires `UseHooks<TDbContext>(provider)` into the same options delegate; calls `AddHookRunner<TDbContext>()` internally. Preferred over hand-registering. |
| `AddDbContextWithHook<TDbContext>` (typed-builder overload) | `IServiceCollection AddDbContextWithHook<TDbContext>(this IServiceCollection services, Action<DbContextOptionsBuilder<TDbContext>> builder, ServiceLifetime contextLifetime = ServiceLifetime.Scoped, ServiceLifetime optionLifetime = ServiceLifetime.Scoped) where TDbContext : DbContext` | Same, but mirrors the standard `AddDbContext<TDbContext>(Action<DbContextOptionsBuilder<TDbContext>>)` overload. |
| `UseHooks<TDbContext>` | `DbContextOptionsBuilder UseHooks<TDbContext>(this DbContextOptionsBuilder options, IServiceProvider provider) where TDbContext : DbContext` | Call yourself only when `TDbContext` must be registered via a plain `AddDbContext` (e.g. a shared base class already calls it). Resolves the keyed `HookRunnerInterceptor` and adds it via `AddInterceptors`. Requires `AddHookRunner<TDbContext>()`/`AddHook<TDbContext,_>()` to have registered the keyed interceptor first, or `GetRequiredKeyedService` throws. |
| `AddHook<TDbContext, THook>` | `IServiceCollection AddHook<TDbContext, THook>(this IServiceCollection services) where TDbContext : DbContext where THook : class, IHookBaseAsync` | Registers `THook` `AddKeyedScoped`, keyed by `typeof(TDbContext).FullName`; idempotent per `(TDbContext, THook)` pair. Also calls `AddHookRunner<TDbContext>()`, so call order versus `AddDbContextWithHook` does not matter. A hook registered against a base `DbContext` runs for every derived `DbContext` too. |
| `DisableHooks` | `IHookDisablingContext DisableHooks(this DbContext context)` | Returns a disposable (sync + async) scope; dispose it to re-enable hooks. Reference-counted per `DbContext` CLR type via an `AsyncLocal`, so nested scopes are safe and unrelated concurrent flows are unaffected. |
| `AddHookRunner<TDbContext>` | `internal IServiceCollection AddHookRunner<TDbContext>(this IServiceCollection services) where TDbContext : DbContext` | **Internal — not callable from application code.** Registers `HookFactory` (`AddScoped`) and `HookRunnerInterceptor` (`AddKeyedSingleton`, keyed by `TDbContext`'s full name), idempotently. Both `AddDbContextWithHook` and `AddHook` call it for you. |

`SetupEfCoreHook` (the static class holding all of the above except `AddHookRunner`, which is `internal`) declares these as C# `extension(IServiceCollection services)` members in namespace `DKNet.EfCore.Hooks` — **not** the ambient `Microsoft.Extensions.DependencyInjection` namespace. `using DKNet.EfCore.Hooks;` is required to call `AddDbContextWithHook`/`AddHook`/`UseHooks`/`DisableHooks`.

## Public surface

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `IHookBaseAsync` | interface (marker) | Base marker every hook interface derives from; implement a more specific interface instead. | none |
| `IBeforeSaveHookAsync` | interface | Contract for a before-save hook. | `Task BeforeSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)` |
| `IAfterSaveHookAsync` | interface | Contract for an after-save hook. | `Task AfterSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)` |
| `IHookAsync` | interface | Combines both phases (`IBeforeSaveHookAsync`, `IAfterSaveHookAsync`). | none (inherits both members) |
| `HookAsync` | abstract class | Convenience base implementing `IHookAsync` with no-op virtual methods; override only what you need. | `virtual Task BeforeSaveAsync(SnapshotContext, CancellationToken = default)`; `virtual Task AfterSaveAsync(SnapshotContext, CancellationToken = default)` — both default to `Task.CompletedTask` |
| `IHookDisablingContext` | interface : `IDisposable`, `IAsyncDisposable` | The disposable handle returned by `DisableHooks()`. | none beyond the disposal contract |

`SnapshotContext`/`SnapshotEntityEntry` (owned by `DKNet.EfCore.Extensions`, namespace `DKNet.EfCore.Extensions.Snapshots`) are the types every hook body touches:

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `SnapshotContext` | sealed class : `IDisposable`, `IAsyncDisposable` | Captures Added/Modified/Deleted entries from `ChangeTracker` once per save phase. | `DbContext DbContext { get; }` (throws `ObjectDisposedException` if disposed); `IReadOnlyCollection<SnapshotEntityEntry> Entities { get; }` (throws `InvalidOperationException` if read before `Initialize()`); `void Initialize()` |
| `SnapshotEntityEntry` | sealed class | One captured entry plus its state at snapshot time. | `object Entity { get; }`; `EntityEntry Entry { get; }`; `EntityState OriginalState { get; }` |

There is no non-`internal` type beyond these — `HookRunnerInterceptor`, `HookFactory`, and `HookDisablingContext` are all `internal`, mentioned here only because "Runtime behaviour" and "Gotchas" below describe what they do.

## Options & defaults

There is no options object — behaviour is entirely a function of what you register.

| Option | Default | Effect | Where set |
|---|---|---|---|
| Which `DbContext` types run hooks | none, until registered | Determines whether `HookRunnerInterceptor` is attached at all | `AddDbContextWithHook<TDbContext>(...)` or `options.UseHooks<TDbContext>(provider)` |
| Which hooks run for a `DbContext` | none | Determines which `IHookBaseAsync` implementations resolve for that type (and its subclasses) | `AddHook<TDbContext, THook>()`, once per `(TDbContext, THook)` pair |
| Hook execution order | DI registration order; all before-hooks, then EF writes, then all after-hooks | No priority/ordering knob exists | Register hooks in the order you need |
| Hook lifetime | `Scoped` (`AddKeyedScoped`) | Not configurable | fixed in `AddHook<TDbContext,THook>` |
| `HookRunnerInterceptor` lifetime | `Singleton`, keyed per `DbContext` type full name | Not configurable | fixed in `AddHookRunner<TDbContext>` |
| `AddDbContextWithHook` — `contextLifetime`/`optionLifetime` | both `ServiceLifetime.Scoped` | Passed straight through to the underlying `AddDbContext` call | parameters on `AddDbContextWithHook<TDbContext>` |
| Hooks enabled | enabled | `using`/`await using (dbContext.DisableHooks())` suppresses all hooks for that `DbContext` CLR type for the scope's duration (reference-counted, `AsyncLocal`-scoped) | `dbContext.DisableHooks()` |

## Usage patterns

### Register a DbContext with hooks and add a hook

**When**: standard setup — hooks for a `DbContext` you register yourself.

```csharp
using DKNet.EfCore.Hooks;
using DKNet.EfCore.Extensions.Snapshots;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public sealed class AuditStampHook : IBeforeSaveHookAsync
{
    public Task BeforeSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in context.Entities)
        {
            if (entry.OriginalState is EntityState.Added or EntityState.Modified)
                entry.Entry.Property("UpdatedOn").CurrentValue = now;
        }

        return Task.CompletedTask;
    }
}

public static class Startup
{
    public static void ConfigureServices(IServiceCollection services, string connectionString)
    {
        services.AddDbContextWithHook<AppDbContext>((provider, options) =>
            options.UseSqlite(connectionString));

        services.AddHook<AppDbContext, AuditStampHook>();
    }
}

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options);
```

**Notes**: registration order between `AddDbContextWithHook` and `AddHook` does not matter — both idempotently ensure the keyed `HookRunnerInterceptor` exists. `entry.Entry.Property(name)` writes through the tracked `EntityEntry` because `SnapshotEntityEntry.Entity` may expose get-only properties.

### Register the DbContext yourself and wire hooks manually

**When**: a base class or existing setup already calls `AddDbContext` and you cannot switch it to `AddDbContextWithHook`.

```csharp
using DKNet.EfCore.Hooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public static class ManualStartup
{
    public static void ConfigureServices(IServiceCollection services, string connectionString)
    {
        services.AddHook<AppDbContext, AuditStampHook>(); // also registers the keyed interceptor
        services.AddDbContext<AppDbContext>((provider, options) =>
        {
            options.UseSqlite(connectionString);
            options.UseHooks<AppDbContext>(provider);
        });
    }
}
```

**Notes**: forgetting `options.UseHooks<AppDbContext>(provider)` here leaves `AddHook` registrations sitting in DI with nothing invoking them — no exception, hooks just silently never run.

### Both phases with IHookAsync / HookAsync

**When**: a hook needs before-save mutation and after-save side effects (e.g. stamping and then publishing).

```csharp
using DKNet.EfCore.Hooks;
using DKNet.EfCore.Extensions.Snapshots;

public sealed class DomainEventPublishingHook(IEventPublisher publisher) : HookAsync
{
    public override async Task AfterSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        foreach (var entry in context.Entities)
        {
            if (entry.Entity is IEventEntity eventEntity)
                await publisher.PublishAsync(eventEntity, cancellationToken);
        }
    }
}

public interface IEventPublisher
{
    Task PublishAsync(object domainEvent, CancellationToken cancellationToken);
}

public interface IEventEntity;
```

**Notes**: `HookAsync`'s default `BeforeSaveAsync` is a no-op, so overriding only `AfterSaveAsync` is enough. (This `IEventPublisher`/`IEventEntity` pair is a local illustration of the pattern — the real, DI-integrated version ships in `DKNet.EfCore.Events`; see `references/DKNet.EfCore.Events.md`.)

### Suppress hooks during seeding or migration

**When**: bulk inserts/fixups must bypass audit/event/ownership hooks entirely.

```csharp
using DKNet.EfCore.Hooks;

public static class SeedData
{
    public static async Task RunAsync(AppDbContext db)
    {
        await using (db.DisableHooks())
        {
            db.Set<Product>().Add(new Product { Name = "Seed data" });
            await db.SaveChangesAsync(); // no hooks run for this save
        }

        // hooks run normally again from here
    }
}

public sealed class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
```

**Notes**: `DisableHooks()` suppresses by `DbContext` CLR *type*, not instance — every concurrently-saving instance of that type is suppressed while any scope for that type is open on the current async flow. Nested scopes are safe (reference-counted); the outermost dispose re-enables hooks.

### Hooks inherited across a DbContext hierarchy

**When**: multiple `DbContext` types share a base and one hook registration should cover all of them.

```csharp
using Microsoft.EntityFrameworkCore;

public class BaseAppDbContext(DbContextOptions options) : DbContext(options);

public sealed class TenantDbContext(DbContextOptions<TenantDbContext> options) : BaseAppDbContext(options);
```

```csharp
using DKNet.EfCore.Hooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public static class HierarchyStartup
{
    public static void ConfigureServices(IServiceCollection services, string tenantConnectionString)
    {
        services.AddHook<BaseAppDbContext, AuditStampHook>();
        // AuditStampHook also runs for TenantDbContext saves, because HookFactory walks
        // TenantDbContext's base-type chain when resolving keyed hooks.
        services.AddDbContextWithHook<TenantDbContext>((_, o) => o.UseSqlite(tenantConnectionString));
    }
}
```

**Notes**: `AddHook<BaseAppDbContext, _>` alone does not attach an interceptor to `TenantDbContext` — `TenantDbContext` still needs its own `AddDbContextWithHook<TenantDbContext>` (or `UseHooks<TenantDbContext>`) registration so `HookRunnerInterceptor` is present to invoke the inherited hooks.

## Runtime behaviour

For one `SaveChangesAsync` call on a `DbContext` registered through `AddDbContextWithHook`/`UseHooks`:

1. EF Core raises `SavingChangesAsync` → `HookRunnerInterceptor.SavingChangesAsync` runs.
2. It builds (or reuses, keyed by `DbContext.ContextId.InstanceId`) a `HookContext`, resolving the hook lists from the `DbContext`'s **application** service provider and constructing a fresh `SnapshotContext`. If the application service provider is missing (see Diagnostics), this throws.
3. The before-save run checks `IsHookDisabled(dbContext)` first — if disabled, it returns without running anything or initializing the snapshot.
4. Otherwise it calls `context.Snapshot.Initialize()`, which runs `ChangeTracker.DetectChanges()` and captures every entry currently `Added`/`Modified`/`Deleted`. If `Entities.Count == 0`, both phases short-circuit for this save.
5. Each registered `IBeforeSaveHookAsync.BeforeSaveAsync(snapshot, ct)` runs in DI registration order, sharing the same `SnapshotContext` instance.
6. EF Core writes to the database.
7. On success, EF Core raises `SavedChangesAsync` → the same cached hook state is retrieved, and every `IAfterSaveHookAsync.AfterSaveAsync(snapshot, ct)` runs in DI order against the same shared `SnapshotContext` (the snapshot is re-initialized here too, but harmlessly — the just-saved entries have already left `Added`/`Modified`/`Deleted` state, so the rescan appends nothing new). The cached state is then removed and disposed.
8. On failure, EF Core raises `SaveChangesFailedAsync` → the cached state is removed and disposed without running any after-save hooks; the original exception still propagates.

A before-save hook that throws aborts the save (the exception propagates out of `SaveChangesAsync`; after-hooks never run). An after-save hook that throws propagates too, but the write has already committed by then — it does not roll back.

## Diagnostics & exceptions

| Exception | When | Fix |
|---|---|---|
| `InvalidOperationException` (thrown from the interceptor's application-service-provider lookup) | The `DbContext` being saved has no `ApplicationServiceProvider` on its `CoreOptionsExtension` — e.g. it was constructed with hand-built `DbContextOptions` that added the interceptor manually but were never routed through DI. | Register the `DbContext` via `AddDbContextWithHook<TDbContext>` (or `AddDbContext` + `options.UseHooks<TDbContext>(provider)`) so EF Core's DI integration sets `ApplicationServiceProvider`. |
| (no exception — silent no-op) | `AddHook<TDbContext, THook>()` was called but `TDbContext` was never registered with `UseHooks<TDbContext>`/`AddDbContextWithHook` — the keyed `HookRunnerInterceptor` is never attached to the pipeline. | Always pair every `AddHook<TDbContext, _>()` with `AddDbContextWithHook<TDbContext>(...)` (or an explicit `UseHooks<TDbContext>` call). |

No `DiagnosticDescriptor`s exist in this package — it ships no Roslyn analyzer.

## Gotchas

- **Plain `AddDbContext` + forgotten `UseHooks` = hooks silently never run.** No exception anywhere; `AddHook` registrations sit unused in DI. Only `AddDbContextWithHook` pairs the two automatically.
- **Missing DI wiring throws, missing `UseHooks` doesn't.** Once `HookRunnerInterceptor` *is* attached but the context lacks the application service provider (hand-built `DbContextOptions`), you get a hard `InvalidOperationException` — a different failure mode from the silent no-op above.
- **The snapshot is re-initialized for both phases, not just once.** The before-save and after-save runs of the same save share one cached `SnapshotContext`, but `Initialize()` is called unconditionally on each run — appending, not replacing. It's harmless after the write only because the saved entries have already left `Added`/`Modified`/`Deleted` state by then. Within one phase, a before-hook that adds entities or flips states is **not** re-snapshotted for hooks running later in that same phase — `Initialize()` runs once, up front.
- **After-save hook failures don't roll back the save.** By the time the after-save run happens, the write has already committed; an exception from an after-save hook still propagates out of `SaveChangesAsync` even though the data is already persisted.
- **`DisableHooks()` suppresses by `DbContext` CLR type, not instance**, scoped to the current async flow. Two different open instances of the *same* `DbContext` type saving concurrently on the same logical call flow are both suppressed while any scope for that type is active.
- **No hook ordering mechanism exists.** Hooks run in DI registration order per phase; there is no `[Order]` attribute, priority number, or dependency graph.
- **A save with zero `Added`/`Modified`/`Deleted` entries skips both phases entirely**, including a save right after an explicit `ChangeTracker.Clear()`.
- **`AddHook<TDbContext, THook>` de-dupes only the exact `(TDbContext, THook)` pair.** Calling it twice for the same pair is a no-op; registering the same `THook` against two different `TDbContext` keys creates two independent keyed registrations, each with its own DI-managed instance.

## Anti-patterns & hallucination traps

- There is **no `IHook` interface** and no non-`Async`-suffixed hook contract — only `IHookBaseAsync`, `IBeforeSaveHookAsync`, `IAfterSaveHookAsync`, `IHookAsync`.
- Do **not** call `AddHookRunner<TDbContext>()` from application code — it is `internal`; it will not compile outside the package's own assembly.
- Do not expect an `[Order]`, `[Priority]`, or similar attribute to control hook sequencing — none exists.
- Do not assume `SnapshotContext` re-detects changes mid-phase — mutating the change tracker from inside a before-save hook is not picked up by other before-save hooks in the same save.
- Do not treat `DisableHooks()` as instance-scoped isolation for concurrent saves of the same `DbContext` type on the same logical flow — it is type-scoped.
- Do not hand-roll audit stamping, domain-event dispatch, or ownership stamping as a fresh hook when `DKNet.EfCore.AuditLogs`, `DKNet.EfCore.Events`, or `DKNet.EfCore.DataAuthorization` already implement exactly that on top of this pipeline.
- Do not call `SaveChangesAsync()` from inside a hook body — hooks run *during* `SavingChangesAsync`/`SavedChangesAsync` of an already-in-flight save; recursively saving the same context from a hook is unsupported.
- `SnapshotContext`/`SnapshotEntityEntry` live in `DKNet.EfCore.Extensions.Snapshots`, not `DKNet.EfCore.Hooks` — don't guess a `DKNet.EfCore.Hooks.Snapshots` namespace.

## Composes with

| Package | Rule |
|---|---|
| `DKNet.EfCore.Extensions` | Owns `SnapshotContext`/`SnapshotEntityEntry` that every hook body reads, plus `UseAutoConfigModel` used to build the `DbContext` model. Always a dependency, never optional. |
| `DKNet.EfCore.Events` | `EventHook : HookAsync` registers via `AddHook<TDbContext, THook>()` to dispatch `[RaisesEvent]` domain events after save. Reach for it instead of writing your own after-save publishing hook. |
| `DKNet.EfCore.AuditLogs` | `EfCoreAuditHook : HookAsync` builds audit entries in `BeforeSaveAsync`, publishes in `AfterSaveAsync`. Reach for it instead of writing your own change-diffing hook. |
| `DKNet.EfCore.DataAuthorization` | `DataOwnerHook : IBeforeSaveHookAsync` stamps/guards row ownership before save. Reach for it for row-level multi-tenant isolation instead of a custom ownership hook. |
| `DKNet.EfCore.Abstractions` | Entity base classes/attributes (e.g. `IAuditedProperties`, `IEventEntity`) that hooks typically pattern-match on inside `SnapshotContext.Entities`. |

## Testing notes

- Tests use xUnit + Shouldly, mostly against `Microsoft.Data.Sqlite` in-memory (`DataSource=:memory:`) via a shared open connection, or EF Core's `UseInMemoryDatabase` for tests that don't need real SQL semantics — this package's own coverage does not follow the repo's "never use InMemory for integration tests" rule, since no SQL-specific behaviour is under test here.
- The canonical fixture pattern: build a `ServiceProvider` with `AddDbContextWithHook<TDbContext>(...).AddHook<TDbContext, THook>()`, open a Sqlite in-memory connection, call `EnsureCreatedAsync()`, then drive real `SaveChangesAsync()` calls and assert on static counters/flags on a test-double hook.
- A recurring pattern worth reusing: comparing hook-registered vs. plain `AddDbContext` vs. no extensions at all isolates whether a given EF Core warning or behaviour is caused by this package specifically.
