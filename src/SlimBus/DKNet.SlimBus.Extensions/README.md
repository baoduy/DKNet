# DKNet.SlimBus.Extensions

[![NuGet](https://img.shields.io/nuget/v/DKNet.SlimBus.Extensions)](https://www.nuget.org/packages/DKNet.SlimBus.Extensions/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DKNet.SlimBus.Extensions)](https://www.nuget.org/packages/DKNet.SlimBus.Extensions/)

CQRS and messaging glue on top of [SlimMessageBus](https://github.com/zarusz/SlimMessageBus), wired for EF Core —
a lighter, result-based alternative to MediatR. Command/query handlers get fluent contracts, successful writes are
auto-saved to your `DbContext`, and EF Core domain events are forwarded onto the bus.

## Install

```bash
dotnet add package DKNet.SlimBus.Extensions
```

## Features

- **Command contracts** — `Fluents.Requests.INoResponse` / `IWitResponse<TResponse>`, with matching `IHandler<>`
  interfaces; auto-saves the `DbContext` after a successful handler, skips it on failure or null.
- **Query contracts** — `Fluents.Queries.IWitResponse<TResponse>` and `IWitPageResponse<TResponse>` (paged, via
  `X.PagedList.EF`); queries never trigger auto-save.
- **Event consumers** — `Fluents.EventsConsumers.IHandler<TEvent>` for reacting to messages/domain events.
- **Domain event publishing** — `SlimBusEventPublisher` forwards events raised by `DKNet.EfCore.Events` onto the
  bus, copying event `AdditionalData` into message headers.
- **Pluggable interceptors** — auto-save is a standard SlimMessageBus `IRequestHandlerInterceptor<,>`; add your
  own for validation, logging, etc.
- **Transport-agnostic** — brings no provider; plug in `SlimMessageBus.Host.Memory`, Azure Service Bus, or any
  other SlimMessageBus provider yourself.

## Quick start

```csharp
using DKNet.SlimBus.Extensions;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.Serialization.SystemTextJson;

services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString));
services.AddSlimBusEfCoreInterceptor<AppDbContext>();

services.AddSlimMessageBus(mbb => mbb
    .AddJsonSerializer()
    .AddServicesFromAssembly(typeof(Program).Assembly)
    .AddChildBus("Memory", bus => bus
        .WithProviderMemory()
        .AutoDeclareFrom(typeof(Program).Assembly)));

// Command
public record CreateProduct(string Name, decimal Price) : Fluents.Requests.IWitResponse<Guid>;

internal sealed class CreateProductHandler(AppDbContext db)
    : Fluents.Requests.IHandler<CreateProduct, Guid>
{
    public async Task<IResult<Guid>> OnHandle(CreateProduct request, CancellationToken cancellationToken)
    {
        var product = new Product(request.Name, request.Price);
        await db.Products.AddAsync(product, cancellationToken);
        return Result.Ok(product.Id); // DbContext is saved automatically after this returns
    }
}
```

## Documentation

Full feature reference, configuration, and composition with `DKNet.EfCore.Events` / `DKNet.EfCore.Repos`:
https://github.com/baoduy/DKNet/blob/dev/docs/Messaging/DKNet.SlimBus.Extensions.md
