# DKNet.AspCore.Tasks

Background task orchestration for ASP.NET Core applications. The package wraps `IHostedService` with a convenient API
for discovering and running `IBackgroundTask` implementations during application start-up.

## ✨ Why use it?

- **Declarative registration** – Register single jobs or scan assemblies for implementations using `AddBackgroundJob` helpers.
- **Scoped execution** – Each job runs within its own DI scope, ensuring dependencies resolve with the correct lifetime.
- **Safe start-up pipeline** – Errors are logged and isolated so one faulty job does not prevent other jobs from finishing.
- **Test-friendly** – Jobs rely on interfaces and cancellation tokens, simplifying unit and integration testing.

## 🚀 Quick Start

```csharp
builder.Services.AddBackgroundJob<SeedReferenceDataTask>();
// or scan assemblies
builder.Services.AddBackgroundJobFrom(new[] { typeof(Program).Assembly });
```

Implement `IBackgroundTask` for each start-up task:

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

Jobs execute once the host starts, leveraging the `BackgroundJobHost` to coordinate execution.

## ⚙️ Registration Options

- `AddBackgroundJob<TTask>()` – Register a specific job type.
- `AddBackgroundJobFrom(IEnumerable<Assembly>)` – Scan assemblies for every `IBackgroundTask` implementation.
- `AddBackgroundJobFrom(params Assembly[])` – Convenience overload for inline arrays.
- `AddBackgroundJobFrom(AppDomain.CurrentDomain.GetAssemblies())` – Discover jobs across the entire application domain.

## 🧱 Architectural Role

`DKNet.AspCore.Tasks` belongs to the **application layer**, orchestrating cross-cutting operations (seed data, queue warm-up,
cache hydration) before inbound traffic hits controllers or message processors. It keeps domain logic isolated by delegating to
services injected into each job.

## ✅ Best Practices

- Keep jobs idempotent so replays during redeployments are safe.
- Respect the `CancellationToken` to support graceful shutdown during container stop events.
- Use structured logging to trace execution and surface metrics (start/end times, failures).
- For long-running work, schedule follow-up tasks to run asynchronously instead of blocking start-up.

## 🔗 Related Packages

- [DKNet Services](../Services/README.md) – Use encryption, storage, or PDF services inside background jobs.
- [DKNet Messaging](../Messaging/README.md) – Warm up messaging infrastructure before handlers start processing events.
