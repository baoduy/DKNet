# DKNet.EfCore.Extensions

**Entity Framework Core functionality enhancements that provide automated entity configuration, data seeding management, static data handling, and streamlined EF Core setup patterns while supporting Domain-Driven Design (DDD) and Onion Architecture principles.**

## What is this project?

DKNet.EfCore.Extensions provides a comprehensive set of enhancements and utilities for Entity Framework Core that automate common configuration tasks, simplify entity management, and reduce boilerplate code. It enables developers to focus on business logic while maintaining consistent and efficient database operations through convention-based configurations and automated discovery patterns.

### Key Features

- **Automated Entity Configuration**: Convention-based entity type configuration discovery
- **Generic Entity Configuration**: Reusable base configurations for common entity patterns  
- **Data Seeding Management**: Organized and maintainable data seeding with `IDataSeedingConfiguration`
- **Static Data Support**: Enum-based static data tables with `[StaticData]` attribute
- **Global Query Filters**: Centralized global filter management with `IGlobalModelBuilderRegister`
- **Auto-Discovery**: Automatic scanning and registration of configurations
- **Convention Over Configuration**: Sensible defaults with override capabilities
- **Assembly Scanning**: Multi-assembly support for modular applications
- **Exclude Mechanisms**: `[IgnoreEntityMapper]` attribute for selective exclusion

## How it contributes to DDD and Onion Architecture

### Infrastructure Layer Enhancement

DKNet.EfCore.Extensions enhances the **Infrastructure Layer** of the Onion Architecture by providing automated configuration patterns that reduce coupling between domain models and persistence technology:

```
┌─────────────────────────────────────────────────────────────────┐
│                    🌐 Presentation Layer                        │
│                   (Controllers, API Endpoints)                  │
│                                                                 │
│  Benefits from: Simplified setup, reduced configuration code   │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                   🎯 Application Layer                          │
│              (Use Cases, Application Services)                  │
│                                                                 │
│  Benefits from: Automatic data seeding, consistent setup       │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                    💼 Domain Layer                             │
│           (Entities, Aggregates, Domain Services)              │
│                                                                 │
│  📋 Clean entity definitions without EF Core attributes        │
│  🎭 Business logic focused on domain concepts                  │
│  🏷️ Minimal persistence infrastructure awareness               │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                 🗄️ Infrastructure Layer                        │
│                  (EF Core Configuration, Data Access)          │
│                                                                 │
│  ⚙️ Auto Entity Configuration Discovery                        │
│  📊 Convention-based configuration patterns                    │
│  🗃️ Automated data seeding orchestration                       │
│  📈 Static data management from enums                          │
│  🔍 Global query filter coordination                           │
│  🎯 Assembly scanning and registration                         │
└─────────────────────────────────────────────────────────────────┘
```

### DDD Benefits

1. **Clean Domain Models**: Entities focus on business logic, not persistence concerns
2. **Convention Alignment**: Configuration conventions match domain patterns
3. **Reference Data Management**: Static data handling aligned with business concepts
4. **Bounded Context Support**: Multi-assembly scanning for modular domain design
5. **Aggregate Consistency**: Global filters support aggregate boundary enforcement
6. **Ubiquitous Language**: Configuration expressed in business terms

### Onion Architecture Benefits

1. **Dependency Inversion**: Domain entities unaware of EF Core configuration specifics
2. **Separation of Concerns**: Configuration logic isolated from domain logic
3. **Technology Independence**: Convention-based approach reduces EF Core coupling
4. **Maintainability**: Centralized configuration management
5. **Testability**: Simplified test setup with consistent configuration patterns
6. **Modularity**: Support for multi-assembly domain organization

## How to use it

### Installation

```bash
dotnet add package DKNet.EfCore.Extensions
dotnet add package DKNet.EfCore.Abstractions
```

### Basic Usage Examples

#### 1. Automated Entity Configuration

```csharp
using DKNet.EfCore.Extensions;
using DKNet.EfCore.Abstractions;

// Base entity that all domain entities inherit from
public abstract class BaseEntity : IEntity<int>
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    protected BaseEntity()
    {
    }
}

// Generic configuration that applies to all entities inheriting from BaseEntity
internal class DefaultEntityTypeConfiguration<T> : IEntityTypeConfiguration<T> 
    where T : BaseEntity
{
    public virtual void Configure(EntityTypeBuilder<T> builder)
    {
        // Common configuration for all entities
        builder.HasKey(e => e.Id);
        
        // Add common properties if needed
        if (typeof(IAuditableEntity).IsAssignableFrom(typeof(T)))
        {
            builder.Property<DateTime>("CreatedAt")
                   .HasDefaultValueSql("GETUTCDATE()");
            builder.Property<DateTime>("UpdatedAt")
                   .HasDefaultValueSql("GETUTCDATE()");
            builder.Property<string>("CreatedBy")
                   .HasMaxLength(256);
            builder.Property<string>("UpdatedBy")
                   .HasMaxLength(256);
        }
        
        // Soft delete support
        if (typeof(ISoftDeletableEntity).IsAssignableFrom(typeof(T)))
        {
            builder.Property<bool>("IsDeleted")
                   .HasDefaultValue(false);
            builder.Property<DateTime?>("DeletedAt");
            builder.Property<string>("DeletedBy")
                   .HasMaxLength(256);
            
            builder.HasQueryFilter(e => !EF.Property<bool>(e, "IsDeleted"));
        }
    }
}

// Domain entities - clean and focused on business logic
public class Customer : BaseEntity, IAuditableEntity, ISoftDeletableEntity
{
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public string Email { get; private set; }
    public bool IsActive { get; private set; }
    
    // Audit properties (handled by configuration)
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string CreatedBy { get; set; }
    public string UpdatedBy { get; set; }
    
    // Soft delete properties (handled by configuration)
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string DeletedBy { get; set; }
    
    protected Customer() { } // EF Core constructor
    
    public Customer(string firstName, string lastName, string email)
    {
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        IsActive = true;
    }
    
    public void UpdateEmail(string newEmail)
    {
        if (string.IsNullOrWhiteSpace(newEmail))
            throw new ArgumentException("Email cannot be empty");
            
        Email = newEmail;
    }
    
    public void Deactivate()
    {
        IsActive = false;
    }
}

public class Product : BaseEntity
{
    public string Name { get; private set; }
    public string Description { get; private set; }
    public decimal Price { get; private set; }
    public int StockQuantity { get; private set; }
    public ProductCategory Category { get; private set; }
    
    protected Product() { } // EF Core constructor
    
    public Product(string name, string description, decimal price, ProductCategory category)
    {
        Name = name;
        Description = description;
        Price = price;
        Category = category;
        StockQuantity = 0;
    }
    
    public void UpdatePrice(decimal newPrice)
    {
        if (newPrice < 0)
            throw new ArgumentException("Price cannot be negative");
            
        Price = newPrice;
    }
    
    public void AddStock(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive");
            
        StockQuantity += quantity;
    }
}
```

#### 2. DbContext with Auto Configuration

```csharp
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }
    
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Order> Orders { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Auto-configure all entities using discovered configurations
        modelBuilder.UseAutoConfigModel();
    }
}

// Service registration with automatic configuration
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationDbContext(
        this IServiceCollection services, 
        string connectionString)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseSqlServer(connectionString)
                   .UseAutoConfigModel(); // Enables automatic entity configuration
        });
        
        return services;
    }
    
    // For multi-assembly scenarios
    public static IServiceCollection AddApplicationDbContextMultiAssembly(
        this IServiceCollection services, 
        string connectionString,
        params Assembly[] assemblies)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseSqlServer(connectionString)
                   .UseAutoConfigModel(config => config.ScanFrom(assemblies));
        });
        
        return services;
    }
}
```

#### 3. Custom Entity-Specific Configuration

```csharp
// When you need specific configuration for an entity, create a dedicated configuration
internal class CustomerTypeConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        // Specific configuration for Customer entity
        builder.Property(c => c.FirstName)
               .IsRequired()
               .HasMaxLength(100);
               
        builder.Property(c => c.LastName)
               .IsRequired()
               .HasMaxLength(100);
               
        builder.Property(c => c.Email)
               .IsRequired()
               .HasMaxLength(256);
               
        builder.HasIndex(c => c.Email)
               .IsUnique();
               
        // Computed column
        builder.Property(c => c.FullName)
               .HasComputedColumnSql("CONCAT([FirstName], ' ', [LastName])");
    }
}

// Complex entity with relationships
internal class OrderTypeConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.Property(o => o.OrderNumber)
               .IsRequired()
               .HasMaxLength(50);
               
        builder.Property(o => o.TotalAmount)
               .HasColumnType("decimal(18,2)");
               
        builder.HasIndex(o => o.OrderNumber)
               .IsUnique();
               
        // Relationships
        builder.HasOne(o => o.Customer)
               .WithMany(c => c.Orders)
               .HasForeignKey(o => o.CustomerId)
               .OnDelete(DeleteBehavior.Restrict);
               
        builder.HasMany(o => o.OrderItems)
               .WithOne(oi => oi.Order)
               .HasForeignKey(oi => oi.OrderId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
```

#### 4. Data Seeding Management

```csharp
// Organized data seeding using IDataSeedingConfiguration
public class DefaultCustomerData : IDataSeedingConfiguration<Customer>
{
    public ICollection<Customer> Data => new[]
    {
        new Customer("John", "Doe", "john.doe@example.com") { Id = 1 },
        new Customer("Jane", "Smith", "jane.smith@example.com") { Id = 2 },
        new Customer("Bob", "Johnson", "bob.johnson@example.com") { Id = 3 }
    };
}

public class DefaultProductData : IDataSeedingConfiguration<Product>
{
    public ICollection<Product> Data => new[]
    {
        new Product("Laptop", "High-performance laptop", 999.99m, ProductCategory.Electronics) { Id = 1 },
        new Product("Office Chair", "Ergonomic office chair", 299.99m, ProductCategory.Furniture) { Id = 2 },
        new Product("Notebook", "Professional notebook", 19.99m, ProductCategory.Office) { Id = 3 }
    };
}

// Environment-specific seeding
public class DevelopmentCustomerData : IDataSeedingConfiguration<Customer>
{
    public ICollection<Customer> Data => new[]
    {
        new Customer("Test", "User", "test@example.com") { Id = 100 },
        new Customer("Demo", "Account", "demo@example.com") { Id = 101 }
    };
}

// Conditional seeding based on environment
public class ConditionalDataSeedingConfiguration<T> : IDataSeedingConfiguration<T> where T : class
{
    private readonly IHostEnvironment _environment;
    private readonly IDataSeedingConfiguration<T> _productionData;
    private readonly IDataSeedingConfiguration<T> _developmentData;
    
    public ConditionalDataSeedingConfiguration(
        IHostEnvironment environment,
        IDataSeedingConfiguration<T> productionData,
        IDataSeedingConfiguration<T> developmentData)
    {
        _environment = environment;
        _productionData = productionData;
        _developmentData = developmentData;
    }
    
    public ICollection<T> Data => _environment.IsDevelopment() 
        ? _developmentData.Data 
        : _productionData.Data;
}
```

#### 5. Static Data Management with Enums

```csharp
// Enum-based static data that gets stored as reference tables
[StaticData(nameof(OrderStatus))]
public enum OrderStatus
{
    [Display(Name = "Pending", Description = "Order is pending processing")]
    Pending = 1,
    
    [Display(Name = "Processing", Description = "Order is being processed")]
    Processing = 2,
    
    [Display(Name = "Shipped", Description = "Order has been shipped")]
    Shipped = 3,
    
    [Display(Name = "Delivered", Description = "Order has been delivered")]
    Delivered = 4,
    
    [Display(Name = "Cancelled", Description = "Order has been cancelled")]
    Cancelled = 5
}

[StaticData(nameof(ProductCategory))]
public enum ProductCategory
{
    [Display(Name = "Electronics")]
    Electronics = 1,
    
    [Display(Name = "Furniture")]
    Furniture = 2,
    
    [Display(Name = "Office Supplies")]
    Office = 3,
    
    [Display(Name = "Books")]
    Books = 4
}

// The framework automatically creates these tables:
// OrderStatus table with Id, Name, Description columns
// ProductCategory table with Id, Name columns

// Usage in entities
public class Order : BaseEntity
{
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public ProductCategory Category { get; set; }
    
    // EF Core will automatically handle the enum-to-database mapping
}
```

#### 6. Global Query Filters

```csharp
// Centralized global filter management
public class SoftDeleteQueryRegister : IGlobalModelBuilderRegister
{
    public void Apply(ModelBuilder? modelBuilder, DbContext context)
    {
        // Apply soft delete filter to all ISoftDeletableEntity implementations
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDeletableEntity).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var property = Expression.Property(parameter, nameof(ISoftDeletableEntity.IsDeleted));
                var filter = Expression.Lambda(Expression.Not(property), parameter);
                
                entityType.SetQueryFilter(filter);
            }
        }
    }
}

public class MultiTenantQueryRegister : IGlobalModelBuilderRegister
{
    private readonly ICurrentTenantService _currentTenantService;
    
    public MultiTenantQueryRegister(ICurrentTenantService currentTenantService)
    {
        _currentTenantService = currentTenantService;
    }
    
    public void Apply(ModelBuilder? modelBuilder, DbContext context)
    {
        // Apply tenant filter to all ITenantEntity implementations
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                var currentTenantId = _currentTenantService.GetCurrentTenantId();
                
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var property = Expression.Property(parameter, nameof(ITenantEntity.TenantId));
                var constant = Expression.Constant(currentTenantId);
                var filter = Expression.Lambda(Expression.Equal(property, constant), parameter);
                
                entityType.SetQueryFilter(filter);
            }
        }
    }
}

// Registration
public class ApplicationDbContext : DbContext
{
    private readonly IEnumerable<IGlobalModelBuilderRegister> _globalRegisters;
    
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        IEnumerable<IGlobalModelBuilderRegister> globalRegisters) : base(options)
    {
        _globalRegisters = globalRegisters;
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Apply automatic configuration
        modelBuilder.UseAutoConfigModel();
        
        // Apply global filters
        foreach (var register in _globalRegisters)
        {
            register.Apply(modelBuilder, this);
        }
    }
}
```

#### 7. Excluding Entities from Auto-Configuration

```csharp
// Sometimes you want to exclude certain entities from automatic configuration
[IgnoreEntityMapper]
public class TemporaryEntity : BaseEntity
{
    public string TempData { get; set; }
    
    // This entity will not be automatically configured
    // You must provide explicit configuration if needed
}

// Or exclude at configuration level
public class ApplicationDbContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.UseAutoConfigModel(options =>
        {
            options.ExcludeTypes(typeof(TemporaryEntity), typeof(AnotherExcludedEntity));
        });
    }
}
```

### Advanced Usage Examples

#### 1. Multi-Assembly Configuration

```csharp
// For large applications with multiple domain assemblies
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDomainDbContext(
        this IServiceCollection services,
        string connectionString)
    {
        var domainAssemblies = new[]
        {
            typeof(Customer).Assembly,      // Customer domain
            typeof(Product).Assembly,       // Product domain  
            typeof(Order).Assembly,         // Order domain
            typeof(Inventory).Assembly      // Inventory domain
        };
        
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseSqlServer(connectionString)
                   .UseAutoConfigModel(config => config.ScanFrom(domainAssemblies));
        });
        
        return services;
    }
}
```

#### 2. Configuration Inheritance

```csharp
// Base configuration for auditable entities
public abstract class AuditableEntityConfiguration<T> : IEntityTypeConfiguration<T>
    where T : BaseEntity, IAuditableEntity
{
    public virtual void Configure(EntityTypeBuilder<T> builder)
    {
        // Common auditable entity configuration
        builder.Property(e => e.CreatedAt)
               .IsRequired();
               
        builder.Property(e => e.UpdatedAt)
               .IsRequired();
               
        builder.Property(e => e.CreatedBy)
               .HasMaxLength(256);
               
        builder.Property(e => e.UpdatedBy)
               .HasMaxLength(256);
               
        // Add indexes for common queries
        builder.HasIndex(e => e.CreatedAt);
        builder.HasIndex(e => e.UpdatedAt);
    }
}

// Specific configuration inheriting from base
public class CustomerConfiguration : AuditableEntityConfiguration<Customer>
{
    public override void Configure(EntityTypeBuilder<Customer> builder)
    {
        // Apply base configuration
        base.Configure(builder);
        
        // Add customer-specific configuration
        builder.Property(c => c.Email)
               .IsRequired()
               .HasMaxLength(256);
               
        builder.HasIndex(c => c.Email)
               .IsUnique();
    }
}
```

#### 3. Dynamic Configuration

```csharp
public class DynamicEntityConfiguration<T> : IEntityTypeConfiguration<T> where T : BaseEntity
{
    private readonly IConfiguration _configuration;
    
    public DynamicEntityConfiguration(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    
    public void Configure(EntityTypeBuilder<T> builder)
    {
        var entityName = typeof(T).Name;
        var configSection = _configuration.GetSection($"EntityConfiguration:{entityName}");
        
        // Apply configuration from appsettings
        if (configSection.Exists())
        {
            var auditEnabled = configSection.GetValue<bool>("AuditEnabled");
            var softDeleteEnabled = configSection.GetValue<bool>("SoftDeleteEnabled");
            
            if (auditEnabled && typeof(IAuditableEntity).IsAssignableFrom(typeof(T)))
            {
                ApplyAuditConfiguration(builder);
            }
            
            if (softDeleteEnabled && typeof(ISoftDeletableEntity).IsAssignableFrom(typeof(T)))
            {
                ApplySoftDeleteConfiguration(builder);
            }
        }
    }
    
    private void ApplyAuditConfiguration(EntityTypeBuilder<T> builder)
    {
        // Apply audit configuration dynamically
    }
    
    private void ApplySoftDeleteConfiguration(EntityTypeBuilder<T> builder)
    {
        // Apply soft delete configuration dynamically
    }
}
```

## Best Practices

### 1. Configuration Organization

```csharp
// Good: Organize configurations by domain/feature
namespace MyApp.Infrastructure.Persistence.Configurations.Customer
{
    internal class CustomerConfiguration : IEntityTypeConfiguration<Customer> { }
    internal class CustomerAddressConfiguration : IEntityTypeConfiguration<CustomerAddress> { }
}

namespace MyApp.Infrastructure.Persistence.Configurations.Order
{
    internal class OrderConfiguration : IEntityTypeConfiguration<Order> { }
    internal class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem> { }
}

// Good: Use base configurations for common patterns
public abstract class BaseEntityConfiguration<T> : IEntityTypeConfiguration<T> 
    where T : BaseEntity
{
    // Common configuration
}
```

### 2. Entity Design

```csharp
// Good: Clean domain entities
public class Product : BaseEntity
{
    public string Name { get; private set; }
    public decimal Price { get; private set; }
    
    protected Product() { } // EF Core
    
    public Product(string name, decimal price)
    {
        Name = name;
        Price = price;
    }
    
    public void UpdatePrice(decimal newPrice)
    {
        if (newPrice < 0) throw new ArgumentException("Price cannot be negative");
        Price = newPrice;
    }
}

// Avoid: EF Core concerns in domain entities
public class Product : BaseEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }
}
```

### 3. Testing

```csharp
[Test]
public void AutoConfiguration_AppliesCorrectly()
{
    // Arrange
    var options = new DbContextOptionsBuilder<TestDbContext>()
        .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
        .UseAutoConfigModel()
        .Options;
    
    // Act
    using var context = new TestDbContext(options);
    var entityType = context.Model.FindEntityType(typeof(Customer));
    
    // Assert
    Assert.NotNull(entityType);
    var emailProperty = entityType.FindProperty(nameof(Customer.Email));
    Assert.NotNull(emailProperty);
    Assert.True(emailProperty.IsUniqueIndex());
}
```

## Integration with Other DKNet Components

DKNet.EfCore.Extensions integrates seamlessly with other DKNet components:

- **DKNet.EfCore.Abstractions**: Uses entity base classes and interfaces
- **DKNet.EfCore.Events**: Supports automatic event entity configuration
- **DKNet.EfCore.Hooks**: Integrates with hook registration patterns
- **DKNet.EfCore.Repos**: Provides configured entities for repository patterns
- **DKNet.Fw.Extensions**: Leverages core framework utilities

---

> 💡 **Configuration Tip**: Use DKNet.EfCore.Extensions to reduce configuration boilerplate and maintain consistency across your EF Core entities. The convention-based approach helps maintain clean domain models while ensuring proper database mapping. Always provide specific configurations for entities that deviate from conventions, and use the exclude mechanisms when you need fine-grained control.