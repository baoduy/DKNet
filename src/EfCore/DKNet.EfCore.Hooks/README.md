# DKNet.EfCore.Hooks

[![NuGet](https://img.shields.io/nuget/v/DKNet.EfCore.Hooks)](https://www.nuget.org/packages/DKNet.EfCore.Hooks/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DKNet.EfCore.Hooks)](https://www.nuget.org/packages/DKNet.EfCore.Hooks/)
[![.NET](https://img.shields.io/badge/.NET-9.0-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](../../../../LICENSE)

Entity Framework Core lifecycle hooks system providing pre and post-save interceptors for implementing cross-cutting concerns like auditing, validation, caching, and event publishing. This package enables clean separation of business logic from data access concerns.

## Features

- **Lifecycle Hooks**: Pre-save and post-save hooks for Entity Framework Core operations
- **Snapshot Context**: Track entity changes with before/after state comparison
- **Async Support**: Full async/await support for non-blocking hook execution
- **Dependency Injection**: Seamless integration with .NET dependency injection
- **Multiple Hooks**: Support for multiple hooks per DbContext with execution ordering
- **Change Tracking**: Access to entity state changes during save operations
- **Error Handling**: Robust error handling and hook execution management
- **Performance Optimized**: Efficient execution with minimal overhead

## Supported Frameworks

- .NET 9.0+
- Entity Framework Core 9.0+

## Installation

Install via NuGet Package Manager:

```bash
dotnet add package DKNet.EfCore.Hooks
```

Or via Package Manager Console:

```powershell
Install-Package DKNet.EfCore.Hooks
```

## Quick Start

### Basic Hook Implementation

```csharp
using DKNet.EfCore.Hooks;
using DKNet.EfCore.Extensions.Snapshots;

// Audit hook example
public class AuditHook : IBeforeSaveHookAsync
{
    private readonly ICurrentUserService _currentUserService;

    public AuditHook(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    public Task RunBeforeSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        var currentUser = _currentUserService.UserId;
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in context.Entries)
        {
            if (entry.Entity is IAuditedProperties auditedEntity)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        auditedEntity.CreatedBy = currentUser;
                        auditedEntity.CreatedOn = now;
                        break;
                    case EntityState.Modified:
                        auditedEntity.UpdatedBy = currentUser;
                        auditedEntity.UpdatedOn = now;
                        break;
                }
            }
        }

        return Task.CompletedTask;
    }
}

// Event publishing hook
public class EventPublishingHook : IAfterSaveHookAsync
{
    private readonly IEventPublisher _eventPublisher;

    public EventPublishingHook(IEventPublisher eventPublisher)
    {
        _eventPublisher = eventPublisher;
    }

    public async Task RunAfterSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        foreach (var entry in context.Entries)
        {
            if (entry.Entity is IEventEntity eventEntity)
            {
                var events = eventEntity.GetEvents();
                foreach (var domainEvent in events)
                {
                    await _eventPublisher.PublishAsync(domainEvent, cancellationToken);
                }
                eventEntity.ClearEvents();
            }
        }
    }
}
```

### Setup and Registration

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public DbSet<Product> Products { get; set; }
    public DbSet<Customer> Customers { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
}

// Configure services
public void ConfigureServices(IServiceCollection services)
{
    // Register hooks
    services.AddHook<AppDbContext, AuditHook>();
    services.AddHook<AppDbContext, EventPublishingHook>();
    services.AddHook<AppDbContext, ValidationHook>();

    // Register hook dependencies
    services.AddScoped<ICurrentUserService, CurrentUserService>();
    services.AddScoped<IEventPublisher, EventPublisher>();

    // Add DbContext with hooks
    services.AddDbContext<AppDbContext>((provider, options) =>
    {
        options.UseSqlServer(connectionString)
               .AddHookInterceptor<AppDbContext>(provider);
    });
}
```

### Combined Hook Implementation

```csharp
public class ComprehensiveHook : IHookAsync
{
    private readonly ILogger<ComprehensiveHook> _logger;
    private readonly IValidator _validator;
    private readonly IEventPublisher _eventPublisher;

    public ComprehensiveHook(
        ILogger<ComprehensiveHook> logger,
        IValidator validator,
        IEventPublisher eventPublisher)
    {
        _logger = logger;
        _validator = validator;
        _eventPublisher = eventPublisher;
    }

    public async Task RunBeforeSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Running pre-save hooks for {EntityCount} entities", context.Entries.Count);

        // Validation
        foreach (var entry in context.Entries)
        {
            if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
            {
                var validationResult = await _validator.ValidateAsync(entry.Entity, cancellationToken);
                if (!validationResult.IsValid)
                {
                    throw new ValidationException($"Validation failed for {entry.Entity.GetType().Name}: {validationResult.Errors}");
                }
            }
        }

        // Auto-set timestamps
        foreach (var entry in context.Entries)
        {
            if (entry.Entity is ITimestampedEntity timestamped)
            {
                if (entry.State == EntityState.Added)
                    timestamped.CreatedAt = DateTimeOffset.UtcNow;
                if (entry.State == EntityState.Modified)
                    timestamped.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }
    }

    public async Task RunAfterSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Running post-save hooks for {EntityCount} entities", context.Entries.Count);

        // Publish domain events
        var events = new List<object>();
        foreach (var entry in context.Entries)
        {
            if (entry.Entity is IEventEntity eventEntity)
            {
                events.AddRange(eventEntity.GetEvents());
                eventEntity.ClearEvents();
            }
        }

        foreach (var domainEvent in events)
        {
            await _eventPublisher.PublishAsync(domainEvent, cancellationToken);
        }

        // Cache invalidation
        foreach (var entry in context.Entries)
        {
            if (entry.State == EntityState.Modified || entry.State == EntityState.Deleted)
            {
                // Invalidate cache for this entity type
                await InvalidateCacheForEntityType(entry.Entity.GetType(), cancellationToken);
            }
        }
    }

    private async Task InvalidateCacheForEntityType(Type entityType, CancellationToken cancellationToken)
    {
        // Implementation depends on your caching strategy
        _logger.LogDebug("Invalidating cache for entity type {EntityType}", entityType.Name);
        await Task.CompletedTask;
    }
}
```

## Configuration

### Multiple Hooks with Ordering

```csharp
public class OrderedValidationHook : IBeforeSaveHookAsync
{
    public int Order => 1; // Run first

    public Task RunBeforeSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        // Validation logic
        return Task.CompletedTask;
    }
}

public class OrderedAuditHook : IBeforeSaveHookAsync
{
    public int Order => 2; // Run after validation

    public Task RunBeforeSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        // Audit logic
        return Task.CompletedTask;
    }
}

// Register in order
services.AddHook<AppDbContext, OrderedValidationHook>();
services.AddHook<AppDbContext, OrderedAuditHook>();
```

### Conditional Hook Execution

```csharp
public class ConditionalHook : IBeforeSaveHookAsync
{
    private readonly IFeatureManager _featureManager;

    public ConditionalHook(IFeatureManager featureManager)
    {
        _featureManager = featureManager;
    }

    public async Task RunBeforeSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        if (!await _featureManager.IsEnabledAsync("AuditLogging"))
            return;

        // Execute hook logic only when feature is enabled
        foreach (var entry in context.Entries)
        {
            // Conditional audit logic
        }
    }
}
```

## API Reference

### Hook Interfaces

- `IHookBaseAsync` - Base interface for all hooks
- `IBeforeSaveHookAsync` - Pre-save hook interface
- `IAfterSaveHookAsync` - Post-save hook interface
- `IHookAsync` - Combined pre and post-save hook interface

### Setup Extensions

- `AddHook<TDbContext, THook>()` - Register hook for specific DbContext
- `AddHookInterceptor<TDbContext>(IServiceProvider)` - Add hook interceptor to DbContext options

### Snapshot Context

- `SnapshotContext.Entries` - Collection of entity change entries
- `SnapshotEntityEntry.Entity` - The tracked entity
- `SnapshotEntityEntry.State` - Entity state (Added, Modified, Deleted, etc.)
- `SnapshotEntityEntry.OriginalValues` - Original property values (for Modified entities)
- `SnapshotEntityEntry.CurrentValues` - Current property values

## Advanced Usage

### Performance Monitoring Hook

```csharp
public class PerformanceMonitoringHook : IHookAsync
{
    private readonly ILogger<PerformanceMonitoringHook> _logger;
    private readonly IMetrics _metrics;
    private readonly Stopwatch _stopwatch = new();

    public PerformanceMonitoringHook(ILogger<PerformanceMonitoringHook> logger, IMetrics metrics)
    {
        _logger = logger;
        _metrics = metrics;
    }

    public Task RunBeforeSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        _stopwatch.Restart();
        _logger.LogDebug("Starting save operation for {EntityCount} entities", context.Entries.Count);
        
        _metrics.Counter("efcore.save_operations.started").Increment();
        _metrics.Histogram("efcore.entities_per_save").Record(context.Entries.Count);

        return Task.CompletedTask;
    }

    public Task RunAfterSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        _stopwatch.Stop();
        var duration = _stopwatch.ElapsedMilliseconds;

        _logger.LogDebug("Completed save operation in {Duration}ms for {EntityCount} entities", 
            duration, context.Entries.Count);

        _metrics.Histogram("efcore.save_operations.duration").Record(duration);
        _metrics.Counter("efcore.save_operations.completed").Increment();

        if (duration > 5000) // Log slow operations
        {
            _logger.LogWarning("Slow save operation detected: {Duration}ms for {EntityCount} entities", 
                duration, context.Entries.Count);
        }

        return Task.CompletedTask;
    }
}
```

### Security and Authorization Hook

```csharp
public class SecurityHook : IBeforeSaveHookAsync
{
    private readonly ICurrentUserService _currentUser;
    private readonly IAuthorizationService _authorizationService;

    public SecurityHook(ICurrentUserService currentUser, IAuthorizationService authorizationService)
    {
        _currentUser = currentUser;
        _authorizationService = authorizationService;
    }

    public async Task RunBeforeSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        foreach (var entry in context.Entries)
        {
            var entityType = entry.Entity.GetType().Name;
            var operation = GetOperationType(entry.State);

            var authResult = await _authorizationService.AuthorizeAsync(
                _currentUser.Principal, 
                entry.Entity, 
                $"{entityType}.{operation}");

            if (!authResult.Succeeded)
            {
                throw new UnauthorizedAccessException(
                    $"User {_currentUser.UserId} is not authorized to {operation} {entityType}");
            }

            // Row-level security for owned entities
            if (entry.Entity is IOwnedEntity ownedEntity)
            {
                if (ownedEntity.OwnerId != _currentUser.UserId && !_currentUser.IsAdmin)
                {
                    throw new UnauthorizedAccessException(
                        $"User {_currentUser.UserId} cannot access entity owned by {ownedEntity.OwnerId}");
                }
            }
        }
    }

    private static string GetOperationType(EntityState state) => state switch
    {
        EntityState.Added => "Create",
        EntityState.Modified => "Update",
        EntityState.Deleted => "Delete",
        _ => "Read"
    };
}
```

### Integration with External Systems

```csharp
public class ExternalIntegrationHook : IAfterSaveHookAsync
{
    private readonly ISearchIndexService _searchService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<ExternalIntegrationHook> _logger;

    public ExternalIntegrationHook(
        ISearchIndexService searchService,
        INotificationService notificationService,
        ILogger<ExternalIntegrationHook> logger)
    {
        _searchService = searchService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task RunAfterSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        var searchUpdateTasks = new List<Task>();
        var notificationTasks = new List<Task>();

        foreach (var entry in context.Entries)
        {
            try
            {
                // Update search index
                if (entry.Entity is ISearchable searchableEntity)
                {
                    var task = entry.State switch
                    {
                        EntityState.Added or EntityState.Modified => 
                            _searchService.IndexAsync(searchableEntity, cancellationToken),
                        EntityState.Deleted => 
                            _searchService.RemoveAsync(searchableEntity.Id, cancellationToken),
                        _ => Task.CompletedTask
                    };
                    searchUpdateTasks.Add(task);
                }

                // Send notifications
                if (entry.Entity is INotifiable notifiableEntity && entry.State == EntityState.Added)
                {
                    var notificationTask = _notificationService.SendCreatedNotificationAsync(
                        notifiableEntity, cancellationToken);
                    notificationTasks.Add(notificationTask);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing external integration for {EntityType} {EntityId}", 
                    entry.Entity.GetType().Name, GetEntityId(entry.Entity));
            }
        }

        // Execute all tasks concurrently
        await Task.WhenAll(searchUpdateTasks.Concat(notificationTasks));
    }

    private static object? GetEntityId(object entity)
    {
        return entity.GetType().GetProperty("Id")?.GetValue(entity);
    }
}
```

## Error Handling and Resilience

```csharp
public class ResilientHook : IHookAsync
{
    private readonly ILogger<ResilientHook> _logger;
    private readonly IRetryPolicy _retryPolicy;

    public ResilientHook(ILogger<ResilientHook> logger, IRetryPolicy retryPolicy)
    {
        _logger = logger;
        _retryPolicy = retryPolicy;
    }

    public async Task RunBeforeSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Pre-save logic with retry
            await _retryPolicy.ExecuteAsync(async () =>
            {
                await ProcessPreSaveLogic(context, cancellationToken);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error in pre-save hook. Operation will be aborted.");
            throw; // Re-throw to prevent save operation
        }
    }

    public async Task RunAfterSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Post-save logic with resilience (don't fail the main operation)
            await _retryPolicy.ExecuteAsync(async () =>
            {
                await ProcessPostSaveLogic(context, cancellationToken);
            });
        }
        catch (Exception ex)
        {
            // Log error but don't re-throw to avoid affecting the main save operation
            _logger.LogError(ex, "Error in post-save hook. Main operation completed successfully.");
        }
    }

    private Task ProcessPreSaveLogic(SnapshotContext context, CancellationToken cancellationToken)
    {
        // Critical pre-save operations
        return Task.CompletedTask;
    }

    private Task ProcessPostSaveLogic(SnapshotContext context, CancellationToken cancellationToken)
    {
        // Non-critical post-save operations
        return Task.CompletedTask;
    }
}
```

## Best Practices

- **Separation of Concerns**: Keep hooks focused on single responsibilities
- **Error Handling**: Use try-catch in post-save hooks to avoid affecting main operations
- **Performance**: Minimize processing time in pre-save hooks
- **Async Operations**: Use async/await for I/O operations
- **Logging**: Add comprehensive logging for debugging and monitoring
- **Testing**: Mock hook dependencies for unit testing

## Performance Considerations

- **Hook Execution Order**: Critical hooks should run first
- **Async Operations**: Use Task.WhenAll for concurrent operations
- **Database Calls**: Minimize additional database calls in hooks
- **Memory Usage**: Be mindful of memory usage when processing large change sets
- **Caching**: Consider caching expensive operations within hook scope

## Thread Safety

- Hook instances are scoped to the DbContext instance
- Concurrent access to shared resources requires proper synchronization
- Use thread-safe services and avoid shared mutable state
- Entity Framework Core change tracking is not thread-safe

## Contributing

See the main [CONTRIBUTING.md](../../../../CONTRIBUTING.md) for guidelines on how to contribute to this project.

## License

This project is licensed under the [MIT License](../../../../LICENSE).

## Related Packages

- [DKNet.EfCore.Extensions](../DKNet.EfCore.Extensions) - EF Core functionality extensions (includes SnapshotContext)
- [DKNet.EfCore.Events](../DKNet.EfCore.Events) - Domain event handling (uses hooks internally)
- [DKNet.EfCore.Abstractions](../DKNet.EfCore.Abstractions) - Core entity abstractions
- [DKNet.EfCore.DataAuthorization](../DKNet.EfCore.DataAuthorization) - Data authorization patterns

---

Part of the [DKNet Framework](https://github.com/baoduy/DKNet) - A comprehensive .NET framework for building modern, scalable applications.