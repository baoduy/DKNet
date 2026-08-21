# DKNet.SlimBus.Extensions

CQRS and messaging glue on top of [SlimMessageBus](https://github.com/zarusz/SlimMessageBus), wired for EF Core. It
gives command/query/event contracts a small set of fluent interfaces, saves the `DbContext` for you after a
successful write, and forwards EF Core domain events onto the bus — without pulling in MediatR.

## When to reach for it

Use this package when you already have (or want) EF Core aggregates raising domain events via
`DKNet.EfCore.Events`, and you want a CQRS message pipeline in front of them that is:

- Lighter than MediatR (SlimMessageBus dispatch, no reflection-heavy pipeline behaviors to hand-roll).
- Result-based rather than exception-based for expected business failures (`FluentResults`).
- Self-saving: a command handler that succeeds doesn't need to call `SaveChangesAsync` itself.

If you don't use SlimMessageBus, or you don't need auto-save, this package doesn't buy you much — it is a thin
adapter, not a general CQRS framework.

## Install and minimum wiring

```bash
dotnet add package DKNet.SlimBus.Extensions
```

The package itself only depends on `SlimMessageBus`, `SlimMessageBus.Host`, `SlimMessageBus.Host.Interceptor`,
`FluentResults`, `X.PagedList.EF` and (via project reference) `DKNet.EfCore.Events`. It does **not** bring in a
transport provider — you add whichever `SlimMessageBus.Host.*` provider package your host needs (memory, Azure
Service Bus, Kafka, …) and configure it yourself through SlimMessageBus's own builder.

Minimum registration to get commands/queries dispatching and auto-saving against an EF Core `DbContext`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.Serialization.SystemTextJson;

services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString));

// 1) Auto-save: SaveChanges the DbContext after a successful write request.
services.AddSlimBusEfCoreInterceptor<AppDbContext>();

// 2) Wire SlimMessageBus itself — this part is plain SlimMessageBus, not this package.
services.AddSlimMessageBus(mbb => mbb
    .AddJsonSerializer()
    .AddServicesFromAssembly(typeof(Program).Assembly) // discovers your Fluents handlers
    .AddChildBus("Memory", bus => bus
        .WithProviderMemory()
        .AutoDeclareFrom(typeof(Program).Assembly)));
```

`AddSlimBusEfCoreInterceptor<TDbContext>()` is the only call this package requires; everything else
(`AddSlimMessageBus`, `WithProviderMemory`, `AddChildBus`, `AddJsonSerializer`, `AutoDeclareFrom`,
`AddServicesFromAssembly`) is SlimMessageBus's own API. If you also want EF Core domain events forwarded onto the
bus, add `AddSlimBusEventPublisher<TDbContext>()` (see [Domain event publishing](#domain-event-publishing)).

## Features

### Commands without a response — `Fluents.Requests.INoResponse` / `IHandler<TRequest>`

For writes that only need to signal success/failure, no payload back.

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

### Commands with a response — `Fluents.Requests.IWitResponse<TResponse>` / `IHandler<TRequest, TResponse>`

For writes that need to hand back data (e.g. a new id).

```csharp
using DKNet.SlimBus.Extensions;
using FluentResults;

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

Callers get an `IResult<Guid>` back from `IMessageBus.Send(...)`; auto-save runs only if `IsSuccess` is `true`.

### Single-item queries — `Fluents.Queries.IWitResponse<TResponse>` / `IHandler<TQuery, TResponse>`

Reads never trigger auto-save (see [Auto-save](#auto-save-behavior)), regardless of any tracked changes the handler
happens to make.

```csharp
using DKNet.SlimBus.Extensions;
using Microsoft.EntityFrameworkCore;

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

Note the return type is `TResponse?`, not wrapped in `FluentResults` — a query answers "found / not found", it
doesn't carry a business-failure result.

### Paged queries — `Fluents.Queries.IWitPageResponse<TResponse>` / `IPageHandler<TQuery, TResponse>`

Backed by `X.PagedList` / `X.PagedList.EF`, already a package dependency.

```csharp
using DKNet.SlimBus.Extensions;
using Microsoft.EntityFrameworkCore;
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

`ToPagedListAsync` (from `X.PagedList.EF`) counts and pages server-side; pass `null` for `totalSetCount` to let it
run the count query itself, or supply a precomputed count to skip it.

### Event consumers — `Fluents.EventsConsumers.IHandler<TEvent>`

A thin alias over SlimMessageBus's own `IConsumer<TEvent>`, so a handler for a domain event published through
[Domain event publishing](#domain-event-publishing) looks like:

```csharp
using DKNet.SlimBus.Extensions;

public class ProductCreatedHandler : Fluents.EventsConsumers.IHandler<ProductCreatedEvent>
{
    public Task OnHandle(ProductCreatedEvent message, CancellationToken cancellationToken)
    {
        // react to the event — e.g. send a notification
        return Task.CompletedTask;
    }
}
```

### Auto-save behavior

`AddSlimBusEfCoreInterceptor<TDbContext>()` registers an internal `IRequestHandlerInterceptor<,>` that runs after
every request handler and, on success, saves any registered `DbContext` that has pending changes:

- Runs only for **write requests** — types implementing `Fluents.Requests.INoResponse` or
  `Fluents.Requests.IWitResponse<TResponse>`. Queries (`Fluents.Queries.*`) and raw SlimMessageBus
  `IRequest<T>`/`IRequestHandler<,>` implementations are never auto-saved.
- Skipped when the handler's response is `null`, or is an `IResultBase` with `IsSuccess == false` — a failed
  command never persists partial state.
- Iterates every `DbContext` type registered via `AddSlimBusEfCoreInterceptor<TDbContext>()` (you can call it once
  per `DbContext` type if you have more than one), resolves each from the current scope, and for the ones with
  `ChangeTracker.HasChanges()`, calls `AddNewEntitiesFromNavigations` then `SaveChangesWithConcurrencyHandlingAsync`
  (both from `DKNet.EfCore.Extensions`) — the same save primitives used elsewhere in DKNet's EF Core stack.
- Looks up an `IEfCoreExceptionHandler` keyed by the `DbContext`'s full type name first, falling back to an
  unkeyed one, letting you plug in per-context or global concurrency-conflict handling.
- Registered with `Order = int.MaxValue` (`IInterceptorWithOrder`), so it is meant to run after your own
  interceptors in the SlimMessageBus interceptor pipeline.

The interceptor and its `DbContext` type registry are internal types — you don't reference them directly; you
opt in purely through `AddSlimBusEfCoreInterceptor<TDbContext>()`.

### Pipeline behaviors / interceptors

Because the auto-save behavior is just a SlimMessageBus `IRequestHandlerInterceptor<TRequest, TResponse>`, you can
add your own the same way (validation, logging, authorization, …):

```csharp
using Microsoft.Extensions.Logging;
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

`AddSlimBusEfCoreInterceptor<TDbContext>()` itself guards against double-registering the open generic
`IRequestHandlerInterceptor<,>` — calling it again (e.g. for a second `DbContext`) only adds that type to the
save registry, it won't add a second interceptor instance.

### Transport / provider support

The package is transport-agnostic: it references `SlimMessageBus`, `SlimMessageBus.Host` and
`SlimMessageBus.Host.Interceptor` only. It doesn't wire, recommend, or restrict any specific provider — the test
suite uses `SlimMessageBus.Host.Memory` (`WithProviderMemory()`), but Azure Service Bus, Kafka, etc. are equally
valid, added and configured entirely through SlimMessageBus's own `AddSlimMessageBus(...)` builder, independent of
this package.

### Legacy: `RequestBase.ByUser`

`RequestBase` is a `[Obsolete]` base record with a `[JsonIgnore] string? ByUser` property. It is **never populated
by this package** — it's retained only for existing consumers. The supported pattern for carrying the acting user
is an `IContextualSource` attribute (e.g. `[FromClaim(ClaimTypes.Name)]` from `DKNet.AspCore.Extensions`) directly
on the request, populated via `AddContextualRequestPopulation()`.

## Configuration and defaults

| Call | What it does | Notes |
|---|---|---|
| `services.AddSlimBusEfCoreInterceptor<TDbContext>()` | Registers `TDbContext` for auto-save and registers the auto-save interceptor (once). | Call once per `DbContext` type if you have several. |
| `services.AddSlimBusEventPublisher<TDbContext>()` | Registers `SlimBusEventPublisher` as the `IEventPublisher` for `TDbContext` and wires `DKNet.EfCore.Events`' save hook. | See below. |
| `services.AddSlimMessageBus(...)` | SlimMessageBus's own bus/provider/serializer configuration. | Not part of this package; `AddJsonSerializer`, `AddChildBus`, `WithProviderMemory`, `AutoDeclareFrom`, `AddServicesFromAssembly` all come from SlimMessageBus host packages. |

There is no separate "options" object owned by this package — the only knobs are which `DbContext` type(s) you
register and how you configure the SlimMessageBus builder itself.

## Composition with other DKNet packages

- **`DKNet.EfCore.Events`** — `AddSlimBusEventPublisher<TDbContext>()` registers `SlimBusEventPublisher` (an
  `IEventPublisher`) and calls that package's `AddEventPublisher<TDbContext, TImplementation>()`, which wires its
  `EventHook` into `TDbContext`'s save pipeline. After a successful `SaveChangesAsync`, the hook collects the
  aggregate's raised/declared domain events and calls every registered `IEventPublisher.PublishAsync(...)` —
  `SlimBusEventPublisher` forwards each event onto `IMessageBus.Publish`, copying `IEventItem.AdditionalData` into
  message headers (case-insensitive) when the event implements `IEventItem`.
- **`DKNet.EfCore.Repos`** — that package's write repository is `[Obsolete]` ("retired — use
  `DKNet.EfCore.Specifications`"), and this package never calls into it. The auto-save interceptor saves the
  `DbContext` directly using the same `AddNewEntitiesFromNavigations` / `SaveChangesWithConcurrencyHandlingAsync`
  extensions that repository used, so handlers can mutate entities through `DbContext` directly, through
  `DKNet.EfCore.Specifications`' `IRepositorySpec`, or through your own repository — auto-save doesn't care, it
  only looks at `ChangeTracker.HasChanges()`.
- **`DKNet.EfCore.Abstractions`** — supplies the `IEventItem`/`EventItem` and `IEventPublisher` contracts that
  `SlimBusEventPublisher` implements, and (via `DKNet.EfCore.Events`) the aggregate/domain-event machinery whose
  output this package forwards onto the bus.
- **`DKNet.Fw.Extensions`** — not referenced directly by this package. It flows in transitively through
  `DKNet.EfCore.Extensions` (used by the auto-save interceptor for save/concurrency handling), so no separate
  wiring is needed on your part.

## Gotchas and limits

- **Query handlers that mutate the `DbContext` won't be saved.** Auto-save only fires for `INoResponse` /
  `IWitResponse<T>` requests. If a `Fluents.Queries.*` handler adds/updates entities, nothing persists them —
  by design, but easy to trip over if a "read" handler accidentally does a write.
- **Raw SlimMessageBus requests bypass auto-save entirely.** A handler implementing `SlimMessageBus.IRequestHandler<,>`
  directly (not through `Fluents.Requests`) is never recognized as a write request, so no save happens even on
  success.
- **No cross-`DbContext` transaction.** When more than one `DbContext` type is registered, each one with pending
  changes is saved independently in a loop — if a later `DbContext`'s `SaveChangesAsync` throws, earlier ones have
  already committed. There's no ambient/distributed transaction wrapping the set.
- **The `DbContext` type registry is static process-wide state.** `AddSlimBusEfCoreInterceptor<TDbContext>()`
  adds to a shared `HashSet<Type>`, not something scoped to one `IServiceCollection`/`ServiceProvider`. In a single
  process that builds multiple service providers (e.g. parallel test fixtures), the registered `DbContext` types
  accumulate across all of them.
- **A `null` or failed response silently skips the save** — including exceptions caught by the handler and turned
  into `Result.Fail(...)`. Unhandled exceptions instead propagate out through SlimMessageBus's own pipeline; this
  package does not add exception handling around the handler call itself.
- **Handlers should be stateless.** They're resolved per SlimMessageBus dispatch (typically from a DI scope);
  don't cache request-specific state on the handler instance.

## Source reference

- Fluent interfaces: `src/SlimBus/DKNet.SlimBus.Extensions/Fluents.cs`
- DI wiring: `src/SlimBus/DKNet.SlimBus.Extensions/SlimBusEfCoreSetup.cs`
- Domain event publisher: `src/SlimBus/DKNet.SlimBus.Extensions/Handlers/SlimBusEventPublisher.cs`
- Auto-save interceptor (internal): `src/SlimBus/DKNet.SlimBus.Extensions/Interceptors/EfAutoSavePostInterceptor.cs`
- Usage patterns verified against `src/SlimBus/SlimBus.Extensions.Tests/`
