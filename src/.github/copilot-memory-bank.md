# DKNet Framework - Copilot Memory Bank

**Last Updated**: October 27, 2025

This document serves as a memory bank for GitHub Copilot to understand the DKNet framework architecture, patterns, and conventions. It helps Copilot provide better suggestions and maintain consistency across the codebase.

---

## Project Overview

**DKNet** is a comprehensive .NET 9.0 framework providing enterprise-grade libraries for building modern, cloud-native applications using Domain-Driven Design (DDD) and Onion Architecture principles.

**Repository**: https://github.com/baoduy/DKNet  
**Author**: Steven Hoang  
**Company**: https://drunkcoding.net  
**License**: MIT  
**Target Framework**: .NET 9.0  
**Nullable**: Enabled globally

---

## Architecture Principles

### 1. **Domain-Driven Design (DDD)**
- Domain entities are the core of the application
- Entities encapsulate business logic and rules
- Use of Value Objects, Aggregates, and Domain Events
- Clear separation between domain and infrastructure

### 2. **Onion Architecture**
- **Core Layer**: Domain entities, interfaces, abstractions
- **Application Layer**: Business logic, repositories, services
- **Infrastructure Layer**: EF Core, external services
- **Presentation Layer**: APIs, endpoints

### 3. **CQRS (Command Query Responsibility Segregation)**
- Separate read and write operations
- `IRepository<T>` for write operations
- `IReadRepository<T>` for read operations
- SlimBus for command/query handling

### 4. **Clean Code Practices**
- Nullable reference types enabled (`#nullable enable`)
- XML documentation for all public APIs
- .editorconfig enforced across all projects
- Roslyn analyzers enabled (`EnableNETAnalyzers`, `AnalysisMode: All`)
- Meziantou.Analyzer for additional code quality checks

---

## Project Structure

### Core Projects

#### **DKNet.Fw.Extensions**
- Location: `/Core/DKNet.Fw.Extensions/`
- Purpose: Fundamental extensions and utilities
- Features:
  - Type extractors for assembly scanning
  - String extensions
  - Collection extensions
  - Reflection utilities

#### **DKNet.RandomCreator**
- Location: `/Core/DKNet.RandomCreator/`
- Purpose: Random data generation utilities
- Dependencies: AutoBogus, Bogus

### EfCore Projects

#### **DKNet.EfCore.Abstractions**
- Location: `/EfCore/DKNet.EfCore.Abstractions/`
- Purpose: Core entity interfaces and base classes
- Key Interfaces:
  - `IEntity<TKey>`: Base entity with ID
  - `IAuditedProperties`: Audit tracking interface
  - `IAuditedEntity<TKey>`: Combined entity + audit
  - `ISoftDeletableEntity`: Soft delete support
  - `IConcurrencyEntity<TRowVersion>`: Optimistic concurrency
  - `IEventEntity`: Domain events support
- Key Classes:
  - `Entity<TKey>`: Base entity class with generic key
  - `Entity`: Base entity class with Guid key (default)
  - `AuditedEntity<TKey>`: Entity with audit properties
- Attributes:
  - `[IgnoreEntity]`: Skip auto-configuration
  - `[SequenceAttribute]`: Define sequences
  - `[SqlSequenceAttribute]`: SQL Server sequences
  - `[IgnoreAuditLog]`: Exclude from audit logs

#### **DKNet.EfCore.Extensions**
- Location: `/EfCore/DKNet.EfCore.Extensions/`
- Purpose: Enhanced EF Core functionality
- Features:
  - **Auto Entity Configuration**: Automatic entity discovery and configuration
  - **Global Query Filters**: Centralized query filter management
  - **Data Seeding**: Structured seeding with DI support
  - **Default Entity Configuration**: Base config for common patterns
  - **Navigation Extensions**: Enhanced navigation property management
  - **Snapshot Context**: Entity state tracking
  - **Sequence Extensions**: SQL Server sequence support
  - **Pagination**: Async enumeration and paging
- Key Classes:
  - `DefaultEntityTypeConfiguration<TEntity>`: Base configuration class
  - `AutoConfigModelCustomizer`: Automatic model customization
  - `GuidV7ValueGenerator`: GUID v7 generation for entities
  - `SnapshotContext`: Entity change tracking
  - `NavigationExtensions`: Navigation property helpers
- Configuration Interfaces:
  - `IGlobalQueryFilter`: Define global filters
  - `IDataSeedingConfiguration<TEntity>`: Define seed data
- Setup:
  ```csharp
  services.AddDbContext<AppDbContext>(options =>
      options.UseSqlServer(connectionString)
             .UseAutoConfigModel<AppDbContext>());
  ```

#### **DKNet.EfCore.Repos.Abstractions**
- Location: `/EfCore/DKNet.EfCore.Repos.Abstractions/`
- Purpose: Repository pattern abstractions
- Interfaces:
  - `IReadRepository<TEntity>`: Read-only operations
  - `IRepository<TEntity>`: Full CRUD operations
  - Transaction support
  - Specification pattern support

#### **DKNet.EfCore.Repos**
- Location: `/EfCore/DKNet.EfCore.Repos/`
- Purpose: Concrete repository implementations
- Features:
  - Generic repository implementations
  - Automatic DI registration
  - Mapster integration for projections
  - Transaction management
- Setup:
  ```csharp
  services.AddMapster();
  services.AddGenericRepositories<AppDbContext>();
  ```

#### **DKNet.EfCore.Hooks**
- Location: `/EfCore/DKNet.EfCore.Hooks/`
- Purpose: Entity lifecycle hooks
- Features:
  - Pre/Post save hooks
  - Entity change interception
  - Audit trail automation

#### **DKNet.EfCore.Events**
- Location: `/EfCore/DKNet.EfCore.Events/`
- Purpose: Domain events handling
- Features:
  - Event publisher integration
  - Entity event tracking
  - SlimBus integration

#### **DKNet.EfCore.AuditLogs**
- Location: `/EfCore/DKNet.EfCore.AuditLogs/`
- Purpose: Automatic audit logging
- Features:
  - Track entity changes
  - Store before/after values
  - User tracking
  - Timestamp tracking

#### **DKNet.EfCore.DataAuthorization**
- Location: `/EfCore/DKNet.EfCore.DataAuthorization/`
- Purpose: Row-level security
- Features:
  - Tenant-based data isolation
  - User-based data filtering
  - Automatic query filter injection

#### **DKNet.EfCore.DtoGenerator**
- Location: `/EfCore/DKNet.EfCore.DtoGenerator/`
- Purpose: Source generator for DTOs
- Features:
  - Compile-time DTO generation
  - Mapster mapping configuration
  - Projection support

#### **DKNet.EfCore.Relational.Helpers**
- Location: `/EfCore/DKNet.EfCore.Relational.Helpers/`
- Purpose: Relational database utilities
- Features:
  - Table name resolution
  - Primary key utilities
  - Index helpers

#### **DKNet.EfCore.Specifications**
- Location: `/EfCore/DKNet.EfCore.Specifications/`
- Purpose: Specification pattern implementation
- Features:
  - Reusable query logic
  - Composable specifications
  - Type-safe filtering

### SlimBus Projects

#### **DKNet.AspCore.SlimBus**
- Location: `/SlimBus/DKNet.AspCore.SlimBus/`
- Purpose: Lightweight mediator/message bus for ASP.NET Core
- Features:
  - Command/Query handling
  - Request/Response pipeline
  - Middleware support
  - Validation integration
  - Problem Details support

#### **DKNet.SlimBus.Extensions**
- Location: `/SlimBus/DKNet.SlimBus.Extensions/`
- Purpose: SlimBus extensions and utilities
- Features:
  - EF Core integration
  - Auto-save changes
  - Event publishing
  - Transaction support

### Background Tasks

#### **DKNet.AspCore.Tasks**
- Location: `/DKNet.AspCore.Tasks/`
- Purpose: Background job management
- Features:
  - `IBackgroundJob` interface
  - Startup job execution
  - Assembly scanning for job discovery
  - Scoped DI support
- Setup:
  ```csharp
  services.AddBackgroundJob<DataInitializationJob>();
  services.AddBackgroundJobFrom(new[] { typeof(Program).Assembly });
  ```

### Service Projects

#### **DKNet.Svc.BlobStorage.*** (Abstractions, Local, Azure, AWS)
- Location: `/Services/DKNet.Svc.BlobStorage.*/`
- Purpose: Cloud-agnostic blob storage abstraction
- Implementations:
  - Local file system
  - Azure Blob Storage
  - AWS S3

#### **DKNet.Svc.Encryption**
- Location: `/Services/DKNet.Svc.Encryption/`
- Purpose: Encryption/decryption utilities

#### **DKNet.Svc.PdfGenerators**
- Location: `/Services/DKNet.Svc.PdfGenerators/`
- Purpose: PDF generation utilities

#### **DKNet.Svc.Transformation**
- Location: `/Services/DKNet.Svc.Transformation/`
- Purpose: Data transformation utilities

### Aspire Projects

#### **Aspire.Hosting.ServiceBus**
- Location: `/Aspire/Aspire.Hosting.ServiceBus/`
- Purpose: .NET Aspire integration
- Features:
  - Service discovery
  - Health checks
  - Telemetry

---

## Coding Conventions

### Naming Conventions

1. **Project Names**: `DKNet.<Category>.<Component>`
   - Examples: `DKNet.EfCore.Extensions`, `DKNet.AspCore.Tasks`

2. **Namespaces**: Match project structure
   - Base: `DKNet.EfCore.Extensions`
   - Sub: `DKNet.EfCore.Extensions.Configurations`

3. **Entity Classes**: Use descriptive nouns
   - `Entity<TKey>`, `AuditedEntity<TKey>`, `Product`, `Customer`

4. **Interfaces**: Prefix with `I`
   - `IEntity<TKey>`, `IRepository<T>`, `IBackgroundJob`

5. **Extensions**: Suffix with `Extensions`
   - `NavigationExtensions`, `EfCoreExtensions`

6. **Configurations**: Suffix with `Configuration`
   - `DefaultEntityTypeConfiguration<T>`, `ProductConfiguration`

### File Organization

1. **One class per file** (unless tightly coupled)
2. **File name matches class name**: `NavigationExtensions.cs`
3. **Group related files in folders**:
   - `/Extensions/` - Extension methods
   - `/Configurations/` - EF Core configurations
   - `/Entities/` - Domain entities
   - `/Internal/` - Internal implementations
   - `/Snapshots/` - Snapshot utilities

### Code Style

1. **XML Documentation**: Required for all public APIs
   ```csharp
   /// <summary>
   /// Gets the navigation values from the specified object.
   /// </summary>
   /// <param name="obj">The object containing the navigation property.</param>
   /// <param name="navigation">The navigation metadata.</param>
   /// <returns>An enumerable of navigation values.</returns>
   public static IEnumerable<object> GetNavigationValues(this object obj, INavigation navigation)
   ```

2. **Null Checking**: Use modern patterns
   ```csharp
   ArgumentNullException.ThrowIfNull(entry);
   ArgumentNullException.ThrowIfNull(propertyName);
   ```

3. **Collection Initialization**: Use collection expressions
   ```csharp
   return navigation.PropertyInfo.GetValue(obj) as IEnumerable<object> ?? [];
   ```

4. **Pattern Matching**: Use modern C# patterns
   ```csharp
   if (entry.State is EntityState.Modified or EntityState.Deleted) return false;
   ```

5. **Record Types**: Use for DTOs and value objects
   ```csharp
   public record ProductDto(Guid Id, string Name, decimal Price);
   ```

6. **Primary Constructors**: Use when appropriate
   ```csharp
   internal sealed class AutoConfigModelCustomizer(ModelCustomizer original) : IModelCustomizer
   ```

### Entity Design Patterns

1. **Base Entity**: Inherit from `Entity` or `Entity<TKey>`
   ```csharp
   public class Product : Entity
   {
       public string Name { get; private set; }
       public decimal Price { get; private set; }
       
       private Product() { } // EF Core
       
       public Product(string name, decimal price)
       {
           Name = name;
           Price = price;
       }
   }
   ```

2. **Audited Entity**: Inherit from `AuditedEntity<TKey>`
   ```csharp
   public class Customer : AuditedEntity<Guid>
   {
       public string Name { get; private set; }
       // Inherits: CreatedBy, CreatedOn, UpdatedBy, UpdatedOn
   }
   ```

3. **Soft Delete**: Implement `ISoftDeletableEntity`
   ```csharp
   public class Order : AuditedEntity<Guid>, ISoftDeletableEntity
   {
       public bool IsDeleted { get; set; }
       public DateTimeOffset? DeletedOn { get; set; }
   }
   ```

4. **Domain Events**: Use `IEventEntity` (already in base `Entity<TKey>`)
   ```csharp
   var product = new Product("Widget", 19.99m);
   product.AddEvent(new ProductCreatedEvent(product.Id));
   ```

### Repository Pattern

DKNet uses separate interfaces for read and write operations following CQRS principles.

#### **Repository Interfaces**:

```csharp
// Read-only operations (no change tracking)
public interface IReadRepository<TEntity> where TEntity : class
{
    // Get queryable for custom queries
    IQueryable<TEntity> Query();
    IQueryable<TEntity> Query(Expression<Func<TEntity, bool>> filter);
    
    // Get queryable with projection (Mapster)
    IQueryable<TModel> Query<TModel>(Expression<Func<TEntity, bool>> filter) where TModel : class;
    
    // Find by key
    ValueTask<TEntity?> FindAsync(object keyValue, CancellationToken cancellationToken = default);
    ValueTask<TEntity?> FindAsync(object[] keyValues, CancellationToken cancellationToken = default);
    
    // Find by filter
    Task<TEntity?> FindAsync(Expression<Func<TEntity, bool>> filter, CancellationToken cancellationToken = default);
    
    // Existence check
    Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> filter, CancellationToken cancellationToken = default);
    
    // Count
    Task<int> CountAsync(Expression<Func<TEntity, bool>> filter, CancellationToken cancellationToken = default);
}

// Write operations (with change tracking)
public interface IWriteRepository<TEntity> where TEntity : class
{
    // Entry access
    EntityEntry<TEntity> Entry(TEntity entity);
    
    // Transaction
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    
    // Save changes
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    
    // Add operations
    ValueTask AddAsync(TEntity entity, CancellationToken cancellationToken = default);
    ValueTask AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);
    
    // Update operations
    Task<int> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);
    Task UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);
    
    // Delete operations
    void Delete(TEntity entity);
    void DeleteRange(IEnumerable<TEntity> entities);
}

// Combined interface (read + write)
public interface IRepository<TEntity> : IReadRepository<TEntity>, IWriteRepository<TEntity>
    where TEntity : class;
```

#### **Usage Examples**:

**1. Read Operations (Query Service)**:
```csharp
public class ProductQueryService
{
    private readonly IReadRepository<Product> _repository;
    
    public ProductQueryService(IReadRepository<Product> repository)
    {
        _repository = repository;
    }
    
    // Simple query with projection
    public async Task<List<ProductDto>> GetActiveProductsAsync()
    {
        return await _repository
            .Query(p => !p.IsDeleted && p.IsActive)
            .ProjectToType<ProductDto>() // Mapster projection
            .ToListAsync();
    }
    
    // Direct projection query
    public async Task<List<ProductDto>> GetProductsByCategoryAsync(Guid categoryId)
    {
        return await _repository
            .Query<ProductDto>(p => p.CategoryId == categoryId)
            .ToListAsync();
    }
    
    // Find by ID
    public async Task<ProductDto?> GetProductByIdAsync(Guid id)
    {
        var product = await _repository.FindAsync(id);
        return product?.Adapt<ProductDto>();
    }
    
    // Find with filter
    public async Task<Product?> GetProductBySkuAsync(string sku)
    {
        return await _repository.FindAsync(p => p.Sku == sku);
    }
    
    // Check existence
    public async Task<bool> IsSkuUniqueAsync(string sku)
    {
        return !await _repository.ExistsAsync(p => p.Sku == sku);
    }
    
    // Get count
    public async Task<int> GetActiveProductCountAsync()
    {
        return await _repository.CountAsync(p => !p.IsDeleted && p.IsActive);
    }
    
    // Complex query with includes
    public async Task<List<ProductDto>> SearchProductsAsync(string searchTerm)
    {
        return await _repository
            .Query()
            .Include(p => p.Category)
            .Include(p => p.Supplier)
            .Where(p => p.Name.Contains(searchTerm) || p.Description.Contains(searchTerm))
            .AsNoTracking() // No change tracking for read
            .ProjectToType<ProductDto>()
            .ToListAsync();
    }
}
```

**2. Write Operations (Command Service)**:
```csharp
public class ProductCommandService
{
    private readonly IRepository<Product> _repository;
    private readonly ILogger<ProductCommandService> _logger;
    
    public ProductCommandService(
        IRepository<Product> repository,
        ILogger<ProductCommandService> logger)
    {
        _repository = repository;
        _logger = logger;
    }
    
    // Create
    public async Task<Result<Product>> CreateProductAsync(CreateProductCommand command)
    {
        // Validate uniqueness
        if (await _repository.ExistsAsync(p => p.Sku == command.Sku))
            return Result.Fail<Product>("SKU already exists");
        
        var product = new Product(command.Name, command.Price, command.Sku);
        
        await _repository.AddAsync(product);
        await _repository.SaveChangesAsync();
        
        _logger.LogInformation("Created product {ProductId}", product.Id);
        return Result.Ok(product);
    }
    
    // Batch create
    public async Task<Result> CreateProductsAsync(IEnumerable<Product> products)
    {
        await _repository.AddRangeAsync(products);
        var count = await _repository.SaveChangesAsync();
        
        _logger.LogInformation("Created {Count} products", count);
        return Result.Ok();
    }
    
    // Update
    public async Task<Result<Product>> UpdateProductAsync(Guid id, UpdateProductCommand command)
    {
        var product = await _repository.FindAsync(id);
        if (product == null)
            return Result.Fail<Product>("Product not found");
        
        product.UpdateName(command.Name);
        product.UpdatePrice(command.Price);
        
        await _repository.UpdateAsync(product);
        
        _logger.LogInformation("Updated product {ProductId}", id);
        return Result.Ok(product);
    }
    
    // Update with concurrency handling
    public async Task<Result> UpdateProductPriceAsync(Guid id, decimal newPrice, byte[] rowVersion)
    {
        try
        {
            var product = await _repository.FindAsync(id);
            if (product == null)
                return Result.Fail("Product not found");
            
            // Check concurrency
            var entry = _repository.Entry(product);
            entry.Property(p => p.RowVersion).OriginalValue = rowVersion;
            
            product.UpdatePrice(newPrice);
            await _repository.SaveChangesAsync();
            
            return Result.Ok();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Fail("Product was modified by another user");
        }
    }
    
    // Delete
    public async Task<Result> DeleteProductAsync(Guid id)
    {
        var product = await _repository.FindAsync(id);
        if (product == null)
            return Result.Fail("Product not found");
        
        _repository.Delete(product);
        await _repository.SaveChangesAsync();
        
        _logger.LogInformation("Deleted product {ProductId}", id);
        return Result.Ok();
    }
    
    // Soft delete
    public async Task<Result> SoftDeleteProductAsync(Guid id)
    {
        var product = await _repository.FindAsync(id);
        if (product == null)
            return Result.Fail("Product not found");
        
        product.MarkAsDeleted(); // Assuming ISoftDeletableEntity
        await _repository.SaveChangesAsync();
        
        return Result.Ok();
    }
    
    // Transaction example
    public async Task<Result> TransferStockAsync(Guid fromProductId, Guid toProductId, int quantity)
    {
        using var transaction = await _repository.BeginTransactionAsync();
        
        try
        {
            var fromProduct = await _repository.FindAsync(fromProductId);
            var toProduct = await _repository.FindAsync(toProductId);
            
            if (fromProduct == null || toProduct == null)
                return Result.Fail("Product not found");
            
            if (fromProduct.Stock < quantity)
                return Result.Fail("Insufficient stock");
            
            fromProduct.DecreaseStock(quantity);
            toProduct.IncreaseStock(quantity);
            
            await _repository.SaveChangesAsync();
            await transaction.CommitAsync();
            
            return Result.Ok();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to transfer stock");
            return Result.Fail("Transfer failed");
        }
    }
}
```

**3. Combined Repository (CRUD Service)**:
```csharp
public class ProductService
{
    private readonly IRepository<Product> _repository; // Has both read and write
    
    public async Task<Product?> GetByIdAsync(Guid id) => 
        await _repository.FindAsync(id);
    
    public async Task<List<ProductDto>> GetAllAsync() =>
        await _repository.Query().ProjectToType<ProductDto>().ToListAsync();
    
    public async Task<Result<Product>> CreateAsync(Product product)
    {
        await _repository.AddAsync(product);
        await _repository.SaveChangesAsync();
        return Result.Ok(product);
    }
    
    public async Task<Result> UpdateAsync(Product product)
    {
        await _repository.UpdateAsync(product);
        return Result.Ok();
    }
    
    public async Task<Result> DeleteAsync(Guid id)
    {
        var product = await _repository.FindAsync(id);
        if (product == null) return Result.Fail("Not found");
        
        _repository.Delete(product);
        await _repository.SaveChangesAsync();
        return Result.Ok();
    }
}
```

#### **Repository Registration**:

```csharp
// Automatic registration of all repositories
services.AddDbContext<AppDbContext>(options => 
    options.UseSqlServer(connectionString));

services.AddMapster(); // For projections
services.AddGenericRepositories<AppDbContext>(); // Registers IRepository<T> for all entities

// Manual registration (if needed)
services.AddScoped<IReadRepository<Product>, ReadRepository<Product>>();
services.AddScoped<IRepository<Product>, Repository<Product>>();
```

#### **Best Practices**:

1. **Use `IReadRepository<T>` for queries**:
   - No change tracking overhead
   - Clear intent (read-only)
   - Better performance
   - Use `AsNoTracking()` explicitly for complex queries

2. **Use `IRepository<T>` for commands**:
   - Change tracking enabled
   - Supports Update/Delete
   - Transaction management

3. **Projections with Mapster**:
   - Use `ProjectToType<TDto>()` for efficient queries
   - Define mappings in startup or mapping profiles
   - Avoid loading full entities when only DTOs needed

4. **Async all the way**:
   - Always use async methods
   - Pass CancellationToken through the stack

5. **Error handling**:
   - Use FluentResults for domain errors
   - Let exceptions bubble for infrastructure errors
   - Log appropriately

6. **Transactions**:
   - Use `BeginTransactionAsync()` for multi-operation transactions
   - Always commit/rollback explicitly
   - Use `using` statement for automatic disposal

### Configuration Pattern

1. **Entity Configuration**: Inherit from `DefaultEntityTypeConfiguration<T>`
   ```csharp
   public class ProductConfiguration : DefaultEntityTypeConfiguration<Product>
   {
       public override void Configure(EntityTypeBuilder<Product> builder)
       {
           base.Configure(builder); // Apply default configs
           
           builder.Property(p => p.Name)
               .HasMaxLength(255)
               .IsRequired();
               
           builder.Property(p => p.Price)
               .HasPrecision(18, 2);
       }
   }
   ```

2. **Global Query Filters**: Implement `IGlobalQueryFilter`
   ```csharp
   public class SoftDeleteQueryFilter : IGlobalQueryFilter
   {
       public void Apply(ModelBuilder modelBuilder, DbContext context)
       {
           foreach (var entityType in modelBuilder.Model.GetEntityTypes())
           {
               if (typeof(ISoftDeletableEntity).IsAssignableFrom(entityType.ClrType))
               {
                   modelBuilder.Entity(entityType.ClrType)
                       .HasQueryFilter(BuildPredicate(entityType.ClrType));
               }
           }
       }
   }
   ```

3. **Data Seeding**: Implement `DataSeedingConfiguration<T>`
   
   **See detailed Data Seeding section below** ↓

---

## Advanced Features

### Data Seeding (EF Core 9+)

DKNet supports **two types of data seeding**:

#### **1. Static Model-Based Seeding** (Applied during migrations)

```csharp
public class UserSeedingConfiguration : DataSeedingConfiguration<User>
{
    protected override ICollection<User> HasData =>
    [
        new(1, "system") { FirstName = "Admin", LastName = "User" },
        new(2, "system") { FirstName = "Test", LastName = "User" }
    ];
}
```

#### **2. Dynamic Runtime Seeding** (Executed after database creation)

```csharp
public class UserSeedingConfiguration : DataSeedingConfiguration<User>
{
    public override Func<DbContext, bool, CancellationToken, Task>? SeedAsync { get; } = 
        async (context, performedStoreOperation, cancellationToken) =>
        {
            // Only seed if database was just created
            if (!performedStoreOperation) return;
            
            var users = context.Set<User>();
            if (await users.AnyAsync(cancellationToken)) return;
            
            // Add dynamic seed data
            await users.AddRangeAsync(
                new[] { new User("system") { FirstName = "Dynamic", LastName = "User" } },
                cancellationToken
            );
            await context.SaveChangesAsync(cancellationToken);
        };
}
```

#### **Setup**:
```csharp
services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString)
           .UseAutoConfigModel() // Auto-discovers seeding from DbContext assembly
           .UseAutoDataSeeding(new[] { typeof(UserSeedingConfiguration).Assembly })); // Explicit assembly
```

#### **Key Concepts**:
- `HasData`: Static data included in migrations (like `modelBuilder.Entity().HasData()`)
- `SeedAsync`: Runtime seeding hook executed after `EnsureCreated()` or migration
- `performedStoreOperation`: True if database was just created/migrated
- Automatically discovered from assemblies during `UseAutoConfigModel()`
- Based on EF Core 9's `UseSeeding` and `UseAsyncSeeding` features

### Entity Lifecycle Hooks

**Package**: `DKNet.EfCore.Hooks`

Hooks provide interception points for cross-cutting concerns like auditing, validation, caching, and event publishing.

#### **Hook Types**:

```csharp
// Before save hook
public interface IBeforeSaveHookAsync : IHookBaseAsync
{
    Task BeforeSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default);
}

// After save hook
public interface IAfterSaveHookAsync : IHookBaseAsync
{
    Task AfterSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default);
}

// Combined hook
public interface IHookAsync : IBeforeSaveHookAsync, IAfterSaveHookAsync;
```

#### **Hook Implementation Examples**:

**Audit Hook**:
```csharp
public class AuditHook : IBeforeSaveHookAsync
{
    private readonly ICurrentUserService _currentUserService;

    public AuditHook(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    public Task BeforeSaveAsync(SnapshotContext snapshot, CancellationToken cancellationToken = default)
    {
        var currentUser = _currentUserService.UserId;
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in snapshot.Entries)
        {
            if (entry.Entity is IAuditedEntity auditedEntity)
            {
                if (entry.State == EntityState.Added)
                    auditedEntity.SetCreatedBy(currentUser, now);
                else if (entry.State == EntityState.Modified)
                    auditedEntity.SetUpdatedBy(currentUser, now);
            }
        }

        return Task.CompletedTask;
    }
}
```

**Event Publishing Hook**:
```csharp
public class EventPublishingHook : IAfterSaveHookAsync
{
    private readonly IEventPublisher _eventPublisher;

    public EventPublishingHook(IEventPublisher eventPublisher)
    {
        _eventPublisher = eventPublisher;
    }

    public async Task AfterSaveAsync(SnapshotContext snapshot, CancellationToken cancellationToken = default)
    {
        foreach (var entry in snapshot.Entries)
        {
            if (entry.Entity is IEventEntity eventEntity)
            {
                var (events, eventTypes) = eventEntity.GetEventsAndClear();
                
                foreach (var evt in events)
                    await _eventPublisher.PublishAsync(evt, cancellationToken);
                    
                foreach (var eventType in eventTypes)
                    await _eventPublisher.PublishAsync(eventType, cancellationToken);
            }
        }
    }
}
```

**Combined Hook**:
```csharp
public class ValidationHook : HookAsync // Base class implements both interfaces
{
    public override async Task BeforeSaveAsync(SnapshotContext snapshot, CancellationToken cancellationToken = default)
    {
        foreach (var entry in snapshot.Entries.Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            // Validate entity
            var validationContext = new ValidationContext(entry.Entity);
            Validator.ValidateObject(entry.Entity, validationContext, validateAllProperties: true);
        }
    }
    
    public override async Task AfterSaveAsync(SnapshotContext snapshot, CancellationToken cancellationToken = default)
    {
        // Clear cache after save
        await _cache.InvalidateAsync(cancellationToken);
    }
}
```

#### **Setup**:
```csharp
services.AddDbContext<AppDbContext>(options => 
    options.UseSqlServer(connectionString)
           .UseAutoConfigModel());

// Register hooks
services.AddScoped<IBeforeSaveHookAsync, AuditHook>();
services.AddScoped<IAfterSaveHookAsync, EventPublishingHook>();
services.AddScoped<IHookAsync, ValidationHook>();

// Enable hooks for DbContext
services.AddEfCoreHooks<AppDbContext>();
```

#### **SnapshotContext Features**:
- `Entries`: Read-only collection of changed entities (Added, Modified, Deleted)
- `DbContext`: Access to the underlying DbContext
- Automatic change tracking detection
- Entity state snapshots (original vs current values)

### Specification Pattern

**Package**: `DKNet.EfCore.Specifications`

Reusable, composable query logic for filtering, sorting, and including related entities.

#### **Basic Specification**:

```csharp
public class ActiveProductsSpec : Specification<Product>
{
    public ActiveProductsSpec()
    {
        // Filter
        AddFilter(p => !p.IsDeleted && p.IsActive);
        
        // Include related entities
        AddInclude(p => p.Category);
        AddInclude(p => p.Supplier);
        
        // Ordering
        AddOrderByDescending(p => p.CreatedOn);
        AddOrderBy(p => p.Name);
    }
}
```

#### **Parameterized Specification**:

```csharp
public class ProductsByCategorySpec : Specification<Product>
{
    public ProductsByCategorySpec(Guid categoryId, decimal? minPrice = null, decimal? maxPrice = null)
    {
        var predicate = CreatePredicate(p => p.CategoryId == categoryId);
        
        if (minPrice.HasValue)
            predicate = predicate.And(p => p.Price >= minPrice.Value);
            
        if (maxPrice.HasValue)
            predicate = predicate.And(p => p.Price <= maxPrice.Value);
        
        AddFilter(predicate);
        AddInclude(p => p.Category);
        AddOrderBy(p => p.Name);
    }
}
```

#### **Composable Specifications**:

```csharp
public class ProductsInStockSpec : Specification<Product>
{
    public ProductsInStockSpec()
    {
        AddFilter(p => p.Stock > 0);
    }
}

public class DiscountedProductsSpec : Specification<Product>
{
    public DiscountedProductsSpec()
    {
        AddFilter(p => p.DiscountPercentage > 0);
    }
}

// Combine specifications
var spec = new ProductsInStockSpec();
var discountedSpec = new DiscountedProductsSpec();
// Use with repository...
```

#### **Advanced Features**:

```csharp
public class PaginatedProductsSpec : Specification<Product>
{
    public PaginatedProductsSpec(int page, int pageSize, string? searchTerm = null)
    {
        var predicate = CreatePredicate();
        
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            predicate = predicate.And(p => 
                p.Name.Contains(searchTerm) || 
                p.Description.Contains(searchTerm));
        }
        
        AddFilter(predicate);
        
        // Ignore global query filters (e.g., soft delete)
        IgnoreQueryFiltersEnabled();
        
        // Paging is handled by repository
    }
}
```

#### **Usage with Repository**:

```csharp
public class ProductService
{
    private readonly IReadRepository<Product> _repository;
    
    public async Task<List<ProductDto>> GetActiveProductsAsync()
    {
        var spec = new ActiveProductsSpec();
        return await _repository
            .Query()
            .ApplySpecification(spec)
            .ProjectToType<ProductDto>()
            .ToListAsync();
    }
    
    public async Task<List<Product>> SearchProductsAsync(string searchTerm, decimal? minPrice, decimal? maxPrice)
    {
        var spec = new ProductSearchSpec(searchTerm, minPrice, maxPrice);
        return await _repository
            .Query()
            .ApplySpecification(spec)
            .ToListAsync();
    }
}
```

#### **Specification Methods**:

- `AddFilter(Expression<Func<T, bool>>)`: Add WHERE clause
- `AddInclude(Expression<Func<T, object>>)`: Add INCLUDE for eager loading
- `AddOrderBy(Expression<Func<T, object>>)`: Add ascending sort
- `AddOrderByDescending(Expression<Func<T, object>>)`: Add descending sort
- `IgnoreQueryFiltersEnabled()`: Bypass global query filters
- `CreatePredicate()`: Start building composable predicate with LinqKit

### DbContext Utility Extensions

#### **Table and Key Information**:

```csharp
// Get table name for entity
var tableName = context.GetTableName(typeof(Product));
// Returns: "[dbo].[Products]"

// Get primary key properties
var keys = context.GetPrimaryKeyProperties<Product>();
// Returns: ["Id"]

// Get primary key values of entity instance
var product = new Product { Id = Guid.NewGuid(), Name = "Widget" };
var keyValues = context.GetPrimaryKeyValues(product);
// Returns: { ["Id"] = Guid }

// Get key values from entity entry
var entry = context.Entry(product);
var entryKeyValues = entry.GetEntityKeyValues();
// Returns: Dictionary<string, object?>
```

#### **SQL Server Sequences**:

Define sequences on enums:
```csharp
[SqlSequence] // Uses default schema "dbo"
public enum DocumentSequences
{
    [Sequence(typeof(long), IncrementsBy = 1, Max = long.MaxValue)]
    Invoice,
    
    [Sequence(typeof(int), FormatString = "PO{DateTime:yyMMdd}{1:00000}", IncrementsBy = 1, Max = 99999)]
    PurchaseOrder,
    
    [Sequence(typeof(short), IncrementsBy = 1, Max = short.MaxValue)]
    Receipt
}
```

Usage:
```csharp
// Get next sequence value
var nextInvoiceNumber = await context.NextSeqValue<DocumentSequences, long>(DocumentSequences.Invoice);

// Get formatted sequence value (includes FormatString)
var nextPO = await context.NextSeqValue<DocumentSequences>(DocumentSequences.PurchaseOrder);
// Returns: "PO2510270001" (format: PO + yyMMdd + sequence)

// Use in entities
public class Invoice : Entity
{
    public long InvoiceNumber { get; private set; }
    
    public async Task AssignInvoiceNumberAsync(DbContext context)
    {
        InvoiceNumber = await context.NextSeqValue<DocumentSequences, long>(DocumentSequences.Invoice) ?? 0;
    }
}
```

Sequence attributes:
- `[SqlSequence]`: Marks enum for SQL Server sequence registration
- `[Sequence(Type, FormatString, IncrementsBy, StartsWith, Min, Max)]`:
  - `Type`: Return type (int, long, short, etc.)
  - `FormatString`: Optional formatting (supports {DateTime} and {1} for sequence value)
  - `IncrementsBy`: Step size (default: 1)
  - `StartsWith`: Starting value (default: 1)
  - `Min/Max`: Range constraints

**Auto-registration**:
Sequences are automatically created when using `UseAutoConfigModel()` with SQL Server.

### Navigation Property Utilities

```csharp
// Get navigation values from entity
var order = context.Orders.Include(o => o.OrderItems).First();
var navigation = context.Model.FindEntityType(typeof(Order))!.FindNavigation("OrderItems")!;
var items = order.GetNavigationValues(navigation);
// Returns: IEnumerable<OrderItem>

// Get original key values from entity entry
var entry = context.Entry(order);
var originalKeys = entry.GetOriginalKeyValues();

// Get current key values
var currentKeys = entry.GetCurrentKeyValues();

// Get property values
var currentValue = entry.GetCurrentValue("Total");
var originalValue = entry.GetOriginalValue("Total");

// Check if property exists
if (entry.HasProperty("DiscountAmount"))
{
    var discount = entry.GetCurrentValue("DiscountAmount");
}

// Check if entity is new
bool isNew = entry.IsNewEntity();

// Get all collection navigations for entity type
var navigations = context.GetCollectionNavigations(typeof(Order));

// Get new entities from navigations
var newEntities = context.GetNewEntitiesFromNavigations();

// Add new entities from navigations to context
context.AddNewEntitiesFromNavigations();
```

### Snapshot Context

Track entity changes with before/after comparison:

```csharp
using var snapshot = new SnapshotContext(context);

// Snapshot is taken at construction time
// Context change tracking is preserved

// Make changes
var product = await context.Products.FindAsync(productId);
product.UpdatePrice(newPrice);
product.UpdateStock(newQuantity);

await context.SaveChangesAsync();

// Access snapshot entries
foreach (var entry in snapshot.Entries)
{
    Console.WriteLine($"Entity: {entry.EntityType.Name}");
    Console.WriteLine($"State: {entry.State}");
    Console.WriteLine($"Original Values: {entry.OriginalValues}");
    Console.WriteLine($"Current Values: {entry.CurrentValues}");
    
    // Check specific property changes
    if (entry.HasProperty("Price"))
    {
        var oldPrice = entry.GetOriginalValue("Price");
        var newPrice = entry.GetCurrentValue("Price");
        Console.WriteLine($"Price changed from {oldPrice} to {newPrice}");
    }
}

// Snapshot is automatically disposed (implements IAsyncDisposable)
```

**Key Features**:
- Captures entity state at snapshot time
- Provides read-only access to changed entities
- Only includes Added, Modified, and Deleted entities
- Access to original and current values
- Property-level change detection
- Automatically disposes (implements `IDisposable` and `IAsyncDisposable`)

**Used by Hooks**:
The `SnapshotContext` is automatically created and passed to hooks during `SaveChanges` operations.

### SlimBus CQRS Pattern

**Packages**: 
- `DKNet.AspCore.SlimBus` - ASP.NET Core integration
- `DKNet.SlimBus.Extensions` - EF Core integration

SlimBus provides a lightweight mediator pattern for CQRS (Command Query Responsibility Segregation).

#### **Message Contracts**:

```csharp
using DKNet.SlimBus.Fluents;

// Query with response
public record GetProductQuery(Guid Id) : IWithResponse<ProductResult>;

// Paged query
public record GetProductsPageQuery(int Page, int PageSize, string? SearchTerm) 
    : IWithPageResponse<ProductResult>;

// Command with response (Create/Update)
public record CreateProductCommand(string Name, decimal Price) : IWithResponse<ProductResult>;

// Command without response (Delete)
public record DeleteProductCommand(Guid Id) : INoResponse;

// Response DTOs
public record ProductResult(Guid Id, string Name, decimal Price, string Category);
```

#### **Handlers**:

```csharp
// Query handler
public class GetProductQueryHandler : IRequestHandler<GetProductQuery, Result<ProductResult>>
{
    private readonly IReadRepository<Product> _repository;

    public async Task<Result<ProductResult>> Handle(
        GetProductQuery request, 
        CancellationToken cancellationToken)
    {
        var product = await _repository.FindAsync(request.Id, cancellationToken);
        
        if (product == null)
            return Result.Fail<ProductResult>($"Product {request.Id} not found");
            
        return Result.Ok(new ProductResult(
            product.Id, 
            product.Name, 
            product.Price, 
            product.Category.Name));
    }
}

// Paged query handler
public class GetProductsPageQueryHandler 
    : IRequestHandler<GetProductsPageQuery, Result<IPagedList<ProductResult>>>
{
    private readonly IReadRepository<Product> _repository;

    public async Task<Result<IPagedList<ProductResult>>> Handle(
        GetProductsPageQuery request, 
        CancellationToken cancellationToken)
    {
        var query = _repository.Query();
        
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            query = query.Where(p => p.Name.Contains(request.SearchTerm));
            
        var pagedList = await query
            .ProjectToType<ProductResult>()
            .ToPagedListAsync(request.Page, request.PageSize, cancellationToken);
            
        return Result.Ok(pagedList);
    }
}

// Command handler with response
public class CreateProductCommandHandler 
    : IRequestHandler<CreateProductCommand, Result<ProductResult>>
{
    private readonly IRepository<Product> _repository;

    public async Task<Result<ProductResult>> Handle(
        CreateProductCommand request, 
        CancellationToken cancellationToken)
    {
        var product = new Product(request.Name, request.Price);
        
        await _repository.AddAsync(product, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        
        return Result.Ok(new ProductResult(product.Id, product.Name, product.Price, ""));
    }
}

// Command handler without response
public class DeleteProductCommandHandler : IRequestHandler<DeleteProductCommand, Result>
{
    private readonly IRepository<Product> _repository;

    public async Task<Result> Handle(
        DeleteProductCommand request, 
        CancellationToken cancellationToken)
    {
        var product = await _repository.FindAsync(request.Id, cancellationToken);
        
        if (product == null)
            return Result.Fail($"Product {request.Id} not found");
            
        _repository.Delete(product);
        await _repository.SaveChangesAsync(cancellationToken);
        
        return Result.Ok();
    }
}
```

#### **ASP.NET Core Minimal API Integration**:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Configure SlimBus
builder.Services.AddSlimBusForEfCore(b =>
{
    b.WithProviderMemory() // or .WithProviderAzureServiceBus(), etc.
     .AutoDeclareFrom(typeof(GetProductQuery).Assembly)
     .AddJsonSerializer();
});

var app = builder.Build();
var products = app.MapGroup("/api/products").WithTags("Products");

// Map query endpoints
products
    .MapGet<GetProductQuery, ProductResult>("/{id}")
    .WithName("GetProduct")
    .ProducesCommons(); // Adds 200, 400, 404, 500 responses

products
    .MapGetPage<GetProductsPageQuery, ProductResult>("/")
    .WithName("GetProductsPage")
    .ProducesCommons();

// Map command endpoints
products
    .MapPost<CreateProductCommand, ProductResult>("/")
    .WithName("CreateProduct")
    .RequireAuthorization()
    .ProducesCommons();

products
    .MapPut<UpdateProductCommand, ProductResult>("/{id}")
    .WithName("UpdateProduct")
    .RequireAuthorization()
    .ProducesCommons();

products
    .MapDelete<DeleteProductCommand>("/{id}")
    .WithName("DeleteProduct")
    .RequireAuthorization()
    .ProducesCommons();

app.Run();
```

#### **SlimBus Features**:

**1. One-Liner Endpoint Mapping**:
- `MapGet<TQuery, TResponse>` - GET with single result (404 if null)
- `MapGetPage<TQuery, TItem>` - GET with paged results
- `MapPost<TCommand, TResponse>` - POST with 201 Created
- `MapPost<TCommand>` - POST with 200 OK
- `MapPut<TCommand, TResponse>` - PUT with 200 OK
- `MapPatch<TCommand, TResponse>` - PATCH with 200 OK
- `MapDelete<TCommand>` - DELETE with 200 OK

**2. Automatic Response Shaping**:
- FluentResults integration
- ProblemDetails for errors
- Standardized HTTP status codes
- Automatic validation errors formatting

**3. ProducesCommons()**:
Adds standard OpenAPI responses:
- 200 OK
- 400 Bad Request (validation errors)
- 404 Not Found
- 500 Internal Server Error

**4. PagedResult Wrapper**:
```csharp
public class PagedResult<T>
{
    public IEnumerable<T> Items { get; set; }
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public bool HasPreviousPage { get; set; }
    public bool HasNextPage { get; set; }
}
```

**5. EF Core Integration** (`DKNet.SlimBus.Extensions`):
- Auto SaveChanges after command handlers
- Transaction support
- Event publishing integration
- Audit hook integration

**Setup with EF Core**:
```csharp
services.AddSlimBusForEfCore<AppDbContext>(config =>
{
    config.WithProviderMemory()
          .AutoDeclareFrom(typeof(Program).Assembly)
          .AddJsonSerializer()
          .AddValidation() // FluentValidation integration
          .AddAutoSaveChanges(); // Automatic SaveChanges
});
```

#### **Validation Integration**:

```csharp
using FluentValidation;

public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(255);
            
        RuleFor(x => x.Price)
            .GreaterThan(0)
            .LessThanOrEqualTo(999999);
    }
}

// Automatically invoked before handler
// Returns 400 Bad Request with validation errors if invalid
```

#### **FluentResults Usage**:

```csharp
// Success
return Result.Ok(productResult);

// Success without value
return Result.Ok();

// Failure
return Result.Fail("Product not found");
return Result.Fail<ProductResult>("Invalid product data");

// Multiple errors
return Result.Fail("Error 1").WithError("Error 2");

// With reasons
return Result.Fail(new NotFoundError("Product not found"));

// Combining results
var result1 = await Operation1();
if (result1.IsFailed) return result1;

var result2 = await Operation2();
return result2;
```

#### **ProblemDetails Response Example**:

When a handler returns `Result.Fail()`, the response is automatically converted to ProblemDetails:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Bad Request",
  "status": 400,
  "errors": {
    "Name": ["Product name is required"],
    "Price": ["Price must be greater than 0"]
  },
  "traceId": "00-abc123..."
}
```

---

## Testing Conventions

### Test Project Naming

- Format: `<ProjectName>.Tests`
- Examples: `EfCore.Extensions.Tests`, `EfCore.Repos.Tests`

### Test Class Naming

- Format: `<ClassUnderTest>Tests`
- Examples: `NavigationExtensionsTests`, `AuditEntityTests`

### Test Method Naming

- Pattern: `MethodName_Scenario_ExpectedResult`
- Examples:
  - `GetNavigationValues_WhenNavigationIsNull_ThrowsArgumentNullException`
  - `IsNewEntity_WhenEntityIsAdded_ReturnsTrue`
  - `SetCreatedBy_SetsCreatedByAndCreatedOn_Successfully`

### Test Structure

1. **Arrange-Act-Assert** pattern
2. **Use xUnit** as test framework
3. **Use FluentAssertions** for assertions
4. **Use AutoBogus/Bogus** for test data generation
5. **Use InMemory database** for integration tests

Example:
```csharp
[Fact]
public void IsNewEntity_WhenEntityIsAdded_ReturnsTrue()
{
    // Arrange
    using var context = new TestDbContext();
    var entity = new TestEntity();
    context.Add(entity);
    var entry = context.Entry(entity);
    
    // Act
    var result = entry.IsNewEntity();
    
    // Assert
    result.Should().BeTrue();
}
```

### Test Fixtures

- Use `IClassFixture<T>` for shared setup
- Create fixture classes in `/Fixtures/` folder
- Dispose resources properly

---

## Common Extension Methods

### NavigationExtensions

Located: `/EfCore/DKNet.EfCore.Extensions/Extensions/NavigationExtensions.cs`

Key methods:
- `GetNavigationValues(object, INavigation)`: Get navigation property values
- `GetOriginalKeyValues(EntityEntry)`: Get original PK values
- `GetCurrentKeyValues(EntityEntry)`: Get current PK values
- `GetCurrentValue(EntityEntry, string)`: Get current property value
- `GetOriginalValue(EntityEntry, string)`: Get original property value
- `HasProperty(EntityEntry, string)`: Check if property exists
- `IsNewEntity(EntityEntry)`: Check if entity is new
- `GetCollectionNavigations(DbContext, Type)`: Get collection navigations
- `GetNewEntitiesFromNavigations(DbContext)`: Get new entities from navigations
- `AddNewEntitiesFromNavigations(DbContext)`: Add new entities to context

### EfCoreExtensions

Common utilities for DbContext operations:
- Table name resolution
- Primary key utilities
- Model metadata access

### SequenceExtensions

SQL Server sequence support:
- Sequence creation
- Next value retrieval
- Sequence management

---

## Dependency Injection Patterns

### Service Registration

1. **DbContext Registration**:
   ```csharp
   services.AddDbContext<AppDbContext>(options =>
       options.UseSqlServer(connectionString)
              .UseAutoConfigModel<AppDbContext>());
   ```

2. **Repository Registration**:
   ```csharp
   services.AddMapster();
   services.AddGenericRepositories<AppDbContext>();
   ```

3. **Global Query Filters**:
   ```csharp
   services.AddGlobalQueryFilter<SoftDeleteQueryFilter>();
   ```

4. **Background Jobs**:
   ```csharp
   services.AddBackgroundJob<DataInitializationJob>();
   services.AddBackgroundJobFrom(new[] { typeof(Program).Assembly });
   ```

---

## Package Management

### Central Package Management

- Uses `Directory.Packages.props` for version management
- All versions centralized in one location
- Projects reference packages without versions:
  ```xml
  <PackageReference Include="Microsoft.EntityFrameworkCore" />
  ```

### Common Dependencies

- **EF Core**: 9.0+
- **Mapster**: Latest
- **FluentValidation**: 12.0.0
- **FluentResults**: 4.0.0
- **AutoBogus/Bogus**: Latest
- **xUnit**: Latest
- **FluentAssertions**: Latest

---

## Build & Package Settings

### Common Properties (Directory.Packages.props)

```xml
<PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>true</ImplicitUsings>
    <LangVersion>default</LangVersion>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <AnalysisMode>All</AnalysisMode>
</PropertyGroup>
```

### NuGet Package Settings

```xml
<PropertyGroup>
    <IsPackable>true</IsPackable>
    <Authors>Steven Hoang</Authors>
    <Company>https://drunkcoding.net</Company>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://github.com/baoduy/DKNet</PackageProjectUrl>
    <RepositoryUrl>https://github.com/baoduy/DKNet</RepositoryUrl>
    <PackageIcon>NugetLogo.png</PackageIcon>
    <PackageReadmeFile>README.md</PackageReadmeFile>
</PropertyGroup>
```

---

## Documentation Standards

### README.md Structure

Each project should have:
1. **Badges**: NuGet, downloads, .NET version, license
2. **Description**: One-line purpose statement
3. **Features**: Bullet list of key features
4. **Supported Frameworks**: .NET versions, dependencies
5. **Installation**: NuGet commands
6. **Quick Start**: Basic usage examples
7. **Advanced Usage**: Complex scenarios
8. **API Reference**: Key classes and methods
9. **Best Practices**: Recommendations
10. **Contributing**: Link to CONTRIBUTING.md
11. **License**: Link to LICENSE file

### XML Documentation

Required for all public members:
- Summary: What the member does
- Remarks: Additional context (optional)
- Parameters: Parameter descriptions
- Returns: Return value description
- Exceptions: Thrown exceptions
- Examples: Code examples (optional)

Example:
```csharp
/// <summary>
/// Determines whether the given <see cref="EntityEntry"/> represents a new entity
/// that hasn't yet been persisted to the database.
/// </summary>
/// <param name="entry">The <see cref="EntityEntry"/> to evaluate.</param>
/// <returns>
/// <c>true</c> if the entity is new; otherwise, <c>false</c>.
/// </returns>
/// <remarks>
/// An entity is considered new if its key is not set, its state is Added,
/// or its original key values are all null.
/// </remarks>
public static bool IsNewEntity(this EntityEntry entry)
```

---

## Version Control & CI/CD

### Commit Messages

Follow conventional commits:
- `feat:` New feature
- `fix:` Bug fix
- `docs:` Documentation changes
- `refactor:` Code refactoring
- `test:` Adding/updating tests
- `chore:` Maintenance tasks

### Branch Strategy

- `main`: Production-ready code
- `develop`: Integration branch
- `feature/*`: Feature branches
- `fix/*`: Bug fix branches

### Package Versioning

Format: `9.9.YYMMDD`
- Example: `9.9.251025` (October 25, 2025)
- Stored in `/nupkgs/` folder

---

## Performance Considerations

### EF Core Best Practices

1. **Use AsNoTracking for read-only queries**:
   ```csharp
   var products = await context.Products
       .AsNoTracking()
       .ToListAsync();
   ```

2. **Use projections instead of loading full entities**:
   ```csharp
   var productDtos = await context.Products
       .ProjectToType<ProductDto>()
       .ToListAsync();
   ```

3. **Avoid N+1 queries with Include**:
   ```csharp
   var orders = await context.Orders
       .Include(o => o.OrderItems)
       .ToListAsync();
   ```

4. **Use compiled queries for frequently executed queries**

5. **Batch operations when possible**

### Repository Pattern

- Use `IReadRepository<T>` for read operations (no change tracking)
- Use `IRepository<T>` for write operations (with change tracking)
- Leverage Mapster for efficient projections
- Use specifications for reusable query logic

---

## Security Considerations

### Data Protection

1. **Encryption**: Use `DKNet.Svc.Encryption` for sensitive data
2. **Data Authorization**: Use `DKNet.EfCore.DataAuthorization` for row-level security
3. **Audit Logs**: Use `DKNet.EfCore.AuditLogs` for change tracking

### Input Validation

1. **Use FluentValidation** for request validation
2. **Validate at boundaries** (API endpoints, commands)
3. **Sanitize user input**

### Concurrency Control

1. **Use `IConcurrencyEntity`** for optimistic concurrency
2. **Handle `DbUpdateConcurrencyException`**
3. **Implement retry logic** where appropriate

---

## Key Patterns & Idioms

### Automatic Configuration Discovery

The framework automatically discovers and applies:
- Entity configurations (IEntityTypeConfiguration<T>)
- Global query filters (IGlobalQueryFilter)
- Data seeding (IDataSeedingConfiguration<T>)
- SQL sequences (SequenceAttribute, SqlSequenceAttribute)

Usage:
```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.UseAutoConfigModel<AppDbContext>(config =>
    {
        config.AddAssembly(typeof(Product).Assembly);
    });
    base.OnModelCreating(modelBuilder);
}
```

### Domain Events

Entities can raise domain events:
```csharp
public class Order : AuditedEntity<Guid>
{
    public void PlaceOrder()
    {
        Status = OrderStatus.Placed;
        AddEvent(new OrderPlacedEvent(Id));
    }
}
```

Events are automatically published when using:
- `DKNet.EfCore.Events` package
- `DKNet.SlimBus.Extensions` package

### Snapshot Pattern

Track entity changes:
```csharp
var snapshot = new SnapshotContext(context);
snapshot.TakeSnapshot();

// Make changes
entity.UpdateName("New Name");

var changes = snapshot.GetChanges();
```

---

## Quick Reference

### Common Global Usings

```csharp
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using Microsoft.EntityFrameworkCore;
global using System.Diagnostics.CodeAnalysis;
```

### Common Attributes

- `[IgnoreEntity]`: Skip auto-configuration
- `[SequenceAttribute]`: Define sequence
- `[SqlSequenceAttribute]`: SQL Server sequence
- `[IgnoreAuditLog]`: Exclude from audit
- `[NotMapped]`: Exclude from database mapping

### Common Interfaces

- `IEntity<TKey>`: Base entity
- `IAuditedEntity<TKey>`: Entity with audit
- `ISoftDeletableEntity`: Soft delete support
- `IConcurrencyEntity<TRowVersion>`: Optimistic concurrency
- `IEventEntity`: Domain events
- `IRepository<T>`: Full repository
- `IReadRepository<T>`: Read-only repository
- `IBackgroundJob`: Startup job

---

## Troubleshooting

### Common Issues

1. **Auto-configuration not working**:
   - Ensure `UseAutoConfigModel<TContext>()` is called
   - Check that entities are in scanned assemblies
   - Verify entities don't have `[IgnoreEntity]` attribute

2. **Repository not registered**:
   - Ensure `AddGenericRepositories<TContext>()` is called
   - Verify DbContext is registered

3. **Global query filter not applying**:
   - Ensure filter implements `IGlobalQueryFilter`
   - Register with `AddGlobalQueryFilter<T>()`
   - Check filter logic

4. **Sequence not working**:
   - SQL Server only
   - Verify `[SqlSequence]` or `[Sequence]` attribute
   - Check sequence registration in auto-config

---

## Future Enhancements

Based on `projectBrief.md`:
- API documentation with DocFX
- Automated changelog generation
- Enhanced SonarCloud integration
- More code samples in documentation
- Performance benchmarking
- Additional database providers

---

## Related Resources

- **Main Repository**: https://github.com/baoduy/DKNet
- **NuGet Packages**: https://www.nuget.org/profiles/drunkcoding.net
- **Documentation**: Embedded in each project's README.md
- **Diagrams**: `/EfCore/Diagrams/`

---

## Notes for Copilot

When assisting with this codebase:

1. **Always check for existing patterns** before suggesting new ones
2. **Follow established naming conventions** (see sections above)
3. **Add XML documentation** to all public members
4. **Use modern C# features** (collection expressions, pattern matching, etc.)
5. **Consider nullable reference types** in all suggestions
6. **Prefer extension methods** for reusable functionality
7. **Follow SOLID principles** and DDD patterns
8. **Write comprehensive tests** for new features
9. **Update README.md** when adding features
10. **Respect the auto-configuration system** - don't manually register what can be auto-discovered

### When Creating New Entities

1. Inherit from `Entity` (Guid key) or `Entity<TKey>` (custom key)
2. Use `AuditedEntity<TKey>` if audit tracking is needed
3. Implement `ISoftDeletableEntity` for soft delete
4. Add domain events with `AddEvent()` when appropriate
5. Create a corresponding `<Entity>Configuration` class
6. Use private setters and constructors for encapsulation

### When Creating New Repositories

1. Use existing `IRepository<T>` and `IReadRepository<T>` interfaces
2. Let auto-registration handle DI (`AddGenericRepositories<T>()`)
3. Use specifications for complex queries
4. Leverage Mapster projections for DTOs

### When Creating Tests

1. Follow AAA pattern (Arrange-Act-Assert)
2. Use FluentAssertions for readability
3. Name tests descriptively: `Method_Scenario_Expected`
4. Use fixtures for shared setup
5. Test both happy paths and edge cases

---

## Common Patterns & Anti-Patterns

### ✅ DO - Recommended Patterns

#### **Entity Creation**:
```csharp
// ✅ DO: Private parameterless constructor for EF Core
// ✅ DO: Public constructor with required parameters
public class Product : Entity
{
    private Product() { } // For EF Core
    
    public Product(string name, decimal price, string sku)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(price);
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        
        Name = name;
        Price = price;
        Sku = sku;
    }
    
    public string Name { get; private set; }
    public decimal Price { get; private set; }
    public string Sku { get; private set; }
    
    // ✅ DO: Use methods for mutations
    public void UpdatePrice(decimal newPrice)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(newPrice);
        Price = newPrice;
        AddEvent(new ProductPriceChangedEvent(Id, newPrice));
    }
}
```

#### **Repository Usage**:
```csharp
// ✅ DO: Use IReadRepository for queries
public class ProductQueryService
{
    private readonly IReadRepository<Product> _repository;
    
    public async Task<List<ProductDto>> GetProductsAsync()
    {
        return await _repository
            .Query()
            .AsNoTracking()
            .ProjectToType<ProductDto>()
            .ToListAsync();
    }
}

// ✅ DO: Use IRepository for commands
public class ProductCommandService
{
    private readonly IRepository<Product> _repository;
    
    public async Task<Result<Product>> CreateAsync(CreateProductCommand cmd)
    {
        var product = new Product(cmd.Name, cmd.Price, cmd.Sku);
        await _repository.AddAsync(product);
        await _repository.SaveChangesAsync();
        return Result.Ok(product);
    }
}
```

#### **CQRS with SlimBus**:
```csharp
// ✅ DO: Use record types for messages
public record GetProductQuery(Guid Id) : IWithResponse<ProductResult>;

// ✅ DO: Return FluentResults
public class GetProductQueryHandler : IRequestHandler<GetProductQuery, Result<ProductResult>>
{
    public async Task<Result<ProductResult>> Handle(GetProductQuery request, CancellationToken ct)
    {
        var product = await _repository.FindAsync(request.Id, ct);
        if (product == null)
            return Result.Fail<ProductResult>($"Product {request.Id} not found");
        
        return Result.Ok(product.Adapt<ProductResult>());
    }
}
```

#### **Data Seeding**:
```csharp
// ✅ DO: Use DataSeedingConfiguration for static data
public class ProductCategorySeeding : DataSeedingConfiguration<ProductCategory>
{
    protected override ICollection<ProductCategory> HasData =>
    [
        new(1, "Electronics"),
        new(2, "Books"),
        new(3, "Clothing")
    ];
}

// ✅ DO: Use SeedAsync for dynamic/complex seeding
public class UserRolesSeeding : DataSeedingConfiguration<UserRole>
{
    public override Func<DbContext, bool, CancellationToken, Task>? SeedAsync { get; } =
        async (context, performedStoreOperation, ct) =>
        {
            if (!performedStoreOperation) return;
            
            var roles = context.Set<UserRole>();
            if (await roles.AnyAsync(ct)) return;
            
            await roles.AddRangeAsync(new[]
            {
                new UserRole("Admin"),
                new UserRole("User")
            }, ct);
            
            await context.SaveChangesAsync(ct);
        };
}
```

### ❌ DON'T - Anti-Patterns

#### **Entity Anti-Patterns**:
```csharp
// ❌ DON'T: Public setters on entities
public class Product : Entity
{
    public string Name { get; set; } // BAD!
    public decimal Price { get; set; } // BAD!
}

// ❌ DON'T: Anemic domain model (no behavior)
public class Order : Entity
{
    public decimal Total { get; set; }
    public OrderStatus Status { get; set; }
    // No methods, just data - this is an anti-pattern!
}

// ❌ DON'T: Missing EF Core constructor
public class Customer : Entity
{
    // Missing private parameterless constructor
    public Customer(string name) // EF Core can't instantiate this!
    {
        Name = name;
    }
}

// ❌ DON'T: Expose collections as writable
public class Order : Entity
{
    public List<OrderItem> Items { get; set; } = new(); // BAD! Allows external modification
}

// ✅ DO: Use read-only collections with controlled mutation
public class Order : Entity
{
    private readonly List<OrderItem> _items = new();
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();
    
    public void AddItem(OrderItem item)
    {
        _items.Add(item);
    }
}
```

#### **Repository Anti-Patterns**:
```csharp
// ❌ DON'T: Use IRepository for read-only operations
public class ProductQueryService
{
    private readonly IRepository<Product> _repository; // Unnecessary change tracking!
    
    public async Task<List<Product>> GetAllAsync()
    {
        return await _repository.Query().ToListAsync(); // Change tracking overhead
    }
}

// ❌ DON'T: Call SaveChanges multiple times unnecessarily
public async Task CreateProductsAsync(List<Product> products)
{
    foreach (var product in products)
    {
        await _repository.AddAsync(product);
        await _repository.SaveChangesAsync(); // BAD! SaveChanges in loop
    }
}

// ✅ DO: Batch operations
public async Task CreateProductsAsync(List<Product> products)
{
    await _repository.AddRangeAsync(products);
    await _repository.SaveChangesAsync(); // Single SaveChanges
}

// ❌ DON'T: Manually manage DbContext
public class ProductService
{
    public async Task DoSomethingAsync()
    {
        using var context = new AppDbContext(); // BAD! Manual DbContext creation
        // ...
    }
}

// ✅ DO: Use DI
public class ProductService
{
    private readonly IRepository<Product> _repository;
    
    public ProductService(IRepository<Product> repository)
    {
        _repository = repository;
    }
}
```

#### **Query Anti-Patterns**:
```csharp
// ❌ DON'T: Load full entities when you only need DTOs
public async Task<List<ProductDto>> GetProductsAsync()
{
    var products = await _repository.Query().ToListAsync(); // Loads full entities
    return products.Select(p => new ProductDto(p.Id, p.Name, p.Price)).ToList();
}

// ✅ DO: Project to DTO in database
public async Task<List<ProductDto>> GetProductsAsync()
{
    return await _repository
        .Query()
        .ProjectToType<ProductDto>()
        .ToListAsync();
}

// ❌ DON'T: N+1 query problem
public async Task<List<Order>> GetOrdersAsync()
{
    var orders = await _repository.Query().ToListAsync();
    foreach (var order in orders)
    {
        var customer = await _customerRepository.FindAsync(order.CustomerId); // N+1!
        // ...
    }
    return orders;
}

// ✅ DO: Use Include for eager loading
public async Task<List<Order>> GetOrdersAsync()
{
    return await _repository
        .Query()
        .Include(o => o.Customer)
        .Include(o => o.OrderItems)
        .ToListAsync();
}

// ❌ DON'T: Synchronous database calls
public List<Product> GetProducts()
{
    return _repository.Query().ToList(); // Blocking!
}

// ✅ DO: Async all the way
public async Task<List<Product>> GetProductsAsync()
{
    return await _repository.Query().ToListAsync();
}
```

#### **Configuration Anti-Patterns**:
```csharp
// ❌ DON'T: Repeat default configuration
public class ProductConfiguration : DefaultEntityTypeConfiguration<Product>
{
    public override void Configure(EntityTypeBuilder<Product> builder)
    {
        // Don't configure Id, audit properties, etc. manually
        builder.HasKey(p => p.Id); // Already done by base!
        builder.Property(p => p.CreatedBy).HasMaxLength(255); // Already done!
        
        base.Configure(builder); // Call base at the end? Wrong!
    }
}

// ✅ DO: Call base first, add only custom config
public class ProductConfiguration : DefaultEntityTypeConfiguration<Product>
{
    public override void Configure(EntityTypeBuilder<Product> builder)
    {
        base.Configure(builder); // Call base FIRST
        
        // Add only custom configuration
        builder.Property(p => p.Name).HasMaxLength(255);
        builder.Property(p => p.Sku).HasMaxLength(50);
        builder.HasIndex(p => p.Sku).IsUnique();
    }
}

// ❌ DON'T: Manually register configurations
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.ApplyConfiguration(new ProductConfiguration()); // Manual!
    modelBuilder.ApplyConfiguration(new CustomerConfiguration());
    modelBuilder.ApplyConfiguration(new OrderConfiguration());
    // ... many more
}

// ✅ DO: Use auto-configuration
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.UseAutoConfigModel<AppDbContext>();
    base.OnModelCreating(modelBuilder);
}
```

#### **CQRS Anti-Patterns**:
```csharp
// ❌ DON'T: Mix read and write in same handler
public class ProductHandler : 
    IRequestHandler<GetProductQuery, Result<ProductDto>>,
    IRequestHandler<CreateProductCommand, Result<ProductDto>>
{
    // BAD! Separate responsibilities
}

// ✅ DO: One handler per message
public class GetProductQueryHandler : IRequestHandler<GetProductQuery, Result<ProductDto>> { }
public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Result<ProductDto>> { }

// ❌ DON'T: Return entities from commands
public record CreateProductCommand(string Name, decimal Price) : IWithResponse<Product>;

// ✅ DO: Return DTOs
public record CreateProductCommand(string Name, decimal Price) : IWithResponse<ProductResult>;

// ❌ DON'T: Throw exceptions for business errors
public async Task<ProductDto> Handle(GetProductQuery request, CancellationToken ct)
{
    var product = await _repository.FindAsync(request.Id, ct);
    if (product == null)
        throw new NotFoundException("Product not found"); // BAD!
}

// ✅ DO: Return FluentResults
public async Task<Result<ProductDto>> Handle(GetProductQuery request, CancellationToken ct)
{
    var product = await _repository.FindAsync(request.Id, ct);
    if (product == null)
        return Result.Fail<ProductDto>("Product not found");
    
    return Result.Ok(product.Adapt<ProductDto>());
}
```

#### **Testing Anti-Patterns**:
```csharp
// ❌ DON'T: Test implementation details
[Fact]
public void Product_ShouldHavePrivateSetters()
{
    var property = typeof(Product).GetProperty("Name");
    Assert.True(property.SetMethod.IsPrivate); // Testing implementation!
}

// ✅ DO: Test behavior
[Fact]
public void UpdateName_ShouldChangeName()
{
    var product = new Product("Old Name", 10m, "SKU1");
    product.UpdateName("New Name");
    product.Name.Should().Be("New Name");
}

// ❌ DON'T: Multiple asserts on unrelated things
[Fact]
public void Product_Tests()
{
    var product = new Product("Test", 10m, "SKU");
    product.Name.Should().Be("Test");
    product.UpdatePrice(20m);
    product.Price.Should().Be(20m);
    // Too much in one test!
}

// ✅ DO: One logical assert per test
[Fact]
public void Constructor_SetsNameCorrectly()
{
    var product = new Product("Test", 10m, "SKU");
    product.Name.Should().Be("Test");
}

[Fact]
public void UpdatePrice_ChangesPrice()
{
    var product = new Product("Test", 10m, "SKU");
    product.UpdatePrice(20m);
    product.Price.Should().Be(20m);
}
```

---

## Quick Decision Matrix

| Scenario | Use This | Not This |
|----------|----------|----------|
| Query data | `IReadRepository<T>` | `IRepository<T>` |
| Modify data | `IRepository<T>` | Direct DbContext |
| Return from API | DTO/Record | Entity |
| Business error | `Result.Fail()` | Exception |
| Entity property | Private setter + method | Public setter |
| Configuration | `DefaultEntityTypeConfiguration<T>` | Manual config |
| Data seeding | `DataSeedingConfiguration<T>` | `OnModelCreating` manual |
| Global filter | `IGlobalQueryFilter` | Repeated filter code |
| Background job | `IBackgroundJob` | `IHostedService` directly |
| CQRS message | Record type | Class |
| Navigation property | `IReadOnlyCollection<T>` | `List<T>` |
| Async method | `async Task<T>` | `Task<T> (sync code)` |

---

## Integration Checklist

When integrating DKNet into a new project:

- [ ] Install required NuGet packages
- [ ] Configure `Directory.Packages.props` for central package management
- [ ] Set up `DbContext` with `UseAutoConfigModel()`
- [ ] Create entity base classes inheriting from `Entity` or `AuditedEntity`
- [ ] Create entity configurations extending `DefaultEntityTypeConfiguration<T>`
- [ ] Register repositories with `AddGenericRepositories<TContext>()`
- [ ] Configure SlimBus with `AddSlimBusForEfCore()`
- [ ] Set up global query filters if needed
- [ ] Configure data seeding for lookup data
- [ ] Add hooks for cross-cutting concerns (audit, events, etc.)
- [ ] Set up validation with FluentValidation
- [ ] Configure Mapster for projections
- [ ] Create CQRS handlers for business operations
- [ ] Map MinimalAPI endpoints with SlimBus extensions
- [ ] Add background jobs for startup tasks
- [ ] Configure logging and telemetry
- [ ] Write integration tests with InMemory database
- [ ] Update documentation in README.md

---

**End of Memory Bank**

This document should be updated whenever significant architectural changes or new patterns are introduced to the DKNet framework.

**Version**: 1.0.0  
**Last Updated**: October 27, 2025  
**Maintained by**: Steven Hoang / DKNet Team

