# DKNet.SlimBus.Extensions

| Field | Value |
|---|---|
| Area | Messaging |
| NuGet | `dotnet add package DKNet.SlimBus.Extensions` |
| Docs | https://github.com/baoduy/DKNet/blob/dev/docs/Messaging/DKNet.SlimBus.Extensions.md |
| Source | https://github.com/baoduy/DKNet/tree/dev/src/SlimBus/DKNet.SlimBus.Extensions |
| Depends on (DKNet) | `DKNet.EfCore.Events` (pulls in `DKNet.EfCore.Abstractions` and `DKNet.EfCore.Extensions` transitively — `AddNewEntitiesFromNavigations`/`SaveChangesWithConcurrencyHandlingAsync`/`IEfCoreExceptionHandler` come from `DKNet.EfCore.Extensions`) |
| Depends on (3rd party) | `FluentResults`, `Mapster`, `Microsoft.Extensions.Hosting.Abstractions`, `SlimMessageBus`, `SlimMessageBus.Host`, `SlimMessageBus.Host.Interceptor`, `X.PagedList.EF` |
| Target framework | `net10.0` |

## Purpose

A thin CQRS adapter over [SlimMessageBus](https://github.com/zarusz/SlimMessageBus):
`Fluents.Requests`/`Fluents.Queries`/`Fluents.EventsConsumers` give handlers a fluent,
`FluentResults`-based shape, and an internal `IRequestHandlerInterceptor<,>` auto-saves a registered
`DbContext` after a successful write request — handlers never call `SaveChangesAsync` themselves. A
second, independent registration (`AddSlimBusEventPublisher<TDbContext>`) bridges `DKNet.EfCore.Events`'
domain-event hook onto `IMessageBus.Publish`.

It is **not** a transport, a validation pipeline, or a MediatR replacement with pipeline behaviors — it
brings no `SlimMessageBus.Host.*` provider. If you don't use SlimMessageBus, or don't want auto-save,
this package buys you nothing.

## Entry points

| Call | Exact signature | Called on | Notes |
|---|---|---|---|
| `AddSlimBusEfCoreInterceptor<TDbContext>()` | `IServiceCollection AddSlimBusEfCoreInterceptor<TDbContext>() where TDbContext : DbContext` | `IServiceCollection` | Adds `TDbContext` to a per-container registry (`TryAddEnumerable`, Singleton). Registers `EfAutoSavePostInterceptor<,>` as `IRequestHandlerInterceptor<,>` (Scoped) **only if no service is already registered for the open generic `IRequestHandlerInterceptor<,>`**. Call once per `DbContext` type, before registering your own `IRequestHandlerInterceptor<,>`. |
| `AddSlimBusEventPublisher<TDbContext>()` | `IServiceCollection AddSlimBusEventPublisher<TDbContext>() where TDbContext : DbContext` | `IServiceCollection` | Calls `DKNet.EfCore.Events`' `AddEventPublisher<TDbContext, SlimBusEventPublisher>()`, which registers `SlimBusEventPublisher` as `IEventPublisher` (Scoped, guarded so repeat calls with the same implementation no-op) and wires `EventHook` via `AddHook<TDbContext, EventHook>()`. Optional and independent of the interceptor above. |
| Implement `Fluents.Requests.INoResponse` / `IWitResponse<T>` | `interface INoResponse : IRequest<IResultBase>`; `interface IWitResponse<out TResponse> : IRequest<IResult<TResponse>>` | request record/class | Marks the message as a **write** — exactly what `EfAutoSavePostInterceptor<,>.IsWrite` checks via reflection on `TRequest`. |
| Implement `Fluents.Requests.IHandler<TRequest>` / `IHandler<TRequest, TResponse>` | `interface IHandler<in TRequest> : IRequestHandler<TRequest, IResultBase> where TRequest : INoResponse`; `interface IHandler<in TRequest, TResponse> : IRequestHandler<TRequest, IResult<TResponse>> where TRequest : IWitResponse<TResponse>` | handler class | Discovered by SlimMessageBus's own `AddServicesFromAssembly`, not by this package. |
| Implement `Fluents.Queries.IWitResponse<T>` / `IWitPageResponse<T>` + `IHandler<,>` / `IPageHandler<,>` | see Public surface below | handler class | Never triggers auto-save, regardless of what the handler does to the `DbContext`. |
| Implement `Fluents.EventsConsumers.IHandler<TEvent>` | `interface IHandler<in TEvent> : IConsumer<TEvent>` | consumer class | Alias over SlimMessageBus's own `IConsumer<TEvent>`; no DKNet-specific behavior. |
| Subclass `SlimBusEventPublisher` | `class SlimBusEventPublisher(IMessageBus bus) : IEventPublisher` with `virtual Task PublishAsync(object, CancellationToken)` and `virtual Task PublishAsync(IEnumerable<object>, CancellationToken)` | register the subtype via `DKNet.EfCore.Events`' `AddEventPublisher<TDbContext, TYourType>()` directly, **not** `AddSlimBusEventPublisher<TDbContext>()` (which always registers the base type, and adds a second registration if both are called) | Both overloads are `virtual`; the constructor throws `ArgumentNullException` if `bus` is `null`. |
| `IMapper.LazyMap<TValue>(value)` / `IMapper.ResultOf<TValue>(value)` | `static ILazyMap<TValue> LazyMap<TValue>(this IMapper mapper, object value)`; `static IResult<TValue> ResultOf<TValue>(this IMapper mapper, object value)` | `IMapper` (Mapster's `MapsterMapper.IMapper`) | Requires an `IMapper` registered in the container (e.g. Mapster's `ServiceMapper`). |

## Public surface

### `DKNet.SlimBus.Extensions`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `Fluents` | static class | Container for the four fluent groups below | Nested static classes `EventsConsumers`, `Queries`, `Requests` |
| `Fluents.EventsConsumers.IHandler<TEvent>` | interface | Marks an event-consumer handler | `: IConsumer<TEvent>` — no members of its own |
| `Fluents.Queries.IWitResponse<TResponse>` | interface | Marks a single-item query | `: IRequest<TResponse?>` |
| `Fluents.Queries.IWitPageResponse<TResponse>` | interface | Marks a paged query | `: IRequest<IPagedList<TResponse>>` |
| `Fluents.Queries.IHandler<TQuery, TResponse>` | interface | Query handler contract | `: IRequestHandler<TQuery, TResponse?> where TQuery : IWitResponse<TResponse>` |
| `Fluents.Queries.IPageHandler<TQuery, TResponse>` | interface | Paged-query handler contract | `: IRequestHandler<TQuery, IPagedList<TResponse>> where TQuery : IWitPageResponse<TResponse>` |
| `Fluents.Requests.INoResponse` | interface | Marks a write with no return value | `: IRequest<IResultBase>` |
| `Fluents.Requests.IWitResponse<TResponse>` | interface | Marks a write that returns a value | `: IRequest<IResult<TResponse>>` |
| `Fluents.Requests.IHandler<TRequest>` | interface | Handler contract for `INoResponse` | `: IRequestHandler<TRequest, IResultBase> where TRequest : INoResponse` |
| `Fluents.Requests.IHandler<TRequest, TResponse>` | interface | Handler contract for `IWitResponse<T>` | `: IRequestHandler<TRequest, IResult<TResponse>> where TRequest : IWitResponse<TResponse>` |
| `Fluents.Requests.IWithKey<TKey>` | interface | Carries a route-bound id on an update/action request | `TKey Id { get; set; }` |
| `NotFoundError` | sealed class | `FluentResults.Error` subtype for "not found"; mapped to HTTP 404 by `DKNet.AspCore.Extensions` | `NotFoundError(string message) : base(message)` |

### `DKNet.SlimBus.Extensions.Handlers`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `SlimBusEventPublisher` | class | `IEventPublisher` implementation forwarding domain events to `IMessageBus` | `SlimBusEventPublisher(IMessageBus bus)`; `virtual Task PublishAsync(object eventObj, CancellationToken cancellationToken = default)`; `virtual Task PublishAsync(IEnumerable<object> eventList, CancellationToken cancellationToken = default)` |

### `DKNet.SlimBus.Extensions.LazyMapper`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `ILazyMap<TResult>` | interface | Value lazily mapped to `TResult` on first access | `TResult Value { get; }` (throws `InvalidOperationException` if the source was `null`); `TResult? ValueOrDefault { get; }` |
| `LazyMapExtensions` | static class | `IMapper` extension entry points | `static ILazyMap<TValue> LazyMap<TValue>(this IMapper mapper, object value)`; `static IResult<TValue> ResultOf<TValue>(this IMapper mapper, object value)` |
| `LazyMap<TResult>` *(internal)* | class | Backs `ILazyMap<TResult>`; caches the mapped value, returns the same instance if `originalValue is TResult` | ctor `(object? originalValue, IMapper mapper)` |
| `LazyResult<TResult>` *(internal)* | class | `LazyMap<TResult>` + `FluentResults.IResult<TResult>` so a lazy value can be returned directly from a handler | `List<IReason> Reasons { get; init; } = []`; `IsFailed`/`IsSuccess`/`Errors`/`Successes` derived from `Reasons` |

### `DKNet.SlimBus.Extensions.Interceptors` (all internal — listed because they explain observable behavior)

| Type | Kind | Purpose |
|---|---|---|
| `IAutoSaveDbContextRegistration` / `AutoSaveDbContextRegistration<TDbContext>` | internal interface/class | One resolvable registry entry per `TDbContext`, added via `TryAddEnumerable`; scoped to the `IServiceCollection` it was built from, not process-wide |
| `EfAutoSavePostInterceptor<TRequest, TResponse>` | internal sealed class | The auto-save interceptor: `IRequestHandlerInterceptor<TRequest, TResponse>, IInterceptorWithOrder` with `Order => int.MaxValue` (not actually read on this dispatch path — see Runtime behaviour) |

### `Microsoft.Extensions.DependencyInjection` (ambient namespace — deliberate, so `AddSlimBus*` resolves without an extra `using`)

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `SlimBusEfCoreSetup` | static class | DI registration surface | `AddSlimBusEventPublisher<TDbContext>()` and `AddSlimBusEfCoreInterceptor<TDbContext>()`, both extension members on `IServiceCollection` |

## Options & defaults

No options object, no `IConfiguration` section, no builder. The only registrations are:

| Registration | Type | Default | Effect | Where set |
|---|---|---|---|---|
| Auto-save registry entry | `AutoSaveDbContextRegistration<TDbContext>` (Singleton, via `TryAddEnumerable`) | not registered | Adds `TDbContext` to the set of contexts the interceptor checks after each write | `AddSlimBusEfCoreInterceptor<TDbContext>()` |
| Auto-save interceptor | `EfAutoSavePostInterceptor<,>` as `IRequestHandlerInterceptor<,>` (Scoped) | not registered | Runs after every request handler; saves registered contexts with pending changes on a successful write | `AddSlimBusEfCoreInterceptor<TDbContext>()`, first call only (see Gotchas) |
| Event publisher | `SlimBusEventPublisher` as `IEventPublisher` (Scoped, guarded so the same implementation type registers once) | not registered | Forwards events raised through `DKNet.EfCore.Events`' hook to `IMessageBus.Publish` | `AddSlimBusEventPublisher<TDbContext>()` → `DKNet.EfCore.Events.AddEventPublisher<TDbContext, TImplementation>()` |

## Usage patterns

Every example below shares one domain model, declared once and reused throughout this file:

```csharp
using DKNet.EfCore.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;

public sealed class Product : Entity
{
    private Product()
    {
    } // EF Core materialization

    public Product(string name, decimal price)
        : base(Guid.NewGuid())
    {
        Name = name;
        Price = price;
        IsActive = true;
        AddEvent(new ProductCreatedEvent(Id));
    }

    public string Name { get; private set; } = string.Empty;

    public decimal Price { get; private set; }

    public bool IsActive { get; private set; }

    public void Deactivate() => IsActive = false;

    public void Rename(string name) => Name = name;
}

public sealed record ProductCreatedEvent(Guid ProductId);

public sealed record ProductDto(Guid Id, string Name, decimal Price);

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
}
```

### A write with no return value

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
        if (product is null) return Result.Fail(new NotFoundError($"Product '{request.ProductId}' was not found."));

        product.Deactivate();
        // No SaveChangesAsync here — EfAutoSavePostInterceptor saves after this returns a success.
        return Result.Ok();
    }
}
```

**Notes**: returning `Result.Fail(...)` skips the save entirely — no partial state persists.
`AppDbContext` must have been registered with `AddSlimBusEfCoreInterceptor<AppDbContext>()`.

### A write that hands back a value

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

**Notes**: auto-save runs only when `IsSuccess` is `true`. `result.Value` (the id) is available to the
caller whether or not the row has actually been flushed, because the id was assigned in the
constructor here — a store-generated key would only be populated after the save completes.

### A single-item query (never auto-saved)

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
        return product is null ? null : new ProductDto(product.Id, product.Name, product.Price);
    }
}
```

**Notes**: the return type is `TResponse?`, not a `FluentResults` wrapper. Even if the handler mutates
the change tracker (e.g. calls `db.Products.Add(...)` by mistake), the interceptor's `IsWrite` check on
`GetProduct` is `false`, so nothing gets saved.

### A paged query

```csharp
using DKNet.SlimBus.Extensions;
using Microsoft.EntityFrameworkCore;
using X.PagedList;
using X.PagedList.EF;

public record GetProductsPage(int PageNumber, int PageSize) : Fluents.Queries.IWitPageResponse<ProductDto>;

internal sealed class GetProductsPageHandler(AppDbContext db)
    : Fluents.Queries.IPageHandler<GetProductsPage, ProductDto>
{
    public Task<IPagedList<ProductDto>> OnHandle(GetProductsPage request, CancellationToken cancellationToken) =>
        db.Products.AsNoTracking()
            .Select(p => new ProductDto(p.Id, p.Name, p.Price))
            .ToPagedListAsync(request.PageNumber, request.PageSize, null, cancellationToken);
}
```

**Notes**: `ToPagedListAsync`'s `totalSetCount` parameter is `null` here, which runs a count query; pass
a precomputed count to skip it. `pageNumber` is 1-based — `ToPagedListAsync` throws
`ArgumentOutOfRangeException` for a value below `1` — so a 0-based UI page index must be incremented
before this call.

### Event consumer and domain-event publishing

```csharp
using DKNet.SlimBus.Extensions;

public class ProductCreatedConsumer : Fluents.EventsConsumers.IHandler<ProductCreatedEvent>
{
    public Task OnHandle(ProductCreatedEvent message, CancellationToken cancellationToken) =>
        Task.CompletedTask; // e.g. send a notification
}

// Registration (in addition to AddSlimBusEfCoreInterceptor<AppDbContext>()):
// services.AddSlimBusEventPublisher<AppDbContext>();
```

**Notes**: `ProductCreatedEvent` must be raised by an aggregate via `AddEvent(...)` and reaches
`SlimBusEventPublisher.PublishAsync` only after a successful `SaveChangesAsync` — the publisher itself
does not decide when it runs. The consumer class is discovered by SlimMessageBus's own
`AddServicesFromAssembly`, not by anything in this package.

### Deferring a Mapster mapping until read

```csharp
using DKNet.SlimBus.Extensions;
using DKNet.SlimBus.Extensions.LazyMapper;
using FluentResults;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;

public record RenameProduct(Guid ProductId, string Name) : Fluents.Requests.IWitResponse<ProductDto>;

internal sealed class RenameProductHandler(AppDbContext db, IMapper mapper)
    : Fluents.Requests.IHandler<RenameProduct, ProductDto>
{
    public async Task<IResult<ProductDto>> OnHandle(RenameProduct request, CancellationToken cancellationToken)
    {
        var product = await db.Products.FindAsync([request.ProductId], cancellationToken);
        if (product is null)
            return Result.Fail<ProductDto>(new NotFoundError($"Product '{request.ProductId}' was not found."));

        product.Rename(request.Name);
        return mapper.ResultOf<ProductDto>(product);
    }
}
```

**Notes**: requires an `IMapper` (Mapster's `MapsterMapper.IMapper`) registered in the container (e.g.
`services.AddSingleton(TypeAdapterConfig.GlobalSettings); services.AddScoped<IMapper, ServiceMapper>();`
from `Mapster`/`Mapster.DependencyInjection`). `mapper.ResultOf<T>(value)` always returns
`IsSuccess == true` — return `Result.Fail<T>(...)` explicitly for the not-found case, as above, rather
than routing it through the mapper.

### Subclassing the event publisher

```csharp
using DKNet.SlimBus.Extensions.Handlers;
using SlimMessageBus;

public sealed class AuditingEventPublisher(IMessageBus bus) : SlimBusEventPublisher(bus)
{
    public override Task PublishAsync(object eventObj, CancellationToken cancellationToken = default) =>
        base.PublishAsync(eventObj, cancellationToken); // e.g. log before forwarding
}

// Registration — call AddEventPublisher directly; do NOT also call AddSlimBusEventPublisher<AppDbContext>()
// (that always registers the base SlimBusEventPublisher as a second, separate IEventPublisher):
// services.AddEventPublisher<AppDbContext, AuditingEventPublisher>();
```

**Notes**: both `PublishAsync` overloads on `SlimBusEventPublisher` are `virtual`, so either can be
overridden independently. The constructor throws `ArgumentNullException` if `bus` is `null`.

## Runtime behaviour

For one write request (`Fluents.Requests.INoResponse` or `IWitResponse<T>`) sent through
`IMessageBus.Send(...)`, in order:

1. SlimMessageBus resolves your handler and any registered `IRequestHandlerInterceptor<,>` chain from
   DI, in **registration order** — this interceptor kind is never sorted by
   `IInterceptorWithOrder.Order` (unlike interceptor kinds with a single generic parameter, which are).
   The first-registered interceptor is outermost and decides when to call `next()`.
   `EfAutoSavePostInterceptor<,>` ends up outermost only because `AddSlimBusEfCoreInterceptor<TDbContext>()`'s
   one-time guard keeps it the first (normally only) `IRequestHandlerInterceptor<,>` registered, provided
   you call it before registering your own. Your handler runs first, inside `next()`.
2. `EfAutoSavePostInterceptor<TRequest, TResponse>.OnHandle` awaits `next()` for the handler's response.
3. If `response is null`, or `response is IResultBase { IsSuccess: false }`, it returns immediately — no
   save, no exception-handler lookup.
4. It checks a `static readonly bool IsWrite` field (computed once via reflection on `TRequest` against
   `INoResponse`/`IWitResponse<>`); if `false` (a query, or a raw SlimMessageBus request not implementing
   `Fluents.*`), it returns without saving.
5. Otherwise it resolves `IEfCoreExceptionHandler` (unkeyed, "global") from the container, enumerates
   every `IAutoSaveDbContextRegistration` registered on this provider, resolves each `DbContext`
   instance from the current scope, and filters to those where `ChangeTracker.HasChanges()`.
6. For each such context, in registration order: `await db.AddNewEntitiesFromNavigations(cancellationToken)`
   then `await db.SaveChangesWithConcurrencyHandlingAsync(contextExceptionHandler ?? exceptionHandler, cancellationToken)`,
   where `contextExceptionHandler` is a keyed `IEfCoreExceptionHandler` resolved by the `DbContext`'s
   `GetType().FullName`. Contexts save one at a time — there is no shared transaction across them.
7. Each `SaveChangesAsync` inside that call, if the `DbContext` is hooked via `DKNet.EfCore.Events`,
   dispatches its `EventHook`, which collects the events the aggregates raised and — if
   `AddSlimBusEventPublisher<TDbContext>()` was called — hands them one at a time to
   `SlimBusEventPublisher.PublishAsync`, which calls `IMessageBus.Publish` (copying
   `IEventItem.AdditionalData` into case-insensitive headers when the event implements `IEventItem`).
8. The interceptor returns the original `response` unchanged to the caller of `IMessageBus.Send(...)`.

## Diagnostics & exceptions

| Type | Severity | When | Fix |
|---|---|---|---|
| `ArgumentNullException` | Error | `new SlimBusEventPublisher(bus)` called with `bus == null` | Resolve `IMessageBus` from a container where SlimMessageBus is registered before constructing the publisher directly. |
| `InvalidOperationException` | Error | `ILazyMap<T>.Value` (or `mapper.LazyMap<T>(...).Value`) read when the wrapped `originalValue` was `null` | Read `ValueOrDefault` instead if the source may be `null`, or check before calling `.Value`. |
| `DbUpdateConcurrencyException` (not thrown by this package; surfaces through `SaveChangesWithConcurrencyHandlingAsync`) | depends on `IEfCoreExceptionHandler.HandlingAsync`'s resolution (`RethrowException`/`RetrySaveChanges`/`IgnoreChanges`) | a concurrency-token mismatch during the auto-save's `SaveChangesAsync` | Register an `IEfCoreExceptionHandler` (global, or keyed by the `DbContext`'s `FullName`) that resolves the conflict the way your app needs. |

No analyzers or source generators ship in this package — that belongs to `DKNet.SlimBus.Generators`.

## Gotchas

- **Registering your own `IRequestHandlerInterceptor<,>` *before* calling
  `AddSlimBusEfCoreInterceptor<TDbContext>()` silently disables auto-save.** The guard checks the open
  generic service type, not a specific implementation, so *any* prior registration of that interface
  short-circuits it. → Call `AddSlimBusEfCoreInterceptor<TDbContext>()` first.
- **The `DbContext` auto-save registry is per `IServiceCollection`/`IServiceProvider`, not
  process-wide.** A test fixture or host that builds a second provider without calling
  `AddSlimBusEfCoreInterceptor<TDbContext>()` on it again never auto-saves that context, even if another
  provider in the same process registered it. → Call it on every provider you build.
- **No cross-`DbContext` transaction.** Contexts are saved one at a time in a loop; if a later one
  throws, earlier saves have already committed. → Wrap the handler in your own ambient transaction if
  you need atomicity across multiple `DbContext`s.
- **`Fluents.Requests.IWitResponse<T>` and `Fluents.Queries.IWitResponse<T>` are different interfaces
  with the identical simple name in different nested static classes.** An unqualified `using` or a
  copy-pasted type name can silently implement the wrong contract. → Always qualify as
  `Fluents.Requests.IWitResponse<T>` / `Fluents.Queries.IWitResponse<T>`.
- **`mapper.ResultOf<T>(value)` always constructs a result whose `Reasons` list starts and stays
  empty.** `IsSuccess` is always `true` and `IsFailed` always `false` for anything built through the
  public `ResultOf<T>` extension — there is no way to hand back a failure through it. → Use
  `Result.Fail<T>(...)`/`Result.Ok(...)` for anything that can fail; reserve `ResultOf<T>`/`LazyMap<T>`
  for guaranteed-success paths.
- **A query handler (`Fluents.Queries.*`) that mutates the `DbContext` is never saved — by design.** The
  `IsWrite` check only matches `INoResponse`/`IWitResponse<>`. → Only mutate inside
  `Fluents.Requests.*` handlers.
- **A handler implementing raw SlimMessageBus `IRequestHandler<,>` (bypassing `Fluents.Requests.*`) is
  never recognized as a write.** Silent no-save, same symptom as the interceptor-order trap above, a
  different cause. → Always implement `Fluents.Requests.IHandler<TRequest>`/`IHandler<TRequest,TResponse>`,
  never `IRequestHandler<,>` directly, for a write.
- **Auto-save happens *after* the handler returns, inside the interceptor.** A
  `DbUpdateException`/`DbUpdateConcurrencyException` from the save surfaces from the interceptor, not
  the handler — a handler-local `try`/`catch` around its own logic never sees it. → Handle persistence
  failures via a registered `IEfCoreExceptionHandler`, not handler-local `try`/`catch`.

## Anti-patterns & hallucination traps

- Calling `db.SaveChangesAsync()` inside a `Fluents.Requests.*` handler — redundant with the auto-save
  interceptor and works against `SaveChangesWithConcurrencyHandlingAsync`'s own concurrency-retry path.
  Don't call it; let the interceptor save.
- `Fluents.Requests.IHandler<TRequest>` / `IHandler<TRequest, TResponse>` are not MediatR's
  `IRequestHandler<TRequest>` / `IRequestHandler<TRequest, TResponse>` — there is no `Handle` method,
  the method is `OnHandle`, and the constraint is `TRequest : INoResponse` / `IWitResponse<TResponse>`.
- There is no `SlimBusEfCoreSetup.AddSlimBusEfCore(...)` single "do everything" call, no
  `IServiceCollection.AddSlimBus()`, and no options type (`SlimBusEfCoreOptions`,
  `SlimBusExtensionsOptions`, etc.) — the entire registration surface is exactly
  `AddSlimBusEfCoreInterceptor<TDbContext>()` and `AddSlimBusEventPublisher<TDbContext>()`.
- `EfAutoSavePostInterceptor<,>`, `AutoSaveDbContextRegistration<TDbContext>`, and
  `IAutoSaveDbContextRegistration` are `internal` — you cannot resolve, subclass, or directly register
  them; the only way in is the two `AddSlimBus*` extension methods.
- `LazyMap<TResult>` and `LazyResult<TResult>` (the concrete classes) are `internal` — a consumer only
  ever sees `ILazyMap<TResult>` (from `mapper.LazyMap<T>(...)`) or `IResult<TResult>` (from
  `mapper.ResultOf<T>(...)`); do not attempt `new LazyMap<T>(...)` from outside the assembly.
- `Fluents.Requests.IWithKey<TKey>` is a marker/carrier interface (`TKey Id { get; set; }`), not a base
  request type and not a validator — it lets shared code (e.g. route-binding in
  `DKNet.SlimBus.Generators`' generated update/action requests) read the key generically.
- `NotFoundError` is a `FluentResults.Error` subclass, not an `Exception`. Don't `throw new
  NotFoundError(...)`; return it via `Result.Fail(new NotFoundError(...))` or `Result.Fail<T>(...)`.
- `Fluents.Queries.IHandler<TQuery, TResponse>` returns `Task<TResponse?>`, never
  `Task<IResult<TResponse>>` — don't wrap a query's response in `Result.Ok(...)`.

## Composes with

| Package | Rule |
|---|---|
| `DKNet.EfCore.Events` | Source of the domain events this package forwards; `AddSlimBusEventPublisher<TDbContext>()` is the bridge from its `EventHook` to `IMessageBus.Publish`. Reach for it to raise events from aggregates via `AddEvent(...)` in the first place. |
| `DKNet.EfCore.Abstractions` | Supplies the `IEventItem`/`IEventPublisher` contracts `SlimBusEventPublisher` implements. |
| `DKNet.EfCore.Extensions` | Supplies `AddNewEntitiesFromNavigations` and `SaveChangesWithConcurrencyHandlingAsync`, the two calls the auto-save interceptor makes, plus `IEfCoreExceptionHandler`/`EfConcurrencyResolution`. |
| `DKNet.EfCore.Specifications` | The recommended query/mutation surface inside handlers — auto-save doesn't care how entities changed, only that `ChangeTracker.HasChanges()`. |
| `DKNet.SlimBus.Generators` | Emits requests/handlers built on exactly these `Fluents` interfaces for `[CrudAction]`-annotated aggregate methods — generate CRUD slices instead of hand-writing them against this package's contracts. |
| `DKNet.AspCore.Extensions` | Supplies `[FromClaim]`/`IContextualSource` + `AddContextualRequestPopulation()` to stamp an acting-user property on a request before the handler runs; its `ProblemDetailsExtensions` recognizes `NotFoundError` and maps it to HTTP 404. |
| `Mapster` / `MapsterMapper` | Required in the container for `ILazyMap<T>`/`ResultOf<T>` — register an `IMapper` (e.g. via `Mapster.DependencyInjection`'s `ServiceMapper`). |
| `SlimMessageBus.Host.*` (Memory, Azure Service Bus, Kafka, …) | Bring exactly one transport provider yourself; this package is transport-agnostic and ships none. |

## Testing notes

- The package's own tests use `Microsoft.EntityFrameworkCore.InMemory` plus `SlimMessageBus.Host.Memory`
  — an in-process bus and in-memory store — because they unit-test the interceptor/registration logic
  itself, not SQL behavior.
- The canonical fixture builds one `ServiceProvider` with `.AddLogging()`, Mapster's
  `TypeAdapterConfig.GlobalSettings` + `ServiceMapper`, `AddDbContext<TestDbContext>(UseInMemoryDatabase)`,
  `AddSlimBusEfCoreInterceptor<TestDbContext>()`, and `AddSlimMessageBus(mbb => mbb.AddJsonSerializer()
  .AddServicesFromAssembly(...).AddChildBus("...", mb => mb.WithProviderMemory().AutoDeclareFrom(...)))`
  — reused by every test in the class via `IClassFixture<TFixture>`.
- The canonical auto-save assertion pattern: flip a static flag inside an overridden `SaveChangesAsync`
  on the test `DbContext`, send a request through `IMessageBus.Send(...)`, then assert the flag.
- A registry-isolation test pattern: build a **fresh** `ServiceCollection`/`ServiceProvider` per scenario
  (not the shared fixture) when asserting per-container isolation or concurrent-registration safety, so
  each scenario's registrations can't leak into another's.
- A `private static readonly` cache (like the `IsWrite` flag) is asserted "computed once" via reflection
  on the field, without exposing it publicly — a reusable pattern for testing a similar cache elsewhere.
