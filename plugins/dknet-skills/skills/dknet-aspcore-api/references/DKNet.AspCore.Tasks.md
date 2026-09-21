# DKNet.AspCore.Tasks

| Field | Value |
|---|---|
| Area | AspNetCore |
| Install | `dotnet add package DKNet.AspCore.Tasks` |
| NuGet | https://www.nuget.org/packages/DKNet.AspCore.Tasks |
| Docs | https://github.com/baoduy/DKNet/blob/dev/docs/AspNetCore/DKNet.AspCore.Tasks.md |
| Source | https://github.com/baoduy/DKNet/tree/dev/src/AspNet/DKNet.AspCore.Tasks |
| Depends on (DKNet) | none |
| Depends on (3rd party) | `Microsoft.Extensions.Hosting.Abstractions` |
| Target framework | net10.0 |

## Purpose

A one-shot, hosted-service wrapper for application start-up work. You implement `IBackgroundTask`,
register the type with `AddBackgroundJob<TJob>()` (or scan assemblies with
`AddBackgroundJobFrom(Assembly[])`), and a single internal `BackgroundService`
(`BackgroundJobHost`) resolves every registered task from one shared DI scope and runs them all
concurrently via `Task.WhenAll` when the generic host starts. Each task's exception is caught,
logged at `Error`, and swallowed — one failing task cannot stop the others or crash the host.

**Not** a scheduler: there is no cron, no recurring timer, no delay-until, and no per-task
scope/ordering/retry/timeout knob. If you need recurring or delayed work, this is the wrong
package — build your own `BackgroundService`/`IHostedService` or reach for a scheduling library.

## Entry points

| Call | Exact signature | Called on | Notes (lifetime, ordering, prerequisites) |
|---|---|---|---|
| `AddBackgroundJob<TJob>()` | `IServiceCollection AddBackgroundJob<TJob>() where TJob : class, IBackgroundTask` (extension member on `IServiceCollection`) | `IServiceCollection` | Registers `TJob` via `services.AddScoped<IBackgroundTask, TJob>()`; a second call for the *same* implementation type is a no-op (checked by `ServiceType == IBackgroundTask && ImplementationType == TJob`). Also registers `BackgroundJobHost` as a hosted service the first time only. Call before `builder.Build()`. |
| `AddBackgroundJobFrom(Assembly[])` | `IServiceCollection AddBackgroundJobFrom(Assembly[] assemblies)` (extension member) | `IServiceCollection` | Scans `assemblies` for every non-abstract class assignable to `IBackgroundTask` via reflection (`Assembly.GetTypes()`), registers each the same de-duplicated, scoped way. Takes `Assembly[]` specifically — not `params`, not `IEnumerable<Assembly>` (a collection expression `[...]` still satisfies it). |
| `IBackgroundTask.RunAsync` | `Task RunAsync(CancellationToken cancellationToken = default)` | interface you implement | The whole body of one start-up task. Implementing classes are ordinary constructor-injected, scoped-lifetime classes resolved from `BackgroundJobHost`'s single shared `AsyncServiceScope`. |

The internal `AddHost()` helper is not itself a public entry point but is the mechanism behind both
calls above: it checks for an existing hosted-service descriptor whose `ImplementationType ==
typeof(BackgroundJobHost)` before calling `services.AddHostedService<BackgroundJobHost>()`, so
mixing `AddBackgroundJob` and `AddBackgroundJobFrom` calls on the same `IServiceCollection` never
registers the host twice. The guard is per-`IServiceCollection`, not process-wide — two separate
containers each get their own `BackgroundJobHost` (asserted by the package's own test
`AddBackgroundJobRegistersHostPerContainer`).

## Public surface

### `DKNet.AspCore.Tasks`

| Type | Kind | Purpose | Key members with exact signatures |
|---|---|---|---|
| `IBackgroundTask` | interface | Contract for one start-up job. | `Task RunAsync(CancellationToken cancellationToken = default)` |

### `Microsoft.Extensions.DependencyInjection` (ambient namespace — ships DI registration methods next to the framework interface they extend)

| Type | Kind | Purpose | Key members with exact signatures |
|---|---|---|---|
| `TaskSetups` | static class, C# 14 `extension(IServiceCollection services)` block | DI registration surface. | `public IServiceCollection AddBackgroundJob<TJob>() where TJob : class, IBackgroundTask`; `public IServiceCollection AddBackgroundJobFrom(Assembly[] assemblies)`; `private IServiceCollection AddHost()` |

### `DKNet.AspCore.Tasks.Internals` (internal — not consumer-facing, documented because it explains observable behaviour)

| Type | Kind | Purpose | Key members with exact signatures |
|---|---|---|---|
| `BackgroundJobHost` | `internal sealed class BackgroundJobHost(ILogger<BackgroundJobHost> logger, IServiceProvider provider) : BackgroundService` | The single hosted service that runs every registered task once per host start-up. | `protected override async Task ExecuteAsync(CancellationToken stoppingToken)`; `private async Task ExecuteJobAsync(IBackgroundTask task, CancellationToken cancellationToken = default)` |

No other public types exist in this package — exactly two public types ship: `IBackgroundTask` and
`TaskSetups` (with its two extension members).

## Options & defaults

**No options type exists** — no `Action<TOptions>` overload, no `appsettings.json` binding, nothing
bound from configuration. The tunable surface is entirely the interface you implement plus the two
registration calls' parameters:

| Option | Type | Default | Effect | Where set |
|---|---|---|---|---|
| `IBackgroundTask.RunAsync` body | method you implement | none — required | The whole unit of start-up work. Returning completes the task; throwing is caught, logged (`Error`, `"{jobType} job failed"`), and swallowed. | Your `IBackgroundTask` implementation |
| `TJob` | type parameter, `where TJob : class, IBackgroundTask` | none — required | The single task type registered as `AddScoped<IBackgroundTask, TJob>()`. | `AddBackgroundJob<TJob>()` call site |
| `assemblies` | `Assembly[]` | none — required | Every non-abstract, `IBackgroundTask`-assignable class in these assemblies is registered the same de-duplicated, scoped way. | `AddBackgroundJobFrom(Assembly[])` call site |
| `cancellationToken` | `CancellationToken` | `default` in the signature; `BackgroundJobHost` always passes its own | The token `BackgroundService.ExecuteAsync` receives on host shutdown; no per-task timeout is applied on top. | Passed by `BackgroundJobHost.ExecuteJobAsync` |

There is no knob for ordering, concurrency limits, retries, or per-task DI scoping — build any of
that inside `RunAsync`, or open a private scope via `IServiceScopeFactory` as shown below.

## Usage patterns

### Register one task type

**When**: you have a single, known start-up job (e.g. seed reference data).

```csharp
using DKNet.AspCore.Tasks;
using Microsoft.Extensions.Logging;

public interface IMySeeder
{
    Task SeedAsync(CancellationToken cancellationToken);
}

public sealed class SeedReferenceDataTask(IMySeeder seeder, ILogger<SeedReferenceDataTask> logger)
    : IBackgroundTask
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Seeding reference data");
        await seeder.SeedAsync(cancellationToken);
    }
}
```

```csharp
using DKNet.AspCore.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBackgroundJob<SeedReferenceDataTask>();

var app = builder.Build();
await app.RunAsync();
```

**Notes**: `SeedReferenceDataTask` is resolved as `AddScoped<IBackgroundTask, SeedReferenceDataTask>()`
from `BackgroundJobHost`'s one shared scope — any other constructor dependency it needs must already
be registered in the container.

### Register every task in an assembly (module-style registration)

**When**: a module or plugin project ships its own start-up work and the host project should not have
to enumerate every task type by hand.

```csharp
using DKNet.AspCore.Tasks;

public sealed class ImportOrdersTask : IBackgroundTask
{
    public Task RunAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
```

```csharp
using System.Reflection;
using DKNet.AspCore.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBackgroundJobFrom([Assembly.GetExecutingAssembly()]);

// or scan more than one assembly in a single call:
// builder.Services.AddBackgroundJobFrom([Assembly.GetExecutingAssembly(), typeof(SharedTasks.Marker).Assembly]);

var app = builder.Build();
await app.RunAsync();
```

**Notes**: scans for every non-abstract class implementing `IBackgroundTask` via reflection
(`Assembly.GetTypes()`); each match is registered the same de-duplicated, scoped way as
`AddBackgroundJob<TJob>()`. Mixing this with direct `AddBackgroundJob<TJob>()` calls for types the
scan would also find is safe — the implementation-type de-dup guard prevents a double registration.

### Idempotent registration from two extension methods

**When**: two separate module-registration methods both want to guarantee the same task is
registered, without knowing whether the other already did it.

```csharp
using DKNet.AspCore.Tasks;
using Microsoft.Extensions.DependencyInjection;

public static class ModuleAExtensions
{
    public static IServiceCollection AddModuleA(this IServiceCollection services) =>
        services.AddBackgroundJob<SharedStartupTask>();
}

public static class ModuleBExtensions
{
    public static IServiceCollection AddModuleB(this IServiceCollection services) =>
        services.AddBackgroundJob<SharedStartupTask>(); // no-op the second time
}

public sealed class SharedStartupTask : IBackgroundTask
{
    public Task RunAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
```

**Notes**: the de-dup check is on `ImplementationType == typeof(SharedStartupTask)`, not on the
calling site, so it is safe to call `AddBackgroundJob<SharedStartupTask>()` from as many extension
methods as you like — only one `IBackgroundTask` descriptor and one `BackgroundJobHost` hosted
service end up registered.

### Open a private scope for a scoped, non-thread-safe dependency

**When**: a task needs an EF Core `DbContext` (or a repository built on one), and another registered
task might touch the same scoped dependency while running concurrently in the shared scope.

```csharp
using DKNet.AspCore.Tasks;
using DKNet.EfCore.Specifications.Definitions;
using DKNet.EfCore.Specifications.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public sealed class Product
{
    public Guid Id { get; init; }
}

public sealed class WarmProductCacheTask(IServiceScopeFactory scopeFactory) : IBackgroundTask
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        // Private scope: this task's IRepositorySpec/DbContext is never shared with
        // another IBackgroundTask running concurrently in BackgroundJobHost's single
        // shared scope.
        await using var scope = scopeFactory.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IRepositorySpec>();

        var products = await repo.Query(new AllProductsSpecification())
            .ToListAsync(cancellationToken);
        // ...warm a cache with `products`
    }
}

internal sealed class AllProductsSpecification : Specification<Product>
{
    public AllProductsSpecification() => AddOrderBy(p => p.Id);
}
```

**Notes**: two tasks that both directly depend on a scoped `DbContext`/`IRepositorySpec` without
opening their own scope will get the *same instance* while `BackgroundJobHost` runs them
concurrently via `Task.WhenAll` — unsafe for a non-thread-safe service. Prefer `IServiceScopeFactory`
for any scoped, non-thread-safe dependency. `IRepositorySpec.Query` already applies `.AsExpandable()`
for you, so nothing extra is needed to query a specification through the repository.

### Design for idempotent re-runs

**When**: every task must tolerate re-execution, because it runs once per host start-up — every
deployment, restart, or scale-out replica start runs it again.

```csharp
using DKNet.AspCore.Tasks;

public sealed class UpsertDefaultTenantTask(ITenantStore store) : IBackgroundTask
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        // Existence check / upsert, not a blind insert, so a re-run on the
        // next deployment or replica start is safe.
        if (!await store.ExistsAsync("default", cancellationToken))
            await store.CreateAsync("default", cancellationToken);
    }
}

public interface ITenantStore
{
    Task<bool> ExistsAsync(string id, CancellationToken cancellationToken);
    Task CreateAsync(string id, CancellationToken cancellationToken);
}
```

**Notes**: a thrown exception does not roll back whatever `RunAsync` already did before throwing —
there is no automatic retry or compensation, so a partial run must leave the system in a safe state.

## Runtime behaviour

When the generic host starts (`app.RunAsync()` / `host.StartAsync()`):

1. `BackgroundJobHost.ExecuteAsync` runs as the hosted service's execution loop and logs
   `"Background job host started"` at `Information`.
2. It opens exactly **one** `AsyncServiceScope` via `provider.CreateAsyncScope()` for the whole
   batch, then resolves every registered `IBackgroundTask` from that single scope with
   `scope.ServiceProvider.GetServices<IBackgroundTask>()` — not one scope per task.
3. It runs every resolved task's `RunAsync` **concurrently** through
   `Task.WhenAll(jobs.Select(j => ExecuteJobAsync(j, stoppingToken)))`. There is no ordering
   guarantee and no way for one task to wait on another built into the package.
4. `ExecuteJobAsync` wraps each `RunAsync` call in a `try`/`catch (Exception ex)`. On failure it logs
   `{jobType} job failed` at `Error` (only when `logger.IsEnabled(LogLevel.Error)`) with the
   exception attached, and swallows it — it never rethrows into the `Task.WhenAll`, so one failing
   task cannot fault the others or the host.
5. Once every task's `RunAsync` has returned (successfully or by being caught) `ExecuteAsync` logs
   `"Background job host finished"` and disposes the shared `AsyncServiceScope` (an `await using`
   around the whole batch).

Because `BackgroundJobHost` is a `BackgroundService`, the generic host reaches "started" as soon as
`ExecuteAsync` hits its first `await` — the first HTTP request can be served while tasks are still
running. Registered tasks are never a precondition for readiness by themselves.

## Diagnostics & exceptions

This package defines no analyzer/`DiagnosticDescriptor` and throws no custom exception types of its
own. The one exception-shaped behaviour is logging, not throwing:

| ID or exception type | Severity | When | Fix |
|---|---|---|---|
| (any `Exception` thrown by `IBackgroundTask.RunAsync`) | Logged at `Error` (`"{jobType} job failed"`), never rethrown | A registered task's `RunAsync` throws | Not a bug to "fix" in the package — design `RunAsync` to be safe to have partially run (upsert/existence-check pattern), since the exception is swallowed and the other tasks still complete. |

## Gotchas

- **Trap**: no options type, no `appsettings.json` binding, no timeout, no concurrency limit exists
  to reach for. **Consequence**: an agent that guesses `AddBackgroundJob<TJob>(opts => ...)` or a
  config-bound overload will hit a compile error. **Workaround**: build throttling/timeouts inside
  `RunAsync` yourself. **Evidence**: `TaskSetups`'s two public members take no configuration delegate.
- **Trap**: `BackgroundJobHost` resolves every `IBackgroundTask` from **one shared** `AsyncServiceScope`
  and runs them **concurrently**. **Consequence**: two tasks that both depend directly on a scoped,
  non-thread-safe service (most commonly an EF Core `DbContext`) get the *same instance* while
  running in parallel — silent data corruption or thrown concurrency exceptions. **Workaround**: open
  a private scope via `IServiceScopeFactory.CreateAsyncScope()` inside the task. **Evidence**:
  `BackgroundJobHost.ExecuteAsync` calls `provider.CreateAsyncScope()` once and runs every task
  through `Task.WhenAll` from that one scope.
- **Trap**: tasks run once per **host start-up**, not once ever — every restart, redeploy, or
  scale-out replica start re-runs every registered task. **Consequence**: a blind `INSERT` seed task
  duplicates rows on the second start. **Workaround**: make `RunAsync` idempotent (existence
  check/upsert). **Evidence**: `BackgroundJobHost.ExecuteAsync` runs unconditionally on every host
  start; there is no run-once-ever ledger in the package.
- **Trap**: a failing task's exception is caught and swallowed by `ExecuteJobAsync` —
  **no rethrow, no automatic retry, no rollback** of whatever the task already did.
  **Consequence**: a task that partially mutates state before throwing leaves that mutation in
  place forever unless the next start-up's idempotent re-run fixes it. **Workaround**: structure
  risky work so a partial failure is safe, or lean on the idempotent-rerun pattern.
  **Evidence**: `ExecuteJobAsync` catches `Exception` and never rethrows.
- **Trap**: `BackgroundJobHost` is a `BackgroundService`, so the host reaches "application started"
  as soon as `ExecuteAsync` hits its first `await` — **tasks do not block the host from serving
  requests**. **Consequence**: an agent that expects a registered task to act as a start-up
  precondition (e.g. "the seed must finish before the first request") is wrong; a request can be
  served mid-seed. **Workaround**: if a request truly must wait, gate it explicitly (e.g. block on
  a shared `TaskCompletionSource`/readiness flag your own task sets) rather than relying on
  registration order. **Evidence**: `BackgroundJobHost : BackgroundService` plus generic-host
  semantics — the package's own test for this, `HostExecutesRegisteredJobOnStart`, starts the host
  and then awaits the task's own `TaskCompletionSource` rather than assuming it already ran.
- **Trap**: `AddBackgroundJobFrom` takes exactly `Assembly[]` — **not** `params Assembly[]` and
  **not** `IEnumerable<Assembly>`. **Consequence**: `AddBackgroundJobFrom(asm1, asm2)` (params-style
  call) or passing a `List<Assembly>` directly fails to compile. **Workaround**: pass a collection
  expression or array literal: `AddBackgroundJobFrom([asm1, asm2])`. **Evidence**:
  `AddBackgroundJobFrom`'s parameter is declared exactly as `Assembly[] assemblies`.
- **Trap**: the de-dup guards on both registration methods and on the internal `AddHost()` helper
  compare against descriptors already present **on that specific `IServiceCollection`** — the guard
  is not process-wide/static. **Consequence**: an agent might assume registering `TJob` once
  anywhere in the process is enough; it is not — each `IServiceCollection`/host you build needs its
  own registration call. **Workaround**: none needed in normal single-host apps; relevant mainly in
  tests that build multiple `ServiceCollection`s. **Evidence**: both registration methods and
  `AddHost()` all check `services.Any(...)` on the instance passed in; the test
  `AddBackgroundJobRegistersHostPerContainer` asserts two separate collections each get their own
  `BackgroundJobHost` descriptor.
- **Trap**: `TaskSetups` declares its members inside a C# 14 `extension(IServiceCollection services)`
  block in the ambient `Microsoft.Extensions.DependencyInjection` namespace, not as classic
  `this IServiceCollection services` extension methods in `DKNet.AspCore.Tasks`. **Consequence**:
  generated code that expects to find `TaskSetups.AddBackgroundJob(services)` as a static call, or
  that expects the type in the `DKNet.AspCore.Tasks` namespace, will not find it that way — but
  `services.AddBackgroundJob<TJob>()` still resolves normally via extension-member lookup because
  the namespace is already ambient to any project using `Microsoft.Extensions.DependencyInjection`.
  **Evidence**: `TaskSetups` is declared under `namespace Microsoft.Extensions.DependencyInjection;`
  using a C# 14 `extension(IServiceCollection services)` block.

## Anti-patterns & hallucination traps

- `AddBackgroundJob<TJob>(options => ...)` / any config-delegate or `IConfiguration`-binding
  overload — **does not exist**. There is no options type in this package at all.
- `services.AddBackgroundJobs(...)` (plural) — the real names are `AddBackgroundJob<TJob>()`
  (singular, generic) and `AddBackgroundJobFrom(Assembly[])`.
- `AddBackgroundJobFrom(asm1, asm2)` (treating the parameter as `params`) — it is a single
  `Assembly[]` parameter; pass `[asm1, asm2]`.
- Expecting a `BackgroundJobHost`/`TaskSetups`-adjacent public type such as
  `IBackgroundTaskScheduler`, `BackgroundJobOptions`, or `CronBackgroundTask` — none exist; this
  package has no scheduling concept, only "run once at start-up."
- Assuming each `IBackgroundTask` gets its own DI scope — it does not; all registered tasks share
  **one** scope for the whole batch, opened once by `BackgroundJobHost`.
- Assuming tasks run in registration order, or that one task can depend on another having finished
  first — they run concurrently via `Task.WhenAll` with no ordering guarantee.
- Catching exceptions yourself inside `RunAsync` expecting the host to retry on failure — there is
  no retry; the host only logs and moves on, it never re-invokes a failed task.
- Awaiting `IHostedService`/`BackgroundJobHost` directly, or resolving `BackgroundJobHost` from the
  container — it is `internal`, resolved only via `IHostedService`/`AddHostedService`, and not part
  of the public surface a consumer touches.
- Treating `AddBackgroundJob<TJob>()` as blocking start-up until `TJob.RunAsync` completes — it does
  not; the host reaches "started" once `ExecuteAsync` hits its first `await`.

## Composes with

| Package | Rule |
|---|---|
| `DKNet.EfCore.Specifications` | Reach for it inside a data-touching `RunAsync` (via `IRepositorySpec`/`AddSpecRepo<TDbContext>()`) — open a private `IServiceScopeFactory` scope first per the shared-scope gotcha above. |
| `DKNet.AspCore.Idempotency` | Reach for it instead of `DKNet.AspCore.Tasks` when the work is triggered per HTTP request and must be safe to retry, rather than once at host start-up. |
| `DKNet.AspCore.Extensions` | Sibling `AspNet/` package for minimal-API glue (endpoint discovery, paged responses, Result/ProblemDetails conversion) — unrelated surface, no direct dependency either way. |

## Testing notes

- **Test through a real host, not mocks.** Build a `HostBuilder` (or `WebApplicationFactory`),
  register the `IBackgroundTask` under test with `AddBackgroundJob`/`AddBackgroundJobFrom`,
  `StartAsync()` it, and assert on an observable side effect — a shared counter or a
  `TaskCompletionSource<bool>` the task completes — rather than mocking `IBackgroundTask` itself.
- **Wait on the task's own signal, not a fixed delay past `StartAsync`.** `BackgroundJobHost` reaches
  "started" before every task finishes; a test that assumes registration order or a short
  `Task.Delay` is enough is racing the host. Await the task's own completion signal instead.
- **Assert failure by inspecting captured logs**, not by expecting an exception to propagate out of
  `StartAsync` — a failing task's exception is logged (`{jobType} job failed`, `Error`) and
  swallowed, never rethrown.
