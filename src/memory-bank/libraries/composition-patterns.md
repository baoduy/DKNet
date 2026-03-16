# Composition Patterns for DKNet RESTful APIs

This file shows how multiple DKNet libraries compose into complete feature slices.

## Pattern 1: Standard CRUD Endpoint

**Libraries**: `DKNet.SlimBus.Extensions` + `DKNet.AspCore.Extensions` + `DKNet.EfCore.Repos` + `DKNet.EfCore.Specifications`

```csharp
// Program.cs
services.AddDbContext<AppDbContext>(...);
services.AddGenericRepositories<AppDbContext>();
services.AddSpecifications<AppDbContext>();
services.AddSlimBusForEfCore(builder => builder
    .WithProviderMemory()
    .AutoDeclareFrom(typeof(CreateProductHandler).Assembly)
    .AddJsonSerializer());

var api = app.MapGroup("/api/v1/products");
api.MapPost<CreateProductCommand, ProductDto>("/");
api.MapGet<GetProductQuery, ProductDto>("/{Id}");
api.MapGetPage<ListProductsQuery, ProductDto>("/");
api.MapPut<UpdateProductCommand, ProductDto>("/{Id}");
api.MapDelete<DeleteProductCommand>("/{Id}");
```

## Pattern 2: Idempotent Mutation Endpoint

**Libraries**: `DKNet.AspCore.Extensions` + `DKNet.AspCore.Idempotency` (+ `DKNet.AspCore.Idempotency.MsSqlStore` in production)

```csharp
services.AddIdempotencyMsSqlStore(configuration.GetConnectionString("Default")!);
services.AddIdempotency();

api.MapPost<CreateOrderCommand, OrderDto>("/orders")
   .WithIdempotency();
```

## Pattern 3: Secure Tenant-Scoped Read Endpoint

**Libraries**: `DKNet.EfCore.DataAuthorization` + `DKNet.EfCore.Specifications` + `DKNet.EfCore.Repos`

```csharp
services.AddAutoDataKeyProvider<AppDbContext, HttpContextOwnerProvider>();

public class TenantOrdersSpec : Specification<Order>
{
    public TenantOrdersSpec(Guid customerId)
    {
        WithFilter(x => x.CustomerId == customerId);
        AddOrderByDescending(x => x.CreatedOn);
    }
}

// IOwnedBy global filters apply automatically
```

## Pattern 4: Audited + Encrypted Write Path

**Libraries**: `DKNet.EfCore.AuditLogs` + `DKNet.EfCore.Encryption` + `DKNet.EfCore.Repos`

```csharp
services.AddEfCoreAuditLogs<AppDbContext, AuditPublisher>();

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.UseEncryption(configuration["Encryption:Key"]!);
    base.OnModelCreating(modelBuilder);
}
```

## Pattern 5: Domain Event Emission on Save

**Libraries**: `DKNet.EfCore.Events` + `DKNet.SlimBus.Extensions`

```csharp
services.AddEventPublisher<AppDbContext, SlimBusEventPublisher>();

public class Order : IHasDomainEvents
{
    public void MarkPaid() => AddDomainEvent(new OrderPaidEvent(Id));
}

// Event is published after successful SaveChanges
```

## Pattern 6: Startup Data Seeding Job

**Libraries**: `DKNet.AspCore.Tasks` + `DKNet.EfCore.Repos`

```csharp
services.AddBackgroundJob<SeedReferenceDataJob>();

public class SeedReferenceDataJob(IWriteRepository<Category> repo) : IBackgroundJob
{
    public async Task RunAsync(CancellationToken ct = default)
    {
        await repo.AddRangeAsync(DefaultCategories.All, ct);
        await repo.SaveChangesAsync(ct);
    }
}
```

