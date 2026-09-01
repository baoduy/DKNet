# Examples & Recipes

Working implementation patterns for the DKNet packages. Every sample on this page compiles against the package
projects in `src/`; the `using` directives are part of the sample, because several DKNet extension methods live in
namespaces you would not guess (see the
[namespace table](../Configuration.md#where-each-extension-method-lives)).

## Contents

- [Complete CRUD API with CQRS](#complete-crud-api-with-cqrs)
- [Domain event implementation](#domain-event-implementation)
- [Querying with specifications](#querying-with-specifications)
- [Multi-tenant application](#multi-tenant-application)
- [Blob storage operations](#blob-storage-operations)
- [Core framework helpers](#core-framework-helpers)

---

## Complete CRUD API with CQRS

### Entity definition

```csharp
using DKNet.EfCore.Abstractions.Entities;
using System.ComponentModel.DataAnnotations.Schema;

[Table("Products", Schema = "catalog")]
public class Product : AuditedEntity
{
    private Product() { } // EF Core

    public static Product Create(string name, decimal price, string description, string createdBy)
    {
        var product = new Product { Name = name, Price = price, Description = description };
        product.SetCreatedBy(createdBy);
        product.AddEvent(new ProductCreatedEvent(product.Id, product.Name));
        return product;
    }

    public string Name { get; private set; } = null!;
    public decimal Price { get; private set; }
    public string Description { get; private set; } = null!;
    public bool IsActive { get; private set; } = true;

    public void UpdateDetails(string name, decimal price, string description, string updatedBy)
    {
        Name = name;
        Price = price;
        Description = description;
        SetUpdatedBy(updatedBy);

        AddEvent(new ProductUpdatedEvent(Id, Name));
    }

    public void Deactivate(string deactivatedBy)
    {
        IsActive = false;
        SetUpdatedBy(deactivatedBy);
        AddEvent(new ProductDeactivatedEvent(Id, Name));
    }
}
```

### Commands and queries

Handlers talk to the `DbContext` directly and let `AddSlimBusEfCoreInterceptor<AppDbContext>()` save on success —
see [DKNet.SlimBus.Extensions](../Messaging/DKNet.SlimBus.Extensions.md).

```csharp
using DKNet.SlimBus.Extensions;
using FluentResults;
using Microsoft.EntityFrameworkCore;

// Create command
public record CreateProductCommand(string Name, decimal Price, string? Description)
    : Fluents.Requests.IWitResponse<Guid>;

internal sealed class CreateProductHandler(AppDbContext db)
    : Fluents.Requests.IHandler<CreateProductCommand, Guid>
{
    public async Task<IResult<Guid>> OnHandle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        if (await db.Products.AnyAsync(p => p.Name == request.Name, cancellationToken))
            return Result.Fail<Guid>($"Product with name '{request.Name}' already exists");

        var product = Product.Create(request.Name, request.Price, request.Description ?? string.Empty, "system");
        await db.Products.AddAsync(product, cancellationToken);

        // No SaveChangesAsync — the auto-save interceptor runs it once this returns a success result.
        return Result.Ok(product.Id);
    }
}

// Query
public record GetProductQuery(Guid Id) : Fluents.Queries.IWitResponse<ProductDto>;

internal sealed class GetProductHandler(AppDbContext db)
    : Fluents.Queries.IHandler<GetProductQuery, ProductDto>
{
    public async Task<ProductDto?> OnHandle(GetProductQuery request, CancellationToken cancellationToken)
    {
        var product = await db.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
        return product is null ? null : new ProductDto(product.Id, product.Name);
    }
}
```

DKNet ships **no validation pipeline**. If you want FluentValidation (or anything else) to run before a handler,
register it as a SlimMessageBus `IRequestHandlerInterceptor` yourself — a bare `AbstractValidator<T>` class next to
the command is never invoked.

### Minimal API endpoints

`.Response()` turns a `FluentResults` result into an `IResult`, mapping failures to `ProblemDetails`:

```csharp
using DKNet.AspCore.Extensions.Responses;
using SlimMessageBus;

app.MapGet("/products/{id:guid}", async (IMessageBus bus, Guid id) =>
    await bus.Send(new GetProductQuery(id)) is { } dto ? Results.Ok(dto) : Results.NotFound());

app.MapPost("/products", async (IMessageBus bus, CreateProductCommand cmd) =>
    (await bus.Send(cmd)).Response(isCreated: true));
```

---

## Domain event implementation

### Event definition

Deriving from `EventItem` is optional but gives the event an `EventType` and an `AdditionalData` bag that
`SlimBusEventPublisher` copies onto the message headers:

```csharp
using DKNet.EfCore.Abstractions.Events;

public record ProductCreatedEvent(Guid ProductId, string ProductName) : EventItem;
public record ProductUpdatedEvent(Guid ProductId, string ProductName) : EventItem;
public record ProductDeactivatedEvent(Guid ProductId, string ProductName) : EventItem;
```

Events are queued on the entity by `AddEvent(...)` (see `UpdateDetails`/`Deactivate` above) and dispatched by
`DKNet.EfCore.Events` to every registered `IEventPublisher` **after** a successful `SaveChangesAsync`.
`AddSlimBusEventPublisher<AppDbContext>()` adds a publisher that forwards each one onto SlimMessageBus, where the
consumers below pick it up. The whole path is traced in
[A domain event end to end](../Architecture.md#a-domain-event-end-to-end).

### Event consumers

```csharp
using DKNet.SlimBus.Extensions;
using Microsoft.Extensions.Logging;

public class ProductCreatedHandler(ILogger<ProductCreatedHandler> logger, IEmailService emailService)
    : Fluents.EventsConsumers.IHandler<ProductCreatedEvent>
{
    public async Task OnHandle(ProductCreatedEvent message, CancellationToken cancellationToken)
    {
        logger.LogInformation("Product created: {ProductId} - {ProductName}",
            message.ProductId, message.ProductName);

        await emailService.SendProductCreatedNotificationAsync(
            message.ProductId, message.ProductName, cancellationToken);
    }
}

public class ProductEventLogger(ILogger<ProductEventLogger> logger) :
    Fluents.EventsConsumers.IHandler<ProductCreatedEvent>,
    Fluents.EventsConsumers.IHandler<ProductUpdatedEvent>,
    Fluents.EventsConsumers.IHandler<ProductDeactivatedEvent>
{
    public Task OnHandle(ProductCreatedEvent message, CancellationToken cancellationToken)
    {
        logger.LogInformation("Product created: {ProductId}", message.ProductId);
        return Task.CompletedTask;
    }

    public Task OnHandle(ProductUpdatedEvent message, CancellationToken cancellationToken)
    {
        logger.LogInformation("Product updated: {ProductId}", message.ProductId);
        return Task.CompletedTask;
    }

    public Task OnHandle(ProductDeactivatedEvent message, CancellationToken cancellationToken)
    {
        logger.LogInformation("Product deactivated: {ProductId}", message.ProductId);
        return Task.CompletedTask;
    }
}
```

A publisher that throws is logged and swallowed: the row stays committed and that event is lost. Use a durable
transport when a consumer must not miss an event.

---

## Querying with specifications

A `Specification<TEntity>` is configured entirely from its constructor via `protected` builder methods — there is
no boolean `.And()`/`.Or()`/`.Not()` combinator; compose criteria by passing them into one specification's
constructor instead. See [DKNet.EfCore.Specifications](../EfCore/DKNet.EfCore.Specifications.md) for the full API.

```csharp
using DKNet.EfCore.Specifications;

public sealed class ActiveProductsInPriceRangeSpec : Specification<Product>
{
    public ActiveProductsInPriceRangeSpec(decimal minPrice, decimal maxPrice)
    {
        WithFilter(p => p.IsActive && p.Price >= minPrice && p.Price <= maxPrice);
        AddOrderBy(p => p.Name);
    }
}

public sealed class ActiveProductsByNameSpec : Specification<Product>
{
    public ActiveProductsByNameSpec(string namePattern, decimal? minPrice = null)
    {
        WithFilter(p => p.IsActive
            && p.Name.Contains(namePattern)
            && (minPrice == null || p.Price >= minPrice));
        AddOrderBy(p => p.Name);
    }
}
```

One injected `IRepositorySpec` serves every entity type; the entity comes from the specification. The
`ToListAsync`/`FirstOrDefaultAsync`/`ToPagedListAsync` overloads are extension members, so
`DKNet.EfCore.Specifications.Extensions` has to be imported:

```csharp
using DKNet.EfCore.Specifications.Extensions;
using DKNet.EfCore.Specifications.Repositories;

public class ProductService(IRepositorySpec repo)
{
    public Task<IList<Product>> GetActiveProductsInPriceRangeAsync(
        decimal minPrice, decimal maxPrice, CancellationToken cancellationToken = default) =>
        repo.ToListAsync(new ActiveProductsInPriceRangeSpec(minPrice, maxPrice), cancellationToken);

    public Task<IList<Product>> SearchProductsAsync(
        string namePattern, decimal? minPrice = null, CancellationToken cancellationToken = default) =>
        repo.ToListAsync(new ActiveProductsByNameSpec(namePattern, minPrice), cancellationToken);
}
```

---

## Multi-tenant application

Row-level, ownership-based isolation is a built-in feature —
[DKNet.EfCore.DataAuthorization](../EfCore/DKNet.EfCore.DataAuthorization.md) — rather than something to hand-roll
per repository. An entity opts in via `IOwnedBy`; a global query filter and a `SaveChanges` hook do the rest.

### Tenant-owned entity

```csharp
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.DataAuthorization;

public class Invoice : AuditedEntity, IOwnedBy
{
    public string OwnedBy { get; private set; } = string.Empty;
    public string Reference { get; private set; } = null!;
}
```

### Tenant provider

```csharp
using DKNet.EfCore.DataAuthorization;
using Microsoft.AspNetCore.Http;

public sealed class HttpTenantProvider(IHttpContextAccessor httpContextAccessor) : IDataOwnerProvider
{
    public string? GetOwnershipKey()
    {
        var context = httpContextAccessor.HttpContext;

        // Header first
        if (context?.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeader) == true)
            return tenantHeader.FirstOrDefault() ?? "default";

        // Then the JWT claim
        return context?.User?.FindFirst("tenant_id")?.Value ?? "default";
    }
}
```

### DbContext and registration

The `DbContext` must implement `IDataOwnerDbContext` — the generic constraint on `AddDataOwnerProvider` enforces
it — and the model must be built through `UseAutoConfigModel<TContext>()`, which is a
`DbContextOptionsBuilder<TContext>` extension and therefore belongs in the `AddDbContextWithHook` callback, **not**
in `OnModelCreating`:

```csharp
using DKNet.EfCore.DataAuthorization;
using DKNet.EfCore.Hooks;
using Microsoft.EntityFrameworkCore;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IDataOwnerDbContext
{
    // Declare it as IEnumerable<string>: EF Core cannot translate ICollection<string>.Contains
    // inside a query filter.
    public IEnumerable<string> AccessibleKeys { get; init; } = [];

    public DbSet<Invoice> Invoices => Set<Invoice>();
}

services
    .AddDataOwnerProvider<AppDbContext, HttpTenantProvider>()
    .AddDbContextWithHook<AppDbContext>(options => options
        .UseSqlServer(connectionString)
        .UseAutoConfigModel<AppDbContext>());
```

`AddDataOwnerProvider` registers its filter in a **static** model-builder list, so every `DbContext` in the process
that calls `UseAutoConfigModel()` applies it. A second context whose model contains `IOwnedBy` entities must also
implement `IDataOwnerDbContext` — see the
[Migration Guide](../Migration-Guide.md#upgrading-dknetefcoredataauthorization-idataownerdbcontext-is-now-required).

---

## Blob storage operations

`IBlobService` is provider-agnostic: swap `AddAzureStorageAdapter` for `AddS3BlobService` or
`AddLocalDirectoryBlobService` and this code does not change.

```csharp
using DKNet.Svc.BlobStorage.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

public class FileUploadService(IBlobService blobStorage, ILogger<FileUploadService> logger)
{
    public async Task<string> UploadFileAsync(IFormFile file, string folder = "uploads")
    {
        if (file is null || file.Length == 0)
            throw new ArgumentException("File is required", nameof(file));

        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var filePath = $"{folder}/{fileName}";

        await using var stream = file.OpenReadStream();
        var blob = new BlobDetails.BlobData(filePath, await BinaryData.FromStreamAsync(stream))
        {
            ContentType = file.ContentType,
        };

        // SaveAsync returns the stored blob's name, after the shared validation in BlobService.
        var location = await blobStorage.SaveAsync(blob);

        logger.LogInformation("File uploaded: {FilePath}", location);
        return location;
    }

    public async Task<BinaryData?> DownloadFileAsync(string filePath)
    {
        var result = await blobStorage.GetAsync(new BlobRequest(filePath));
        return result?.Data;
    }

    public async Task DeleteFileAsync(string filePath)
    {
        var deleted = await blobStorage.DeleteAsync(new BlobRequest(filePath));
        logger.LogInformation("File delete requested: {FilePath}, deleted: {Deleted}", filePath, deleted);
    }
}
```

`BlobRequest` infers its `Type` from the name: a name with no file extension is treated as a directory. Give
files an extension, or set `Type` explicitly.

---

## Core framework helpers

### Type and property reflection

```csharp
using DKNet.EfCore.Abstractions.Events;
using DKNet.Fw.Extensions.Reflection;

// Does this type implement an interface?
if (typeof(Product).IsImplementOf<IEventEntity>())
{
    // Product carries a domain-event queue
}

var product = Product.Create("Widget", 9.99m, "A widget", "system");

// Read and write by name
var name = product.GetPropertyValue("Name");
product.SetPropertyValue("Name", "New product name");
```

### Enum metadata

```csharp
using DKNet.Fw.Extensions.Enums;
using System.ComponentModel.DataAnnotations;

public enum OrderStatus
{
    [Display(Name = "Order is pending")] Pending,
    [Display(Name = "Order is confirmed")] Confirmed,
    [Display(Name = "Order is shipped")] Shipped,
}

var status = OrderStatus.Pending;

// Read the attribute off the value
var description = status.GetAttribute<DisplayAttribute>()?.Name;   // "Order is pending"

// Or enumerate every named value at once (method name is spelled GetEumInfos)
var allInfos = EnumExtensions.GetEumInfos<OrderStatus>();
```

---

## More examples

- **[SlimBus.ApiEndpoints template](https://github.com/baoduy/DKNet.Templates)** — a complete API implementation
  with end-to-end tests, in the DKNet.Templates repository
- **Unit tests** — the sibling `*.Tests` projects next to each package under `src/` are the most current usage
  reference for any API on these pages
