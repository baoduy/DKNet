# Migration Guide

This guide helps you migrate between different versions of DKNet Framework and provides guidance for handling breaking changes.

## Table of Contents

- [Current migration scenarios](#current-migration-scenarios)
- [Version-specific migrations](#version-specific-migrations)
  - [Off `DKNet.EfCore.Repos` onto Specifications](#entity-framework-core-migration)
  - [`DKNet.Svc.Encryption` — AES ciphers are now opt-in](#dknetsvcencryption--aes-ciphers-are-now-opt-in)
  - [`DKNet.EfCore.DataAuthorization` — `IDataOwnerDbContext` is now required](#upgrading-dknetefcoredataauthorization-idataownerdbcontext-is-now-required)
- [Architecture migration](#architecture-migration)
- [CQRS migration](#cqrs-migration)
- [Database migration](#database-migration)
- [Testing migration](#testing-migration)
- [Migration tools](#migration-tools)
- [Common issues](#common-issues)
- [Migration checklist](#migration-checklist)

---

## Current Migration Scenarios

### From Legacy DKNet to 2024.12.0+

This is a **major architectural migration** from legacy packages to the new Domain-Driven Design framework.

#### Key Changes
- **Architecture**: complete shift to DDD/Onion Architecture — see the [Architecture Guide](Architecture.md)
- **Technology**: .NET 10.0; every package targets `net10.0` with `LangVersion=latest`, except the two Roslyn
  source generators (`DKNet.EfCore.DtoGenerator`, `DKNet.SlimBus.Generators`) which target `netstandard2.0` to be
  loadable by the compiler
- **Patterns**: CQRS via SlimMessageBus, the specification pattern instead of generic repositories, and domain
  events dispatched from the `SaveChanges` pipeline
- **Testing**: TestContainers.MsSql for anything touching persistence — see [Testing Strategy](Testing-Strategy.md)

Released versions and their breaking changes are listed in the [Changelog](CHANGELOG.md).

#### Migration Strategy

**1. Assessment Phase**
```bash
# Analyze your current usage
git grep -r "DKNet" --include="*.cs" src/
# Review dependencies
dotnet list package --include-transitive | grep DKNet
```

**2. Incremental Migration**
- Start with new projects using the SlimBus.ApiEndpoints template in the [DKNet.Templates](https://github.com/baoduy/DKNet.Templates) repository
- Migrate existing projects component by component
- Run both old and new implementations in parallel during transition

**3. Component Migration Order**
1. **Core Extensions** → `DKNet.Fw.Extensions`
2. **Data Access** → `DKNet.EfCore.*` packages
3. **Business Logic** → Domain entities and services
4. **API Layer** → Controllers/endpoints
5. **Infrastructure** → External service integrations

---

## Version-Specific Migrations

### Upgrading to .NET 10.0

**Prerequisites** — install the .NET 10 SDK from
[dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/10.0), then pin it. DKNet's own
`src/global.json` uses `rollForward` so a newer patch SDK still builds:

```json
{
  "sdk": {
    "version": "10.0.0",
    "rollForward": "latestMajor",
    "allowPrerelease": false
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

**Language version.** Every DKNet project sets `LangVersion=latest`, so on .NET 10 the packages are built with
C# 14 — including the C# 14 extension-member syntax several of them use for their public API
(`SpecRepoExtensions`, `SetupEfCoreHook`, `PropertyExtensions`). Your own project does not have to match: the
compiled API surface is ordinary extension methods, callable from any language version that can consume
`net10.0`.

### Entity Framework Core Migration

**Before (Legacy)**
```csharp
using Microsoft.EntityFrameworkCore;

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

**After — `DKNet.EfCore.Specifications`**

`DKNet.EfCore.Repos` is retired; `IRepositorySpec` (registered via `services.AddSpecRepo<AppDbContext>()`) is the
current repository surface. It is not generic over the entity — the entity type comes from the `Specification<T>`
passed to each call:

```csharp
using DKNet.EfCore.Specifications.Definitions;
using DKNet.EfCore.Specifications.Extensions;
using DKNet.EfCore.Specifications.Repositories;

public sealed class ProductService(IRepositorySpec repo)
{
    // Specification support
    public Task<Product?> FindAsync(Specification<Product> spec, CancellationToken cancellationToken = default) =>
        repo.FirstOrDefaultAsync(spec, cancellationToken);
}
```

See [`Migrating-Repos-To-Specifications.md`](./EfCore/Migrating-Repos-To-Specifications.md) for the full call-site mapping.

### DKNet.Svc.Encryption — AES ciphers are now opt-in

**Symptom**

The DI container throws `InvalidOperationException` ("Unable to resolve service for type
`DKNet.Svc.Encryption.Ciphers.IAesGcmEncryption`") where injecting the cipher used to work. `AddEncryptionServices()`
registered `IAesGcmEncryption` and the obsolete `IAesEncryption` as transients over a randomly generated key that was
never persisted, so the resolution that used to succeed was already producing ciphertext nothing could decrypt.

**Before**
```csharp
using DKNet.Svc.Encryption;

builder.Services.AddEncryptionServices(); // also registered IAesGcmEncryption and IAesEncryption
```

**After** — supply the key from configuration or a key vault:
```csharp
using DKNet.Svc.Encryption;

builder.Services.AddEncryptionServices();                                       // IShaHashing, IHmacHashing
builder.Services.AddAesGcmEncryption(builder.Configuration["Crypto:AesKey"]!);  // singleton IAesGcmEncryption
```

`AddAesGcmEncryption` takes a plain Base64 AES key (128/192/256-bit); a `key:iv` string is rejected. Consumers still on
the obsolete AES-CBC type call `AddAesEncryption(keyString)` with the combined `key:iv` value that `AesEncryption.Key`
returns. Ciphertext produced under the old registration cannot be recovered — its key was discarded with the instance.

### Upgrading DKNet.EfCore.DataAuthorization: IDataOwnerDbContext is now required

**Symptom** — after upgrading, a registration that used to build now fails to compile with a generic-constraint
error (`CS0311`) on the `AddDataOwnerProvider<TDbContext, TProvider>()` call, saying your `DbContext` cannot be used
as `TDbContext` because it is not convertible to `DKNet.EfCore.DataAuthorization.IDataOwnerDbContext`.

**Cause** — `AddDataOwnerProvider<TDbContext, TProvider>()` is now declared
`where TDbContext : DbContext, IDataOwnerDbContext` (it previously constrained `TDbContext` to `DbContext` only).
The old signature let you register a `DbContext` that the ownership query filter could not read: the filter was then
skipped for every `IOwnedBy` entity in Release builds, so every caller saw every owner's rows. The compile error is
the fix surfacing a real, previously silent, data-isolation hole.

**Fix** — implement `IDataOwnerDbContext` on the exact `DbContext` type you register:

```csharp
using DKNet.EfCore.DataAuthorization;
using Microsoft.EntityFrameworkCore;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IDataOwnerDbContext
{
    public IEnumerable<string> AccessibleKeys { get; init; } = [];
    // IsUnrestrictedAccess defaults to false via the interface —
    // override it only for system/admin contexts that must bypass ownership filtering.
}
```

Keep `AccessibleKeys` declared as `IEnumerable<string>` — EF Core cannot translate `ICollection<string>.Contains`
inside a query filter.

The ownership filter is attached by `UseAutoConfigModel<TContext>()`, which is a
`DbContextOptionsBuilder<TContext>` extension — it belongs in the registration callback, **not** in
`OnModelCreating`, where no `DbContextOptionsBuilder` exists:

```csharp
using DKNet.EfCore.DataAuthorization;
using DKNet.EfCore.Hooks;
using Microsoft.EntityFrameworkCore;

services
    .AddDataOwnerProvider<AppDbContext, TenantOwnerProvider>()
    .AddDbContextWithHook<AppDbContext>(options => options
        .UseSqlServer(connectionString)
        .UseAutoConfigModel<AppDbContext>());   // required for the filter to be attached
```

**Also check every other `DbContext` in the process** — the compile error is not the only way this upgrade bites.
The same release makes `DataOwnerAuthQuery` throw `InvalidOperationException` at model-build time when a context
reaches the filter without the interface, instead of quietly applying no filter. And `AddDataOwnerProvider`
registers that filter in `EfCoreSetup.GlobalModelBuilders`, which is **static**: every `DbContext` calling
`UseAutoConfigModel()` applies it, not only the one you passed as `TDbContext`. A second context — an audit or
reporting `DbContext`, say — whose model contains `IOwnedBy` entities and that does not implement
`IDataOwnerDbContext` therefore starts throwing at model-build time after this upgrade, with no compile error
pointing at it. Implement `IDataOwnerDbContext` on that context too, or keep `IOwnedBy` entities out of its model.
Details: [DKNet.EfCore.DataAuthorization](./EfCore/DKNet.EfCore.DataAuthorization.md).

---

## Architecture Migration

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

**After** — there is no separate `AggregateRoot` type; derive from `AuditedEntity` (or plain `Entity` if you don't
need audit fields) — see [DKNet.EfCore.Abstractions](EfCore/DKNet.EfCore.Abstractions.md):
```csharp
using DKNet.EfCore.Abstractions.Entities;
using System.ComponentModel.DataAnnotations.Schema;

[Table("Products", Schema = "catalog")]
public class Product : AuditedEntity
{
    private Product() { } // EF Core

    public static Product Create(string name, decimal price, string createdBy)
    {
        var product = new Product { Name = name, Price = price };
        product.SetCreatedBy(createdBy);
        return product;
    }

    public string Name { get; private set; } = null!;
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

## CQRS Migration

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

**After (CQRS via `DKNet.SlimBus.Extensions`)** — see
[DKNet.SlimBus.Extensions](Messaging/DKNet.SlimBus.Extensions.md) for the full contract set:
```csharp
using DKNet.SlimBus.Extensions;
using FluentResults;

// Command
public record CreateProductCommand(string Name, decimal Price) : Fluents.Requests.IWitResponse<Guid>;

internal sealed class CreateProductHandler(AppDbContext db) : Fluents.Requests.IHandler<CreateProductCommand, Guid>
{
    public async Task<IResult<Guid>> OnHandle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        // Command handling logic — no explicit SaveChangesAsync; the auto-save interceptor does it on success.
        return Result.Ok(Guid.NewGuid());
    }
}

// Query
public record GetProductQuery(Guid Id) : Fluents.Queries.IWitResponse<ProductResult>;

internal sealed class GetProductHandler(AppDbContext db) : Fluents.Queries.IHandler<GetProductQuery, ProductResult>
{
    public async Task<ProductResult?> OnHandle(GetProductQuery request, CancellationToken cancellationToken)
    {
        // Query handling logic
        return null;
    }
}
```

---

## Database Migration

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

## Testing Migration

DKNet's own suite uses xUnit, Shouldly, and TestContainers.MsSql, and the same reasoning applies to your tests of
DKNet-backed persistence: the EF Core in-memory provider does not translate global query filters, generated SQL,
or sequences, so it silently passes tests that would fail against a real database. See
[Testing Strategy](Testing-Strategy.md).

**Before** — in-memory, so the ownership filter and the generated SQL are never exercised:

```csharp
using Microsoft.EntityFrameworkCore;
using Xunit;

[Fact]
public async Task ActiveProducts_AreReturned()
{
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase("TestDb")
        .Options;

    await using var context = new AppDbContext(options);

    // Test logic
}
```

**After** — a real SQL Server, with the model built the way production builds it:

```csharp
using DKNet.EfCore.Hooks;
using DKNet.EfCore.Specifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;
using Xunit;

[Fact]
public async Task ActiveProducts_AreReturned()
{
    await using var container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    await container.StartAsync();

    var services = new ServiceCollection();
    services.AddDbContextWithHook<AppDbContext>(options => options
        .UseSqlServer(container.GetConnectionString())
        // Keep this: dropping it removes the global query filters, and the test then
        // passes for the wrong reason.
        .UseAutoConfigModel<AppDbContext>());
    services.AddSpecRepo<AppDbContext>();

    // Test against the real provider
}
```

> `mcr.microsoft.com/mssql/server` publishes no ARM64 image. On an ARM development machine, run these tests on an
> x64 runner rather than substituting a different engine — DKNet does this via the `remote-tests.yml` workflow.

## Migration Tools

### Automated Migration Helper

```csharp
using Microsoft.Extensions.DependencyInjection;

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
            var newProduct = Product.Create(
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

There is no `DKNetOptions` and no single aggregator to configure DKNet through — each package exposes its own
strongly-typed options, bound from your own config section via that package's own `Add*` extension. See
[Configuration & Setup](Configuration.md) for the full list; for example, migrating blob storage config:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class ConfigurationMigration
{
    public static IServiceCollection MigrateFromLegacy(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Point each package's own Add* method at your configuration, then let it bind itself.
        // The section name is the options type's own constant — LocalDirectoryOptions.Name is
        // "BlobStorage:LocalFolder"; the Azure and S3 adapters use "BlobService:AzureStorage"
        // and "BlobService:S3".
        services.AddLocalDirectoryBlobService(configuration);

        return services;
    }
}
```

---

## Common Issues

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
using DKNet.EfCore.Specifications;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.Serialization.SystemTextJson;

// Old (e.g. a hand-rolled service, or a MediatR-based handler)
services.AddScoped<ProductService>();

// New: one IRepositorySpec for every entity, plus SlimBus (DKNet's MediatR-free CQRS package).
// No per-entity repository registration, and no per-handler registration either.
services.AddSpecRepo<AppDbContext>();
services.AddSlimMessageBus(mbb => mbb
    .AddJsonSerializer()
    .AddServicesFromAssembly(typeof(CreateProductHandler).Assembly)
    .AddChildBus("Memory", builder => builder
        .WithProviderMemory()
        .AutoDeclareFrom(typeof(CreateProductHandler).Assembly)));
```

---

## Migration Checklist

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

## Getting Help

If you encounter issues during migration:

1. **Check Documentation**: Review [Getting Started](Getting-Started.md) and [Examples](Examples/README.md)
2. **Search Issues**: Look for similar problems in [GitHub Issues](https://github.com/baoduy/DKNet/issues)
3. **Ask Questions**: Create a new issue with the `migration` label
4. **Join Discussions**: Participate in [GitHub Discussions](https://github.com/baoduy/DKNet/discussions)

---

> 💡 **Migration Tip**: Take your time with migration. It's better to migrate correctly in stages than to rush and introduce bugs. Use the SlimBus template as your reference implementation!