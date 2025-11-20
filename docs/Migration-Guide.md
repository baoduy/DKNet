# Migration Guide

This guide helps you migrate between different versions of DKNet Framework and provides guidance for handling breaking changes.

## 📋 Table of Contents

- [Current Migration Scenarios](#current-migration-scenarios)
- [Migration from Legacy DKNet](#migration-from-legacy-dknet)
- [Version-Specific Migrations](#version-specific-migrations)
- [Breaking Changes Reference](#breaking-changes-reference)
- [Migration Tools](#migration-tools)
- [Common Issues](#common-issues)

---

## 🚀 Current Migration Scenarios

### From Legacy DKNet to 2024.12.0+

This is a **major architectural migration** from legacy packages to the new Domain-Driven Design framework.

#### Key Changes
- **Architecture**: Complete shift to DDD/Onion Architecture
- **Technology**: Upgrade to .NET 10.0 with C# 14 features
- **Patterns**: Introduction of CQRS, Repository patterns, Domain Events
- **Testing**: New testing strategy with TestContainers

#### Migration Strategy

**1. Assessment Phase**
```bash
# Analyze your current usage
git grep -r "DKNet" --include="*.cs" src/
# Review dependencies
dotnet list package --include-transitive | grep DKNet
```

**2. Incremental Migration**
- Start with new projects using the [SlimBus template](../src/Templates/SlimBus.ApiEndpoints/)
- Migrate existing projects component by component
- Run both old and new implementations in parallel during transition

**3. Component Migration Order**
1. **Core Extensions** → `DKNet.Fw.Extensions`
2. **Data Access** → `DKNet.EfCore.*` packages
3. **Business Logic** → Domain entities and services
4. **API Layer** → Controllers/endpoints
5. **Infrastructure** → External service integrations

---

## 📦 Version-Specific Migrations

### Upgrading to .NET 10.0

**Prerequisites**
```bash
# Install .NET 10.0 SDK
./src/dotnet-install.sh --version 10.0.100

# Update global.json
{
  "sdk": {
    "version": "10.0.100"
  }
}
```

**Project Files**
```xml
<!-- Update target framework -->
<TargetFramework>net10.0</TargetFramework>

<!-- Update package references -->
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.0" />
<PackageReference Include="Microsoft.AspNetCore.App" />
```

**C# 14 Language Features**

Take advantage of new C# 14 features in your code:

```csharp
// Params collections - more efficient parameter passing
public static void ProcessItems(params ReadOnlySpan<string> items)
{
    foreach (var item in items)
    {
        // Process item
    }
}

// Enhanced pattern matching
public decimal CalculateDiscount(Order order) => order switch
{
    { Total: > 1000, IsPremium: true } => 0.20m,
    { Total: > 500 } => 0.10m,
    { ItemCount: > 10 } => 0.05m,
    _ => 0m
};

// Lock object improvements - better performance
private readonly Lock _lock = new();

public void SafeOperation()
{
    lock (_lock)
    {
        // Thread-safe operations
    }
}
```

### Entity Framework Core Migration

**Before (Legacy)**
```csharp
public class ProductRepository
{
    private readonly DbContext _context;
    
    public ProductRepository(DbContext context)
    {
        _context = context;
    }
    
    public async Task<Product> GetAsync(int id)
    {
        return await _context.Set<Product>().FindAsync(id);
    }
}
```

**After (DKNet 2024.12.0+)**
```csharp
public class ProductRepository : Repository<Product>, IProductRepository
{
    public ProductRepository(AppDbContext context) : base(context) { }
    
    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await GetByIdAsync(id, cancellationToken);
    }
    
    // Specification support
    public async Task<IEnumerable<Product>> FindAsync(Specification<Product> spec)
    {
        return await Gets().Where(spec.ToExpression()).ToListAsync();
    }
}
```

---

## 🏗️ Architecture Migration

### From N-Layer to Onion Architecture

**Legacy Structure**
```
Solution/
├── Web/              # Presentation
├── Business/         # Business Logic
├── Data/            # Data Access
└── Common/          # Shared
```

**New Structure (Onion)**
```
Solution/
├── Api/             # Presentation Layer
├── AppServices/     # Application Layer
├── Domains/         # Domain Layer
└── Infra/           # Infrastructure Layer
```

### Domain Entity Migration

**Before**
```csharp
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

**After**
```csharp
[Table("Products", Schema = "catalog")]
public class Product : AggregateRoot
{
    public Product(string name, decimal price, string createdBy)
        : base(Guid.NewGuid(), createdBy)
    {
        Name = name;
        Price = price;
    }

    public string Name { get; private set; }
    public decimal Price { get; private set; }

    public void UpdatePrice(decimal newPrice, string updatedBy)
    {
        Price = newPrice;
        SetUpdatedBy(updatedBy);
        AddEvent(new ProductPriceChangedEvent(Id, Price));
    }
}
```

---

## 🔄 CQRS Migration

### Command/Query Separation

**Before (Traditional Service)**
```csharp
public class ProductService
{
    public async Task<Product> CreateProductAsync(CreateProductDto dto)
    {
        // Create logic
    }
    
    public async Task<Product> GetProductAsync(int id)
    {
        // Get logic
    }
}
```

**After (CQRS)**
```csharp
// Command
public record CreateProductCommand : IRequest<ProductResult>
{
    public string Name { get; init; }
    public decimal Price { get; init; }
}

public class CreateProductHandler : IRequestHandler<CreateProductCommand, ProductResult>
{
    public async Task<ProductResult> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        // Command handling logic
    }
}

// Query
public record GetProductQuery : IRequest<ProductResult>
{
    public Guid Id { get; init; }
}

public class GetProductHandler : IRequestHandler<GetProductQuery, ProductResult>
{
    public async Task<ProductResult> Handle(GetProductQuery request, CancellationToken cancellationToken)
    {
        // Query handling logic
    }
}
```

---

## 📊 Database Migration

### Schema Updates

**Add Migration for New Structure**
```bash
# Create migration
dotnet ef migrations add UpgradeToOnionArchitecture

# Review generated migration
# Update database
dotnet ef database update
```

**Data Migration Script**
```sql
-- Migrate existing data to new schema
-- Add audit fields
ALTER TABLE Products ADD CreatedBy NVARCHAR(255) NOT NULL DEFAULT 'SYSTEM';
ALTER TABLE Products ADD CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE();
ALTER TABLE Products ADD UpdatedBy NVARCHAR(255) NULL;
ALTER TABLE Products ADD UpdatedAt DATETIME2 NULL;

-- Convert INT IDs to GUIDs (if needed)
-- This is a complex migration - consider keeping INT IDs if possible
```

---

## 🧪 Testing Migration

### From Legacy Testing to Modern Patterns

**Before**
```csharp
[Test]
public async Task CreateProduct_ShouldReturnProduct()
{
    // In-memory database setup
    var options = new DbContextOptionsBuilder<DbContext>()
        .UseInMemoryDatabase(databaseName: "TestDb")
        .Options;
        
    using var context = new DbContext(options);
    var repository = new ProductRepository(context);
    
    // Test logic
}
```

**After**
```csharp
[Test]
public async Task CreateProduct_ShouldReturnProduct()
{
    // TestContainers setup
    await using var container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();
        
    await container.StartAsync();
    
    var connectionString = container.GetConnectionString();
    var services = new ServiceCollection();
    services.AddDbContext<AppDbContext>(options => 
        options.UseSqlServer(connectionString));
    
    // Test with real database
}
```

---

## 🛠️ Migration Tools

### Automated Migration Helper

```csharp
public class MigrationHelper
{
    public static async Task MigrateDataAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        // Ensure database is created
        await context.Database.EnsureCreatedAsync();
        
        // Run custom migrations
        await MigrateProductsAsync(context);
        await MigrateUsersAsync(context);
    }
    
    private static async Task MigrateProductsAsync(AppDbContext context)
    {
        // Custom migration logic for products
        var products = await context.Set<OldProduct>().ToListAsync();
        foreach (var oldProduct in products)
        {
            var newProduct = new Product(
                oldProduct.Name, 
                oldProduct.Price, 
                "MIGRATION");
            context.Set<Product>().Add(newProduct);
        }
        await context.SaveChangesAsync();
    }
}
```

### Configuration Migration

```csharp
public static class ConfigurationMigration
{
    public static IServiceCollection MigrateFromLegacy(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // Map old configuration to new structure
        var legacyConfig = configuration.GetSection("Legacy");
        var newConfig = new DKNetOptions
        {
            DatabaseConnectionString = legacyConfig["Database:ConnectionString"],
            EnableAuditFields = bool.Parse(legacyConfig["Audit:Enabled"] ?? "true"),
            // ... other mappings
        };
        
        services.Configure<DKNetOptions>(options =>
        {
            options.DatabaseConnectionString = newConfig.DatabaseConnectionString;
            options.EnableAuditFields = newConfig.EnableAuditFields;
        });
        
        return services;
    }
}
```

---

## ⚠️ Common Issues

### 1. ID Type Changes

**Issue**: Converting from `int` to `Guid` IDs
**Solution**: 
- Keep existing `int` IDs if possible
- Use mapping layer for external APIs
- Consider gradual migration with both ID types

### 2. Breaking API Changes

**Issue**: Public API contracts change
**Solution**: 
- Version your APIs (`/api/v1/`, `/api/v2/`)
- Maintain compatibility layer
- Use deprecation warnings

### 3. Performance Issues

**Issue**: New patterns may impact performance
**Solution**: 
- Profile before and after migration
- Optimize hot paths
- Use caching where appropriate

### 4. Dependency Injection Changes

**Issue**: Service registration patterns change
**Solution**: 
```csharp
// Old
services.AddScoped<ProductService>();

// New
services.AddScoped<IProductRepository, ProductRepository>();
services.AddMediatR(typeof(CreateProductHandler));
```

---

## 📋 Migration Checklist

### Pre-Migration
- [ ] Backup production databases
- [ ] Document current architecture
- [ ] Identify critical business logic
- [ ] Plan rollback strategy
- [ ] Set up staging environment

### During Migration
- [ ] Migrate in small increments
- [ ] Maintain comprehensive tests
- [ ] Monitor performance metrics
- [ ] Document changes as you go
- [ ] Regular communication with stakeholders

### Post-Migration
- [ ] Verify all functionality works
- [ ] Performance testing
- [ ] Update documentation
- [ ] Train team on new patterns
- [ ] Plan for ongoing maintenance

---

## 🆘 Getting Help

If you encounter issues during migration:

1. **Check Documentation**: Review [Getting Started](Getting-Started.md) and [Examples](Examples/README.md)
2. **Search Issues**: Look for similar problems in [GitHub Issues](https://github.com/baoduy/DKNet/issues)
3. **Ask Questions**: Create a new issue with the `migration` label
4. **Join Discussions**: Participate in [GitHub Discussions](https://github.com/baoduy/DKNet/discussions)

---

> 💡 **Migration Tip**: Take your time with migration. It's better to migrate correctly in stages than to rush and introduce bugs. Use the SlimBus template as your reference implementation!