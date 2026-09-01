# DKNet.SlimBus.Extensions

CQRS contracts and EF Core glue on top of [SlimMessageBus](https://github.com/zarusz/SlimMessageBus) — fluent
command/query/event interfaces, automatic `SaveChanges` after a successful write, and domain events forwarded onto the
bus.

## ✨ Why use it?

- **Handlers stop calling `SaveChangesAsync`.** A write request that returns a successful `IResult` gets its
  `DbContext` saved by an interceptor, using the same concurrency-aware save path as the rest of DKNet's EF Core stack.
- **Expected business failures are results, not exceptions.** Commands return `FluentResults` types, so "product not
  found" is a value the caller inspects rather than a thrown exception to catch.
- **Request shape says what a message is.** Implementing `Fluents.Requests.*` marks a write, `Fluents.Queries.*` marks a
  read, and the auto-save interceptor keys off exactly that — no attributes, no naming conventions.
- **Domain events reach the bus without handler code.** `AddSlimBusEventPublisher<TDbContext>()` hooks
  `DKNet.EfCore.Events` up to `IMessageBus.Publish`, so events raised by aggregates are published after the save that
  made them true.
- **No MediatR, no framework.** It is a thin adapter over SlimMessageBus — a few interfaces, one interceptor, one
  publisher.

If you are not using SlimMessageBus, or don't want auto-save, this package buys you little.

## 🚀 Quick Start

```bash
dotnet add package DKNet.SlimBus.Extensions
```

It brings no transport with it — add whichever `SlimMessageBus.Host.*` provider your host needs (memory, Azure Service
Bus, Kafka, …) and configure it through SlimMessageBus's own builder:

```csharp
using Microsoft.Extensions.DependencyInjection;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.Serialization.SystemTextJson;

services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString));

// 1) This package: SaveChanges the DbContext after a successful write request.
services.AddSlimBusEfCoreInterceptor<AppDbContext>();

// 2) This package (optional): forward EF Core domain events onto the bus.
services.AddSlimBusEventPublisher<AppDbContext>();

// 3) Plain SlimMessageBus — bus, provider, serializer, handler discovery.
services.AddSlimMessageBus(mbb => mbb
    .AddJsonSerializer()
    .AddServicesFromAssembly(typeof(Program).Assembly) // discovers your Fluents handlers
    .AddChildBus("Memory", bus => bus
        .WithProviderMemory()
        .AutoDeclareFrom(typeof(Program).Assembly)));
```

Those two `AddSlimBus*` calls are this package's entire registration surface — there is no options object. Everything
else (`AddSlimMessageBus`, `AddJsonSerializer`, `AddChildBus`, `WithProviderMemory`, `AutoDeclareFrom`,
`AddServicesFromAssembly`) is SlimMessageBus's own API. Call `AddSlimBusEfCoreInterceptor<T>()` once per `DbContext`
type; repeat calls add the type to the save registry without registering a second interceptor.

Then send a command:

```csharp
var result = await bus.Send(new CreateProduct("Widget", 9.99m), cancellationToken);
if (result.IsSuccess) return TypedResults.Created($"/products/{result.Value}");
```

## 🧩 Features

### Commands without a response — `Fluents.Requests.INoResponse`

For writes that only signal success or failure.

```csharp
using DKNet.SlimBus.Extensions;
using FluentResults;

public record DeactivateProduct(Guid ProductId) : Fluents.Requests.INoResponse;

internal sealed class DeactivateProductHandler(AppDbContext db)
    : Fluents.Requests.IHandler<DeactivateProduct>
{
    public async Task<IResultBase> OnHandle(DeactivateProduct request, CancellationToken cancellationToken)
    {
        var product = await db.Products.FindAsync([request.ProductId], cancellationToken);
        if (product is null) return Result.Fail("Product not found");

        product.Deactivate();
        // No SaveChangesAsync call here — the auto-save interceptor does it after this returns Ok.
        return Result.Ok();
    }
}
```

`Fluents.Requests.IHandler<TRequest>` is `IRequestHandler<TRequest, IResultBase>` constrained to
`TRequest : INoResponse`, so `OnHandle` returns `Task<IResultBase>`.

### Commands with a response — `Fluents.Requests.IWitResponse<TResponse>`

For writes that hand data back, such as a new identifier.

```csharp
public record CreateProduct(string Name, decimal Price) : Fluents.Requests.IWitResponse<Guid>;

internal sealed class CreateProductHandler(AppDbContext db)
    : Fluents.Requests.IHandler<CreateProduct, Guid>
{
    public async Task<IResult<Guid>> OnHandle(CreateProduct request, CancellationToken cancellationToken)
    {
        var product = new Product(request.Name, request.Price);
        await db.Products.AddAsync(product, cancellationToken);
        return Result.Ok(product.Id);
    }
}
```

Callers get an `IResult<Guid>` from `IMessageBus.Send(...)`; auto-save runs only when `IsSuccess` is `true`.

`Fluents.Requests.IWithKey<TKey>` is a small companion interface (`TKey Id { get; set; }`) for requests addressed by
identifier — [DKNet.SlimBus.Generators](./DKNet.SlimBus.Generators.md) puts it on the update and action requests it
emits so shared code can read the key generically.

### Single-item queries — `Fluents.Queries.IWitResponse<TResponse>`

```csharp
public record GetProduct(Guid Id) : Fluents.Queries.IWitResponse<ProductDto>;

internal sealed class GetProductHandler(AppDbContext db)
    : Fluents.Queries.IHandler<GetProduct, ProductDto>
{
    public async Task<ProductDto?> OnHandle(GetProduct request, CancellationToken cancellationToken)
    {
        var product = await db.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
        return product is null ? null : new ProductDto(product.Id, product.Name);
    }
}
```

The return type is `TResponse?`, not a `FluentResults` wrapper — a query answers "found / not found", it does not carry
a business-failure result. Reads never trigger auto-save (see [Auto-save behaviour](#auto-save-behaviour)), whatever the
handler leaves in the change tracker.

### Paged queries — `Fluents.Queries.IWitPageResponse<TResponse>`

Backed by `X.PagedList` / `X.PagedList.EF`, already a dependency.

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

`ToPagedListAsync` counts and pages server-side; pass `null` for `totalSetCount` to let it run the count query, or a
precomputed count to skip it.

### Event consumers — `Fluents.EventsConsumers.IHandler<TEvent>`

A thin alias over SlimMessageBus's `IConsumer<TEvent>`, so a consumer of a published domain event reads as:

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

### Domain event publishing

`AddSlimBusEventPublisher<TDbContext>()` registers `SlimBusEventPublisher` as an `IEventPublisher` for `TDbContext` via
`DKNet.EfCore.Events`' `AddEventPublisher<TDbContext, TImplementation>()`, which wires that package's event hook into
the context's save pipeline. After a successful `SaveChangesAsync`, the hook collects the events the aggregates raised
and hands each to the publisher, which forwards it to `IMessageBus.Publish`. When an event implements `IEventItem`, its
`AdditionalData` entries are copied onto the message as headers with case-insensitive keys.

`SlimBusEventPublisher` is public and both `PublishAsync` overloads are `virtual`, so you can subclass it — to stamp
extra headers, for example — and register the subclass instead:

```csharp
public sealed class LoggingEventPublisher(IMessageBus bus, ILogger<LoggingEventPublisher> logger)
    : SlimBusEventPublisher(bus)
{
    public override Task PublishAsync(object eventObj, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Publishing {EventType}", eventObj.GetType().Name);
        return base.PublishAsync(eventObj, cancellationToken);
    }
}
```

A collection of events is published one at a time, in order, awaiting each — there is no batching.

### Auto-save behaviour

`AddSlimBusEfCoreInterceptor<TDbContext>()` registers an internal `IRequestHandlerInterceptor<,>` that runs after every
request handler and, on success, saves any registered `DbContext` with pending changes:

- Runs only for **write requests** — types implementing `Fluents.Requests.INoResponse` or
  `Fluents.Requests.IWitResponse<TResponse>`. Queries (`Fluents.Queries.*`) and raw SlimMessageBus
  `IRequest<T>`/`IRequestHandler<,>` implementations are never auto-saved.
- Skipped when the response is `null`, or is an `IResultBase` with `IsSuccess == false` — a failed command never
  persists partial state.
- Iterates every `DbContext` type registered through the call, resolves each from the current scope and, for those where
  `ChangeTracker.HasChanges()`, calls `AddNewEntitiesFromNavigations` then `SaveChangesWithConcurrencyHandlingAsync`
  (both from `DKNet.EfCore.Extensions`).
- Resolves an `IEfCoreExceptionHandler` keyed by the `DbContext`'s full type name first, falling back to an unkeyed
  registration — per-context or global concurrency-conflict handling.
- Is registered with `Order = int.MaxValue` (`IInterceptorWithOrder`), so it runs after your own interceptors.

The interceptor and its `DbContext` type registry are internal; you opt in purely through
`AddSlimBusEfCoreInterceptor<TDbContext>()`.

![Sequence diagram of one write request: the auto-save interceptor wraps the handler and saves the DbContext when the result succeeded and the ChangeTracker has changes. That save publishes the raised events through SlimBusEventPublisher back onto IMessageBus.](../diagrams/slimbus-request-pipeline.svg)

Read the wrapping order from the diagram rather than the bullet list: because auto-save is registered last, the
handler's result passes through it on the way out, which is where the save — and therefore the event publish — actually
happens. Event publishing is a second, separate opt-in: without `AddSlimBusEventPublisher<TDbContext>()` the save
still runs and nothing reaches the bus.

### Your own interceptors

Because auto-save is just a SlimMessageBus `IRequestHandlerInterceptor<TRequest, TResponse>`, add validation, logging,
or authorization the same way:

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

### Lazy mapping — `ILazyMap<T>` and `IMapper.ResultOf<T>`

Two Mapster-backed helpers for handlers that return a DTO, so the mapping cost is paid only if something reads the
value:

```csharp
using DKNet.SlimBus.Extensions.LazyMapper;

// IResult<ProductDto> whose Value is mapped on first access
public Task<IResult<ProductDto>> OnHandle(CreateProduct request, CancellationToken cancellationToken)
{
    var product = new Product(request.Name, request.Price);
    db.Products.Add(product);
    return Task.FromResult(mapper.ResultOf<ProductDto>(product));
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

### Legacy: `RequestBase.ByUser`

`RequestBase` is an `[Obsolete]` base record with a `[JsonIgnore] string? ByUser` property. It is **never populated by
this package** and is retained only for existing consumers. The supported way to carry the acting user is an
`IContextualSource` attribute (e.g. `[FromClaim(ClaimTypes.Name)]` from `DKNet.AspCore.Extensions`) on the request's own
property, populated by `AddContextualRequestPopulation()`.

## ⚙️ Configuration reference

There is no options type, no `IConfiguration` section, and no builder in this package. What you can vary is
which of the two registrations you call, the `DbContext` you call them with, and which contracts your own types
implement — so that is what this section documents.

### Registration surface (`SlimBusEfCoreSetup`)

| Method | Constraint | Registers | Lifetime | Repeat call |
|---|---|---|---|---|
| `AddSlimBusEfCoreInterceptor<TDbContext>()` | `TDbContext : DbContext` | `IRequestHandlerInterceptor<,>` → the internal auto-save interceptor | Scoped | Adds `TDbContext` to the save registry; the interceptor itself is registered only once |
| `AddSlimBusEventPublisher<TDbContext>()` | `TDbContext : DbContext` | `SlimBusEventPublisher` as `IEventPublisher` for `TDbContext`, via `DKNet.EfCore.Events` | Delegated to `AddEventPublisher` | Delegated to `AddEventPublisher` |

Both are declared inside a C# 14 `extension(IServiceCollection)` block, so they are called as ordinary
extension methods on `IServiceCollection`. Calling them does not require C# 14 in your own project — a
consumer at `LangVersion` 13 compiles against them fine.

### Auto-save behaviour — the fixed rules

None of these are switchable; they are the contract the interceptor implements.

| Concern | Value |
|---|---|
| Interceptor order | `int.MaxValue` (`IInterceptorWithOrder`) — runs outermost, after your own interceptors |
| Saved for | `Fluents.Requests.INoResponse` and `Fluents.Requests.IWitResponse<T>` only |
| Skipped when | The response is `null`, or is an `IResultBase` with `IsSuccess == false` |
| Saved contexts | Every type registered through `AddSlimBusEfCoreInterceptor<T>()` whose `ChangeTracker.HasChanges()` |
| Save call | `AddNewEntitiesFromNavigations`, then `SaveChangesWithConcurrencyHandlingAsync` |
| Exception handler | `IEfCoreExceptionHandler` keyed by the `DbContext`'s `FullName`, falling back to the unkeyed registration |

### Public extension points

| Type | Accessibility | What you do with it |
|---|---|---|
| `Fluents.Requests.INoResponse` / `IWitResponse<T>` | `public interface` | Mark a message as a write — this is what auto-save keys off. |
| `Fluents.Requests.IHandler<TRequest>` / `IHandler<TRequest, TResponse>` | `public interface` | Implement the handler for a write. |
| `Fluents.Requests.IWithKey<TKey>` | `public interface` | Carry a route-bound `Id`; the generated update and action requests implement it. |
| `Fluents.Queries.IWitResponse<T>` / `IWitPageResponse<T>` and their handlers | `public interface` | Mark and handle a read. Never auto-saved. |
| `Fluents.EventsConsumers.IHandler<TEvent>` | `public interface` | Consume a published event. |
| `SlimBusEventPublisher` | `public class`, both `PublishAsync` overloads `virtual` | Subclass to add headers or logging, then register the subclass. |
| `NotFoundError` | `public sealed class : FluentResults.Error` | Return it from `Result.Fail` so the API layer can map one type to `404`. |
| `ILazyMap<T>`, `LazyMapExtensions.LazyMap<T>` / `ResultOf<T>` | `public interface` / `public static class` | Defer a Mapster mapping until the value is read. |
| `RequestBase` | `public record`, `[Obsolete]` | Nothing — retained for existing consumers and never populated by this package. |

The auto-save interceptor (`EfAutoSavePostInterceptor<,>`), its `DbContext` registry, and the `LazyMap`/`LazyResult`
implementations are all `internal` — you opt in through the two registration methods, not by implementing or
replacing those types.

## 🧱 Where it fits

- **[DKNet.EfCore.Events](../EfCore/DKNet.EfCore.Events.md)** — the source of the domain events this package forwards;
  `AddSlimBusEventPublisher<TDbContext>()` is the bridge between its hook and the bus.
- **[DKNet.EfCore.Abstractions](../EfCore/DKNet.EfCore.Abstractions.md)** — supplies the `IEventItem` / `IEventPublisher`
  contracts `SlimBusEventPublisher` implements.
- **[DKNet.EfCore.Specifications](../EfCore/DKNet.EfCore.Specifications.md)** — the recommended query/mutation surface
  inside handlers; auto-save doesn't care how entities were changed, only that the change tracker has changes.
- **[DKNet.SlimBus.Generators](./DKNet.SlimBus.Generators.md)** — emits requests and handlers built on exactly these
  `Fluents` interfaces, so generated and hand-written slices sit in one pipeline.
- **`DKNet.EfCore.Extensions`** — provides `AddNewEntitiesFromNavigations` and
  `SaveChangesWithConcurrencyHandlingAsync`, the save primitives the interceptor calls.

## ⚠️ Gotchas & limits

- **Query handlers that mutate the `DbContext` are not saved.** Auto-save only fires for `INoResponse` /
  `IWitResponse<T>` requests — by design, but easy to trip over when a "read" handler writes.
- **Raw SlimMessageBus requests bypass auto-save entirely.** A handler implementing `IRequestHandler<,>` directly, not
  through `Fluents.Requests`, is never recognized as a write.
- **No cross-`DbContext` transaction.** Each registered context with pending changes is saved independently in a loop;
  if a later save throws, earlier ones have already committed.
- **The `DbContext` type registry is static process-wide state.** Registration adds to a shared `HashSet<Type>`, not
  something scoped to one `IServiceCollection` — in a process that builds several service providers (parallel test
  fixtures, for instance) the registered types accumulate across all of them.
- **A `null` or failed response silently skips the save**, including an exception the handler caught and turned into
  `Result.Fail(...)`. Unhandled exceptions propagate through SlimMessageBus's own pipeline; this package adds no
  exception handling around the handler call.
- **Auto-save runs inside the interceptor, after the handler returns.** A `DbUpdateException` surfaces from the
  interceptor rather than from the handler, so handler-local try/catch will not see it.
- **`Fluents.Requests.IWitResponse<T>` and `Fluents.Queries.IWitResponse<T>` are different interfaces** with the same
  name in different nested classes — one wraps `IResult<T>`, the other returns `T?`. Import them explicitly enough to
  keep them apart.
- **Handlers should be stateless.** They are resolved per dispatch; don't cache request-specific state on the instance.

## 🔗 Related packages

- [DKNet.SlimBus.Generators](./DKNet.SlimBus.Generators.md) – reach for it to generate CRUD requests, handlers, and
  endpoints instead of hand-writing the repetitive slices.
- [DKNet.EfCore.Events](../EfCore/DKNet.EfCore.Events.md) – reach for it to raise domain events from aggregates in the
  first place.
- [DKNet.EfCore.Specifications](../EfCore/DKNet.EfCore.Specifications.md) – reach for it to express handler queries as
  reusable specifications.
- [DKNet.AspCore.Extensions](../AspNetCore/DKNet.AspCore.Extensions.md) – reach for it to populate request properties
  from claims, headers, or route values before a handler runs.
