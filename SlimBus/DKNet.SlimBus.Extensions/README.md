# DKNet.SlimBus.Extensions

## Overview

The **DKNet.SlimBus.Extensions** project is a .NET library designed to extend the functionality of SlimMessageBus with integrations for Entity Framework Core (EF Core). It simplifies message-based communication and automates database operations for applications using the SlimMessageBus framework.

---

## Features and Functionality

### Core Features
- **Fluent Definitions**: Provides reusable abstractions for request, query, and notification patterns.
    - `Requests`: Interfaces for handling commands (`INoResponse`, `IWitResponse`).
    - `Queries`: Support for paginated and standard query patterns (`IWitResponse`, `IWitPageResponse`).
    - `Notifications`: Notification handling using `IEventHandler`.

- **EF Core Integration**:
    - `EfAutoSavePostProcessor`: Automatically saves changes in EF Core contexts after a successful request or query.

- **Service Registration**:
    - `AddSlimBusForEfCore`: A setup method to register SlimMessageBus with EF Core in your dependency injection container.

### Behaviors
- **Automatic Persistence**: Ensures that database changes tracked by EF Core are saved automatically after handling requests.
- **Customizable Interceptors**: Implements `IRequestHandlerInterceptor` to enable middleware behavior for message handling.

---

## Requirements

- **Framework**: .NET 9.0
- **Dependencies**:
    - [FluentResults](https://github.com/altmann/FluentResults): Result handling.
    - [Microsoft.EntityFrameworkCore.Abstractions](https://learn.microsoft.com/en-us/ef/): EF Core support.
    - [SlimMessageBus](https://github.com/zarusz/SlimMessageBus): Lightweight message bus library.

---

## Installation

1. **Add the NuGet Package**:
   ```bash
   dotnet add package DKNet.SlimBus.Extensions
   ```

2. **Configure Dependency Injection**:
   In your application startup, add SlimMessageBus and EF Core integration:
   ```csharp
   services.AddSlimBusForEfCore(busConfig =>
   {
       // SlimMessageBus configuration here
   });
   ```

3. **Ensure EF Core Contexts are Registered**:
   Ensure all `DbContext` instances used in your application are registered in the dependency injection container.

---

## Project Structure

- **`Fluent.cs`**: Defines fluent abstractions for SlimMessageBus patterns (requests, queries, notifications).
- **`SlimBusEfCoreSetup.cs`**: Contains the setup method `AddSlimBusForEfCore` for DI registration.
- **`Behaviors/EfAutoSavePostProcessor.cs`**: Implements `IRequestHandlerInterceptor` to handle automatic database saving after processing.

---

## Design Highlights

- **Domain-Driven Design (DDD)**: Encapsulates request, query, and notification patterns in well-defined interfaces.
- **CQRS Support**: Enables separation of commands and queries with SlimMessageBus.
- **Extensibility**: Built to allow additional behaviors by leveraging SlimMessageBus interceptors.

---

## Usage

### Fluents Class Samples

#### Requests

Define a command request without a response:
```csharp
public class CreateOrderRequest : Fluents.Requests.INoResponse { }

public class CreateOrderHandler : Fluents.RequestHandlers.INoResponse<CreateOrderRequest>
{
    public Task<IResultBase> Handle(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        // Handle request logic
        return Task.FromResult(Result.Ok() as IResultBase);
    }
}
```

Define a request with a response:
```csharp
public class GetOrderRequest : Fluents.Requests.IWitResponse<OrderDto>
{
    public int OrderId { get; set; }
}

public class GetOrderHandler : Fluents.RequestHandlers.IWitResponse<GetOrderRequest, OrderDto>
{
    public Task<IResult<OrderDto>> Handle(GetOrderRequest request, CancellationToken cancellationToken)
    {
        // Handle logic to get order details
        var order = new OrderDto { OrderId = request.OrderId, Status = "Completed" };
        return Task.FromResult(Result.Ok(order));
    }
}
```

#### Queries

Define a paginated query:
```csharp
public class ListOrdersQuery : Fluents.Queries.IWitPageResponse<OrderDto>
{
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}

public class ListOrdersHandler : Fluents.QueryHandlers.IWitPageResponse<ListOrdersQuery, OrderDto>
{
    public Task<IPageable<OrderDto>> Handle(ListOrdersQuery query, CancellationToken cancellationToken)
    {
        // Handle logic to get paginated results
        var orders = new Pageable<OrderDto>(
            new List<OrderDto> { new() { OrderId = 1, Status = "Pending" } },
            totalCount: 100
        );
        return Task.FromResult<IPageable<OrderDto>>(orders);
    }
}
```

#### Notifications

Define and handle a notification:
```csharp
public class OrderCreatedEvent { public int OrderId { get; set; } }

public class OrderCreatedEventHandler : Fluents.Notifications.IEventHandler<OrderCreatedEvent>
{
    public Task Handle(OrderCreatedEvent notification, CancellationToken cancellationToken)
    {
        // Handle notification logic
        Console.WriteLine($"Order {notification.OrderId} created.");
        return Task.CompletedTask;
    }
}
```

---

## Issues and Support

If you encounter any issues or have feature requests, please open a ticket on the [project repository](https://thewixo@dev.azure.com/thewixo/WIXO/_git/WIXO.FW). Provide the following:
- A description of the issue or request.
- Steps to reproduce (if applicable).
- Environment details (e.g., .NET version).

---

## License

This project is licensed under the [MIT License](https://opensource.org/licenses/MIT).

---

## Acknowledgments

Special thanks to contributors and the developers of SlimMessageBus and FluentResults for enabling robust messaging and result handling.



