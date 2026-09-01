# DKNet.AspCore.Tasks

[![NuGet](https://img.shields.io/nuget/v/DKNet.AspCore.Tasks.svg)](https://www.nuget.org/packages/DKNet.AspCore.Tasks/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/baoduy/DKNet/blob/main/LICENSE)

A lightweight, easy-to-use library for managing background jobs that need to run when your ASP.NET Core application
starts.

## Features

- Simple API to register background jobs that run once per host start-up
- One `BackgroundService` runs every registered task, so there is no `IHostedService` per job
- Automatic detection and registration of background jobs via assembly scanning
- Graceful error handling — an exception in one job is logged and swallowed, and the others still finish
- Built on ASP.NET Core's `BackgroundService`, so the tasks do not block the host from serving requests

## Installation

```bash
dotnet add package DKNet.AspCore.Tasks
```

## Quick Start

Implement `IBackgroundTask` for each start-up job:

```csharp
public sealed class DataInitializationTask(IMyService service, ILogger<DataInitializationTask> logger) : IBackgroundTask
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Initializing data...");
        await service.InitializeAsync(cancellationToken);
        logger.LogInformation("Data initialization complete");
    }
}
```

Then register it in `Program.cs`:

```csharp
// Register a specific task
builder.Services.AddBackgroundJob<DataInitializationTask>();

// ...or scan assemblies for every IBackgroundTask implementation
builder.Services.AddBackgroundJobFrom([typeof(Program).Assembly]);
```

## Customisation reference

**There is no options type.** The assembly exposes exactly two public types — `IBackgroundTask` and
the `TaskSetups` registration class — and neither takes a settings object, so nothing is bound from
`appsettings.json`. The customisation surface is the interface you implement plus the two
type/parameter slots on the registration methods:

| Knob | Kind | Default | Effect |
|---|---|---|---|
| `IBackgroundTask.RunAsync(CancellationToken)` | interface method you implement | none — required | The body of a start-up task. Throwing is caught, logged at `Error` as `{TaskFullTypeName} job failed`, and swallowed. |
| `TJob` on `AddBackgroundJob<TJob>()` | type parameter, `where TJob : class, IBackgroundTask` | none — required | Registered as `AddScoped<IBackgroundTask, TJob>()`; a second call for the same implementation type is a no-op. |
| `assemblies` on `AddBackgroundJobFrom(Assembly[])` | `Assembly[]` — not `params`, not `IEnumerable<Assembly>` | none — required | Every non-abstract class implementing `IBackgroundTask` in these assemblies is registered the same way. |
| `cancellationToken` on `RunAsync` | `CancellationToken` | `default` in the signature; the host always passes its own | The token `BackgroundService.ExecuteAsync` receives on shutdown. There is no per-task timeout. |

Execution is fixed, not configurable: the host opens **one shared async scope**, resolves every
registered `IBackgroundTask` from it, and runs them all concurrently through a single
`Task.WhenAll`. There is no ordering, no per-task scope, and no concurrency limit — two tasks
sharing a scoped `DbContext` share the *same instance* while running in parallel, so a
data-touching task should open its own scope via `IServiceScopeFactory`.

Full feature walkthrough (scoping/execution model, error isolation, gotchas):
[DKNet.AspCore.Tasks docs](https://github.com/baoduy/DKNet/blob/main/docs/AspNetCore/DKNet.AspCore.Tasks.md)

## Compatibility

- .NET 10.0 and above
- Compatible with ASP.NET Core and any application using Microsoft's Generic Host

## License

MIT — see [LICENSE](https://github.com/baoduy/DKNet/blob/main/LICENSE).

## About

Developed by [Steven Hoang](https://drunkcoding.net).
