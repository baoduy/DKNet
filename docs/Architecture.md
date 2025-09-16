# Architecture Guide

This guide explains the architectural principles, patterns, and design decisions behind the DKNet Framework.

## 📋 Table of Contents

- [Architectural Overview](#architectural-overview)
- [Domain-Driven Design (DDD)](#domain-driven-design-ddd)
- [Onion Architecture](#onion-architecture)
- [CQRS Pattern](#cqrs-pattern)
- [Event-Driven Architecture](#event-driven-architecture)
- [Repository Pattern](#repository-pattern)
- [Dependency Injection](#dependency-injection)
- [Cross-Cutting Concerns](#cross-cutting-concerns)

---

## 🏗️ Architectural Overview

DKNet Framework is built on the foundation of **Domain-Driven Design (DDD)** principles and implements the **Onion Architecture** pattern. This approach ensures:

- **Maintainability**: Clear separation of concerns and dependencies
- **Testability**: Business logic isolated from infrastructure concerns
- **Flexibility**: Infrastructure can be easily replaced or extended
- **Scalability**: Modular design supports horizontal and vertical scaling

### Core Architectural Principles

1. **Dependency Inversion**: High-level modules don't depend on low-level modules
2. **Single Responsibility**: Each component has one reason to change
3. **Open/Closed Principle**: Open for extension, closed for modification
4. **Interface Segregation**: Clients depend only on interfaces they use
5. **Don't Repeat Yourself**: Common functionality is centralized

### Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                      🌐 Presentation Layer                       │
│                 (Controllers, API Endpoints, UI)                │
│                                                                 │
│  📋 Minimal API Endpoints                                        │
│  📡 REST API Controllers                                         │
│  🔍 GraphQL Endpoints (optional)                                │
│  📱 Blazor Components (optional)                                │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                 🎯 Application Layer                            │
│            (Application Services, Command/Query Handlers)       │
│                                                                 │
│  📨 CQRS Commands & Queries                                     │
│  ✅ Validation (FluentValidation)                               │
│  🔄 Data Transformation                                          │
│  📋 Application Services                                         │
│  🗃️ DTOs and ViewModels                                         │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                    💼 Domain Layer                              │
│              (Entities, Value Objects, Domain Services)         │
│                                                                 │
│  🏢 Aggregate Roots                                             │
│  📊 Entities & Value Objects                                    │
│  📋 Domain Events                                               │
│  🔒 Business Rules & Logic                                      │
│  📐 Domain Services                                             │
│  🔧 Specifications                                              │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                   🗄️ Infrastructure Layer                       │
│         (Data Access, External Services, Cross-cutting)         │
│                                                                 │
│  🗃️ Entity Framework Core                                       │
│  📁 Repository Implementations                                  │
│  📨 Message Bus Integration                                     │
│  🗂️ File Storage Services                                       │
│  🔐 Authentication Providers                                    │
│  📊 Logging & Monitoring                                        │
└─────────────────────────────────────────────────────────────────┘
```

---

## 🎯 Domain-Driven Design (DDD)

DDD is a software development approach that focuses on the core business domain and domain logic. DKNet implements DDD through several key concepts:

### Ubiquitous Language

All code uses the same terminology as domain experts:

```csharp
// Good: Uses business terminology
public class Order
{
    public void PlaceOrder(Customer customer, IEnumerable<OrderItem> items)
    {
        ValidateCustomerCanPlaceOrder(customer);
        CalculateTotalAmount(items);
        AddEvent(new OrderPlacedEvent(Id, customer.Id));
    }
}

// Avoid: Technical terminology that doesn't match business
public class OrderEntity
{
    public void Insert(CustomerEntity customer, List<OrderItemEntity> items)
    {
        // Technical implementation details
    }
}
```

### Bounded Contexts

DKNet organizes code into bounded contexts, each with its own models:

```
src/
├── Sales/              # Sales Bounded Context
│   ├── Orders/
│   ├── Customers/
│   └── Products/
├── Inventory/          # Inventory Bounded Context
│   ├── Stock/
│   ├── Warehouses/
│   └── Suppliers/
└── Billing/            # Billing Bounded Context
    ├── Invoices/
    ├── Payments/
    └── TaxCalculation/
```

### Aggregate Roots

Aggregates ensure consistency boundaries:

```csharp
[Table("Orders", Schema = "sales")]
public class Order : AggregateRoot
{
    private readonly List<OrderItem> _items = new();

    public Order(string customerName, string shippingAddress, string createdBy)
        : base(Guid.NewGuid(), createdBy)
    {
        CustomerName = customerName;
        ShippingAddress = shippingAddress;
        Status = OrderStatus.Draft;
    }

    public string CustomerName { get; private set; }
    public string ShippingAddress { get; private set; }
    public OrderStatus Status { get; private set; }
    public decimal TotalAmount { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    public void AddItem(string productName, decimal unitPrice, int quantity, string userId)
    {
        if (Status != OrderStatus.Draft)
            throw new InvalidOperationException("Cannot modify confirmed order");

        var item = new OrderItem(productName, unitPrice, quantity);
        _items.Add(item);
        
        RecalculateTotal();
        SetUpdatedBy(userId);
        
        AddEvent(new OrderItemAddedEvent(Id, item.ProductName, item.Quantity));
    }

    public void ConfirmOrder(string userId)
    {
        if (Status != OrderStatus.Draft)
            throw new InvalidOperationException("Order is already confirmed");

        if (!_items.Any())
            throw new InvalidOperationException("Cannot confirm empty order");

        Status = OrderStatus.Confirmed;
        SetUpdatedBy(userId);
        
        AddEvent(new OrderConfirmedEvent(Id, CustomerName, TotalAmount));
    }

    private void RecalculateTotal()
    {
        TotalAmount = _items.Sum(item => item.TotalPrice);
    }
}
```

### Value Objects

Immutable objects that represent concepts:

```csharp
public record Money
{
    public Money(decimal amount, string currency)
    {
        if (amount < 0)
            throw new ArgumentException("Amount cannot be negative");
        
        if (string.IsNullOrEmpty(currency))
            throw new ArgumentException("Currency is required");

        Amount = amount;
        Currency = currency.ToUpperInvariant();
    }

    public decimal Amount { get; }
    public string Currency { get; }

    public Money Add(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException("Cannot add different currencies");

        return new Money(Amount + other.Amount, Currency);
    }

    public static Money Zero(string currency) => new(0, currency);
}

public record Address
{
    public Address(string street, string city, string state, string zipCode, string country)
    {
        Street = street ?? throw new ArgumentNullException(nameof(street));
        City = city ?? throw new ArgumentNullException(nameof(city));
        State = state ?? throw new ArgumentNullException(nameof(state));
        ZipCode = zipCode ?? throw new ArgumentNullException(nameof(zipCode));
        Country = country ?? throw new ArgumentNullException(nameof(country));
    }

    public string Street { get; }
    public string City { get; }
    public string State { get; }
    public string ZipCode { get; }
    public string Country { get; }

    public override string ToString() => $"{Street}, {City}, {State} {ZipCode}, {Country}";
}
```

### Domain Services

Complex business logic that doesn't belong to a single entity:

```csharp
public interface IPricingService
{
    Task<Money> CalculatePriceAsync(Product product, Customer customer, int quantity);
}

public class PricingService : IPricingService
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IDiscountRepository _discountRepository;

    public PricingService(ICustomerRepository customerRepository, IDiscountRepository discountRepository)
    {
        _customerRepository = customerRepository;
        _discountRepository = discountRepository;
    }

    public async Task<Money> CalculatePriceAsync(Product product, Customer customer, int quantity)
    {
        var basePrice = product.Price.Multiply(quantity);
        
        var discounts = await _discountRepository.GetActiveDiscountsAsync(customer.Id, product.Id);
        var discountAmount = CalculateDiscountAmount(basePrice, discounts);
        
        return basePrice.Subtract(discountAmount);
    }

    private Money CalculateDiscountAmount(Money basePrice, IEnumerable<Discount> discounts)
    {
        // Complex discount calculation logic
        return discounts.Aggregate(Money.Zero(basePrice.Currency), 
            (total, discount) => total.Add(discount.CalculateDiscount(basePrice)));
    }
}
```

---

## 🧅 Onion Architecture

The Onion Architecture ensures that dependencies flow inward, making the domain layer independent of infrastructure concerns.

### Layer Responsibilities

#### 1. Domain Layer (Core)
- **Purpose**: Contains business entities, value objects, and domain logic
- **Dependencies**: None (pure business logic)
- **Examples**: `Product`, `Order`, `Customer`, domain events

```csharp
// Domain Entity - No infrastructure dependencies
public class Product : AggregateRoot
{
    public Product(string name, Money price, string description, string createdBy)
        : base(Guid.NewGuid(), createdBy)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Price = price ?? throw new ArgumentNullException(nameof(price));
        Description = description ?? string.Empty;
        IsActive = true;
    }

    public string Name { get; private set; }
    public Money Price { get; private set; }
    public string Description { get; private set; }
    public bool IsActive { get; private set; }

    public void UpdatePrice(Money newPrice, string userId)
    {
        if (!IsActive)
            throw new InvalidOperationException("Cannot update price for inactive product");

        var oldPrice = Price;
        Price = newPrice;
        SetUpdatedBy(userId);

        AddEvent(new ProductPriceChangedEvent(Id, oldPrice, newPrice));
    }

    public void Deactivate(string userId)
    {
        IsActive = false;
        SetUpdatedBy(userId);
        AddEvent(new ProductDeactivatedEvent(Id, Name));
    }
}
```

#### 2. Application Layer
- **Purpose**: Orchestrates domain objects and coordinates with infrastructure
- **Dependencies**: Domain layer only
- **Examples**: Command/query handlers, application services

```csharp
// Application Service - Orchestrates domain objects
public class CreateProductHandler : IRequestHandler<CreateProductCommand, ProductResult>
{
    private readonly IProductRepository _repository;
    private readonly IPricingService _pricingService;
    private readonly IMapper _mapper;

    public CreateProductHandler(
        IProductRepository repository,
        IPricingService pricingService,
        IMapper mapper)
    {
        _repository = repository;
        _pricingService = pricingService;
        _mapper = mapper;
    }

    public async Task<ProductResult> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        // Business validation
        if (await _repository.ExistsWithNameAsync(request.Name))
            throw new BusinessException($"Product with name '{request.Name}' already exists");

        // Create domain entity
        var price = new Money(request.Price, request.Currency);
        var product = new Product(request.Name, price, request.Description, request.UserId);

        // Apply business rules through domain service
        var validatedPrice = await _pricingService.ValidatePriceAsync(product);
        if (validatedPrice != price)
        {
            product.UpdatePrice(validatedPrice, request.UserId);
        }

        // Persist
        await _repository.AddAsync(product, cancellationToken);

        // Map to response
        return _mapper.Map<ProductResult>(product);
    }
}
```

#### 3. Infrastructure Layer
- **Purpose**: Implements interfaces defined in inner layers
- **Dependencies**: Application and Domain layers
- **Examples**: Repository implementations, external service clients

```csharp
// Infrastructure Implementation - Implements domain interfaces
public class ProductRepository : Repository<Product>, IProductRepository
{
    public ProductRepository(CatalogDbContext context) : base(context) { }

    public async Task<bool> ExistsWithNameAsync(string name)
    {
        return await Gets().AnyAsync(p => p.Name == name);
    }

    public async Task<IEnumerable<Product>> GetActiveProductsAsync()
    {
        return await Gets()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Product>> FindByPriceRangeAsync(Money minPrice, Money maxPrice)
    {
        return await Gets()
            .Where(p => p.IsActive)
            .Where(p => p.Price.Amount >= minPrice.Amount && p.Price.Amount <= maxPrice.Amount)
            .Where(p => p.Price.Currency == minPrice.Currency)
            .ToListAsync();
    }
}
```

#### 4. Presentation Layer
- **Purpose**: Handles user interaction and external communication
- **Dependencies**: Application layer only
- **Examples**: API controllers, web pages, message handlers

```csharp
// Presentation Layer - Handles external communication
[ApiController]
[Route("api/v1/products")]
public class ProductsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProductsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<ActionResult<ProductResult>> CreateProduct([FromBody] CreateProductCommand command)
    {
        command.UserId = User.Identity?.Name ?? "anonymous";
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetProduct), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductResult>> GetProduct(Guid id)
    {
        var query = new GetProductQuery { Id = id };
        var result = await _mediator.Send(query);
        return result != null ? Ok(result) : NotFound();
    }
}
```

### Dependency Flow

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Presentation  │───▶│   Application   │───▶│     Domain      │
│      Layer      │    │      Layer      │    │     Layer       │
└─────────────────┘    └─────────────────┘    └─────────────────┘
         │                       │                       ▲
         │                       │                       │
         ▼                       ▼                       │
┌─────────────────────────────────────────────────────────┴─────┐
│                Infrastructure Layer                           │
│  (Implements interfaces defined in inner layers)             │
└───────────────────────────────────────────────────────────────┘
```

---

## ⚡ CQRS Pattern

Command Query Responsibility Segregation (CQRS) separates read and write operations, allowing optimization of each concern independently.

### Command Side (Writes)

Commands modify state and should follow business rules:

```csharp
// Command - Represents intent to change state
public record UpdateProductPriceCommand : IRequest<ProductResult>
{
    public Guid ProductId { get; init; }
    public decimal NewPrice { get; init; }
    public string Currency { get; init; } = "USD";
    public string Reason { get; init; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
}

// Command Validator - Ensures command is valid
public class UpdateProductPriceValidator : AbstractValidator<UpdateProductPriceCommand>
{
    public UpdateProductPriceValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.NewPrice).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.Reason).NotEmpty().When(x => x.NewPrice > 1000);
    }
}

// Command Handler - Implements business logic
public class UpdateProductPriceHandler : IRequestHandler<UpdateProductPriceCommand, ProductResult>
{
    private readonly IProductRepository _repository;
    private readonly IPricingService _pricingService;
    private readonly IMapper _mapper;

    public async Task<ProductResult> Handle(UpdateProductPriceCommand request, CancellationToken cancellationToken)
    {
        // Load aggregate
        var product = await _repository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product == null)
            throw new NotFoundException($"Product {request.ProductId} not found");

        // Apply business rules
        var newPrice = new Money(request.NewPrice, request.Currency);
        await _pricingService.ValidatePriceChangeAsync(product, newPrice);

        // Execute command
        product.UpdatePrice(newPrice, request.UserId);

        // Persist changes
        await _repository.SaveChangesAsync(cancellationToken);

        return _mapper.Map<ProductResult>(product);
    }
}
```

### Query Side (Reads)

Queries retrieve data optimized for specific use cases:

```csharp
// Query - Requests data
public record GetProductsQuery : IRequest<PagedResult<ProductSummary>>
{
    public int PageIndex { get; init; } = 0;
    public int PageSize { get; init; } = 20;
    public string? SearchTerm { get; init; }
    public bool ActiveOnly { get; init; } = true;
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
}

// Query Result - Optimized for display
public record ProductSummary
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public string Currency { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTime LastUpdated { get; init; }
}

// Query Handler - Optimized for reading
public class GetProductsHandler : IRequestHandler<GetProductsQuery, PagedResult<ProductSummary>>
{
    private readonly IReadRepository<Product> _repository;

    public GetProductsHandler(IReadRepository<Product> repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<ProductSummary>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        var query = _repository.Gets();

        // Apply filters
        if (request.ActiveOnly)
            query = query.Where(p => p.IsActive);

        if (!string.IsNullOrEmpty(request.SearchTerm))
            query = query.Where(p => p.Name.Contains(request.SearchTerm) || 
                                     p.Description.Contains(request.SearchTerm));

        if (request.MinPrice.HasValue)
            query = query.Where(p => p.Price.Amount >= request.MinPrice.Value);

        if (request.MaxPrice.HasValue)
            query = query.Where(p => p.Price.Amount <= request.MaxPrice.Value);

        // Project to summary
        var summaries = query.Select(p => new ProductSummary
        {
            Id = p.Id,
            Name = p.Name,
            Price = p.Price.Amount,
            Currency = p.Price.Currency,
            IsActive = p.IsActive,
            LastUpdated = p.UpdatedAt ?? p.CreatedAt
        });

        // Apply paging
        return await summaries.ToPagedResultAsync(request.PageIndex, request.PageSize, cancellationToken);
    }
}
```

### CQRS Benefits

1. **Scalability**: Read and write sides can be scaled independently
2. **Performance**: Queries can be optimized without affecting commands
3. **Flexibility**: Different data models for different use cases
4. **Maintainability**: Clear separation of concerns

---

## 🔄 Event-Driven Architecture

Domain events enable loose coupling between bounded contexts and support complex business workflows.

### Domain Events

Events represent things that have happened in the domain:

```csharp
// Domain Event - Something that happened
public record ProductPriceChangedEvent(
    Guid ProductId,
    Money OldPrice,
    Money NewPrice,
    string ChangedBy,
    string Reason = "") : DomainEvent;

public record OrderPlacedEvent(
    Guid OrderId,
    Guid CustomerId,
    Money TotalAmount,
    IReadOnlyList<Guid> ProductIds) : DomainEvent;

public record CustomerUpgradedEvent(
    Guid CustomerId,
    CustomerTier OldTier,
    CustomerTier NewTier) : DomainEvent;
```

### Event Handlers

Handlers respond to events and implement side effects:

```csharp
// Event Handler - Responds to domain events
public class ProductPriceChangedHandler : IDomainEventHandler<ProductPriceChangedEvent>
{
    private readonly INotificationService _notificationService;
    private readonly IAuditService _auditService;
    private readonly ILogger<ProductPriceChangedHandler> _logger;

    public ProductPriceChangedHandler(
        INotificationService notificationService,
        IAuditService auditService,
        ILogger<ProductPriceChangedHandler> logger)
    {
        _notificationService = notificationService;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task Handle(ProductPriceChangedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Log the event
        _logger.LogInformation(
            "Product {ProductId} price changed from {OldPrice} to {NewPrice} by {ChangedBy}",
            domainEvent.ProductId,
            domainEvent.OldPrice,
            domainEvent.NewPrice,
            domainEvent.ChangedBy);

        // Audit the change
        await _auditService.RecordPriceChangeAsync(
            domainEvent.ProductId,
            domainEvent.OldPrice,
            domainEvent.NewPrice,
            domainEvent.ChangedBy,
            domainEvent.Reason);

        // Notify stakeholders if significant change
        var changePercentage = CalculateChangePercentage(domainEvent.OldPrice, domainEvent.NewPrice);
        if (changePercentage > 0.1m) // 10% change
        {
            await _notificationService.NotifySignificantPriceChangeAsync(
                domainEvent.ProductId,
                changePercentage,
                cancellationToken);
        }
    }

    private decimal CalculateChangePercentage(Money oldPrice, Money newPrice)
    {
        if (oldPrice.Amount == 0) return 0;
        return Math.Abs(newPrice.Amount - oldPrice.Amount) / oldPrice.Amount;
    }
}

// Multiple handlers can respond to the same event
public class InventoryNotificationHandler : IDomainEventHandler<ProductPriceChangedEvent>
{
    private readonly IInventoryService _inventoryService;

    public InventoryNotificationHandler(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    public async Task Handle(ProductPriceChangedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Update inventory forecasts based on price change
        await _inventoryService.UpdateForecastsAsync(domainEvent.ProductId, domainEvent.NewPrice);
    }
}
```

### Event Dispatching

Events are dispatched when aggregate changes are persisted:

```csharp
public class EventDispatchingDbContext : DbContext
{
    private readonly IDomainEventDispatcher _eventDispatcher;

    public EventDispatchingDbContext(
        DbContextOptions options,
        IDomainEventDispatcher eventDispatcher) : base(options)
    {
        _eventDispatcher = eventDispatcher;
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Get all domain events before saving
        var domainEvents = ChangeTracker.Entries<AggregateRoot>()
            .SelectMany(entry => entry.Entity.GetUncommittedEvents())
            .ToList();

        // Save changes to database
        var result = await base.SaveChangesAsync(cancellationToken);

        // Dispatch events after successful save
        foreach (var domainEvent in domainEvents)
        {
            await _eventDispatcher.DispatchAsync(domainEvent, cancellationToken);
        }

        // Clear events from aggregates
        ChangeTracker.Entries<AggregateRoot>()
            .ToList()
            .ForEach(entry => entry.Entity.ClearEvents());

        return result;
    }
}
```

---

## 📊 Repository Pattern

The Repository pattern abstracts data access and provides a more object-oriented view of the persistence layer.

### Repository Interfaces

```csharp
// Generic repository for common operations
public interface IRepository<TEntity> where TEntity : AggregateRoot
{
    IQueryable<TEntity> Gets();
    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default);
    void Update(TEntity entity);
    void Delete(TEntity entity);
    Task<IEnumerable<TEntity>> FindAsync(Specification<TEntity> specification, CancellationToken cancellationToken = default);
}

// Specific repository with domain-specific methods
public interface IProductRepository : IRepository<Product>
{
    Task<bool> ExistsWithNameAsync(string name);
    Task<IEnumerable<Product>> GetActiveProductsAsync();
    Task<IEnumerable<Product>> GetProductsByCategory(string category);
    Task<Product?> GetBySkuAsync(string sku);
}
```

### Repository Implementation

```csharp
public class ProductRepository : Repository<Product>, IProductRepository
{
    public ProductRepository(CatalogDbContext context) : base(context) { }

    public async Task<bool> ExistsWithNameAsync(string name)
    {
        return await Gets().AnyAsync(p => p.Name == name);
    }

    public async Task<IEnumerable<Product>> GetActiveProductsAsync()
    {
        return await Gets()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Product>> GetProductsByCategory(string category)
    {
        return await Gets()
            .Where(p => p.Category == category && p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<Product?> GetBySkuAsync(string sku)
    {
        return await Gets().FirstOrDefaultAsync(p => p.Sku == sku);
    }
}
```

### Specification Pattern

Specifications encapsulate query logic and can be combined:

```csharp
public class ActiveProductsSpecification : Specification<Product>
{
    public override Expression<Func<Product, bool>> ToExpression()
    {
        return product => product.IsActive;
    }
}

public class ProductsInCategorySpecification : Specification<Product>
{
    private readonly string _category;

    public ProductsInCategorySpecification(string category)
    {
        _category = category;
    }

    public override Expression<Func<Product, bool>> ToExpression()
    {
        return product => product.Category == _category;
    }
}

public class ProductsInPriceRangeSpecification : Specification<Product>
{
    private readonly decimal _minPrice;
    private readonly decimal _maxPrice;

    public ProductsInPriceRangeSpecification(decimal minPrice, decimal maxPrice)
    {
        _minPrice = minPrice;
        _maxPrice = maxPrice;
    }

    public override Expression<Func<Product, bool>> ToExpression()
    {
        return product => product.Price.Amount >= _minPrice && product.Price.Amount <= _maxPrice;
    }
}

// Usage: Combine specifications
var spec = new ActiveProductsSpecification()
    .And(new ProductsInCategorySpecification("Electronics"))
    .And(new ProductsInPriceRangeSpecification(100, 1000));

var products = await repository.FindAsync(spec);
```

---

## 🔌 Dependency Injection

DKNet heavily uses dependency injection to maintain loose coupling and enable testability.

### Service Registration

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCatalogServices(this IServiceCollection services)
    {
        // Register repositories
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();

        // Register domain services
        services.AddScoped<IPricingService, PricingService>();
        services.AddScoped<IInventoryService, InventoryService>();

        // Register application services
        services.AddScoped<IProductApplicationService, ProductApplicationService>();

        // Register event handlers
        services.AddScoped<IDomainEventHandler<ProductPriceChangedEvent>, ProductPriceChangedHandler>();
        services.AddScoped<IDomainEventHandler<ProductPriceChangedEvent>, InventoryNotificationHandler>();

        return services;
    }
}
```

### Constructor Injection

```csharp
public class ProductApplicationService : IProductApplicationService
{
    private readonly IProductRepository _productRepository;
    private readonly IPricingService _pricingService;
    private readonly IInventoryService _inventoryService;
    private readonly IMapper _mapper;
    private readonly ILogger<ProductApplicationService> _logger;

    public ProductApplicationService(
        IProductRepository productRepository,
        IPricingService pricingService,
        IInventoryService inventoryService,
        IMapper mapper,
        ILogger<ProductApplicationService> logger)
    {
        _productRepository = productRepository;
        _pricingService = pricingService;
        _inventoryService = inventoryService;
        _mapper = mapper;
        _logger = logger;
    }

    // Methods use injected dependencies
}
```

---

## 🔧 Cross-Cutting Concerns

DKNet handles cross-cutting concerns through various patterns and implementations.

### Logging

```csharp
public class ProductService
{
    private readonly ILogger<ProductService> _logger;

    public async Task<Product> CreateProductAsync(CreateProductCommand command)
    {
        _logger.LogInformation("Creating product {ProductName} for user {UserId}", 
            command.Name, command.UserId);

        try
        {
            // Business logic
            var product = new Product(command.Name, command.Price, command.UserId);
            
            _logger.LogInformation("Successfully created product {ProductId}", product.Id);
            return product;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create product {ProductName}", command.Name);
            throw;
        }
    }
}
```

### Validation

```csharp
public class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    private readonly IProductRepository _repository;

    public CreateProductValidator(IProductRepository repository)
    {
        _repository = repository;

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200)
            .MustAsync(BeUniqueName).WithMessage("Product name must be unique");

        RuleFor(x => x.Price)
            .GreaterThan(0)
            .LessThan(10000);

        RuleFor(x => x.Category)
            .NotEmpty()
            .Must(BeValidCategory).WithMessage("Invalid category");
    }

    private async Task<bool> BeUniqueName(string name, CancellationToken cancellationToken)
    {
        return !await _repository.ExistsWithNameAsync(name);
    }

    private bool BeValidCategory(string category)
    {
        var validCategories = new[] { "Electronics", "Clothing", "Books", "Home" };
        return validCategories.Contains(category);
    }
}
```

### Error Handling

```csharp
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var response = exception switch
        {
            NotFoundException => new { error = "Resource not found", statusCode = 404 },
            ValidationException => new { error = "Validation failed", statusCode = 400 },
            BusinessException => new { error = exception.Message, statusCode = 422 },
            _ => new { error = "An error occurred", statusCode = 500 }
        };

        context.Response.StatusCode = response.statusCode;
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
```

---

## 📖 Related Documentation

- **[Getting Started](Getting-Started.md)** - Quick start guide
- **[Configuration](Configuration.md)** - Setup and configuration
- **[Examples](Examples/README.md)** - Practical implementations
- **[API Reference](API-Reference.md)** - Detailed API documentation

---

> 🏗️ **Architecture Note**: This architecture guide represents the current state of DKNet Framework. As the framework evolves, architectural patterns may be refined based on community feedback and real-world usage.