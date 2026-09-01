# DKNet.SlimBus.Extensions

[![NuGet](https://img.shields.io/nuget/v/DKNet.SlimBus.Extensions)](https://www.nuget.org/packages/DKNet.SlimBus.Extensions/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DKNet.SlimBus.Extensions)](https://www.nuget.org/packages/DKNet.SlimBus.Extensions/)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](https://github.com/baoduy/DKNet/blob/main/LICENSE)

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

## Configuration — registration surface

There is no options type and no `IConfiguration` section. What you vary is which of the two registrations you
call and with which `DbContext`; everything else (`AddSlimMessageBus`, serializers, providers, handler discovery)
is SlimMessageBus's own API.

| Method | Constraint | Registers | Lifetime | Repeat call |
|---|---|---|---|---|
| `AddSlimBusEfCoreInterceptor<TDbContext>()` | `TDbContext : DbContext` | `IRequestHandlerInterceptor<,>` → the internal auto-save interceptor | Scoped | Adds `TDbContext` to the save registry; the interceptor is registered only once |
| `AddSlimBusEventPublisher<TDbContext>()` | `TDbContext : DbContext` | `SlimBusEventPublisher` as `IEventPublisher` for `TDbContext`, via `DKNet.EfCore.Events` | Delegated to `AddEventPublisher` | Delegated to `AddEventPublisher` |

Auto-save behaviour is fixed, not switchable:

| Concern | Value |
|---|---|
| Interceptor order | `int.MaxValue` — runs outermost, after your own interceptors |
| Saved for | `Fluents.Requests.INoResponse` and `Fluents.Requests.IWitResponse<T>` only |
| Skipped when | The response is `null`, or an `IResultBase` with `IsSuccess == false` |
| Saved contexts | Every registered type whose `ChangeTracker.HasChanges()` |
| Save call | `AddNewEntitiesFromNavigations`, then `SaveChangesWithConcurrencyHandlingAsync` |
| Exception handler | `IEfCoreExceptionHandler` keyed by the `DbContext`'s `FullName`, falling back to the unkeyed registration |

## Public extension points

| Type | Accessibility | What you do with it |
|---|---|---|
| `Fluents.Requests.INoResponse` / `IWitResponse<T>` | public interface | Mark a message as a write — auto-save keys off exactly this. |
| `Fluents.Requests.IHandler<TRequest>` / `IHandler<TRequest, TResponse>` | public interface | Implement the handler for a write. |
| `Fluents.Requests.IWithKey<TKey>` | public interface | Carry a route-bound `Id`. |
| `Fluents.Queries.IWitResponse<T>` / `IWitPageResponse<T>` and their handlers | public interface | Mark and handle a read. Never auto-saved. |
| `Fluents.EventsConsumers.IHandler<TEvent>` | public interface | Consume a published event. |
| `SlimBusEventPublisher` | public class, both `PublishAsync` overloads `virtual` | Subclass to add headers or logging, then register the subclass. |
| `NotFoundError` | public sealed class : `FluentResults.Error` | Return it from `Result.Fail` so the API layer maps one type to `404`. |
| `ILazyMap<T>`, `LazyMapExtensions.LazyMap<T>` / `ResultOf<T>` | public interface / public static class | Defer a Mapster mapping until the value is read. |
| `RequestBase` | public record, `[Obsolete]` | Nothing — never populated by this package. |

The auto-save interceptor, its `DbContext` registry, and the `LazyMap`/`LazyResult` implementations are all
`internal`: you opt in through the two registration methods, not by implementing those types.

## Documentation

Full feature reference, diagrams, and composition with `DKNet.EfCore.Events` / `DKNet.EfCore.Specifications`:
https://github.com/baoduy/DKNet/blob/main/docs/Messaging/DKNet.SlimBus.Extensions.md
