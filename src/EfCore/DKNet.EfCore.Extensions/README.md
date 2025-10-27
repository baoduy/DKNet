# DKNet.EfCore.Extensions

[![NuGet](https://img.shields.io/nuget/v/DKNet.EfCore.Extensions)](https://www.nuget.org/packages/DKNet.EfCore.Extensions/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DKNet.EfCore.Extensions)](https://www.nuget.org/packages/DKNet.EfCore.Extensions/)
[![.NET](https://img.shields.io/badge/.NET-9.0-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](../../../../LICENSE)

Enhanced Entity Framework Core functionality with automatic entity configuration, global query filters, data seeding,
and advanced extension methods. This package streamlines EF Core setup and provides powerful utilities for entity
management, pagination, and database operations.

## Features

- **Auto Entity Configuration**: Automatic discovery and configuration of entities from assemblies
- **Global Query Filters**: Centralized query filter management for cross-cutting concerns
- **Data Seeding**: Structured data seeding with dependency injection support
- **Default Entity Configuration**: Base configuration for common entity patterns (audit, soft delete)
- **Advanced Extensions**: Table name resolution, primary key utilities, sequence generation
- **Snapshot Context**: Entity state tracking and change detection utilities
- **Navigation Extensions**: Enhanced navigation property management
- **Pagination Support**: Async enumeration and paging capabilities

## Supported Frameworks

- .NET 9.0+
- Entity Framework Core 9.0+
- SQL Server (for sequence features)

## Installation

Install via NuGet Package Manager:

```bash
dotnet add package DKNet.EfCore.Extensions
```

Or via Package Manager Console:

```powershell
Install-Package DKNet.EfCore.Extensions
```

## Quick Start

### Auto Entity Configuration

```csharp
using Microsoft.EntityFrameworkCore;
using DKNet.EfCore.Extensions;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Automatically configure all entities from assemblies
        modelBuilder.UseAutoConfigModel<AppDbContext>(config =>
        {
            config.AddAssembly(typeof(Product).Assembly);
            config.AddAssembly(typeof(Customer).Assembly);
        });
        
        base.OnModelCreating(modelBuilder);
    }
}

// Configure in Startup/Program
services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString)
           .UseAutoConfigModel<AppDbContext>());
```

### Default Entity Configuration

```csharp
using DKNet.EfCore.Extensions.Configurations;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class ProductConfiguration : DefaultEntityTypeConfiguration<Product>
{
    public override void Configure(EntityTypeBuilder<Product> builder)
    {
        // Apply default configurations (Id, audit properties, etc.)
        base.Configure(builder);
        
        // Add custom configurations
        builder.Property(p => p.Name)
               .HasMaxLength(255)
               .IsRequired();
               
        builder.Property(p => p.Price)
               .HasPrecision(18, 2);
               
        builder.HasIndex(p => p.Sku)
               .IsUnique();
    }
}
```

### Global Query Filters

```csharp
using DKNet.EfCore.Extensions.Configurations;

public class SoftDeleteQueryFilter : IGlobalQueryFilterRegister
{
    public void RegisterFilters(ModelBuilder modelBuilder)
    {
        // Apply soft delete filter globally
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDeletableEntity).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var property = Expression.Property(parameter, nameof(ISoftDeletableEntity.IsDeleted));
                var condition = Expression.Equal(property, Expression.Constant(false));
                var lambda = Expression.Lambda(condition, parameter);
                
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }
    }
}

// Register in Startup
services.AddGlobalModelBuilderRegister<SoftDeleteQueryFilter>();
```

## Configuration

### Data Seeding Configuration

```csharp
using DKNet.EfCore.Extensions.Configurations;

public class CategorySeedData : IDataSeedingConfiguration<Category>
{
    public async Task SeedAsync(DbContext context, IServiceProvider serviceProvider)
    {
        var repository = serviceProvider.GetRequiredService<ICategoryRepository>();
        
        if (!await repository.AnyAsync())
        {
            var categories = new[]
            {
                new Category("Electronics", "Electronic devices"),
                new Category("Books", "Books and publications"),
                new Category("Clothing", "Apparel and accessories")
            };
            
            await repository.AddRangeAsync(categories);
            await context.SaveChangesAsync();
        }
    }
}

// Apply seeding
await context.SeedDataAsync<Category>();
```

### Entity Auto-Registration Options

```csharp
services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(connectionString)
           .UseAutoConfigModel<AppDbContext>(config =>
           {
               // Add specific assemblies
               config.AddAssembly(typeof(Product).Assembly);
               
               // Exclude specific entity types
               config.ExcludeEntity<TemporaryEntity>();
               
               // Configure naming conventions
               config.UseSnakeCaseNaming();
               config.UsePluralTableNames();
           });
});
```

## API Reference

### Core Extensions

- `UseAutoConfigModel<TContext>()` - Auto-configure entities from assemblies
- `AddGlobalModelBuilderRegister<T>()` - Register global query filters
- `SeedDataAsync<TEntity>()` - Perform structured data seeding

### Entity Extensions

- `GetTableName(Type)` - Get schema-qualified table name for entity
- `GetPrimaryKeyProperty(Type)` - Extract primary key property information
- `GetPrimaryKeyValue(object)` - Get primary key value from entity instance

### Sequence Extensions (SQL Server)

- `GetNextSequenceValue(string)` - Get next value from database sequence
- `GetFormattedSequenceValue(string, string)` - Get formatted sequence with prefix/suffix

### Snapshot Extensions

- `CreateSnapshot()` - Create entity state snapshot for change tracking
- `GetChanges(SnapshotContext)` - Detect changes between snapshots

## Advanced Usage

### Custom Entity Configuration with Sequences

```csharp
public class InvoiceConfiguration : DefaultEntityTypeConfiguration<Invoice>
{
    public override void Configure(EntityTypeBuilder<Invoice> builder)
    {
        base.Configure(builder);
        
        // Configure SQL sequence for invoice numbers
        builder.Property(i => i.InvoiceNumber)
               .HasDefaultValueSql("NEXT VALUE FOR invoice_number_seq");
               
        // Configure custom table and schema
        builder.ToTable("Invoices", "billing");
        
        // Configure relationships
        builder.HasMany(i => i.LineItems)
               .WithOne(li => li.Invoice)
               .HasForeignKey(li => li.InvoiceId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
```

### Advanced Query Filter with User Context

```csharp
public class TenantQueryFilter : IGlobalQueryFilterRegister
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    
    public TenantQueryFilter(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }
    
    public void RegisterFilters(ModelBuilder modelBuilder)
    {
        var tenantId = GetCurrentTenantId();
        
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var property = Expression.Property(parameter, nameof(ITenantEntity.TenantId));
                var condition = Expression.Equal(property, Expression.Constant(tenantId));
                var lambda = Expression.Lambda(condition, parameter);
                
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }
    }
    
    private Guid GetCurrentTenantId()
    {
        // Extract tenant ID from HTTP context, claims, etc.
        return _httpContextAccessor.HttpContext?.User
            ?.FindFirst("tenant_id")?.Value 
            ?? Guid.Empty;
    }
}
```

### Bulk Data Seeding with Dependencies

```csharp
public class ProductSeedData : IDataSeedingConfiguration<Product>
{
    public async Task SeedAsync(DbContext context, IServiceProvider serviceProvider)
    {
        var categoryRepo = serviceProvider.GetRequiredService<ICategoryRepository>();
        var logger = serviceProvider.GetRequiredService<ILogger<ProductSeedData>>();
        
        try
        {
            if (!await context.Set<Product>().AnyAsync())
            {
                var categories = await categoryRepo.GetAllAsync();
                var electronics = categories.First(c => c.Name == "Electronics");
                
                var products = new[]
                {
                    new Product("Laptop", 1299.99m, electronics.Id, "admin"),
                    new Product("Mouse", 29.99m, electronics.Id, "admin"),
                    new Product("Keyboard", 89.99m, electronics.Id, "admin")
                };
                
                context.Set<Product>().AddRange(products);
                await context.SaveChangesAsync();
                
                logger.LogInformation("Seeded {Count} products", products.Length);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to seed product data");
            throw;
        }
    }
}
```

## Entity Discovery Rules

The auto-configuration system follows these rules:

1. **Included Entities**: Classes implementing `IEntity<TKey>` from specified assemblies
2. **Excluded Entities**: Classes marked with `[IgnoreEntity]` attribute
3. **Configuration Priority**: Explicit configurations override default configurations
4. **Naming Conventions**: Configurable table and column naming strategies

## Performance Considerations

- **Assembly Scanning**: Performed once during startup, cached for application lifetime
- **Global Filters**: Applied at query compilation time, minimal runtime overhead
- **Sequence Operations**: Direct SQL execution for optimal performance
- **Snapshot Context**: Lightweight change tracking, use for critical audit scenarios

## Thread Safety

- Configuration registration is thread-safe during startup
- Runtime query operations are thread-safe following EF Core patterns
- Sequence generation is atomic at database level
- Global filter state is immutable after configuration

## Contributing

See the main [CONTRIBUTING.md](../../../../CONTRIBUTING.md) for guidelines on how to contribute to this project.

## License

This project is licensed under the [MIT License](../../../../LICENSE).

## Related Packages

- [DKNet.EfCore.Abstractions](../DKNet.EfCore.Abstractions) - Core abstractions and interfaces
- [DKNet.EfCore.Hooks](../DKNet.EfCore.Hooks) - Entity lifecycle hooks
- [DKNet.EfCore.Repos](../DKNet.EfCore.Repos) - Repository pattern implementations
- [DKNet.EfCore.Events](../DKNet.EfCore.Events) - Domain event handling

---

Part of the [DKNet Framework](https://github.com/baoduy/DKNet) - A comprehensive .NET framework for building modern,
scalable applications.