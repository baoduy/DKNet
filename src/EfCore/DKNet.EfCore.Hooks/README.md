# DKNet.EfCore.Hooks

A collection of hooks for Entity Framework Core to extend and customize database operations with ease.

## Overview

DKNet.EfCore.Hooks provides a flexible way to add custom functionality before or after specific database operations in your Entity Framework Core applications. These hooks allow you to intercept key events during the data lifecycle, such as saving changes, querying, or updating entities, enabling you to execute custom logic seamlessly.

## Features

- **Pre and Post Hooks**: Define actions to be executed before (`PreHook`) and after (`PostHook`) specific operations.

- **Global Hooks**: Apply hooks across all entities or specific entity types for consistent behavior.

- **Performance Monitoring**: Track query execution times and other performance metrics to optimize your database interactions.

- **Audit Logging**: Automatically log changes made to entities, including who made the change and when.

## Getting Started

### Installation

To integrate DKNet.EfCore.Hooks into your project, install the NuGet package:

```bash
dotnet add package DKNet.EfCore.Hooks --version 1.0.0-alpha
```

Replace `--version 1.0.0-alpha` with the appropriate version number when available.

### Basic Usage

Here's a simple example of how to use hooks in your Entity Framework Core project:

```csharp
public class Program
{
    public static void Main()
    {
        var optionsBuilder = new DbContextOptionsBuilder<YourDbContext>();
        
        // Add hooks to the context configuration
        HooksConfiguration.AddHooks(optionsBuilder);
        
        var dbContext = new YourDbContext(optionsBuilder.Options);
        
        // Use thedbContext as needed
        ...
    }
}
```

## Detailed Features

### Pre and Post Hooks

These hooks allow you to execute custom logic before or after specific database operations.

**Example:**

```csharp
public class EntityHooks : IPreSaveChangesHook, IPostSaveChangesHook
{
    public void OnPreSaveChanges(DbContext context)
    {
        // Custom logic before saving changes
        Console.WriteLine("Executing pre-save changes hook.");
    }

    public void OnPostSaveChanges(DbContext context)
    {
        // Custom logic after saving changes
        Console.WriteLine("Executing post-save changes hook.");
    }
}

// Register the hooks in your DbContext configuration
public static class HooksConfiguration
{
    public static void AddHooks(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.ConfigureDbContext(
            (context) =>
                context.AddHook<IPreSaveChangesHook, EntityHooks>()
                       .AddHook<IPostSaveChangesHook, EntityHooks>());
    }
}
```

### Global Hooks

Global hooks ensure that your custom logic applies consistently across all entities or specific entity types.

**Example:**

```csharp
public class QueryExecutionTimeMonitor : IPreQueryExecutionHook
{
    public void OnPreQueryExecute(QueryContext context)
    {
        var watch = Stopwatch.StartNew();
        context.CommandExecuted += (sender, eventArgs) =>
        {
            watch.Stop();
            Console.WriteLine($"Query executed in {watch.Elapsed.TotalMilliseconds} ms");
        };
    }
}

// Register global hooks
public static class HooksConfiguration
{
    public static void AddGlobalHooks(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.ConfigureDbContext(
            (context) =>
                context.AddHook<IPreQueryExecutionHook, QueryExecutionTimeMonitor>());
    }
}
```

### Performance Monitoring

Track the performance of your database queries and optimize based on real data.

**Example:**

```csharp
public class PerformanceMetricsCollector : IPostQueryExecutionHook
{
    private readonly ILogger _logger;

    public PerformanceMetricsCollector(ILogger logger)
    {
        _logger = logger;
    }

    public void OnPostQueryExecute(QueryContext context)
    {
        var elapsed = context.ElapsedTime.TotalMilliseconds;
        
        if (elapsed > 100)
        {
            _logger.LogWarning($"Slow query detected: {context.Command.CommandText} executed in {elapsed} ms");
        }
    }
}

// Register performance monitoring hooks
public static class HooksConfiguration
{
    public static void AddPerformanceMonitoring(DbContextOptionsBuilder optionsBuilder, ILoggerFactory loggerFactory)
    {
        optionsBuilder.ConfigureDbContext(
            (context) =>
                context.AddHook<IPostQueryExecutionHook, PerformanceMetricsCollector>(loggerFactory.CreateLogger(typeof(PerformanceMetricsCollector))));
    }
}
```

### Audit Logging

Automatically log changes made to your entities for auditing purposes.

**Example:**

```csharp
public interface IAuditLog
{
    int Id { get; set; }
    string CreatedBy { get; set; }
    DateTime CreatedAt { get; set; }
    string UpdatedBy { get; set; }
    DateTime UpdatedAt { get; set; }
}

public class AuditHooks : IPreSaveChangesHook, IPostSaveChangesHook
{
    private readonly IAuditLog _auditLog;

    public AuditHooks(IAuditLog auditLog)
    {
        _auditLog = auditLog;
    }

    public void OnPreSaveChanges(DbContext context)
    {
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
            {
                var entity = entry.Entity as IAuditLog;
                if (entity != null)
                {
                    if (entry.State == EntityState.Added)
                    {
                        entity.CreatedBy = "CurrentUser"; // Replace with actual user
                        entity.CreatedAt = DateTime.Now;
                    }
                    
                    entity.UpdatedAt = DateTime.Now;
                }
            }
        }
    }

    public void OnPostSaveChanges(DbContext context)
    {
        // Additional logic after save, if needed
    }
}

// Register audit hooks
public static class HooksConfiguration
{
    public static void AddAuditHooks(DbContextOptionsBuilder optionsBuilder, IAuditLog审计日志>)
    {
        optionsBuilder.ConfigureDbContext(
            (context) =>
                context.AddHook<IPreSaveChangesHook, AuditHooks>(_auditLog));
    }
}
```

## Contributing

Contributions are welcome and encouraged! If you'd like to contribute to the project, please follow these steps:

1. **Fork the repository**: Create your own copy of the project on GitHub.
2. **Create a feature branch**: Develop new features or bug fixes in a dedicated branch.
3. **Commit changes**: Keep your commits clear and descriptive.
4. **Push to the branch**: Share your changes with the remote repository.
5. **Create a Pull Request**: Submit your changes for review and merging.

## License

This project is licensed under [MIT License](LICENSE).

## Acknowledgments

- Thanks to the Entity Framework Core team for providing a robust framework.
- Special thanks to contributors who have enhanced this library.