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

## Documentation

| Guide | Read when |
|---|---|
| [Package overview](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.AspCore.Tasks/index.md) | Start with architecture and the end-to-end flow. |
| [Register startup tasks](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.AspCore.Tasks/register-startup-tasks.md) | Choose explicit or scanned registrations and understand duplicate guards. |
| [Run a concurrent startup batch](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.AspCore.Tasks/run-startup-tasks.md) | Handle concurrent tasks, private dependencies, and individual failures. |
