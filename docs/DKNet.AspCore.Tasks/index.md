# DKNet.AspCore.Tasks

A hosted startup batch for .NET developers who implement once-per-start background work.

## ✨ Why use it?

- Run small startup jobs without a hosted service for every job.
- Deduplicate task registrations across composition roots.
- Log individual task failures while the remaining jobs finish.

## 🚀 Quick Start

Use a .NET 10 console project.

```bash
dotnet new console -f net10.0 -n PackageDemo
cd PackageDemo
dotnet add package DKNet.AspCore.Tasks
```

Replace `Program.cs` with the example, then run `dotnet run`.

Add `Microsoft.Extensions.Hosting` to the console project as well (`dotnet add package Microsoft.Extensions.Hosting`).

```csharp
using DKNet.AspCore.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddBackgroundJob<PrintTask>();
await builder.Build().RunAsync();

public sealed class PrintTask : IBackgroundTask
{
    public Task RunAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine("Startup task ran.");
        return Task.CompletedTask;
    }
}
```

The host prints `Startup task ran.` once. Stop it with Ctrl+C; task completion does not stop the host.

## 🏗️ Architecture

![The generic host starts BackgroundJobHost, which resolves scoped task implementations and logs failures.](diagrams/components.svg)

One hosted service owns one async scope for all tasks. Tasks share scoped dependencies and run concurrently; this is neither a scheduler nor a readiness gate.

## 🔄 How it works

![BackgroundJobHost resolves a batch, runs tasks concurrently, catches individual exceptions and disposes the batch scope.](diagrams/aspcore-tasks-startup-run.svg)

There is no ordering guarantee or automatic retry. The host’s stopping token reaches every task. Task completion leaves the application host running.

## 🧩 Features

- [Register startup tasks](register-startup-tasks.md) — Choose explicit or scanned registrations and understand duplicate guards.
- [Run a concurrent startup batch](run-startup-tasks.md) — Handle concurrent tasks, private dependencies, and individual failures.

## 🔗 Related packages

- [Extensions](../DKNet.AspCore.Extensions/index.md) — use its endpoint and host helpers.
- [Idempotency](../DKNet.AspCore.Idempotency/index.md) — protect per-request retries.
- [EF Core Specifications](../EfCore/DKNet.EfCore.Specifications.md) — use repositories inside task-owned scopes.
