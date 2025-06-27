# DKNet.EfCore.Hooks

**Lifecycle hooks for Entity Framework Core operations that provide extensible interception points for database operations, enabling cross-cutting concerns and custom logic execution during the EF Core lifecycle while supporting Domain-Driven Design (DDD) and Onion Architecture principles.**

## What is this project?

DKNet.EfCore.Hooks provides a flexible hook system for Entity Framework Core that allows you to intercept and extend database operations at various points in the EF Core lifecycle. This enables the implementation of cross-cutting concerns such as auditing, validation, performance monitoring, and custom business logic without cluttering your domain entities or application services.

### Key Features

- **IHook Interface**: Extensible hook system for custom logic injection
- **Pre/Post Operation Hooks**: Execute logic before and after database operations
- **Lifecycle Integration**: Seamless integration with EF Core lifecycle events
- **Performance Monitoring**: Built-in performance tracking and monitoring capabilities
- **Audit Logging**: Comprehensive audit trail for entity changes
- **Validation Hooks**: Pre-save validation with custom business rules
- **Global Hooks**: Apply hooks across all entities or specific entity types
- **Async Support**: Full async/await support for hook operations
- **Error Handling**: Robust error handling and recovery mechanisms

## How it contributes to DDD and Onion Architecture

### Cross-Cutting Concerns Layer

DKNet.EfCore.Hooks implements **Cross-Cutting Concerns** that span multiple layers of the Onion Architecture, providing infrastructure services that support all layers without creating dependencies:

```
┌─────────────────────────────────────────────────────────────────┐
│                    🌐 Presentation Layer                        │
│                   (Controllers, API Endpoints)                  │
│                                                                 │
│  Benefits from: Audit logs, performance metrics, validation    │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                   🎯 Application Layer                          │
│              (Use Cases, Application Services)                  │
│                                                                 │
│  Benefits from: Transaction hooks, validation, error handling  │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                    💼 Domain Layer                             │
│           (Entities, Aggregates, Domain Services)              │
│                                                                 │
│  Benefits from: Domain rule enforcement, business validation   │
│  🏷️ Remains unaware of hook implementations                    │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                 🗄️ Infrastructure Layer                        │
│                  (Hooks, Persistence, Cross-cutting)           │
│                                                                 │
│  🎯 Hook Implementations:                                       │
│  📊 Performance Monitoring Hooks                               │
│  📝 Audit Logging Hooks                                        │
│  ✅ Validation Hooks                                           │
│  🔒 Security Hooks                                             │
│  🔄 EF Core Integration                                         │
└─────────────────────────────────────────────────────────────────┘
```

### DDD Benefits

1. **Domain Logic Separation**: Hooks keep infrastructure concerns out of domain entities
2. **Business Rule Enforcement**: Pre-save hooks can enforce domain rules consistently
3. **Audit Trail**: Comprehensive business event tracking without domain complexity
4. **Validation**: Business rule validation without coupling to domain logic
5. **Performance Monitoring**: Track domain operations without performance impact
6. **Error Handling**: Consistent error handling across domain operations

### Onion Architecture Benefits

1. **Dependency Inversion**: Hooks are configured in infrastructure, used by all layers
2. **Separation of Concerns**: Cross-cutting concerns isolated from business logic
3. **Testability**: Hooks can be mocked or disabled for unit testing
4. **Maintainability**: Centralized location for cross-cutting concerns
5. **Extensibility**: Easy to add new hooks without changing existing code
6. **Technology Independence**: Abstract hooks can be implemented for any data access technology

## How to use it

### Installation

```bash
dotnet add package DKNet.EfCore.Hooks
dotnet add package DKNet.EfCore.Abstractions
```

### Basic Usage Examples

#### 1. Audit Logging Hook

```csharp
using DKNet.EfCore.Hooks;
using DKNet.EfCore.Abstractions;

public class AuditLoggingHook : IHook
{
    private readonly ILogger<AuditLoggingHook> _logger;
    private readonly ICurrentUserService _currentUserService;
    
    public AuditLoggingHook(ILogger<AuditLoggingHook> logger, ICurrentUserService currentUserService)
    {
        _logger = logger;
        _currentUserService = currentUserService;
    }
    
    public async Task OnPreSaveChangesAsync(DbContext context, CancellationToken cancellationToken = default)
    {
        var auditEntries = new List<AuditEntry>();
        var currentUser = _currentUserService.GetCurrentUser();
        
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is IAuditableEntity auditableEntity)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        auditableEntity.CreatedBy = currentUser?.Id;
                        auditableEntity.CreatedAt = DateTime.UtcNow;
                        auditableEntity.UpdatedBy = currentUser?.Id;
                        auditableEntity.UpdatedAt = DateTime.UtcNow;
                        
                        auditEntries.Add(new AuditEntry
                        {
                            EntityName = entry.Entity.GetType().Name,
                            Action = AuditAction.Create,
                            EntityId = GetEntityId(entry.Entity),
                            UserId = currentUser?.Id,
                            Timestamp = DateTime.UtcNow,
                            Changes = GetPropertyChanges(entry)
                        });
                        break;
                        
                    case EntityState.Modified:
                        auditableEntity.UpdatedBy = currentUser?.Id;
                        auditableEntity.UpdatedAt = DateTime.UtcNow;
                        
                        auditEntries.Add(new AuditEntry
                        {
                            EntityName = entry.Entity.GetType().Name,
                            Action = AuditAction.Update,
                            EntityId = GetEntityId(entry.Entity),
                            UserId = currentUser?.Id,
                            Timestamp = DateTime.UtcNow,
                            Changes = GetPropertyChanges(entry)
                        });
                        break;
                        
                    case EntityState.Deleted:
                        auditEntries.Add(new AuditEntry
                        {
                            EntityName = entry.Entity.GetType().Name,
                            Action = AuditAction.Delete,
                            EntityId = GetEntityId(entry.Entity),
                            UserId = currentUser?.Id,
                            Timestamp = DateTime.UtcNow
                        });
                        break;
                }
            }
        }
        
        // Store audit entries
        foreach (var auditEntry in auditEntries)
        {
            context.Set<AuditEntry>().Add(auditEntry);
        }
        
        _logger.LogInformation("Audit logging completed for {Count} entities", auditEntries.Count);
    }
    
    public Task OnPostSaveChangesAsync(DbContext context, CancellationToken cancellationToken = default)
    {
        // Post-save audit logic if needed
        return Task.CompletedTask;
    }
    
    private static string GetEntityId(object entity)
    {
        // Extract entity ID using reflection or conventions
        var idProperty = entity.GetType().GetProperty("Id");
        return idProperty?.GetValue(entity)?.ToString() ?? string.Empty;
    }
    
    private static Dictionary<string, object> GetPropertyChanges(EntityEntry entry)
    {
        var changes = new Dictionary<string, object>();
        
        foreach (var property in entry.Properties)
        {
            if (property.IsModified)
            {
                changes[property.Metadata.Name] = new
                {
                    OldValue = property.OriginalValue,
                    NewValue = property.CurrentValue
                };
            }
        }
        
        return changes;
    }
}
```

#### 2. Performance Monitoring Hook

```csharp
public class PerformanceMonitoringHook : IHook
{
    private readonly ILogger<PerformanceMonitoringHook> _logger;
    private readonly IMetricsCollector _metricsCollector;
    private readonly Dictionary<DbContext, Stopwatch> _contextTimers = new();
    
    public PerformanceMonitoringHook(ILogger<PerformanceMonitoringHook> logger, IMetricsCollector metricsCollector)
    {
        _logger = logger;
        _metricsCollector = metricsCollector;
    }
    
    public Task OnPreSaveChangesAsync(DbContext context, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        _contextTimers[context] = stopwatch;
        
        var changeCount = context.ChangeTracker.Entries()
            .Count(e => e.State == EntityState.Added || 
                       e.State == EntityState.Modified || 
                       e.State == EntityState.Deleted);
        
        _logger.LogInformation("Starting SaveChanges operation with {ChangeCount} changes", changeCount);
        
        return Task.CompletedTask;
    }
    
    public async Task OnPostSaveChangesAsync(DbContext context, CancellationToken cancellationToken = default)
    {
        if (_contextTimers.TryGetValue(context, out var stopwatch))
        {
            stopwatch.Stop();
            _contextTimers.Remove(context);
            
            var elapsed = stopwatch.Elapsed;
            var changeCount = context.ChangeTracker.Entries()
                .Count(e => e.State == EntityState.Unchanged);
            
            _logger.LogInformation("SaveChanges completed in {ElapsedMs}ms for {ChangeCount} changes", 
                elapsed.TotalMilliseconds, changeCount);
            
            // Collect metrics
            await _metricsCollector.RecordSaveChangesMetricAsync(elapsed, changeCount);
            
            // Warn about slow operations
            if (elapsed.TotalMilliseconds > 1000)
            {
                _logger.LogWarning("Slow SaveChanges operation detected: {ElapsedMs}ms", 
                    elapsed.TotalMilliseconds);
            }
        }
    }
}
```

#### 3. Validation Hook

```csharp
public class ValidationHook : IHook
{
    private readonly ILogger<ValidationHook> _logger;
    private readonly IServiceProvider _serviceProvider;
    
    public ValidationHook(ILogger<ValidationHook> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }
    
    public async Task OnPreSaveChangesAsync(DbContext context, CancellationToken cancellationToken = default)
    {
        var validationErrors = new List<ValidationError>();
        
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
            {
                // Standard validation using data annotations
                var validationContext = new ValidationContext(entry.Entity, _serviceProvider, null);
                var validationResults = new List<ValidationResult>();
                
                if (!Validator.TryValidateObject(entry.Entity, validationContext, validationResults, true))
                {
                    foreach (var validationResult in validationResults)
                    {
                        validationErrors.Add(new ValidationError
                        {
                            EntityType = entry.Entity.GetType().Name,
                            PropertyName = validationResult.MemberNames.FirstOrDefault(),
                            ErrorMessage = validationResult.ErrorMessage,
                            AttemptedValue = GetPropertyValue(entry.Entity, validationResult.MemberNames.FirstOrDefault())
                        });
                    }
                }
                
                // Custom business rule validation
                if (entry.Entity is IValidatableEntity validatableEntity)
                {
                    var businessValidationResults = await validatableEntity.ValidateAsync(cancellationToken);
                    foreach (var result in businessValidationResults.Where(r => !r.IsValid))
                    {
                        validationErrors.Add(new ValidationError
                        {
                            EntityType = entry.Entity.GetType().Name,
                            PropertyName = result.PropertyName,
                            ErrorMessage = result.ErrorMessage,
                            AttemptedValue = result.AttemptedValue
                        });
                    }
                }
            }
        }
        
        if (validationErrors.Any())
        {
            _logger.LogWarning("Validation failed for {Count} entities", validationErrors.Count);
            throw new ValidationException("Entity validation failed", validationErrors);
        }
    }
    
    public Task OnPostSaveChangesAsync(DbContext context, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
    
    private static object? GetPropertyValue(object entity, string? propertyName)
    {
        if (string.IsNullOrEmpty(propertyName)) return null;
        
        var property = entity.GetType().GetProperty(propertyName);
        return property?.GetValue(entity);
    }
}
```

#### 4. Security Hook

```csharp
public class SecurityHook : IHook
{
    private readonly ILogger<SecurityHook> _logger;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuthorizationService _authorizationService;
    
    public SecurityHook(
        ILogger<SecurityHook> logger,
        ICurrentUserService currentUserService,
        IAuthorizationService authorizationService)
    {
        _logger = logger;
        _currentUserService = currentUserService;
        _authorizationService = authorizationService;
    }
    
    public async Task OnPreSaveChangesAsync(DbContext context, CancellationToken cancellationToken = default)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is ISecurableEntity securableEntity)
            {
                var operation = entry.State switch
                {
                    EntityState.Added => "Create",
                    EntityState.Modified => "Update",
                    EntityState.Deleted => "Delete",
                    _ => null
                };
                
                if (operation != null)
                {
                    var isAuthorized = await _authorizationService.AuthorizeAsync(
                        currentUser,
                        securableEntity,
                        operation);
                    
                    if (!isAuthorized)
                    {
                        _logger.LogWarning("User {UserId} attempted unauthorized {Operation} on {EntityType} {EntityId}",
                            currentUser?.Id, operation, entry.Entity.GetType().Name, securableEntity.Id);
                        
                        throw new UnauthorizedAccessException(
                            $"User is not authorized to {operation.ToLower()} this {entry.Entity.GetType().Name}");
                    }
                }
            }
        }
    }
    
    public Task OnPostSaveChangesAsync(DbContext context, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
```

#### 5. DbContext Integration

```csharp
public class ApplicationDbContext : DbContext
{
    private readonly IEnumerable<IHook> _hooks;
    
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        IEnumerable<IHook> hooks) : base(options)
    {
        _hooks = hooks;
    }
    
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<AuditEntry> AuditEntries { get; set; }
    
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Execute pre-save hooks
        foreach (var hook in _hooks)
        {
            await hook.OnPreSaveChangesAsync(this, cancellationToken);
        }
        
        try
        {
            // Save changes to database
            var result = await base.SaveChangesAsync(cancellationToken);
            
            // Execute post-save hooks
            foreach (var hook in _hooks)
            {
                await hook.OnPostSaveChangesAsync(this, cancellationToken);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            // Execute error hooks if needed
            foreach (var hook in _hooks.OfType<IErrorHook>())
            {
                await hook.OnErrorAsync(this, ex, cancellationToken);
            }
            throw;
        }
    }
}
```

#### 6. Service Registration

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEfCoreHooks(this IServiceCollection services)
    {
        // Register hooks
        services.AddScoped<IHook, AuditLoggingHook>();
        services.AddScoped<IHook, PerformanceMonitoringHook>();
        services.AddScoped<IHook, ValidationHook>();
        services.AddScoped<IHook, SecurityHook>();
        
        // Register supporting services
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IAuthorizationService, AuthorizationService>();
        services.AddScoped<IMetricsCollector, MetricsCollector>();
        
        return services;
    }
}

// In Program.cs or Startup.cs
services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

services.AddEfCoreHooks();
```

### Advanced Usage Examples

#### 1. Conditional Hooks

```csharp
public class ConditionalAuditHook : IHook
{
    private readonly IConfiguration _configuration;
    
    public ConditionalAuditHook(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    
    public async Task OnPreSaveChangesAsync(DbContext context, CancellationToken cancellationToken = default)
    {
        var auditEnabled = _configuration.GetValue<bool>("Auditing:Enabled");
        if (!auditEnabled) return;
        
        var sensitiveEntities = context.ChangeTracker.Entries()
            .Where(e => e.Entity.GetType().GetCustomAttribute<SensitiveDataAttribute>() != null)
            .ToList();
        
        if (sensitiveEntities.Any())
        {
            // Special handling for sensitive data
            await ProcessSensitiveDataAuditAsync(sensitiveEntities, cancellationToken);
        }
    }
    
    public Task OnPostSaveChangesAsync(DbContext context, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
```

#### 2. Soft Delete Hook

```csharp
public class SoftDeleteHook : IHook
{
    private readonly ICurrentUserService _currentUserService;
    
    public SoftDeleteHook(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }
    
    public Task OnPreSaveChangesAsync(DbContext context, CancellationToken cancellationToken = default)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is ISoftDeletableEntity softDeletableEntity && entry.State == EntityState.Deleted)
            {
                // Convert hard delete to soft delete
                entry.State = EntityState.Modified;
                softDeletableEntity.IsDeleted = true;
                softDeletableEntity.DeletedAt = DateTime.UtcNow;
                softDeletableEntity.DeletedBy = currentUser?.Id;
            }
        }
        
        return Task.CompletedTask;
    }
    
    public Task OnPostSaveChangesAsync(DbContext context, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
```

#### 3. Event Integration Hook

```csharp
public class EventIntegrationHook : IHook
{
    private readonly IEventPublisher _eventPublisher;
    
    public EventIntegrationHook(IEventPublisher eventPublisher)
    {
        _eventPublisher = eventPublisher;
    }
    
    public Task OnPreSaveChangesAsync(DbContext context, CancellationToken cancellationToken = default)
    {
        // Collect events before saving
        return Task.CompletedTask;
    }
    
    public async Task OnPostSaveChangesAsync(DbContext context, CancellationToken cancellationToken = default)
    {
        // Publish integration events after successful save
        var eventEntities = context.ChangeTracker.Entries()
            .Where(e => e.Entity is IEventEntity && e.State != EntityState.Detached)
            .Select(e => e.Entity as IEventEntity)
            .ToList();
        
        foreach (var eventEntity in eventEntities)
        {
            var events = eventEntity?.GetEvents() ?? Enumerable.Empty<EntityEventItem>();
            foreach (var eventItem in events)
            {
                await _eventPublisher.PublishAsync(eventItem.EventData, cancellationToken);
            }
            
            eventEntity?.ClearEvents();
        }
    }
}
```

## Best Practices

### 1. Hook Design Principles

```csharp
// Good: Focused single responsibility
public class AuditLoggingHook : IHook
{
    // Only handles audit logging
}

public class ValidationHook : IHook
{
    // Only handles validation
}

// Avoid: Multiple responsibilities in one hook
public class CompositeHook : IHook
{
    // Handles audit, validation, security, etc. (too many responsibilities)
}
```

### 2. Error Handling

```csharp
public class ResilientHook : IHook
{
    private readonly ILogger<ResilientHook> _logger;
    
    public async Task OnPreSaveChangesAsync(DbContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await ProcessHookLogicAsync(context, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hook processing failed");
            
            // Decide whether to fail the entire operation or continue
            if (IsHookCritical())
            {
                throw; // Fail the entire save operation
            }
            
            // Log and continue for non-critical hooks
            _logger.LogWarning("Non-critical hook failed, continuing with save operation");
        }
    }
}
```

### 3. Performance Considerations

```csharp
public class OptimizedHook : IHook
{
    public async Task OnPreSaveChangesAsync(DbContext context, CancellationToken cancellationToken = default)
    {
        // Get only relevant entities to avoid processing everything
        var relevantEntries = context.ChangeTracker.Entries()
            .Where(e => e.State != EntityState.Unchanged && e.Entity is IRelevantEntity)
            .ToList();
        
        if (!relevantEntries.Any()) return;
        
        // Batch operations for efficiency
        var tasks = relevantEntries
            .Select(entry => ProcessEntryAsync(entry, cancellationToken))
            .ToList();
        
        await Task.WhenAll(tasks);
    }
}
```

### 4. Testing Hooks

```csharp
[Test]
public async Task AuditLoggingHook_EntityModified_CreatesAuditEntry()
{
    // Arrange
    var context = CreateInMemoryDbContext();
    var currentUserService = new Mock<ICurrentUserService>();
    currentUserService.Setup(x => x.GetCurrentUser()).Returns(new User { Id = "user123" });
    
    var hook = new AuditLoggingHook(Mock.Of<ILogger<AuditLoggingHook>>(), currentUserService.Object);
    
    var customer = new Customer("John", "Doe", "john@example.com");
    context.Customers.Add(customer);
    await context.SaveChangesAsync();
    
    // Modify the entity
    customer.ChangeEmail("john.doe@example.com");
    
    // Act
    await hook.OnPreSaveChangesAsync(context);
    await context.SaveChangesAsync();
    
    // Assert
    var auditEntry = context.AuditEntries.FirstOrDefault();
    Assert.NotNull(auditEntry);
    Assert.Equal("Customer", auditEntry.EntityName);
    Assert.Equal(AuditAction.Update, auditEntry.Action);
    Assert.Equal("user123", auditEntry.UserId);
}
```

## Integration with Other DKNet Components

DKNet.EfCore.Hooks integrates seamlessly with other DKNet components:

- **DKNet.EfCore.Abstractions**: Uses entity interfaces and base classes
- **DKNet.EfCore.Events**: Coordinates with domain event publishing
- **DKNet.EfCore.Repos**: Hooks execute during repository operations
- **DKNet.EfCore.DataAuthorization**: Integrates with authorization hooks
- **DKNet.Fw.Extensions**: Leverages core framework utilities

---

> 💡 **Architecture Tip**: Use DKNet.EfCore.Hooks to implement cross-cutting concerns that need to execute during database operations. Hooks provide a clean way to separate infrastructure concerns from business logic while ensuring consistent behavior across your application. Keep hooks focused on single responsibilities and consider their performance impact on database operations.