# Run a concurrent startup batch

Part of [DKNet.AspCore.Tasks](index.md). `BackgroundJobHost` opens one async scope, resolves all tasks, and awaits `Task.WhenAll`. A task can open a private scope when its dependencies must not be shared with other concurrent tasks:

```csharp
using DKNet.AspCore.Tasks;
using Microsoft.Extensions.DependencyInjection;
var services = new ServiceCollection();
services.AddScoped<Work>();
await using var provider = services.BuildServiceProvider();
await new IsolatedTask(provider.GetRequiredService<IServiceScopeFactory>()).RunAsync();

public sealed class IsolatedTask(IServiceScopeFactory scopes) : IBackgroundTask
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<Work>().RunAsync(cancellationToken);
    }
}
public sealed class Work
{
    public Task RunAsync(CancellationToken token) => Task.CompletedTask;
}
```

![Task.WhenAll invokes per-task wrappers that catch RunAsync exceptions and let the other tasks finish.](diagrams/task-isolation.svg)

Each `RunAsync` exception is caught and logged as `{TaskFullTypeName} job failed`; other tasks continue. The scope is disposed after the batch. Dependency construction and enumeration happen outside that per-task catch.

## ⚙️ Configuration reference

| Input | Type | Default | Effect |
|---|---|---|---|
| `cancellationToken` | `CancellationToken` | default in interface | Host passes its stopping token; tasks must observe it. |

There are no ordering, retry, concurrency-limit, or per-task-timeout settings.

## ⚠️ Gotchas & limits

- Tasks run concurrently in one shared scope by default. Two tasks can receive the same scoped `DbContext`; open private scopes for non-thread-safe dependencies.
- This background service is not an application readiness gate. Requests can arrive before the batch finishes.
- Tasks run once per host start, including every replica and restart. Make side effects safe to repeat.
- Exceptions, including cancellation raised inside `RunAsync`, are caught. There is no retry, rollback or compensation.
- A failed constructor or scope disposal is outside the `RunAsync` error wrapper.
