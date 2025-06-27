# DKNet.SlimBus.Extensions

**SlimMessageBus extensions for Entity Framework Core that provide a lightweight, high-performance alternative to MediatR, implementing CQRS patterns with automatic persistence and domain event integration for Domain-Driven Design applications.**

## What is this project?

DKNet.SlimBus.Extensions provides a comprehensive integration between SlimMessageBus and Entity Framework Core, offering fluent interfaces for Commands, Queries, and Events while maintaining clean separation of concerns. It includes automatic change tracking, result handling, and domain event dispatching.

### Key Features

- **Fluent CQRS Interfaces**: Type-safe command, query, and event contracts
- **Auto-Save Behavior**: Automatic EF Core change persistence after successful operations
- **Result Handling**: Integrated FluentResults for consistent error handling
- **Paged Queries**: Built-in pagination support for large result sets
- **Domain Event Integration**: Seamless domain event publishing
- **Pipeline Behaviors**: Extensible request/response pipeline
- **Multiple Transports**: Support for in-memory, Azure Service Bus, and other providers
- **Performance Optimized**: Lightweight alternative to MediatR with better performance

## How it contributes to DDD and Onion Architecture

### CQRS Implementation in Onion Architecture

DKNet.SlimBus.Extensions implements the **Application Layer** patterns for CQRS while maintaining strict dependency inversion:

```
┌─────────────────────────────────────────────────────────────────┐
│                    🌐 Presentation Layer                        │
│                   (Controllers, API Endpoints)                  │
│                                                                 │
│  Sends: Commands, Queries via IMessageBus.Send()               │
│  No knowledge of handlers or EF Core                           │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                   🎯 Application Layer                          │
│                 (CQRS Handlers & Behaviors)                    │
│                                                                 │
│  📝 Command Handlers: IHandler<CreateOrder, Result<Guid>>      │
│  📊 Query Handlers: IQueryHandler<GetOrders, OrderDto[]>       │
│  🎭 Event Handlers: IEventHandler<OrderCreated>                │
│  ⚡ EfAutoSavePostProcessor: Auto DbContext.SaveChanges()      │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                    💼 Domain Layer                             │
│           (Entities, Aggregates, Domain Services)              │
│                                                                 │
│  🏗️ Pure business logic, no messaging dependencies            │
│  📋 Domain events raised by aggregates                         │
│  🎯 Commands and queries defined in domain terms               │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                 🗄️ Infrastructure Layer                        │
│                (Message Bus, Data Persistence)                 │
│                                                                 │
│  🚌 SlimMessageBus configuration and routing                   │
│  🗄️ EF Core DbContext and repositories                        │
│  📡 External message transport providers                       │
└─────────────────────────────────────────────────────────────────┘
```

### DDD Benefits

1. **Command/Query Separation**: Clear distinction between state-changing operations and read operations
2. **Aggregate Coordination**: Commands operate on single aggregates, maintaining consistency boundaries
3. **Domain Event Publishing**: Automatic event dispatching enables loose coupling between bounded contexts
4. **Ubiquitous Language**: Messages and handlers use domain terminology
5. **Business Logic Isolation**: Domain logic remains pure, free from infrastructure concerns

### Onion Architecture Benefits

1. **Dependency Inversion**: Application layer defines message contracts, infrastructure implements routing
2. **Technology Independence**: Domain and application layers unaware of SlimMessageBus specifics
3. **Testability**: Easy to mock message bus and test handlers in isolation
4. **Cross-Cutting Concerns**: Behaviors handle infrastructure concerns (persistence, logging, validation)
5. **Scalability**: Separate read and write models enable independent scaling

## How to use it

### Installation

```bash
dotnet add package DKNet.SlimBus.Extensions
```

### Basic Configuration

```csharp
using Microsoft.Extensions.DependencyInjection;
using DKNet.SlimBus.Extensions;

public void ConfigureServices(IServiceCollection services)
{
    // Add EF Core DbContext
    services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString));
    
    // Add SlimMessageBus with EF Core integration
    services.AddSlimBusForEfCore(mbb =>
    {
        mbb.AddJsonSerializer();
        
        // In-memory bus for internal operations
        mbb.AddChildBus("Memory", mb => 
            mb.WithProviderMemory()
              .AutoDeclareFrom(typeof(Program).Assembly));
        
        // Optional: External message bus for integration events
        mbb.AddChildBus("External", mb =>
            mb.WithProviderServiceBus(cfg => cfg.ConnectionString = serviceBusConnectionString)
              .AutoDeclareFrom(typeof(Program).Assembly, consumerTypeFilter: t => t.Name.EndsWith("IntegrationHandler")));
    });
    
    // Register handlers
    services.AddScoped<CreateOrderHandler>();
    services.AddScoped<GetOrdersHandler>();
    services.AddScoped<OrderCreatedEventHandler>();
}
```

### Command Usage Examples

#### 1. Simple Command (No Response)

```csharp
using DKNet.SlimBus.Extensions;
using FluentResults;

// Command definition
public record DeactivateCustomerCommand(Guid CustomerId) : Fluents.Requests.INoResponse;

// Command handler
public class DeactivateCustomerHandler : Fluents.Requests.IHandler<DeactivateCustomerCommand>
{
    private readonly ICustomerRepository _customerRepository;
    
    public DeactivateCustomerHandler(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }
    
    public async Task<IResultBase> Handle(DeactivateCustomerCommand request)
    {
        var customer = await _customerRepository.FindAsync(request.CustomerId);
        if (customer == null)
            return Result.Fail("Customer not found");
        
        if (!customer.CanBeDeactivated())
            return Result.Fail("Customer cannot be deactivated");
        
        customer.Deactivate(); // Domain operation
        _customerRepository.Update(customer);
        
        // Auto-save will persist changes automatically
        return Result.Ok();
    }
}

// Usage in controller
[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly IMessageBus _messageBus;
    
    public CustomersController(IMessageBus messageBus)
    {
        _messageBus = messageBus;
    }
    
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeactivateCustomer(Guid id)
    {
        var command = new DeactivateCustomerCommand(id);
        var result = await _messageBus.Send(command);
        
        return result.IsSuccess ? Ok() : BadRequest(result.Errors);
    }
}
```

#### 2. Command with Response

```csharp
using DKNet.SlimBus.Extensions;
using FluentResults;

// Command definition
public record CreateOrderCommand(
    Guid CustomerId, 
    List<CreateOrderItem> Items, 
    string ShippingAddress) : Fluents.Requests.IWitResponse<Result<Guid>>;

public record CreateOrderItem(Guid ProductId, int Quantity, decimal UnitPrice);

// Command handler
public class CreateOrderHandler : Fluents.Requests.IHandler<CreateOrderCommand, Result<Guid>>
{
    private readonly IOrderRepository _orderRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IProductRepository _productRepository;
    
    public CreateOrderHandler(
        IOrderRepository orderRepository,
        ICustomerRepository customerRepository,
        IProductRepository productRepository)
    {
        _orderRepository = orderRepository;
        _customerRepository = customerRepository;
        _productRepository = productRepository;
    }
    
    public async Task<IResult<Result<Guid>>> Handle(CreateOrderCommand request)
    {
        // Validate customer
        var customer = await _customerRepository.FindAsync(request.CustomerId);
        if (customer == null)
            return Result.Ok(Result.Fail<Guid>("Customer not found"));
        
        if (!customer.CanPlaceOrders())
            return Result.Ok(Result.Fail<Guid>("Customer cannot place orders"));
        
        // Validate products
        var productIds = request.Items.Select(i => i.ProductId).ToList();
        var products = await _productRepository.GetByIdsAsync(productIds);
        
        if (products.Count != productIds.Count)
            return Result.Ok(Result.Fail<Guid>("Some products not found"));
        
        // Create domain entity
        var order = Order.Create(
            customerId: request.CustomerId,
            shippingAddress: request.ShippingAddress);
        
        foreach (var item in request.Items)
        {
            var product = products.First(p => p.Id == item.ProductId);
            order.AddItem(product, item.Quantity, item.UnitPrice);
        }
        
        // Validate order
        var validationResult = order.Validate();
        if (validationResult.IsFailed)
            return Result.Ok(Result.Fail<Guid>(validationResult.Errors.Select(e => e.Message)));
        
        // Save order
        _orderRepository.Add(order);
        
        // Auto-save will persist changes and publish domain events
        return Result.Ok(Result.Ok(order.Id));
    }
}

// Usage in controller
[HttpPost]
public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
{
    var command = new CreateOrderCommand(
        request.CustomerId,
        request.Items.Select(i => new CreateOrderItem(i.ProductId, i.Quantity, i.UnitPrice)).ToList(),
        request.ShippingAddress);
    
    var result = await _messageBus.Send(command);
    
    if (result.IsSuccess)
        return CreatedAtAction(nameof(GetOrder), new { id = result.Value }, result.Value);
    
    return BadRequest(result.Errors);
}
```

### Query Usage Examples

#### 1. Simple Query

```csharp
using DKNet.SlimBus.Extensions;

// Query definition
public record GetOrderQuery(Guid OrderId) : Fluents.Queries.IWitResponse<OrderDto>;

// Query handler
public class GetOrderHandler : Fluents.Queries.IHandler<GetOrderQuery, OrderDto>
{
    private readonly IReadRepository<Order> _orderRepository;
    
    public GetOrderHandler(IReadRepository<Order> orderRepository)
    {
        _orderRepository = orderRepository;
    }
    
    public async Task<OrderDto?> Handle(GetOrderQuery request)
    {
        return await _orderRepository
            .GetProjection<OrderDto>()
            .FirstOrDefaultAsync(o => o.Id == request.OrderId);
    }
}

// Usage in controller
[HttpGet("{id}")]
public async Task<IActionResult> GetOrder(Guid id)
{
    var query = new GetOrderQuery(id);
    var order = await _messageBus.Send(query);
    
    return order == null ? NotFound() : Ok(order);
}
```

#### 2. Paged Query

```csharp
using DKNet.SlimBus.Extensions;
using X.PagedList;

// Query definition
public record GetCustomerOrdersQuery(
    Guid CustomerId, 
    int Page = 1, 
    int PageSize = 20) : Fluents.Queries.IWitPageResponse<OrderSummaryDto>;

// Query handler
public class GetCustomerOrdersHandler : Fluents.Queries.IPageHandler<GetCustomerOrdersQuery, OrderSummaryDto>
{
    private readonly IReadRepository<Order> _orderRepository;
    
    public GetCustomerOrdersHandler(IReadRepository<Order> orderRepository)
    {
        _orderRepository = orderRepository;
    }
    
    public async Task<IPagedList<OrderSummaryDto>> Handle(GetCustomerOrdersQuery request)
    {
        var query = _orderRepository
            .GetProjection<OrderSummaryDto>()
            .Where(o => o.CustomerId == request.CustomerId)
            .OrderByDescending(o => o.CreatedOn);
        
        return await query.ToPagedListAsync(request.Page, request.PageSize);
    }
}

// Usage in controller
[HttpGet("customers/{customerId}/orders")]
public async Task<IActionResult> GetCustomerOrders(
    Guid customerId, 
    [FromQuery] int page = 1, 
    [FromQuery] int pageSize = 20)
{
    var query = new GetCustomerOrdersQuery(customerId, page, pageSize);
    var orders = await _messageBus.Send(query);
    
    return Ok(new
    {
        Data = orders,
        Page = orders.PageNumber,
        PageSize = orders.PageSize,
        TotalCount = orders.TotalItemCount,
        TotalPages = orders.PageCount
    });
}
```

### Event Handling Examples

#### 1. Domain Event Handler

```csharp
using SlimMessageBus;

// Domain event (defined in domain layer)
public record OrderCreatedEvent(Guid OrderId, Guid CustomerId, decimal TotalAmount, DateTime CreatedAt);

// Event handler (application layer)
public class OrderCreatedEventHandler : IEventHandler<OrderCreatedEvent>
{
    private readonly IEmailService _emailService;
    private readonly ICustomerRepository _customerRepository;
    private readonly ILogger<OrderCreatedEventHandler> _logger;
    
    public OrderCreatedEventHandler(
        IEmailService emailService,
        ICustomerRepository customerRepository,
        ILogger<OrderCreatedEventHandler> logger)
    {
        _emailService = emailService;
        _customerRepository = customerRepository;
        _logger = logger;
    }
    
    public async Task Handle(OrderCreatedEvent evt)
    {
        try
        {
            // Get customer details
            var customer = await _customerRepository.FindAsync(evt.CustomerId);
            if (customer == null)
            {
                _logger.LogWarning("Customer {CustomerId} not found for order {OrderId}", 
                    evt.CustomerId, evt.OrderId);
                return;
            }
            
            // Send confirmation email
            await _emailService.SendOrderConfirmationAsync(
                customer.Email,
                customer.FullName,
                evt.OrderId,
                evt.TotalAmount);
            
            _logger.LogInformation("Order confirmation sent for order {OrderId}", evt.OrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process OrderCreated event for order {OrderId}", evt.OrderId);
            throw; // Will be handled by message bus error handling
        }
    }
}
```

#### 2. Integration Event Handler

```csharp
// Integration event for external systems
public record CustomerOrderPlacedIntegrationEvent(
    Guid CustomerId, 
    Guid OrderId, 
    decimal TotalAmount, 
    string CustomerEmail,
    DateTime OrderDate);

// Integration event handler
public class CustomerOrderPlacedIntegrationEventHandler : IEventHandler<CustomerOrderPlacedIntegrationEvent>
{
    private readonly IExternalCrmService _crmService;
    private readonly IInventoryService _inventoryService;
    
    public async Task Handle(CustomerOrderPlacedIntegrationEvent evt)
    {
        // Update external CRM system
        await _crmService.UpdateCustomerOrderHistoryAsync(evt.CustomerId, evt.OrderId, evt.TotalAmount);
        
        // Update inventory system
        await _inventoryService.ReserveInventoryAsync(evt.OrderId);
    }
}

// Publishing integration events from domain event handler
public class OrderCreatedToIntegrationEventHandler : IEventHandler<OrderCreatedEvent>
{
    private readonly IMessageBus _messageBus;
    private readonly ICustomerRepository _customerRepository;
    
    public async Task Handle(OrderCreatedEvent evt)
    {
        var customer = await _customerRepository.FindAsync(evt.CustomerId);
        if (customer == null) return;
        
        var integrationEvent = new CustomerOrderPlacedIntegrationEvent(
            evt.CustomerId,
            evt.OrderId,
            evt.TotalAmount,
            customer.Email,
            evt.CreatedAt);
        
        // Publish to external bus
        await _messageBus.Publish(integrationEvent, "External");
    }
}
```

### Advanced Configuration Examples

#### 1. Multi-Bus Configuration

```csharp
services.AddSlimBusForEfCore(mbb =>
{
    mbb.AddJsonSerializer();
    
    // Internal memory bus for domain events and commands
    mbb.AddChildBus("Memory", mb =>
        mb.WithProviderMemory()
          .AutoDeclareFrom(typeof(Program).Assembly, 
              consumerTypeFilter: t => !t.Name.Contains("Integration")));
    
    // Azure Service Bus for integration events
    mbb.AddChildBus("ServiceBus", mb =>
        mb.WithProviderServiceBus(cfg => cfg.ConnectionString = serviceBusConnectionString)
          .AutoDeclareFrom(typeof(Program).Assembly,
              consumerTypeFilter: t => t.Name.Contains("Integration")));
    
    // Redis for high-performance scenarios
    mbb.AddChildBus("Redis", mb =>
        mb.WithProviderRedis(cfg => cfg.ConnectionString = redisConnectionString)
          .AutoDeclareFrom(typeof(Program).Assembly,
              consumerTypeFilter: t => t.Name.Contains("Cache")));
});
```

#### 2. Custom Behaviors

```csharp
// Custom validation behavior
public class ValidationBehavior<TRequest, TResponse> : IRequestHandlerInterceptor<TRequest, TResponse>
{
    private readonly IValidator<TRequest> _validator;
    
    public ValidationBehavior(IValidator<TRequest> validator)
    {
        _validator = validator;
    }
    
    public async Task<TResponse> OnHandle(TRequest request, Func<Task<TResponse>> next, IConsumerContext context)
    {
        var validationResult = await _validator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            throw new ValidationException(errors);
        }
        
        return await next();
    }
    
    public int Order => 1; // Run before auto-save behavior
}

// Registration
services.AddScoped(typeof(IRequestHandlerInterceptor<,>), typeof(ValidationBehavior<,>));
```

## Best Practices

### 1. Command Design
- Keep commands focused on single business operations
- Include all necessary data in the command (no database lookups in validation)
- Use value objects for complex command parameters
- Return meaningful business identifiers from commands

### 2. Query Optimization
- Use projections to reduce data transfer
- Implement paging for large result sets
- Cache frequently accessed read models
- Consider read replicas for high-volume queries

### 3. Event Handling
- Keep event handlers idempotent
- Handle failures gracefully with proper logging
- Use integration events for cross-bounded context communication
- Avoid long-running operations in event handlers

### 4. Error Handling
- Use FluentResults for consistent error handling
- Log errors at appropriate levels
- Provide meaningful error messages for business rule violations
- Handle infrastructure failures with retry policies

### 5. Testing
- Mock IMessageBus for unit testing controllers
- Test handlers in isolation with proper setup
- Use in-memory message bus for integration tests
- Verify event publishing in handler tests

## Integration with Other DKNet Components

DKNet.SlimBus.Extensions integrates seamlessly with other DKNet components:

- **DKNet.EfCore.Events**: Automatic domain event dispatching
- **DKNet.EfCore.Repos**: Repository pattern integration with auto-save
- **DKNet.EfCore.Abstractions**: Entity and event interfaces
- **DKNet.Fw.Extensions**: Utility methods and extensions

---

> 💡 **Performance Tip**: SlimMessageBus is significantly faster than MediatR with lower memory allocation. The auto-save behavior eliminates the need for explicit SaveChanges calls while maintaining transaction boundaries.