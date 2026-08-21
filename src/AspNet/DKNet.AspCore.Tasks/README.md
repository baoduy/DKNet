# DKNet.AspCore.Tasks

[![NuGet](https://img.shields.io/nuget/v/DKNet.AspCore.Tasks.svg)](https://www.nuget.org/packages/DKNet.AspCore.Tasks/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A lightweight, easy-to-use library for managing background jobs that need to run when your ASP.NET Core application
starts.

## Features

- Simple API to register background jobs that execute on application startup
- Jobs run in scoped lifetime with proper dependency injection
- Automatic detection and registration of background jobs via assembly scanning
- Graceful error handling - errors in one job won't affect others
- Built on top of ASP.NET Core's IHostedService for proper lifecycle management

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

Full feature walkthrough (scoping/execution model, error isolation, gotchas):
[DKNet.AspCore.Tasks docs](https://github.com/baoduy/DKNet/blob/main/docs/AspNetCore/DKNet.AspCore.Tasks.md)

## Compatibility

- .NET 10.0 and above
- Compatible with ASP.NET Core and any application using Microsoft's Generic Host

## License

This project is licensed under the MIT License - see the [LICENSE](https://opensource.org/licenses/MIT) file for
details.

## About

Developed by [Steven Hoang](https://drunkcoding.net).
