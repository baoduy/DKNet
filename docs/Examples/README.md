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
[Table("Products", Schema = "catalog")]
public class Product : AggregateRoot
{
    public Product(string name, decimal price, string description, string createdBy)
        : base(Guid.NewGuid(), createdBy)
    {
        Name = name;
        Price = price;
        Description = description;
    }

    public string Name { get; private set; }
    public decimal Price { get; private set; }
    public string Description { get; private set; }
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

### Repository Implementation

```csharp
public interface IProductRepository : IRepository<Product>
{
    Task<bool> IsNameUniqueAsync(string name, Guid? excludeId = null);
    Task<IEnumerable<Product>> GetActiveProductsAsync();
}

public class ProductRepository : Repository<Product>, IProductRepository
{
    public ProductRepository(AppDbContext context) : base(context) { }

    public async Task<bool> IsNameUniqueAsync(string name, Guid? excludeId = null)
    {
        var query = Gets().Where(p => p.Name == name);
        if (excludeId.HasValue)
            query = query.Where(p => p.Id != excludeId.Value);
            
        return !await query.AnyAsync();
    }

    public Task<IEnumerable<Product>> GetActiveProductsAsync()
    {
        return Gets().Where(p => p.IsActive).ToListAsync();
    }
}
```

### Commands and Queries

```csharp
// Create Command
public record CreateProductCommand : IRequest<ProductResult>
{
    [Required] public string Name { get; init; } = null!;
    [Required] public decimal Price { get; init; }
    public string? Description { get; init; }
}

public class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public class CreateProductHandler : IRequestHandler<CreateProductCommand, ProductResult>
{
    private readonly IProductRepository _repository;
    private readonly IMapper _mapper;

    public CreateProductHandler(IProductRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<ProductResult> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        // Business validation
        if (!await _repository.IsNameUniqueAsync(request.Name))
            throw new BusinessException($"Product with name '{request.Name}' already exists");

        // Create entity
        var product = new Product(
            request.Name, 
            request.Price, 
            request.Description ?? string.Empty,
            "system"); // In real app, get from current user

        await _repository.AddAsync(product, cancellationToken);
        
        // Add domain event
        product.AddEvent(new ProductCreatedEvent(product.Id, product.Name));

        return _mapper.Map<ProductResult>(product);
    }
}

// Query
public record GetProductQuery : IRequest<ProductResult?>
{
    public Guid Id { get; init; }
}

public class GetProductHandler : IRequestHandler<GetProductQuery, ProductResult?>
{
    private readonly IReadRepository<Product> _repository;
    private readonly IMapper _mapper;

    public GetProductHandler(IReadRepository<Product> repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<ProductResult?> Handle(GetProductQuery request, CancellationToken cancellationToken)
    {
        var product = await _repository.GetByIdAsync(request.Id, cancellationToken);
        return product != null ? _mapper.Map<ProductResult>(product) : null;
    }
}
```

### API Controller

```csharp
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProductsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductResult>> GetProduct(Guid id)
    {
        var result = await _mediator.Send(new GetProductQuery { Id = id });
        return result != null ? Ok(result) : NotFound();
    }

    [HttpPost]
    public async Task<ActionResult<ProductResult>> CreateProduct([FromBody] CreateProductCommand command)
    {
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetProduct), new { id = result.Id }, result);
    }
}
```

---

## 🔥 Domain Event Implementation

### Event Definition

```csharp
public record ProductCreatedEvent(Guid ProductId, string ProductName) : DomainEvent;

public record ProductUpdatedEvent(Guid ProductId, string ProductName) : DomainEvent;

public record ProductDeactivatedEvent(Guid ProductId, string ProductName) : DomainEvent;
```

### Event Handlers

```csharp
public class ProductCreatedHandler : IDomainEventHandler<ProductCreatedEvent>
{
    private readonly ILogger<ProductCreatedHandler> _logger;
    private readonly IEmailService _emailService;

    public ProductCreatedHandler(ILogger<ProductCreatedHandler> logger, IEmailService emailService)
    {
        _logger = logger;
        _emailService = emailService;
    }

    public async Task Handle(ProductCreatedEvent domainEvent, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Product created: {ProductId} - {ProductName}", 
            domainEvent.ProductId, domainEvent.ProductName);

        // Send notification email
        await _emailService.SendProductCreatedNotificationAsync(
            domainEvent.ProductId, domainEvent.ProductName, cancellationToken);
    }
}

public class ProductEventLogger : 
    IDomainEventHandler<ProductCreatedEvent>,
    IDomainEventHandler<ProductUpdatedEvent>,
    IDomainEventHandler<ProductDeactivatedEvent>
{
    private readonly ILogger<ProductEventLogger> _logger;

    public ProductEventLogger(ILogger<ProductEventLogger> logger)
    {
        _logger = logger;
    }

    public Task Handle(ProductCreatedEvent domainEvent, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Product created: {ProductId}", domainEvent.ProductId);
        return Task.CompletedTask;
    }

    public Task Handle(ProductUpdatedEvent domainEvent, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Product updated: {ProductId}", domainEvent.ProductId);
        return Task.CompletedTask;
    }

    public Task Handle(ProductDeactivatedEvent domainEvent, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Product deactivated: {ProductId}", domainEvent.ProductId);
        return Task.CompletedTask;
    }
}
```

---

## 🗄️ Repository Pattern with Specifications

### Specification Pattern

```csharp
public class ActiveProductsSpecification : Specification<Product>
{
    public override Expression<Func<Product, bool>> ToExpression()
    {
        return product => product.IsActive;
    }
}

public class ProductsByPriceRangeSpecification : Specification<Product>
{
    private readonly decimal _minPrice;
    private readonly decimal _maxPrice;

    public ProductsByPriceRangeSpecification(decimal minPrice, decimal maxPrice)
    {
        _minPrice = minPrice;
        _maxPrice = maxPrice;
    }

    public override Expression<Func<Product, bool>> ToExpression()
    {
        return product => product.Price >= _minPrice && product.Price <= _maxPrice;
    }
}

public class ProductsByNameSpecification : Specification<Product>
{
    private readonly string _namePattern;

    public ProductsByNameSpecification(string namePattern)
    {
        _namePattern = namePattern;
    }

    public override Expression<Func<Product, bool>> ToExpression()
    {
        return product => product.Name.Contains(_namePattern);
    }
}
```

### Using Specifications

```csharp
public class ProductService
{
    private readonly IProductRepository _repository;

    public ProductService(IProductRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<Product>> GetActiveProductsInPriceRangeAsync(decimal minPrice, decimal maxPrice)
    {
        var spec = new ActiveProductsSpecification()
            .And(new ProductsByPriceRangeSpecification(minPrice, maxPrice));

        return await _repository.FindAsync(spec);
    }

    public async Task<IEnumerable<Product>> SearchProductsAsync(string namePattern, decimal? minPrice = null)
    {
        var spec = new ActiveProductsSpecification()
            .And(new ProductsByNameSpecification(namePattern));

        if (minPrice.HasValue)
        {
            spec = spec.And(new ProductsByPriceRangeSpecification(minPrice.Value, decimal.MaxValue));
        }

        return await _repository.FindAsync(spec);
    }
}
```

---

## 🔐 Multi-tenant Application

### Tenant Entity

```csharp
public interface ITenantEntity
{
    string TenantId { get; set; }
}

public class Product : AggregateRoot, ITenantEntity
{
    public string TenantId { get; set; } = null!;
    public string Name { get; private set; }
    // ... other properties
}
```

### Tenant-Aware Repository

```csharp
public class TenantProductRepository : Repository<Product>, IProductRepository
{
    private readonly ITenantProvider _tenantProvider;

    public TenantProductRepository(AppDbContext context, ITenantProvider tenantProvider) 
        : base(context)
    {
        _tenantProvider = tenantProvider;
    }

    protected override IQueryable<Product> Gets()
    {
        var query = base.Gets();
        var tenantId = _tenantProvider.GetCurrentTenant();
        
        return query.Where(p => p.TenantId == tenantId);
    }

    public override async Task<Product> AddAsync(Product entity, CancellationToken cancellationToken = default)
    {
        entity.TenantId = _tenantProvider.GetCurrentTenant();
        return await base.AddAsync(entity, cancellationToken);
    }
}
```

### Tenant Provider

```csharp
public interface ITenantProvider
{
    string GetCurrentTenant();
}

public class HttpTenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpTenantProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetCurrentTenant()
    {
        var context = _httpContextAccessor.HttpContext;
        
        // Try header first
        if (context?.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeader) == true)
        {
            return tenantHeader.FirstOrDefault() ?? "default";
        }

        // Try claim from JWT
        var tenantClaim = context?.User?.FindFirst("tenant_id");
        return tenantClaim?.Value ?? "default";
    }
}
```

---

## 🗃️ Blob Storage Operations

### File Upload Service

```csharp
public class FileUploadService
{
    private readonly IBlobStorageService _blobStorage;
    private readonly ILogger<FileUploadService> _logger;

    public FileUploadService(IBlobStorageService blobStorage, ILogger<FileUploadService> logger)
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
        await _blobStorage.UploadAsync(filePath, stream, file.ContentType);

        _logger.LogInformation("File uploaded: {FilePath}", filePath);
        return filePath;
    }

    public async Task<Stream> DownloadFileAsync(string filePath)
    {
        return await _blobStorage.DownloadAsync(filePath);
    }

    public async Task DeleteFileAsync(string filePath)
    {
        await _blobStorage.DeleteAsync(filePath);
        _logger.LogInformation("File deleted: {FilePath}", filePath);
    }
}
```

### Image Processing Example

```csharp
public class ImageProcessingService
{
    private readonly IBlobStorageService _blobStorage;
    private readonly IImageProcessor _imageProcessor;

    public async Task<string> ProcessAndUploadImageAsync(IFormFile imageFile)
    {
        // Download original
        using var originalStream = imageFile.OpenReadStream();
        
        // Process image (resize, optimize, etc.)
        using var processedStream = await _imageProcessor.ResizeAsync(originalStream, 800, 600);
        
        // Upload processed image
        var fileName = $"processed/{Guid.NewGuid()}.jpg";
        await _blobStorage.UploadAsync(fileName, processedStream, "image/jpeg");
        
        return fileName;
    }
}
```

---

## 🔧 Extension Methods Usage

### Type Extensions

```csharp
// Check if type implements interface
if (typeof(Product).ImplementsInterface<IAuditable>())
{
    // Handle auditable entity
}

// Get property value dynamically
var product = new Product();
var name = product.GetPropertyValue("Name");
var price = product.GetPropertyValue<decimal>("Price");

// Set property value
product.SetPropertyValue("Name", "New Product Name");
```

### Enum Extensions

```csharp
public enum OrderStatus
{
    [Description("Order is pending")]
    Pending,
    
    [Description("Order is confirmed")]
    Confirmed,
    
    [Description("Order is shipped")]
    Shipped
}

// Get description
var status = OrderStatus.Pending;
var description = status.GetDescription(); // "Order is pending"

// Get all descriptions
var allDescriptions = EnumExtensions.GetAllDescriptions<OrderStatus>();
```

### Collection Extensions

```csharp
// Async enumerable to list
var asyncItems = GetItemsAsync();
var list = await asyncItems.ToListAsync();

// Chunked processing
var largeList = Enumerable.Range(1, 10000);
await largeList.ForEachChunkedAsync(100, async chunk =>
{
    await ProcessChunkAsync(chunk);
});
```

---

## 📖 More Examples

For complete working examples, check out:

- **[SlimBus Template](../src/Templates/SlimBus.ApiEndpoints/README.md)** - Complete API implementation
- **[Unit Tests](../src/Tests/)** - Comprehensive test examples
- **[Integration Tests](../src/Templates/SlimBus.ApiEndpoints/SlimBus.App.Tests/)** - End-to-end testing

---

> 💡 **Example Tip**: All examples are based on real implementations in the DKNet codebase. Check the source code for the most up-to-date patterns!