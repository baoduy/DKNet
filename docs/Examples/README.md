# Examples & Recipes

This section provides practical examples and implementation patterns for using DKNet Framework components.

## 📋 Table of Contents

### 🏗️ Architecture Patterns
- [Complete CRUD API with CQRS](#complete-crud-api-with-cqrs)
- [Domain Event Implementation](#domain-event-implementation)
- [Repository Pattern with Specifications](#repository-pattern-with-specifications)
- [Multi-tenant Application](#multi-tenant-application)

### 🔧 Core Framework
- [Extension Methods Usage](#extension-methods-usage)
- [Property Utilities](#property-utilities)
- [Type Conversions](#type-conversions)

### 🗄️ Entity Framework Core
- [Custom Repository Implementation](#custom-repository-implementation)
- [Entity Hooks and Lifecycle](#entity-hooks-and-lifecycle)
- [Data Authorization](#data-authorization)

### 📨 Messaging & CQRS
- [Command/Query Handlers](#commandquery-handlers)
- [Event-Driven Architecture](#event-driven-architecture)
- [Message Bus Integration](#message-bus-integration)

### 🗃️ Services
- [Blob Storage Operations](#blob-storage-operations)
- [Data Transformation](#data-transformation)

---

## 🏗️ Complete CRUD API with CQRS

### Entity Definition

```csharp
using DKNet.EfCore.Abstractions.Entities;

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

### Commands and Queries

Handlers talk to the `DbContext` directly and let `AddSlimBusEfCoreInterceptor<AppDbContext>()` save on success —
see [DKNet.SlimBus.Extensions](../Messaging/DKNet.SlimBus.Extensions.md).

```csharp
using DKNet.SlimBus.Extensions;
using FluentResults;
using Microsoft.EntityFrameworkCore;

// Create Command
public record CreateProductCommand(string Name, decimal Price, string? Description)
    : Fluents.Requests.IWitResponse<Guid>;

public class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

internal sealed class CreateProductHandler(AppDbContext db)
    : Fluents.Requests.IHandler<CreateProductCommand, Guid>
{
    public async Task<IResult<Guid>> OnHandle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        if (await db.Products.AnyAsync(p => p.Name == request.Name, cancellationToken))
            return Result.Fail($"Product with name '{request.Name}' already exists");

        var product = Product.Create(request.Name, request.Price, request.Description ?? string.Empty, "system");
        await db.Products.AddAsync(product, cancellationToken);

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

### Minimal API Endpoints

```csharp
using DKNet.AspCore.Extensions;
using DKNet.SlimBus.Extensions;

app.MapGet("/products/{id:guid}", async (IMessageBus bus, Guid id) =>
    (await bus.Send(new GetProductQuery(id))) is { } dto ? Results.Ok(dto) : Results.NotFound());

app.MapPost("/products", async (IMessageBus bus, CreateProductCommand cmd) =>
    (await bus.Send(cmd)).Response(isCreated: true));
```

---

## 🔥 Domain Event Implementation

### Event Definition

```csharp
public record ProductCreatedEvent(Guid ProductId, string ProductName) : EventItem;

public record ProductUpdatedEvent(Guid ProductId, string ProductName) : EventItem;

public record ProductDeactivatedEvent(Guid ProductId, string ProductName) : EventItem;
```

Raised from the aggregate via `AddEvent(...)` (see `UpdateDetails`/`Deactivate` above) and dispatched to every
registered `IEventPublisher` by `DKNet.EfCore.Events` after a successful `SaveChangesAsync`.
`AddSlimBusEventPublisher<AppDbContext>()` forwards each one onto SlimMessageBus for the consumers below to
pick up — see [DKNet.SlimBus.Extensions](../Messaging/DKNet.SlimBus.Extensions.md).

### Event Handlers

```csharp
public class ProductCreatedHandler(ILogger<ProductCreatedHandler> logger, IEmailService emailService)
    : Fluents.EventsConsumers.IHandler<ProductCreatedEvent>
{
    public async Task OnHandle(ProductCreatedEvent message, CancellationToken cancellationToken)
    {
        logger.LogInformation("Product created: {ProductId} - {ProductName}",
            message.ProductId, message.ProductName);

        // Send notification email
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

---

## 🗄️ Repository Pattern with Specifications

### Specification Pattern

A `Specification<TEntity>` is configured entirely from its constructor via `protected` builder methods — there is
no boolean `.And()`/`.Or()`/`.Not()` combinator; compose criteria by passing them into one specification's
constructor instead. See [DKNet.EfCore.Specifications](../EfCore/DKNet.EfCore.Specifications.md) for the full API.

```csharp
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

### Using Specifications

```csharp
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

## 🔐 Multi-tenant Application

Row-level, ownership-based isolation is a built-in feature —
[DKNet.EfCore.DataAuthorization](../EfCore/DKNet.EfCore.DataAuthorization.md) — rather than something to hand-roll
per repository. An entity opts in via `IOwnedBy`; a global query filter and a `SaveChanges` hook do the rest.

### Tenant-Owned Entity

```csharp
using DKNet.EfCore.DataAuthorization;

public class Product : AuditedEntity, IOwnedBy
{
    public string OwnedBy { get; private set; } = string.Empty;
    public string Name { get; private set; } = null!;
    // ... other properties
}
```

### Tenant Provider

```csharp
using DKNet.EfCore.DataAuthorization;

public sealed class HttpTenantProvider(IHttpContextAccessor httpContextAccessor) : IDataOwnerProvider
{
    public string? GetOwnershipKey()
    {
        var context = httpContextAccessor.HttpContext;

        // Try header first
        if (context?.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeader) == true)
            return tenantHeader.FirstOrDefault() ?? "default";

        // Try claim from JWT
        return context?.User?.FindFirst("tenant_id")?.Value ?? "default";
    }
}
```

### Registration

`AppDbContext` must implement `IDataOwnerDbContext` and call `UseAutoConfigModel()` in `OnModelCreating` — see
[DKNet.EfCore.DataAuthorization](../EfCore/DKNet.EfCore.DataAuthorization.md) for the full wiring.

```csharp
services
    .AddDataOwnerProvider<AppDbContext, HttpTenantProvider>()
    .AddDbContextWithHook<AppDbContext>(options => options.UseSqlServer(connectionString));
```

---

## 🗃️ Blob Storage Operations

### File Upload Service

```csharp
public class FileUploadService
{
    private readonly IBlobService _blobStorage;
    private readonly ILogger<FileUploadService> _logger;

    public FileUploadService(IBlobService blobStorage, ILogger<FileUploadService> logger)
    {
        _blobStorage = blobStorage;
        _logger = logger;
    }

    public async Task<string> UploadFileAsync(IFormFile file, string folder = "uploads")
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("File is required");

        // Generate unique filename
        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var filePath = $"{folder}/{fileName}";

        // Upload file
        using var stream = file.OpenReadStream();
        var blob = new BlobDetails.BlobData(filePath, BinaryData.FromStream(stream)) { ContentType = file.ContentType };
        var location = await _blobStorage.SaveAsync(blob);

        _logger.LogInformation("File uploaded: {FilePath}", location);
        return location;
    }

    public async Task<BinaryData?> DownloadFileAsync(string filePath)
    {
        var result = await _blobStorage.GetAsync(new BlobRequest(filePath));
        return result?.Data;
    }

    public async Task DeleteFileAsync(string filePath)
    {
        await _blobStorage.DeleteAsync(new BlobRequest(filePath));
        _logger.LogInformation("File deleted: {FilePath}", filePath);
    }
}
```

### Image Processing Example

```csharp
public class ImageProcessingService
{
    private readonly IBlobService _blobStorage;
    private readonly IImageProcessor _imageProcessor;

    public async Task<string> ProcessAndUploadImageAsync(IFormFile imageFile)
    {
        // Download original
        using var originalStream = imageFile.OpenReadStream();
        
        // Process image (resize, optimize, etc.)
        using var processedStream = await _imageProcessor.ResizeAsync(originalStream, 800, 600);
        
        // Upload processed image
        var fileName = $"processed/{Guid.NewGuid()}.jpg";
        var blob = new BlobDetails.BlobData(fileName, BinaryData.FromStream(processedStream)) { ContentType = "image/jpeg" };
        return await _blobStorage.SaveAsync(blob);
    }
}
```

---

## 🔧 Extension Methods Usage

### Type Extensions

```csharp
// Check if type implements interface
if (typeof(Product).IsImplementOf<IAuditable>())
{
    // Handle auditable entity
}

// Get property value dynamically
var product = new Product();
var name = product.GetPropertyValue("Name");

// Set property value
product.SetPropertyValue("Name", "New Product Name");
```

### Enum Extensions

```csharp
public enum OrderStatus
{
    [Display(Name = "Order is pending")]
    Pending,

    [Display(Name = "Order is confirmed")]
    Confirmed,

    [Display(Name = "Order is shipped")]
    Shipped
}

// Get description via the Display attribute
var status = OrderStatus.Pending;
var description = status.GetAttribute<DisplayAttribute>()?.Name; // "Order is pending"

// Get info for every named value
var allInfos = EnumExtensions.GetEumInfos<OrderStatus>();
```

### Collection Extensions

```csharp
// Async enumerable to list
var asyncItems = GetItemsAsync();
var list = await asyncItems.ToListAsync();
```

---

## 📖 More Examples

For complete working examples, check out:

- **[SlimBus.ApiEndpoints template](https://github.com/baoduy/DKNet.Templates)** - Complete API implementation and end-to-end tests, in the DKNet.Templates repository
- **Unit Tests** - Comprehensive test examples in the sibling `*.Tests` projects next to each package under `src/`

---

> 💡 **Example Tip**: All examples are based on real implementations in the DKNet codebase. Check the source code for the most up-to-date patterns!