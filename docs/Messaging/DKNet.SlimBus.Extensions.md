# DKNet.SlimBus.Extensions

CQRS contracts on top of [SlimMessageBus](https://github.com/zarusz/SlimMessageBus) with no EF Core dependency —
fluent command/query/event interfaces, `FluentResults`-based handlers, lazy Mapster mapping, and a one-call in-memory
setup that replaces [MediatR](https://github.com/zarusz/SlimMessageBus/blob/master/docs/UseCases/ReplaceMediatR.md).

Using EF Core? Add [DKNet.SlimBus.Extensions.EfCore](./DKNet.SlimBus.Extensions.EfCore.md) on top for automatic
`SaveChanges` after a successful write and domain events forwarded onto the bus.

## ✨ Why use it?

- **A MediatR replacement in one call.** `AddSlimBusForMediator(assembly)` wires SlimMessageBus's memory provider and
  discovers your handlers — in-process `Send`/`Publish` with an interceptor pipeline, and a path to a real transport
  later without changing handlers.
- **No EF Core in your dependency graph.** The package references no `Microsoft.EntityFrameworkCore*` assembly,
  directly or transitively, so it fits a domain/application layer or a service that has no database.
- **Expected business failures are results, not exceptions.** Commands return `FluentResults` types, so "product not
  found" is a value the caller inspects rather than a thrown exception to catch.
- **Request shape says what a message is.** Implementing `Fluents.Requests.*` marks a write, `Fluents.Queries.*` marks a
  read — no attributes, no naming conventions. The `.EfCore` add-on keys auto-save off exactly that split.

If you are not using SlimMessageBus, this package buys you little.

## 🚀 Quick Start

```bash
dotnet add package DKNet.SlimBus.Extensions
```

```csharp
using DKNet.SlimBus.Extensions;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using SlimMessageBus;

// Memory bus + handler discovery — the whole registration.
builder.Services.AddSlimBusForMediator(typeof(Program).Assembly);

app.MapGet("/ping", async (IMessageBus bus) =>
{
    var result = await bus.Send(new Ping("hello"));
    return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest();
});

public record Ping(string Text) : Fluents.Requests.IWitResponse<string>;

internal sealed class PingHandler : Fluents.Requests.IHandler<Ping, string>
{
    public Task<IResult<string>> OnHandle(Ping request, CancellationToken cancellationToken) =>
        Task.FromResult<IResult<string>>(Result.Ok($"pong: {request.Text}"));
}
```

`AddSlimBusForMediator(params Assembly[] handlerAssemblies)` is equivalent to:

```csharp
services.AddSlimMessageBus(mbb =>
{
    mbb.WithProviderMemory();
    foreach (var assembly in handlerAssemblies)
    {
        mbb.AddServicesFromAssembly(assembly);   // register handlers in DI
        mbb.AutoDeclareFrom(assembly);           // declare them on the memory bus
    }
});
```

No serializer is registered: the memory provider hands the message object to the handler by reference, as MediatR
does. Passing no assemblies throws `ArgumentException`.

### Coming from MediatR

| MediatR | DKNet.SlimBus.Extensions |
|---|---|
| `services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>())` | `services.AddSlimBusForMediator(typeof(Program).Assembly)` |
| `IMediator.Send(request)` | `IMessageBus.Send(request)` |
| `IMediator.Publish(notification)` | `IMessageBus.Publish(message)` |
| `IRequest<TResponse>` | `Fluents.Requests.IWitResponse<T>` (write) / `Fluents.Queries.IWitResponse<T>` (read) |
| `IRequest` | `Fluents.Requests.INoResponse` |
| `IRequestHandler<TRequest, TResponse>.Handle(...)` | `Fluents.*.IHandler<...>.OnHandle(...)` |
| `INotificationHandler<T>` | `Fluents.EventsConsumers.IHandler<T>` |
| `IPipelineBehavior<TRequest, TResponse>` | SlimMessageBus `IRequestHandlerInterceptor<TRequest, TResponse>` |

### Need a real transport?

`AddSlimBusForMediator` is memory-only by design. For Azure Service Bus, Kafka, or a hybrid bus, skip it and call
SlimMessageBus's own `AddSlimMessageBus(...)` with the provider you need — the `Fluents` contracts and handlers stay the
same.

## 🧩 Features

### Commands without a response — `Fluents.Requests.INoResponse`

For writes that only signal success or failure.

```csharp
using DKNet.SlimBus.Extensions;
using FluentResults;

public record DeactivateProduct(Guid ProductId) : Fluents.Requests.INoResponse;

internal sealed class DeactivateProductHandler(IProductStore store)
    : Fluents.Requests.IHandler<DeactivateProduct>
{
    public async Task<IResultBase> OnHandle(DeactivateProduct request, CancellationToken cancellationToken)
    {
        var product = await store.FindAsync(request.ProductId, cancellationToken);
        if (product is null) return Result.Fail(new NotFoundError("Product not found"));

        product.Deactivate();
        return Result.Ok();
    }
}
```

`Fluents.Requests.IHandler<TRequest>` is `IRequestHandler<TRequest, IResultBase>` constrained to
`TRequest : INoResponse`, so `OnHandle` returns `Task<IResultBase>`. With
[DKNet.SlimBus.Extensions.EfCore](./DKNet.SlimBus.Extensions.EfCore.md) registered, a successful write saves the
`DbContext` for you — the handler never calls `SaveChangesAsync`.

### Commands with a response — `Fluents.Requests.IWitResponse<TResponse>`

For writes that hand data back, such as a new identifier.

```csharp
public record CreateProduct(string Name, decimal Price) : Fluents.Requests.IWitResponse<Guid>;

internal sealed class CreateProductHandler(IProductStore store)
    : Fluents.Requests.IHandler<CreateProduct, Guid>
{
    public async Task<IResult<Guid>> OnHandle(CreateProduct request, CancellationToken cancellationToken)
    {
        var product = new Product(request.Name, request.Price);
        await store.AddAsync(product, cancellationToken);
        return Result.Ok(product.Id);
    }
}
```

Callers get an `IResult<Guid>` from `IMessageBus.Send(...)`.

`Fluents.Requests.IWithKey<TKey>` is a small companion interface (`TKey Id { get; set; }`) for requests addressed by
identifier — [DKNet.SlimBus.Generators](./DKNet.SlimBus.Generators.md) puts it on the update and action requests it
emits so shared code can read the key generically.

### Single-item queries — `Fluents.Queries.IWitResponse<TResponse>`

```csharp
public record GetProduct(Guid Id) : Fluents.Queries.IWitResponse<ProductDto>;

internal sealed class GetProductHandler(IProductStore store)
    : Fluents.Queries.IHandler<GetProduct, ProductDto>
{
    public async Task<ProductDto?> OnHandle(GetProduct request, CancellationToken cancellationToken)
    {
        var product = await store.FindAsync(request.Id, cancellationToken);
        return product is null ? null : new ProductDto(product.Id, product.Name);
    }
}
```

The return type is `TResponse?`, not a `FluentResults` wrapper — a query answers "found / not found", it does not carry
a business-failure result. Queries are never auto-saved by the `.EfCore` add-on, whatever the handler leaves in the
change tracker.

### Paged queries — `Fluents.Queries.IWitPageResponse<TResponse>`

The contract returns `X.PagedList`'s `IPagedList<T>`. This package depends on the EF-free `X.PagedList` only; to page an
EF Core `IQueryable` server-side, add `X.PagedList.EF` (it already comes with
[DKNet.EfCore.Specifications](../EfCore/DKNet.EfCore.Specifications.md)) for `ToPagedListAsync`.

```csharp
using X.PagedList;
using X.PagedList.EF;

public record GetProductsPage(int PageIndex, int PageSize) : Fluents.Queries.IWitPageResponse<ProductDto>;

internal sealed class GetProductsPageHandler(AppDbContext db)
    : Fluents.Queries.IPageHandler<GetProductsPage, ProductDto>
{
    public Task<IPagedList<ProductDto>> OnHandle(GetProductsPage request, CancellationToken cancellationToken) =>
        db.Products.AsNoTracking()
            .Select(p => new ProductDto(p.Id, p.Name))
            .ToPagedListAsync(request.PageIndex, request.PageSize, null, cancellationToken);
}
```

Without EF Core, build the page from any `IEnumerable<T>` / `IQueryable<T>` with `X.PagedList`'s `ToPagedList(...)`.

### Event consumers — `Fluents.EventsConsumers.IHandler<TEvent>`

A thin alias over SlimMessageBus's `IConsumer<TEvent>` — the `INotificationHandler<T>` equivalent:

```csharp
public class ProductCreatedHandler : Fluents.EventsConsumers.IHandler<ProductCreatedEvent>
{
    public Task OnHandle(ProductCreatedEvent message, CancellationToken cancellationToken)
    {
        // react to the event — e.g. send a notification
        return Task.CompletedTask;
    }
}
```

Publish with `IMessageBus.Publish(new ProductCreatedEvent(...))`. To publish domain events raised by EF Core aggregates
automatically after the save, see [DKNet.SlimBus.Extensions.EfCore](./DKNet.SlimBus.Extensions.EfCore.md).

### Your own interceptors

SlimMessageBus's `IRequestHandlerInterceptor<TRequest, TResponse>` is the `IPipelineBehavior` equivalent — add
validation, logging, or authorization as an open generic:

```csharp
using SlimMessageBus;
using SlimMessageBus.Host.Interceptor;

public class LoggingInterceptor<TRequest, TResponse>(ILogger<LoggingInterceptor<TRequest, TResponse>> logger)
    : IRequestHandlerInterceptor<TRequest, TResponse>
{
    public async Task<TResponse> OnHandle(TRequest request, Func<Task<TResponse>> next, IConsumerContext context)
    {
        logger.LogInformation("Handling {RequestType}", typeof(TRequest).Name);
        return await next();
    }
}

services.AddScoped(typeof(IRequestHandlerInterceptor<,>), typeof(LoggingInterceptor<,>));
```

Implement `IInterceptorWithOrder` to control where yours runs; the `.EfCore` auto-save interceptor uses
`int.MaxValue`, so it runs after yours.

### Lazy mapping — `ILazyMap<T>` and `IMapper.ResultOf<T>`

Two Mapster-backed helpers for handlers that return a DTO, so the mapping cost is paid only if something reads the
value:

```csharp
using DKNet.SlimBus.Extensions.LazyMapper;

// IResult<ProductDto> whose Value is mapped on first access
public async Task<IResult<ProductDto>> OnHandle(CreateProduct request, CancellationToken cancellationToken)
{
    var product = new Product(request.Name, request.Price);
    await store.AddAsync(product, cancellationToken);
    return mapper.ResultOf<ProductDto>(product);
}
```

`mapper.LazyMap<T>(value)` gives the same laziness without the result wrapper: `Value` throws
`InvalidOperationException` when the source was `null`, `ValueOrDefault` returns `default` instead. When the source is
already a `T`, the same instance is returned rather than mapped. Both require an `IMapper` (Mapster) in the container.

`NotFoundError` is a `FluentResults.Error` subclass for the "the thing you addressed doesn't exist" case — the shape the
generated handlers in [DKNet.SlimBus.Generators](./DKNet.SlimBus.Generators.md) return, and worth matching in
hand-written handlers so an API layer can map one error type to `404`:

```csharp
return Result.Fail<ProductDto>(new NotFoundError($"Product '{request.Id}' was not found."));
```

### Carrying the acting user

This package supplies no base record and no acting-user property. Declare the property on the request itself and mark
it with an `IContextualSource` attribute — e.g. `[FromClaim(ClaimTypes.Name)]` from
[DKNet.AspCore.Extensions](../DKNet.AspCore.Extensions/index.md) — then register
`AddContextualRequestPopulation()` so the value is stamped before validation and before the handler runs.

## ⚙️ Configuration reference

There is no options type and no `IConfiguration` section. The registration surface is one method.

### Registration surface (`SlimBusSetup`)

| Method | Registers | Repeat call |
|---|---|---|
| `AddSlimBusForMediator(params Assembly[] handlerAssemblies)` | `AddSlimMessageBus` with the memory provider; handlers from the assemblies in DI (`AddServicesFromAssembly`) and on the bus (`AutoDeclareFrom`) | Not supported — call it once, listing every handler assembly. Throws `ArgumentException` when no assembly is passed. |

It sits in the ambient `Microsoft.Extensions.DependencyInjection` namespace, so no extra `using` is needed.

### Public extension points

| Type | Accessibility | What you do with it |
|---|---|---|
| `Fluents.Requests.INoResponse` / `IWitResponse<T>` | `public interface` | Mark a message as a write. |
| `Fluents.Requests.IHandler<TRequest>` / `IHandler<TRequest, TResponse>` | `public interface` | Implement the handler for a write. |
| `Fluents.Requests.IWithKey<TKey>` | `public interface` | Carry a route-bound `Id`; the generated update and action requests implement it. |
| `Fluents.Queries.IWitResponse<T>` / `IWitPageResponse<T>` and their handlers | `public interface` | Mark and handle a read. |
| `Fluents.EventsConsumers.IHandler<TEvent>` | `public interface` | Consume a published event. |
| `NotFoundError` | `public sealed class : FluentResults.Error` | Return it from `Result.Fail` so the API layer can map one type to `404`. |
| `ILazyMap<T>`, `LazyMapExtensions.LazyMap<T>` / `ResultOf<T>` | `public interface` / `public static class` | Defer a Mapster mapping until the value is read. |

The `LazyMap`/`LazyResult` implementations are `internal`.

## 🧱 Where it fits

- **[DKNet.SlimBus.Extensions.EfCore](./DKNet.SlimBus.Extensions.EfCore.md)** — the EF Core add-on: auto-save after a
  successful write and domain events onto the bus. References this package.
- **[DKNet.SlimBus.Generators](./DKNet.SlimBus.Generators.md)** — emits requests and handlers built on exactly these
  `Fluents` interfaces, so generated and hand-written slices sit in one pipeline.
- **[DKNet.AspCore.Extensions](../DKNet.AspCore.Extensions/index.md)** — maps `Fluents` requests to minimal-API
  endpoints and turns their results into HTTP responses.

## ⚠️ Gotchas & limits

- **`AddSlimBusForMediator` is memory-only.** It configures a single memory bus; for an out-of-process transport call
  `AddSlimMessageBus` yourself instead of mixing the two.
- **List every handler assembly.** A handler in an assembly you didn't pass is never discovered; `Send` then fails
  with SlimMessageBus's "no handler" error at runtime, not at start-up.
- **No auto-save here.** This package never touches a `DbContext`. Handlers that change EF entities either call
  `SaveChangesAsync` themselves or the host adds [DKNet.SlimBus.Extensions.EfCore](./DKNet.SlimBus.Extensions.EfCore.md).
- **`Fluents.Requests.IWitResponse<T>` and `Fluents.Queries.IWitResponse<T>` are different interfaces** with the same
  name in different nested classes — one wraps `IResult<T>`, the other returns `T?`. Import them explicitly enough to
  keep them apart.
- **Handlers should be stateless.** They are resolved per dispatch; don't cache request-specific state on the instance.

## 🔗 Related packages

- [DKNet.SlimBus.Extensions.EfCore](./DKNet.SlimBus.Extensions.EfCore.md) – reach for it when handlers change EF Core
  entities and you want the save and the domain-event publish done for you.
- [DKNet.SlimBus.Generators](./DKNet.SlimBus.Generators.md) – reach for it to generate CRUD requests, handlers, and
  endpoints instead of hand-writing the repetitive slices.
- [DKNet.AspCore.Extensions](../DKNet.AspCore.Extensions/index.md) – reach for it to populate request properties
  from claims, headers, or route values before a handler runs.
