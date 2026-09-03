# DKNet.AspCore.Tasks

A hosted-service wrapper for start-up work: implement `IBackgroundTask`, register it, and one
`BackgroundService` discovers and runs every registered task once, when the host starts.

## ✨ Why use it?

- **No `IHostedService` per job.** Seeding reference data, warming a cache, priming a queue, or
  running a data-migration check each becomes one small class instead of its own
  `BackgroundService` with its own scope wiring and its own try/catch-and-log.
- **A failing task cannot take the host down.** Every `RunAsync` is wrapped: an exception is logged
  and swallowed, and the other registered tasks still run to completion.
- **Registration is idempotent.** Registering the same task type twice — from two extension methods
  that each want to guarantee it — is a no-op the second time, and the hosted service is added only
  once however many tasks you register.
- **Assembly scanning when you want it.** `AddBackgroundJobFrom([...])` picks up every
  `IBackgroundTask` in the assemblies you name, so a module can ship start-up work without the host
  project enumerating it.

If you need a job that runs later, or repeatedly on a schedule (cron-style), this package is the
wrong tool — every registered task runs exactly once per host start-up, not on a recurring timer.

## 🚀 Quick Start

```bash
dotnet add package DKNet.AspCore.Tasks
```

```csharp
using DKNet.AspCore.Tasks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBackgroundJob<SeedReferenceDataTask>();

var app = builder.Build();
await app.RunAsync();
```

```csharp
public sealed class SeedReferenceDataTask(IMySeeder seeder, ILogger<SeedReferenceDataTask> logger) : IBackgroundTask
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Seeding reference data");
        await seeder.SeedAsync(cancellationToken);
    }
}
```

`AddBackgroundJob<TJob>()` registers `TJob` as a scoped `IBackgroundTask` and — the first time it
is called on a given `IServiceCollection` — also registers the hosted service that runs it. Nothing
else is required; the task runs automatically once the generic host starts.

## 🧩 Features

### `AddBackgroundJob<TJob>()` — register a single task type

```csharp
public IServiceCollection AddBackgroundJob<TJob>() where TJob : class, IBackgroundTask
```

Registers `TJob` with `services.AddScoped<IBackgroundTask, TJob>()`. Calling it more than once for
the *same* `TJob` (e.g. from two separate extension methods that both want to guarantee the task is
registered) is a no-op the second time — it checks the collection for an existing `IBackgroundTask`
descriptor whose implementation type is `TJob` before adding another one:

```csharp
builder.Services.AddBackgroundJob<FirstTask>();
builder.Services.AddBackgroundJob<SecondTask>();
builder.Services.AddBackgroundJob<FirstTask>(); // no-op — already registered
```

### `AddBackgroundJobFrom(Assembly[])` — assembly-scanning registration

```csharp
public IServiceCollection AddBackgroundJobFrom(Assembly[] assemblies)
```

Scans the given assemblies for every non-abstract class that implements `IBackgroundTask` and
registers each one the same way `AddBackgroundJob<TJob>()` does (scoped, de-duplicated by
implementation type):

```csharp
builder.Services.AddBackgroundJobFrom([typeof(Program).Assembly]);

// scan more than one assembly in a single call
builder.Services.AddBackgroundJobFrom([typeof(Program).Assembly, typeof(SharedTasks.Marker).Assembly]);
```

There is only this one overload — it takes `Assembly[]`, not `IEnumerable<Assembly>` and not a
`params` array — but a collection expression (`[...]`) or an array literal (`new[] { ... }`) both
satisfy it directly.

### `BackgroundJobHost` — how execution is sequenced and scoped

![Workflow diagram: registering a task also adds BackgroundJobHost once; at host start-up the host opens one shared async scope, resolves every IBackgroundTask, runs them all concurrently through Task.WhenAll, logs and swallows any exception, and finally disposes the scope.](../diagrams/aspcore-tasks-startup-run.svg)

Both registration methods route through a private `AddHost()` helper that calls
`services.AddHostedService<BackgroundJobHost>()` — but only the first time; it checks whether a
`BackgroundJobHost` hosted-service descriptor already exists on that `IServiceCollection` first, so
mixing `AddBackgroundJob` and `AddBackgroundJobFrom` calls never registers the host twice. The
guard is per `IServiceCollection`, not process-wide — building two separate containers in the same
process (e.g. in tests) gives each its own `BackgroundJobHost`.

`BackgroundJobHost` is an internal `BackgroundService`. When the generic host starts:

1. It logs `"Background job host started"`.
2. It opens **one** `IServiceProvider.CreateAsyncScope()` for the whole batch and resolves every
   registered `IBackgroundTask` from that single scope — not one scope per task.
3. It runs all of them **concurrently** via `Task.WhenAll`, so there is no ordering guarantee
   between tasks and no built-in way to make one task wait for another.
4. Once every task's `RunAsync` has returned (or failed) it logs `"Background job host finished"`
   and disposes the shared scope.

### Error isolation

Each task's `RunAsync` is invoked through an internal wrapper that catches any exception, logs it
(`{TaskFullTypeName} job failed`, at `Error` level, with the exception attached), and swallows it —
it never rethrows into the `Task.WhenAll`. A failing task therefore cannot stop the others from
completing, and cannot crash the host:

```csharp
public sealed class ImportOrdersTask(IOrderImporter importer) : IBackgroundTask
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        // If ImportAsync throws, BackgroundJobHost logs the failure and
        // every other registered IBackgroundTask still runs to completion.
        await importer.ImportAsync(cancellationToken);
    }
}
```

Whatever side effects the task performed *before* the throw are not rolled back — there is no
automatic retry or compensation, so keep each `RunAsync` safe to have partially run (see
[Gotchas & limits](#-gotchas--limits)).

## ⚙️ Configuration reference

**There is no options type in this package** — no `Action<TOptions>` overload, no `appsettings.json`
section, and nothing bound from configuration. That is provable from the public surface: the
assembly exposes exactly two public types, `IBackgroundTask` and the `TaskSetups` registration
class, and neither takes a settings object. The customisation surface is therefore the interface
you implement and the two type/parameter slots the registration methods expose:

| Knob | Kind | Default | Effect |
|---|---|---|---|
| `IBackgroundTask.RunAsync(CancellationToken)` | interface method you implement | none — required | The whole body of a start-up task. Returning completes it; throwing is caught, logged at `Error` as `{TaskFullTypeName} job failed`, and swallowed. |
| `TJob` on `AddBackgroundJob<TJob>()` | type parameter, `where TJob : class, IBackgroundTask` | none — required | The single task type to register. Registered as `AddScoped<IBackgroundTask, TJob>()`, skipped when a descriptor for the same implementation type already exists. |
| `assemblies` on `AddBackgroundJobFrom(Assembly[])` | `Assembly[]` | none — required, and not `params` | Every non-abstract class in these assemblies that implements `IBackgroundTask` is registered the same way `AddBackgroundJob<TJob>()` registers one. |
| `cancellationToken` on `RunAsync` | `CancellationToken` | `default` in the signature; the host always passes its own | The token `BackgroundService.ExecuteAsync` receives on host shutdown. Nothing in the package shortens it — there is no per-task timeout. |

There is deliberately no knob for ordering, concurrency limits, retries, or per-task scoping: the
host resolves every registered task from one shared scope and runs them all through a single
`Task.WhenAll`. If you need any of those, build them inside your `RunAsync`, or open a private
scope as shown under [Where it fits](#-where-it-fits).

## 🧱 Where it fits

`IBackgroundTask` implementations are ordinary constructor-injected classes, so they can resolve
anything else registered in the container — `DKNet.EfCore.Specifications` repositories
(`IRepositorySpec`, registered via `AddSpecRepo<TDbContext>()`), `DKNet.Svc.*` services, SlimBus
senders, and so on — the same way a controller or handler would. Because all tasks share one DI
scope for the whole batch (see above), a task that needs a scoped, non-thread-safe dependency such
as an EF Core `DbContext` (or an `IRepositorySpec` built on top of one) should open its own private
scope rather than depend on it directly:

```csharp
using DKNet.EfCore.Specifications.Definitions;
using DKNet.EfCore.Specifications.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public sealed class WarmProductCacheTask(IServiceScopeFactory scopeFactory) : IBackgroundTask
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        // Private scope: this task's IRepositorySpec/DbContext is never
        // shared with another IBackgroundTask running concurrently in
        // BackgroundJobHost's single shared scope.
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

This keeps `DKNet.AspCore.Tasks` in the application layer: it orchestrates cross-cutting start-up
work while delegating the actual domain/data logic to services and repositories from the layers
below.

## ⚠️ Gotchas & limits

- **There is nothing to configure.** The package has no options type, no `appsettings.json`
  binding, and no tunable timeouts or concurrency limits — the only inputs are the ones listed
  under [Configuration reference](#-configuration-reference). If you need throttling, ordering, or
  scheduling, build it into your task implementations (or reach for a package designed for it)
  rather than expecting this one to grow the knob.
- **The tasks do not block start-up.** `BackgroundJobHost` is a `BackgroundService`, so the host
  reaches "application started" as soon as `ExecuteAsync` hits its first `await` — the first HTTP
  request can be served while your seed task is still running. Do not treat a registered task as a
  precondition for the first request.
- **All tasks share one DI scope, running concurrently.** `BackgroundJobHost` resolves every
  `IBackgroundTask` from a single shared scope and runs them all at once with `Task.WhenAll` — it
  does **not** give each task its own scope. If two registered tasks depend on the same scoped
  service (most commonly a `DbContext`), they get the *same instance* while executing in parallel,
  which is unsafe for services that are not thread-safe. Either make sure no two tasks touch the
  same scoped dependency, or have the task open its own scope via `IServiceScopeFactory` as shown
  above.
- **Design every `RunAsync` to be idempotent.** Tasks run once per host start-up — every
  deployment, restart, or scale-out replica start runs them again. Prefer upserts/existence checks
  over blind inserts so a re-run is safe.
- **Respect the `CancellationToken`.** It is the token passed to `BackgroundService.ExecuteAsync` on
  host shutdown; pass it through to every awaited call. A task that ignores it can delay graceful
  shutdown, since the host waits for `Task.WhenAll` to finish before it can stop.
- **A thrown exception does not undo earlier side effects.** The failing task's exception is caught,
  logged, and swallowed — whatever it already wrote or mutated before throwing stays as-is. There is
  no automatic retry or rollback, so structure risky work so a partial run leaves the system in a
  safe state (or lean on the idempotency above to fix itself on the next start-up).

## 🔗 Related packages

- [DKNet.AspCore.Extensions](DKNet.AspCore.Extensions.md) — the minimal-API glue for the same
  `AspNet/` area: endpoint discovery, paged responses, Result/ProblemDetails conversion.
- [DKNet.AspCore.Idempotency](DKNet.AspCore.Idempotency.md) — reach for it when the work is
  triggered per request and must be safe to retry, rather than once at start-up.
- [DKNet.EfCore.Specifications](../EfCore/DKNet.EfCore.Specifications.md) — the repository surface a
  data-touching start-up task normally uses, as shown above.
