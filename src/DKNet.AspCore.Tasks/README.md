# DKNet.AspCore.Tasks

[![NuGet](https://img.shields.io/nuget/v/DKNet.AspCore.Tasks.svg)](https://www.nuget.org/packages/DKNet.AspCore.Tasks/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A lightweight, easy-to-use library for managing background jobs that need to run when your ASP.NET Core application starts.

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

### 1. Create a Background Job

Implement the `IBackgroundJob` interface:

```csharp
public class DataInitializationJob : IBackgroundJob
{
    private readonly IMyService _service;
    private readonly ILogger<DataInitializationJob> _logger;

    public DataInitializationJob(IMyService service, ILogger<DataInitializationJob> logger)
    {
        _service = service;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing data...");
        await _service.InitializeAsync(cancellationToken);
        _logger.LogInformation("Data initialization complete");
    }
}
```

### 2. Register the Background Job

In your `Program.cs` or startup configuration:

```csharp
// Register a specific job
services.AddBackgroundJob<DataInitializationJob>();

// Or scan assemblies for all IBackgroundJob implementations
services.AddBackgroundJobFrom(new[] { typeof(Program).Assembly });
```

## How It Works

When your application starts:

1. The `BackgroundJobHost` (registered as a hosted service) executes all registered jobs in parallel
2. Each job runs in its own scoped context with proper dependency resolution
3. Errors in individual jobs are caught and logged, ensuring other jobs still complete
4. All jobs must finish before the host reports completion

## Advanced Usage

### Multiple Jobs

Register multiple jobs individually:

```csharp
services.AddBackgroundJob<FirstJob>();
services.AddBackgroundJob<SecondJob>();
services.AddBackgroundJob<ThirdJob>();
```

### Assembly Scanning

Automatically detect and register all `IBackgroundJob` implementations:

```csharp
// Scan current assembly
services.AddBackgroundJobFrom(new[] { Assembly.GetExecutingAssembly() });

// Scan multiple assemblies
services.AddBackgroundJobFrom(new[] { 
    typeof(Program).Assembly, 
    typeof(ExternalComponent).Assembly 
});
```

## Best Practices

- Keep jobs focused on a single responsibility
- Use dependency injection for services needed by your jobs
- Respect cancellation tokens for graceful shutdown
- Don't block the main thread with long-running synchronous operations
- Use logging to track job execution progress and issues

## Compatibility

- .NET 9.0 and above
- Compatible with ASP.NET Core and any application using Microsoft's Generic Host

## License

This project is licensed under the MIT License - see the [LICENSE](https://opensource.org/licenses/MIT) file for details.

## About

Developed by [Steven Hoang](https://drunkcoding.net).
