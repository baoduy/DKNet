# Register startup tasks

Part of [DKNet.AspCore.Tasks](index.md). Implement `IBackgroundTask.RunAsync(CancellationToken)` and register each implementation explicitly or scan named assemblies:

```csharp
using DKNet.AspCore.Tasks;
using Microsoft.Extensions.DependencyInjection;
var services = new ServiceCollection();
services.AddBackgroundJob<PrintTask>();
services.AddBackgroundJobFrom([typeof(PrintTask).Assembly]);
Console.WriteLine(services.Count(d => d.ServiceType == typeof(IBackgroundTask))); // 1

public sealed class PrintTask : IBackgroundTask
{
    public Task RunAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
```

![Explicit registration and assembly scanning share an implementation-type duplicate guard and register one BackgroundJobHost.](diagrams/task-registration.svg)

`TaskSetups` lives in `Microsoft.Extensions.DependencyInjection`. Both methods add one internal hosted `BackgroundJobHost`. Each task is registered as scoped `IBackgroundTask`, deduplicated by service type plus implementation type. Scanning selects nonabstract classes assignable to `IBackgroundTask` from the supplied assemblies.

## ⚙️ Configuration reference

| Input | Type | Default | Effect |
|---|---|---|---|
| `TJob` | `class, IBackgroundTask` | required | Registers one task implementation. |
| `assemblies` | `Assembly[]` | required | Supplies scanning scope; this is not a `params` argument. |

There is no options object or configuration section.

## ⚠️ Gotchas & limits

- The guard recognizes implementation-type descriptors; a custom factory registration is not the same descriptor.
- Scanning does not filter open generic classes, and type-loading exceptions are not caught. Prefer explicit registrations for predictable discovery.
- Registration only prepares services. A host must start to run the task.
