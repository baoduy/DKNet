# Configuration & Setup Guide

This guide covers configuration and setup options for all DKNet Framework components.

## 📋 Table of Contents

- [Core Framework Configuration](#core-framework-configuration)
- [Entity Framework Core Setup](#entity-framework-core-setup)
- [Messaging & CQRS Configuration](#messaging--cqrs-configuration)
- [Blob Storage Configuration](#blob-storage-configuration)
- [Authentication & Authorization](#authentication--authorization)
- [Development Environment](#development-environment)
- [Production Deployment](#production-deployment)

## 🔧 Core Framework Configuration

### Basic ServiceCollection Setup

```csharp
public void ConfigureServices(IServiceCollection services)
{
    // DKNet ships no single "core" aggregator — each package (EF Core, SlimBus, Blob Storage, etc.)
    // is registered independently via its own extension method, shown in the sections below.

    // Add logging
    services.AddLogging();

    // Add configuration
    services.Configure<DKNetOptions>(Configuration.GetSection("DKNet"));
}
```

### Configuration Options

```json
{
  "DKNet": {
    "EnableAuditFields": true,
    "DefaultTimeZone": "UTC",
    "EnableSoftDelete": true,
    "MaxPageSize": 1000
  }
}
```

## 🗄️ Entity Framework Core Setup

### Database Context Configuration

```csharp
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply DKNet configurations
        modelBuilder.ApplyDKNetConfigurations();
        
        // Apply entity configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Enable DKNet hooks and events
        optionsBuilder.EnableDKNetHooks();
        optionsBuilder.EnableDKNetEvents();
    }
}
```

### Repository Registration

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(connectionString));

    // Register the specification repository (DKNet.EfCore.Specifications)
    services.AddSpecRepo<AppDbContext>();
    
    // Register specific repositories
    services.AddScoped<IProductRepository, ProductRepository>();
}
```

### Migration Configuration

```csharp
// Add migration with proper naming
dotnet ef migrations add InitialCreate --context AppDbContext

// Update database
dotnet ef database update --context AppDbContext
```

### Connection String Configuration

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=DKNetApp;Trusted_Connection=true;",
    "Production": "Server=prod-server;Database=DKNetApp;User Id=sa;Password=***;"
  }
}
```

## 📨 Messaging & CQRS Configuration

### SlimBus Setup

```csharp
public void ConfigureServices(IServiceCollection services)
{
    // Auto-save the DbContext after a successful request handler (DKNet.SlimBus.Extensions)
    services.AddSlimBusEfCoreInterceptor<AppDbContext>();

    // Optionally forward domain events raised via DKNet.EfCore.Events onto the bus
    services.AddSlimBusEventPublisher<AppDbContext>();

    // Wire up SlimMessageBus itself (transport-agnostic; plain SlimMessageBus API).
    // DKNet brings no transport provider — plug in SlimMessageBus.Host.Memory, an Azure Service Bus
    // provider, or any other SlimMessageBus provider package yourself.
    services.AddSlimMessageBus(mbb => mbb
        .AddChildBus("Memory", bus => bus
            .WithProviderMemory()
            .AutoDeclareFrom(typeof(Program).Assembly)));
}
```

### Command/Query Configuration

```csharp
// Configure CQRS pipeline
services.AddScoped<ICommandHandler<CreateProductCommand>, CreateProductHandler>();
services.AddScoped<IQueryHandler<GetProductQuery>, GetProductHandler>();

// Add validation
services.AddFluentValidation(fv => 
    fv.RegisterValidatorsFromAssemblyContaining<CreateProductValidator>());
```

## 🗃️ Blob Storage Configuration

### Azure Blob Storage

```csharp
public void ConfigureServices(IServiceCollection services)
{
    // DKNet.Svc.BlobStorage.AzureStorage — binds AzureStorageOptions from the "BlobService:AzureStorage" section
    services.AddAzureStorageAdapter(Configuration);
}
```

```json
{
  "ConnectionStrings": {
    "AzureStorage": "DefaultEndpointsProtocol=https;AccountName=***;AccountKey=***"
  }
}
```

### AWS S3 Storage

```csharp
public void ConfigureServices(IServiceCollection services)
{
    // DKNet.Svc.BlobStorage.AwsS3 — binds options from the "BlobService:S3" section
    services.AddS3BlobService(Configuration);
}
```

### Local File Storage

```csharp
public void ConfigureServices(IServiceCollection services)
{
    // DKNet.Svc.BlobStorage.Local — binds LocalDirectoryOptions from the "BlobStorage:LocalFolder" section
    services.AddLocalDirectoryBlobService(Configuration);
}
```

## 🔐 Authentication & Authorization

### Data Authorization Setup

```csharp
public void ConfigureServices(IServiceCollection services)
{
    // DKNet.EfCore.DataAuthorization — registers the data-ownership query filter and hook for AppDbContext,
    // backed by your own IDataOwnerProvider implementation (e.g. reading the tenant id from the current user claims)
    services.AddDataOwnerProvider<AppDbContext, TenantOwnerProvider>();
}
```

### Entity Configuration for Data Authorization

```csharp
public class Product : AggregateRoot, ITenantEntity
{
    public string TenantId { get; set; }
    public string Name { get; set; }
    // ... other properties
}
```

## 🛠️ Development Environment

### Local Development Setup

```bash
# Install .NET 10.0 SDK
./src/dotnet-install.sh --version 10.0.100

# Restore packages
cd src
dotnet restore DKNet.FW.sln

# Run with development settings
dotnet run --environment Development
```

### Development Configuration

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "DKNet": "Debug",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  "DKNet": {
    "EnableDetailedErrors": true,
    "EnableSensitiveDataLogging": true
  }
}
```

### Docker Development

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["*.csproj", "./"]
RUN dotnet restore
COPY . .
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "YourApp.dll"]
```

## 🚀 Production Deployment

### Production Configuration

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "DKNet": "Information"
    }
  },
  "DKNet": {
    "EnableDetailedErrors": false,
    "EnableSensitiveDataLogging": false,
    "MaxPageSize": 100
  }
}
```

### Health Checks

DKNet does not ship a health-check package. Wire up standard ASP.NET Core health checks in your own app:

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddHealthChecks()
        .AddDbContextCheck<AppDbContext>();
}

public void Configure(IApplicationBuilder app)
{
    app.UseHealthChecks("/health");
}
```

### Performance Monitoring

DKNet does not ship a performance-counters package; use standard .NET observability tooling such as Application Insights:

```csharp
public void ConfigureServices(IServiceCollection services)
{
    // Add Application Insights
    services.AddApplicationInsightsTelemetry();
}
```

## 🔧 Advanced Configuration

### Custom Entity Configuration

```csharp
public class ProductConfiguration : DefaultEntityTypeConfiguration<Product>
{
    public override void Configure(EntityTypeBuilder<Product> builder)
    {
        base.Configure(builder);
        
        builder.HasIndex(p => p.Name).IsUnique();
        builder.Property(p => p.Name).HasMaxLength(200);
        
        // Configure relationships
        builder.HasMany(p => p.Categories)
               .WithOne(c => c.Product)
               .HasForeignKey(c => c.ProductId);
    }
}
```

### Custom Domain Events

```csharp
public class ProductCreatedEvent : DomainEvent
{
    public string ProductId { get; }
    public string ProductName { get; }
    
    public ProductCreatedEvent(string productId, string productName)
    {
        ProductId = productId;
        ProductName = productName;
    }
}

public class ProductCreatedHandler : IDomainEventHandler<ProductCreatedEvent>
{
    public async Task Handle(ProductCreatedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Handle the event
        await Task.CompletedTask;
    }
}
```

## 🧪 Testing Configuration

### Test Environment Setup

```csharp
public class TestStartup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Use in-memory database for testing
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase("TestDb"));

        // Register your own test-specific fakes/mocks here — DKNet has no dedicated test-services package
    }
}
```

### Integration Test Configuration

```csharp
public class IntegrationTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace database with test database
            services.RemoveAll(typeof(DbContextOptions<AppDbContext>));
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase("TestDb"));
        });
    }
}
```

---

## 📖 Related Documentation

- [Getting Started](Getting-Started.md)
- [Architecture Guide](Architecture.md)
- [API Reference](API-Reference.md)
- [Examples](Examples/README.md)
- [Troubleshooting](FAQ.md#troubleshooting)

---

> 💡 **Configuration Tip**: Use the options pattern for all configuration to enable easy testing and environment-specific settings.