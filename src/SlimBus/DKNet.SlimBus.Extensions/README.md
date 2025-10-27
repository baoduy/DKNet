# DKNet.SlimBus.Extensions

[![NuGet](https://img.shields.io/nuget/v/DKNet.SlimBus.Extensions)](https://www.nuget.org/packages/DKNet.SlimBus.Extensions/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DKNet.SlimBus.Extensions)](https://www.nuget.org/packages/DKNet.SlimBus.Extensions/)
[![.NET](https://img.shields.io/badge/.NET-9.0-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](../../../../LICENSE)

Enhanced SlimMessageBus integration with Entity Framework Core, providing fluent interfaces for CQRS patterns, automatic
transaction management, and result-based error handling. This package bridges SlimMessageBus with EF Core for seamless
domain-driven applications.

## Features

- **CQRS Fluent Interfaces**: Strongly-typed request/query handlers with result patterns
- **EF Core Integration**: Automatic SaveChanges after successful request processing
- **Result Pattern Support**: Built-in FluentResults integration for error handling
- **Transaction Management**: Automatic transaction handling with rollback on failures
- **Query Abstractions**: Specialized interfaces for queries vs commands
- **Pagination Support**: Built-in support for paged query results
- **Event Consumer Abstractions**: Fluent interfaces for domain event handling
- **Auto-Save Behavior**: Intelligent EF Core change detection and persistence

## Supported Frameworks

- .NET 9.0+
- Entity Framework Core 9.0+
- SlimMessageBus 2.0+
- FluentResults 3.0+

## Installation

Install via NuGet Package Manager:

```bash
dotnet add package DKNet.SlimBus.Extensions
```

Or via Package Manager Console:

```powershell
Install-Package DKNet.SlimBus.Extensions
```

## Quick Start

### Setup SlimBus with EF Core

```csharp
using Microsoft.Extensions.DependencyInjection;
using DKNet.SlimBus.Extensions;

// Configure SlimBus with EF Core integration
services.AddSlimBusForEfCore(builder =>
{
    builder
        .WithProviderMemory() // or other providers
        .AutoDeclareFrom(typeof(CreateProductHandler).Assembly)
        .AddJsonSerializer();
});

// Add your DbContext
services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));
```

### Command Handler with Result Pattern

```csharp
using DKNet.SlimBus.Extensions;
using FluentResults;

// Command definition
public record CreateProduct : Fluents.Requests.IWitResponse<ProductResult>
{
    public required string Name { get; init; }
    public required decimal Price { get; init; }
    public required string CategoryId { get; init; }
}

public record ProductResult
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required decimal Price { get; init; }
}

// Command handler
public class CreateProductHandler : Fluents.Requests.IHandler<CreateProduct, ProductResult>
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public CreateProductHandler(AppDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<IResult<ProductResult>> Handle(CreateProduct request, CancellationToken cancellationToken)
    {
        // Validation
        if (await _context.Products.AnyAsync(p => p.Name == request.Name, cancellationToken))
            return Result.Fail<ProductResult>($"Product with name '{request.Name}' already exists");

        // Create entity
        var product = new Product(request.Name, request.Price, request.CategoryId);
        _context.Products.Add(product);
        
        // EF Core SaveChanges is called automatically after successful processing
        
        return Result.Ok(_mapper.Map<ProductResult>(product));
    }
}
```

### Query Handler

```csharp
using DKNet.SlimBus.Extensions;

// Query definition
public record GetProduct : Fluents.Queries.IWitResponse<ProductResult>
{
    public required Guid Id { get; init; }
}

// Query handler (no auto-save for queries)
public class GetProductHandler : Fluents.Queries.IHandler<GetProduct, ProductResult>
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public GetProductHandler(AppDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<ProductResult?> Handle(GetProduct request, CancellationToken cancellationToken)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
            
        return product == null ? null : _mapper.Map<ProductResult>(product);
    }
}
```

### Paged Query Handler

```csharp
using X.PagedList;
using DKNet.SlimBus.Extensions;

// Paged query definition
public record GetProductsPage : Fluents.Queries.IWitPageResponse<ProductResult>
{
    public int PageIndex { get; init; } = 0;
    public int PageSize { get; init; } = 20;
    public string? CategoryId { get; init; }
}

// Paged query handler
public class GetProductsPageHandler : Fluents.Queries.IPageHandler<GetProductsPage, ProductResult>
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public GetProductsPageHandler(AppDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<IPagedList<ProductResult>> Handle(GetProductsPage request, CancellationToken cancellationToken)
    {
        var query = _context.Products.AsQueryable();
        
        if (!string.IsNullOrEmpty(request.CategoryId))
            query = query.Where(p => p.CategoryId == request.CategoryId);
            
        return await query
            .OrderBy(p => p.Name)
            .Select(p => _mapper.Map<ProductResult>(p))
            .ToPagedListAsync(request.PageIndex, request.PageSize, cancellationToken);
    }
}
```

## Configuration

### Handler Registration

```csharp
// Handlers are automatically discovered and registered
services.AddSlimBusForEfCore(builder =>
{
    builder
        .WithProviderMemory()
        // Auto-discover handlers from assemblies
        .AutoDeclareFrom(typeof(CreateProductHandler).Assembly, typeof(GetProductHandler).Assembly)
        .AddJsonSerializer();
});
```

### Custom Behaviors and Interceptors

```csharp
using SlimMessageBus.Host.Interceptor;

public class ValidationInterceptor<TRequest, TResponse> : IRequestHandlerInterceptor<TRequest, TResponse>
{
    private readonly IValidator<TRequest> _validator;

    public ValidationInterceptor(IValidator<TRequest> validator)
    {
        _validator = validator;
    }

    public async Task<TResponse> OnHandle(TRequest request, Func<Task<TResponse>> next, IConsumerContext context)
    {
        // Validate request
        var validationResult = await _validator.ValidateAsync(request, context.CancellationToken);
        if (!validationResult.IsValid)
        {
            if (typeof(TResponse).IsAssignableFrom(typeof(IResultBase)))
            {
                var errors = validationResult.Errors.Select(e => e.ErrorMessage);
                return (TResponse)(object)Result.Fail(errors);
            }
            throw new ValidationException(validationResult.Errors);
        }

        return await next();
    }
}

// Register the interceptor
services.AddScoped(typeof(IRequestHandlerInterceptor<,>), typeof(ValidationInterceptor<,>));
```

## API Reference

### Request Interfaces

- `Fluents.Requests.INoResponse` - Commands that don't return data
- `Fluents.Requests.IWitResponse<TResponse>` - Commands that return data
- `Fluents.Requests.IHandler<TRequest>` - Handler for no-response commands
- `Fluents.Requests.IHandler<TRequest, TResponse>` - Handler for commands with response

### Query Interfaces

- `Fluents.Queries.IWitResponse<TResponse>` - Single-item queries
- `Fluents.Queries.IWitPageResponse<TResponse>` - Paged queries
- `Fluents.Queries.IHandler<TQuery, TResponse>` - Single-item query handler
- `Fluents.Queries.IPageHandler<TQuery, TResponse>` - Paged query handler

### Event Interfaces

- `Fluents.EventsConsumers.IHandler<TEvent>` - Domain event handler

### EF Core Integration

- `AddSlimBusForEfCore()` - Register SlimBus with EF Core auto-save
- `EfAutoSavePostProcessor<,>` - Automatic SaveChanges after successful commands

## Advanced Usage

### Event Handler with Domain Events

```csharp
using DKNet.SlimBus.Extensions;

// Domain event
public record ProductCreatedEvent(Guid ProductId, string Name, decimal Price);

// Event handler
public class ProductCreatedHandler : Fluents.EventsConsumers.IHandler<ProductCreatedEvent>
{
    private readonly ILogger<ProductCreatedHandler> _logger;
    private readonly IEmailService _emailService;

    public ProductCreatedHandler(ILogger<ProductCreatedHandler> logger, IEmailService emailService)
    {
        _logger = logger;
        _emailService = emailService;
    }

    public async Task Handle(ProductCreatedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Product created: {ProductId} - {Name}", notification.ProductId, notification.Name);
        
        // Send notification email
        await _emailService.SendProductCreatedNotificationAsync(notification.ProductId, cancellationToken);
    }
}
```

### Complex Command with Multiple Operations

```csharp
public record ProcessOrder : Fluents.Requests.IWitResponse<OrderResult>
{
    public required Guid CustomerId { get; init; }
    public required List<OrderItemRequest> Items { get; init; }
}

public class ProcessOrderHandler : Fluents.Requests.IHandler<ProcessOrder, OrderResult>
{
    private readonly AppDbContext _context;
    private readonly IInventoryService _inventoryService;
    private readonly IPaymentService _paymentService;

    public ProcessOrderHandler(AppDbContext context, IInventoryService inventoryService, IPaymentService paymentService)
    {
        _context = context;
        _inventoryService = inventoryService;
        _paymentService = paymentService;
    }

    public async Task<IResult<OrderResult>> Handle(ProcessOrder request, CancellationToken cancellationToken)
    {
        // Validate inventory
        var inventoryCheck = await _inventoryService.CheckAvailabilityAsync(request.Items, cancellationToken);
        if (!inventoryCheck.IsSuccess)
            return Result.Fail<OrderResult>(inventoryCheck.Errors);

        // Create order
        var order = new Order(request.CustomerId);
        foreach (var item in request.Items)
        {
            order.AddItem(item.ProductId, item.Quantity, item.Price);
        }
        
        _context.Orders.Add(order);

        // Process payment
        var paymentResult = await _paymentService.ProcessPaymentAsync(order.TotalAmount, cancellationToken);
        if (!paymentResult.IsSuccess)
            return Result.Fail<OrderResult>(paymentResult.Errors);

        order.MarkAsPaid(paymentResult.Value.TransactionId);
        
        // EF Core will auto-save all changes if no errors occur
        
        return Result.Ok(new OrderResult 
        { 
            OrderId = order.Id, 
            TotalAmount = order.TotalAmount,
            Status = order.Status 
        });
    }
}
```

## Error Handling

The package uses FluentResults for comprehensive error handling:

```csharp
public async Task<IResult<ProductResult>> Handle(CreateProduct request, CancellationToken cancellationToken)
{
    // Business rule validation
    if (request.Price <= 0)
        return Result.Fail<ProductResult>("Price must be greater than zero");

    // Multiple validation errors
    var errors = new List<string>();
    if (string.IsNullOrEmpty(request.Name))
        errors.Add("Name is required");
    if (request.Price <= 0)
        errors.Add("Price must be positive");
        
    if (errors.Any())
        return Result.Fail<ProductResult>(errors);

    // Success case
    var product = new Product(request.Name, request.Price);
    _context.Products.Add(product);
    
    return Result.Ok(_mapper.Map<ProductResult>(product));
}
```

## Transaction Behavior

- **Commands**: Automatic SaveChanges after successful processing
- **Queries**: No SaveChanges (read-only operations)
- **Failures**: Automatic rollback if any step fails
- **Multiple DbContexts**: All contexts with changes are saved atomically

## Performance Considerations

- **Change Tracking**: Only contexts with changes trigger SaveChanges
- **Query Optimization**: Queries bypass transaction overhead
- **Pagination**: Efficient database paging with X.PagedList
- **Result Patterns**: Minimal overhead with compile-time safety

## Thread Safety

- Handlers should be stateless for thread safety
- DbContext instances are scoped per request
- SlimMessageBus handles concurrency according to its configuration

## Contributing

See the main [CONTRIBUTING.md](../../../../CONTRIBUTING.md) for guidelines on how to contribute to this project.

## License

This project is licensed under the [MIT License](../../../../LICENSE).

## Related Packages

- [DKNet.EfCore.Extensions](../EfCore/DKNet.EfCore.Extensions) - EF Core functionality extensions
- [DKNet.EfCore.Events](../EfCore/DKNet.EfCore.Events) - Domain event handling and dispatching
- [DKNet.Fw.Extensions](../Core/DKNet.Fw.Extensions) - Framework-level extensions

---

Part of the [DKNet Framework](https://github.com/baoduy/DKNet) - A comprehensive .NET framework for building modern,
scalable applications.