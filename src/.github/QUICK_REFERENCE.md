# DKNet Quick Reference Card

**Quick lookup for common patterns and decisions**

## 🎯 Entity Design

```csharp
// Standard entity with Guid key
public class Product : Entity
{
    private Product() { } // EF Core
    public Product(string name, decimal price) { /* ... */ }
    public string Name { get; private set; }
    public void UpdateName(string name) { /* ... */ }
}

// Audited entity
public class Customer : AuditedEntity<Guid>
{
    // Inherits: Id, CreatedBy, CreatedOn, UpdatedBy, UpdatedOn
}

// With soft delete
public class Order : AuditedEntity<Guid>, ISoftDeletableEntity
{
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedOn { get; set; }
}
```

## 📦 Repository Pattern

| Operation | Interface | Method |
|-----------|-----------|--------|
| Read single | `IReadRepository<T>` | `FindAsync(id)` |
| Read query | `IReadRepository<T>` | `Query().Where(...).ToListAsync()` |
| Read with projection | `IReadRepository<T>` | `Query().ProjectToType<TDto>()` |
| Create | `IRepository<T>` | `AddAsync(entity)` + `SaveChangesAsync()` |
| Update | `IRepository<T>` | `UpdateAsync(entity)` |
| Delete | `IRepository<T>` | `Delete(entity)` + `SaveChangesAsync()` |
| Transaction | `IRepository<T>` | `BeginTransactionAsync()` |

## 🎬 CQRS with SlimBus

```csharp
// Query
public record GetProductQuery(Guid Id) : IWithResponse<ProductResult>;

// Query handler
public class Handler : IRequestHandler<GetProductQuery, Result<ProductResult>>
{
    public async Task<Result<ProductResult>> Handle(GetProductQuery request, CancellationToken ct)
    {
        var product = await _repository.FindAsync(request.Id, ct);
        return product == null 
            ? Result.Fail<ProductResult>("Not found")
            : Result.Ok(product.Adapt<ProductResult>());
    }
}

// API endpoint
products.MapGet<GetProductQuery, ProductResult>("/{id}");
```

## 🌱 Data Seeding

```csharp
// Static seeding
public class CategorySeeding : DataSeedingConfiguration<Category>
{
    protected override ICollection<Category> HasData => 
        [new(1, "Electronics"), new(2, "Books")];
}

// Dynamic seeding  
public class UserSeeding : DataSeedingConfiguration<User>
{
    public override Func<DbContext, bool, CancellationToken, Task>? SeedAsync { get; } =
        async (ctx, performed, ct) => { /* seed logic */ };
}
```

## ⚙️ Configuration

```csharp
public class ProductConfig : DefaultEntityTypeConfiguration<Product>
{
    public override void Configure(EntityTypeBuilder<Product> builder)
    {
        base.Configure(builder); // ALWAYS call first!
        
        builder.Property(p => p.Name).HasMaxLength(255);
        builder.HasIndex(p => p.Sku).IsUnique();
    }
}
```

## 🔍 Specifications

```csharp
public class ActiveProductsSpec : Specification<Product>
{
    public ActiveProductsSpec()
    {
        AddFilter(p => !p.IsDeleted && p.IsActive);
        AddInclude(p => p.Category);
        AddOrderBy(p => p.Name);
    }
}

// Usage
var products = await _repository
    .Query()
    .ApplySpecification(new ActiveProductsSpec())
    .ToListAsync();
```

## 🪝 Hooks

```csharp
public class AuditHook : IBeforeSaveHookAsync
{
    public Task BeforeSaveAsync(SnapshotContext snapshot, CancellationToken ct)
    {
        foreach (var entry in snapshot.Entries)
        {
            if (entry.Entity is IAuditedEntity audited && entry.State == EntityState.Added)
                audited.SetCreatedBy(_currentUser.Id);
        }
        return Task.CompletedTask;
    }
}
```

## ✅ Decision Matrix

| Need | Use |
|------|-----|
| Query only | `IReadRepository<T>` |
| Modify data | `IRepository<T>` |
| Unique ID | `Entity` (Guid) |
| Custom key | `Entity<TKey>` |
| Track changes | `AuditedEntity<TKey>` |
| Soft delete | `ISoftDeletableEntity` |
| Optimistic locking | `IConcurrencyEntity<byte[]>` |
| API response | DTO/Record |
| Business error | `Result.Fail()` |
| Infrastructure error | Exception |
| Static seed data | `HasData` property |
| Dynamic seed | `SeedAsync` method |
| Cross-cutting concern | Hook |
| Reusable query | Specification |

## 🚫 Common Mistakes

| ❌ Don't | ✅ Do |
|---------|------|
| `public set` on entities | `private set` + methods |
| Load entities for DTOs | `ProjectToType<TDto>()` |
| `IRepository<T>` for reads | `IReadRepository<T>` |
| `SaveChanges()` in loop | Batch + single save |
| Manual `DbContext` creation | DI injection |
| Return entities from API | Return DTOs |
| Throw for business errors | Return `Result.Fail()` |
| `ToList()` synchronously | `await ToListAsync()` |

## 📋 Setup Checklist

```csharp
// 1. DbContext
services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString)
           .UseAutoConfigModel());

// 2. Repositories  
services.AddMapster();
services.AddGenericRepositories<AppDbContext>();

// 3. SlimBus
services.AddSlimBusForEfCore<AppDbContext>(b =>
    b.WithProviderMemory()
     .AutoDeclareFrom(typeof(Program).Assembly));

// 4. Hooks (optional)
services.AddScoped<IBeforeSaveHookAsync, AuditHook>();
services.AddEfCoreHooks<AppDbContext>();

// 5. Background jobs (optional)
services.AddBackgroundJobFrom(new[] { typeof(Program).Assembly });
```

## 🔗 Quick Links

- **Full Memory Bank**: `.github/copilot-memory-bank.md`
- **Memory Bank Guide**: `.github/MEMORY_BANK_README.md`
- **Main README**: `../README.md`
- **Project Structure**: `../PROJECT_STRUCTURE_CHECKLIST.md`

---

**Print this page for quick reference! 📄**

