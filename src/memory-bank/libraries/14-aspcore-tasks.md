# DKNet.AspCore.Tasks — AI Skill File

> **Package**: `DKNet.AspCore.Tasks`  
> **Minimum Version**: `10.0.0`  
> **Minimum .NET**: `net10.0`

## Purpose
Runs startup background jobs (`IBackgroundJob`) with DI scopes and fault isolation.

## When To Use
- ✅ Seed reference data at startup
- ✅ Warm caches at startup
- ✅ Run one-time initialization workflows

## When NOT To Use
- ❌ Repeating schedules (cron); use Hangfire/Quartz/Azure Functions timer
- ❌ Long-running queue workers; use dedicated hosted service

## Installation
```bash
dotnet add package DKNet.AspCore.Tasks
```

## Setup / DI Registration
```csharp
public class SeedJob : IBackgroundJob
{
    public Task RunAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

services.AddBackgroundJob<SeedJob>();
// or services.AddBackgroundJobFrom(new[] { typeof(Program).Assembly });
```

## Key API Surface
- `IBackgroundJob.RunAsync(...)` for startup job execution.
- `AddBackgroundJob<TJob>()` and `AddBackgroundJobFrom(...)` for registration.

## Usage Pattern
```csharp
public class SeedJob : IBackgroundJob
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
    }
}

services.AddBackgroundJob<SeedJob>();
```

## Anti-Patterns
```csharp
// ❌ Wrong: do heavy synchronous blocking work in RunAsync
// ✅ Correct: always await async I/O and honor cancellation token
```

## Composes With
- `DKNet.EfCore.Repos` (seed via repositories)

## Test Example
```csharp
// Uses TestContainers.MsSql in integration scenarios when jobs touch EF Core infrastructure.

[Fact]
public async Task RunAsync_WhenInvoked_CompletesWithoutException()
{
    var job = new SeedJob();
    await Should.NotThrowAsync(() => job.RunAsync());
}
```

## Quick Decision Guide
- Use this package for startup-only background initialization work.
- Use dedicated schedulers for recurring jobs.
- Keep jobs idempotent and cancellation-aware.

## Version
- `10.0.0`: Initial documentation
