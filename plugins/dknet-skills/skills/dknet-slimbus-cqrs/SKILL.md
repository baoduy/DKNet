---
name: dknet-slimbus-cqrs
description: "Covers DKNet.SlimBus.Extensions: the Fluents.Requests/Fluents.Queries/Fluents.EventsConsumers contracts (INoResponse, IWitResponse<T>, IWitPageResponse<T>, IHandler<,>, IWithKey<T>), NotFoundError, and FluentResults Result/IResultBase conventions inside a CQRS handler. Explains AddSlimBusEfCoreInterceptor's auto-SaveChanges-after-handler rule (why a CQRS handler calling SaveChangesAsync twice is wrong), AddSlimBusEventPublisher forwarding domain events onto the bus, and wiring SlimMessageBus itself (AddSlimMessageBus, the memory provider) since DKNet ships no transport or validation pipeline. Also covers Aspire.Hosting.ServiceBus's AddServiceBus Azure Service Bus emulator resource for an AppHost. Use for SlimMessageBus with EF Core, IRequestHandler-shaped handlers, ICommand/IQuery/IEvent handler DKNet, or a write that silently never gets persisted."
license: MIT
metadata:
  author: baoduy
  version: "0.1.0"
  packages: "DKNet.SlimBus.Extensions, Aspire.Hosting.ServiceBus"
---

# DKNet CQRS with SlimMessageBus

`DKNet.SlimBus.Extensions` is a thin CQRS adapter over SlimMessageBus: a fluent, `FluentResults`-based
shape for requests/queries/event consumers, plus one interceptor that saves your `DbContext` after a
successful write so handlers never call `SaveChangesAsync` themselves. `Aspire.Hosting.ServiceBus` is
an unrelated, AppHost-only package that provisions the Azure Service Bus emulator container for local
dev. This skill teaches both. Depth lives in `references/DKNet.SlimBus.Extensions.md` and
`references/Aspire.Hosting.ServiceBus.md` — read the relevant one before writing non-trivial code.

## Packages

| Package | Install | What it gives you | Depends on | Reference |
|---|---|---|---|---|
| `DKNet.SlimBus.Extensions` | `dotnet add package DKNet.SlimBus.Extensions` | `Fluents.Requests`/`Fluents.Queries`/`Fluents.EventsConsumers` handler contracts, the auto-save interceptor, the domain-event-to-bus bridge | `DKNet.EfCore.Events` (→ `DKNet.EfCore.Abstractions`, `DKNet.EfCore.Extensions`); `FluentResults`, `Mapster`, `Microsoft.Extensions.Hosting.Abstractions`, `SlimMessageBus`/`.Host`/`.Host.Interceptor`, `X.PagedList.EF` | `references/DKNet.SlimBus.Extensions.md` |
| `Aspire.Hosting.ServiceBus` | ProjectReference only — `IsPackable=false`, not published to NuGet | `AddServiceBus`: registers the Azure Service Bus emulator as a `ContainerResource` in an Aspire AppHost | `Aspire.Hosting`, `Aspire.Hosting.SqlServer` (a SQL Server resource is mandatory) | `references/Aspire.Hosting.ServiceBus.md` |

Neither package brings a SlimMessageBus transport or a validation pipeline — add `SlimMessageBus.Host.Memory`
(dev/tests) or a real `SlimMessageBus.Host.*` provider, and your own `IRequestHandlerInterceptor<,>` if needed.

## Quick start

The smallest complete wiring — an in-memory `DbContext`, the auto-save interceptor, SlimMessageBus on
the memory provider — plus one real use: a write that returns a value. Every later recipe in this
skill reuses `Product`/`AppDbContext`/`ProductDto` declared here.

```csharp
using Microsoft.EntityFrameworkCore;
using SlimMessageBus;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.Serialization.SystemTextJson;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase("products"));

builder.Services
    .AddSlimBusEfCoreInterceptor<AppDbContext>() // register before any custom IRequestHandlerInterceptor<,>
    .AddSlimMessageBus(mbb => mbb
        .AddJsonSerializer()
        .AddServicesFromAssembly(typeof(CreateProductHandler).Assembly)
        .AddChildBus("Memory", mb => mb.WithProviderMemory().AutoDeclareFrom(typeof(CreateProductHandler).Assembly)));

var app = builder.Build();

app.MapPost("/products", async (CreateProduct request, IMessageBus bus, CancellationToken ct) =>
{
    var result = await bus.Send(request, cancellationToken: ct);
    return result.IsSuccess
        ? Results.Created($"/products/{result.Value}", result.Value)
        : Results.BadRequest(result.Errors);
});

app.Run();
```

```csharp
using DKNet.EfCore.Abstractions.Entities;
using DKNet.SlimBus.Extensions;
using FluentResults;
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
        AddEvent(new ProductCreatedEvent(Id)); // queued only — nothing dispatches it until events are wired
    }

    public string Name { get; private set; } = string.Empty;

    public decimal Price { get; private set; }

    public bool IsActive { get; private set; }

    public void Deactivate() => IsActive = false;

    public void ChangePrice(decimal price) => Price = price;
}

public sealed record ProductCreatedEvent(Guid ProductId);

public sealed record ProductDto(Guid Id, string Name, decimal Price);

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
}

public sealed record CreateProduct(string Name, decimal Price) : Fluents.Requests.IWitResponse<Guid>;

internal sealed class CreateProductHandler(AppDbContext db) : Fluents.Requests.IHandler<CreateProduct, Guid>
{
    public async Task<IResult<Guid>> OnHandle(CreateProduct request, CancellationToken cancellationToken)
    {
        var product = new Product(request.Name, request.Price);
        await db.Products.AddAsync(product, cancellationToken);
        return Result.Ok(product.Id);
        // No SaveChangesAsync here — EfAutoSavePostInterceptor saves AppDbContext after this returns success.
    }
}
```

**Notes**: `AddSlimBusEfCoreInterceptor<TDbContext>()` resolves via the ambient
`Microsoft.Extensions.DependencyInjection` namespace — no `DKNet.SlimBus.Extensions` import is needed for
the wiring, only for `Fluents`/`NotFoundError` in handler code. Swap `SlimMessageBus.Host.Memory` for a
real transport in production. `IMessageBus.Send`'s second positional parameter is a routing `path`
(`string`), not a `CancellationToken` — pass the token as `cancellationToken: ct`.

## Rules

1. **Call `AddSlimBusEfCoreInterceptor<TDbContext>()` before registering any other
   `IRequestHandlerInterceptor<,>`.** Its guard matches *any* prior registration of the open generic
   interface, yours or another package's — after that it no-ops silently and auto-save never happens.
2. **Never call `SaveChangesAsync`/`SaveChanges` inside a `Fluents.Requests.*` handler.** The interceptor
   saves every registered `DbContext` with pending changes once the handler returns success; a
   handler-local call is redundant and fights `SaveChangesWithConcurrencyHandlingAsync`'s own retry.
3. **Implement a write as `Fluents.Requests.INoResponse` or `Fluents.Requests.IWitResponse<T>`.** A raw
   SlimMessageBus `IRequestHandler<,>`, or a request implementing neither, is invisible to the
   interceptor's `IsWrite` check and is never saved.
4. **Return `Result.Fail(...)` to skip the save entirely.** No partial state persists; only
   `Result.Ok()`/`Result.Ok(value)` triggers auto-save.
5. **Use the generic `Result.Fail<TValue>(error)` inside a handler declared `Task<IResult<TValue>>`.**
   The non-generic `Result.Fail(error)` returns `Result`, which does not implement `IResult<TValue>`.
6. **Never mutate the `DbContext` inside a `Fluents.Queries.*` handler expecting it to persist.** Queries
   are never auto-saved, regardless of what the handler does to the change tracker.
7. **Qualify `Fluents.Requests.IWitResponse<T>` and `Fluents.Queries.IWitResponse<T>` fully.** Same
   simple name, different nested static class, different contract (`IResult<T>` vs `T?`).
8. **Register `TDbContext` on every `IServiceCollection`/`IServiceProvider` you build a provider from.**
   The auto-save registry is per-container, not process-wide.
9. **Handle a `DbUpdateConcurrencyException` from the auto-save via a registered
   `IEfCoreExceptionHandler`, not a handler-local `try`/`catch`.** The save runs after the handler returns.
10. **Bring your own SlimMessageBus transport and validation.** `DKNet.SlimBus.Extensions` ships no
    `SlimMessageBus.Host.*` provider and no validation pipeline — add one (e.g.
    `SlimMessageBus.Host.FluentValidation`) yourself if you need it.
11. **To subclass `SlimBusEventPublisher`, register it via `DKNet.EfCore.Events`' own
    `AddEventPublisher<TDbContext, TYourType>()`**, not `AddSlimBusEventPublisher<TDbContext>()` — the
    latter always registers the base type, and calling both adds two publishers.
12. **Never hand-write a request/handler/endpoint that `DKNet.SlimBus.Generators` already emits** for
    `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]` — override only the handler, by declaring one with the
    exact generated request-type name (see `dknet-codegen`).

## How to

### Accept a write with no return value

When a command only needs to succeed or fail, with nothing to hand back.

```csharp
using DKNet.SlimBus.Extensions;
using FluentResults;
using Microsoft.EntityFrameworkCore;

public sealed record DeactivateProduct(Guid ProductId) : Fluents.Requests.INoResponse;

internal sealed class DeactivateProductHandler(AppDbContext db) : Fluents.Requests.IHandler<DeactivateProduct>
{
    public async Task<IResultBase> OnHandle(DeactivateProduct request, CancellationToken cancellationToken)
    {
        var product = await db.Products.FindAsync([request.ProductId], cancellationToken);
        if (product is null)
            return Result.Fail(new NotFoundError($"Product '{request.ProductId}' was not found."));

        product.Deactivate();
        return Result.Ok();
    }
}
```

**Notes**: the non-generic `Result.Fail(...)` is correct here because `OnHandle` returns
`Task<IResultBase>`, not a generic `IResult<T>` (contrast with the next recipe). `NotFoundError` is a
`FluentResults.Error` subclass — return it, never `throw` it.

### Fetch via IRepositorySpec, mutate the aggregate, and return a mapped result

When the handler needs to load the aggregate first, and the response is a DTO, not the write's own
input. This is also the shape a generated `[CrudUpdate]` handler takes if you override it (see
`dknet-codegen`) — `IWithKey<TKey>` is the marker the generator's route binding relies on.

```csharp
using DKNet.EfCore.Specifications;
using DKNet.EfCore.Specifications.Definitions;
using DKNet.EfCore.Specifications.Extensions;
using DKNet.EfCore.Specifications.Repositories;
using DKNet.SlimBus.Extensions;
using DKNet.SlimBus.Extensions.LazyMapper;
using FluentResults;
using Mapster;
using MapsterMapper;

public sealed record ChangePriceProductRequest(decimal Price)
    : Fluents.Requests.IWitResponse<ProductDto>, Fluents.Requests.IWithKey<Guid>
{
    public Guid Id { get; set; }
}

internal sealed class ProductByIdSpec : Specification<Product>
{
    public ProductByIdSpec(Guid id) => WithFilter(p => p.Id == id);
}

internal sealed class ChangePriceProductHandler(IRepositorySpec repository, IMapper mapper)
    : Fluents.Requests.IHandler<ChangePriceProductRequest, ProductDto>
{
    public async Task<IResult<ProductDto>> OnHandle(ChangePriceProductRequest request, CancellationToken cancellationToken)
    {
        var product = await repository.FirstOrDefaultAsync(new ProductByIdSpec(request.Id), cancellationToken);
        if (product is null)
            return Result.Fail<ProductDto>(new NotFoundError($"Product '{request.Id}' was not found."));

        product.ChangePrice(request.Price);
        return mapper.ResultOf<ProductDto>(product); // lazily mapped; always IsSuccess == true
    }
}

// Extra registration this recipe needs on top of the Quick start wiring.
public static class ProductSpecRepoSetup
{
    public static IServiceCollection AddProductSpecRepo(this IServiceCollection services) =>
        services.AddSpecRepo<AppDbContext>()
            .AddSingleton(TypeAdapterConfig.GlobalSettings)
            .AddScoped<IMapper, ServiceMapper>();
}
```

**Notes**: needs `AddSpecRepo<AppDbContext>()` and a Mapster `IMapper` registered — see
`dknet-efcore-specifications` for the rest of `IRepositorySpec`. `.ResultOf<T>` can never carry a failure
reason; use `Result.Fail<T>(...)` for anything that can fail, as above.

### Answer a query that must never trigger a save

When the handler only reads, and any accidental mutation must not be persisted.

```csharp
using DKNet.SlimBus.Extensions;
using Microsoft.EntityFrameworkCore;

public sealed record GetProduct(Guid Id) : Fluents.Queries.IWitResponse<ProductDto>;

internal sealed class GetProductHandler(AppDbContext db) : Fluents.Queries.IHandler<GetProduct, ProductDto>
{
    public async Task<ProductDto?> OnHandle(GetProduct request, CancellationToken cancellationToken)
    {
        var product = await db.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
        return product is null ? null : new ProductDto(product.Id, product.Name, product.Price);
    }
}
```

**Notes**: the return type is `TResponse?`, never a `FluentResults` wrapper — don't wrap it in
`Result.Ok(...)`. Even a handler that mistakenly mutates the change tracker here is never saved; the
interceptor's `IsWrite` check is `false` for anything implementing `Fluents.Queries.*`.

### Return a paged query result

When a list endpoint needs page metadata (total count, has-next) rather than every row.

```csharp
using DKNet.SlimBus.Extensions;
using Microsoft.EntityFrameworkCore;
using X.PagedList;
using X.PagedList.EF;

public sealed record GetProductsPage(int PageNumber, int PageSize) : Fluents.Queries.IWitPageResponse<ProductDto>;

internal sealed class GetProductsPageHandler(AppDbContext db)
    : Fluents.Queries.IPageHandler<GetProductsPage, ProductDto>
{
    public Task<IPagedList<ProductDto>> OnHandle(GetProductsPage request, CancellationToken cancellationToken) =>
        db.Products.AsNoTracking()
            .Select(p => new ProductDto(p.Id, p.Name, p.Price))
            .ToPagedListAsync(request.PageNumber, request.PageSize, null, cancellationToken);
}
```

**Notes**: `pageNumber` is 1-based and throws `ArgumentOutOfRangeException` below `1` — increment a
0-based UI page index first. Passing `null` for `totalSetCount` runs an extra count query.

### Forward a domain event raised by an aggregate onto the bus

When code elsewhere in the app should react to `Product`'s constructor having called
`AddEvent(new ProductCreatedEvent(...))`, once the row actually commits.

```csharp
using DKNet.SlimBus.Extensions;

public class ProductCreatedConsumer : Fluents.EventsConsumers.IHandler<ProductCreatedEvent>
{
    public Task OnHandle(ProductCreatedEvent message, CancellationToken cancellationToken) => Task.CompletedTask;
}

// Extra registration this recipe needs on top of the Quick start wiring.
public static class ProductEventForwardingSetup
{
    public static IServiceCollection AddProductEventForwarding(this IServiceCollection services) =>
        services.AddSlimBusEventPublisher<AppDbContext>();
}
```

**Notes**: `AddSlimBusEventPublisher<AppDbContext>()` is independent of `AddSlimBusEfCoreInterceptor` —
call both. Without it, `AddEvent(...)` still queues the event, but nothing drains or publishes it. The
consumer is discovered by SlimMessageBus's own `AddServicesFromAssembly`.

### Provision the Aspire Service Bus emulator for local development

When an AppHost needs a local Azure Service Bus for the API project above to send/consume against,
without a real Azure namespace.

```csharp
using Aspire.Hosting;
using Aspire.Hosting.ServiceBus;

public static class AppHostSetup
{
    public static void ConfigureServiceBus(string[] args)
    {
        var builder = DistributedApplication.CreateBuilder(args);
        var sql = builder.AddSqlServer("sql");
        var serviceBus = builder.AddServiceBus(sql, configFilePath: "servicebus-config.json");

        // The API project (a separate project, referenced from the AppHost) reads the connection
        // string the normal Aspire way: builder.AddProject<Projects.Api>("api").WithReference(serviceBus);
        builder.Build().Run();
    }
}
```

**Notes**: `AddServiceBus` always requires and waits for a SQL Server resource. All queue/topic/
subscription topology goes in the `Config.json` at `configFilePath`; there is no fluent
`WithQueue`/`WithTopic` API. Custom resource names, `PrimaryEndpoint`, and real-namespace redirection
are in `references/Aspire.Hosting.ServiceBus.md`.

## Runtime behaviour

For one write (`Fluents.Requests.INoResponse`/`IWitResponse<T>`) sent through `IMessageBus.Send(...)`:

1. SlimMessageBus resolves your handler and the `IRequestHandlerInterceptor<,>` chain from DI in
   **registration order** (this interceptor kind is never sorted by `IInterceptorWithOrder.Order`).
   Your handler runs first, inside `next()`.
2. `EfAutoSavePostInterceptor<,>` awaits `next()` for the handler's response; a `null` response or
   `IResultBase.IsSuccess == false` returns immediately — no save.
3. Otherwise it checks a cached `IsWrite` flag (`true` only for `Fluents.Requests.*`); a query or a raw
   SlimMessageBus request returns without saving.
4. It enumerates every `DbContext` registered via `AddSlimBusEfCoreInterceptor<TDbContext>()` in this
   scope, keeps the ones with `ChangeTracker.HasChanges()`, and for each calls
   `AddNewEntitiesFromNavigations` then `SaveChangesWithConcurrencyHandlingAsync` — one context at a
   time, no shared transaction across contexts.
5. Each such `SaveChangesAsync`, if hooked via `DKNet.EfCore.Events`, dispatches queued `AddEvent(...)`
   events; if `AddSlimBusEventPublisher<TDbContext>()` was called, each goes to
   `SlimBusEventPublisher.PublishAsync` → `IMessageBus.Publish`.
6. The interceptor returns the handler's original response unchanged to the `Send(...)` caller.

Aspire startup order is separate: `AddServiceBus` chains `WaitFor(sqlServer)`, so the emulator does not
start until SQL Server is ready. When Aspire later resolves the connection string, a subscribed handler
calls `GetConnectionStringAsync`; a `null` result throws `DistributedApplicationException` there — not
inside `AddServiceBus(...)` itself — failing AppHost startup fast.

## Gotchas

- **The auto-save registry is per `IServiceCollection`/`IServiceProvider`, not process-wide.** A second
  provider built without its own `AddSlimBusEfCoreInterceptor<TDbContext>()` call never auto-saves that
  context, even if another provider in the same process already registered it.
- **A handler implementing raw SlimMessageBus `IRequestHandler<,>` (bypassing `Fluents.Requests.*`) is
  never recognized as a write** — same silent-no-save symptom as registering a custom interceptor first,
  different cause.
- **Auto-save happens after the handler returns, inside the interceptor.** A `DbUpdateException`/
  `DbUpdateConcurrencyException` from the save surfaces there, not in the handler — a handler-local
  `try`/`catch` never sees it; use a registered `IEfCoreExceptionHandler` instead.
- **`mapper.ResultOf<T>(value)` always succeeds — it can never carry a failure.** Its `Reasons` list
  starts and stays empty; reserve it for guaranteed-success paths.
- **Non-generic `Result.Fail(error)` does not satisfy a value-returning handler.** It returns `Result`,
  not `Result<TValue>`/`IResult<TValue>` — a `Task<IResult<TValue>>` handler needs `Result.Fail<TValue>`.
- **The interceptor "already registered" guard matches *any* prior `IRequestHandlerInterceptor<,>`
  registration**, not only its own — a custom interceptor registered first silently blocks auto-save.
- **`AddServiceBus` always requires and `WaitFor`s a `SqlServerServerResource`** — no overload without
  one, and the image tag (`latest`) and both AMQP ports are hard-coded, so only one emulator per AppHost.
- **The null-connection-string guard throws from an event handler, not the `AddServiceBus(...)` call
  site** — a `try`/`catch` around that call never sees it; let AppHost startup fail fast as designed.
- **No health check is registered by `AddServiceBus`** — a `WaitFor` against it before it can actually
  accept connections will race; add your own health check for readiness gating.

## Do not

- `SlimBusEfCoreSetup.AddSlimBusEfCore(...)`, `IServiceCollection.AddSlimBus()`, `SlimBusEfCoreOptions` —
  none exist. The entire registration surface is exactly `AddSlimBusEfCoreInterceptor<TDbContext>()` and
  `AddSlimBusEventPublisher<TDbContext>()`.
- `Fluents.Requests.IHandler<TRequest>.Handle(...)` — there is no `Handle` method and this is not
  MediatR's `IRequestHandler<TRequest>`. The method is `OnHandle`.
- `new LazyMap<T>(...)`, `new LazyResult<T>(...)` — both concrete types are `internal`. Reach them only
  via `mapper.LazyMap<T>(value)` (`ILazyMap<T>`) or `mapper.ResultOf<T>(value)` (`IResult<T>`).
- `throw new NotFoundError(...)` — `NotFoundError` is a `FluentResults.Error` subclass, not an
  `Exception`. Return it: `Result.Fail(new NotFoundError(...))` or `Result.Fail<T>(...)`.
- `serviceBus.WithQueue(...)`, `.WithTopic(...)`, `.WithSubscription(...)` — no fluent topology API
  exists on `ServiceBusResource`/`ServiceBusExtensions`. Declare topology in the `Config.json` passed as
  `configFilePath`.
- `AddServiceBus(builder, configFilePath)` with no SQL Server argument, or called from an API/worker
  project — `sqlServer` is required, and this extension only exists on `IDistributedApplicationBuilder`
  (the AppHost).
- `ServiceBusResource.PrimaryEndpointName`/`SecondaryEndpointName` referenced from consumer code — both
  constants are `internal`; only `PrimaryEndpoint` (the property) is public.
- `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]` raising or wiring domain events — they don't;
  `AddEvent(...)` inside the marked entity method is ordinary domain code, unrelated to the generator.

```csharp
// no-compile
// Calling SaveChangesAsync inside a Fluents.Requests.* handler is redundant, not merely wasteful:
// EfAutoSavePostInterceptor saves AppDbContext again right after this returns, doubling the round trip
// and bypassing SaveChangesWithConcurrencyHandlingAsync's retry for the handler's own call.
internal sealed class BadCreateProductHandler(AppDbContext db) : Fluents.Requests.IHandler<CreateProduct, Guid>
{
    public async Task<IResult<Guid>> OnHandle(CreateProduct request, CancellationToken cancellationToken)
    {
        var product = new Product(request.Name, request.Price);
        await db.Products.AddAsync(product, cancellationToken);
        await db.SaveChangesAsync(cancellationToken); // the interceptor calls this again after OnHandle returns
        return Result.Ok(product.Id);
    }
}
```

## Related skills

- `dknet-packages` — package router and Program.cs setup order across all of DKNet. Start there first.
- `dknet-efcore-domain-model` — `Entity<TKey>`/`Entity`, `IEventEntity`, `UseAutoConfigModel`; read it
  for how `Product` above becomes a modeled aggregate, which this skill takes as a given.
- `dknet-efcore-specifications` — `Specification<TEntity>` and `IRepositorySpec`'s full surface (dynamic
  predicates, keyset pagination) beyond the one fetch-and-mutate shape shown here.
- `dknet-efcore-save-pipeline` — how `AddEvent(...)` is drained and dispatched (`EventHook`,
  `DKNet.EfCore.Events`) and the hooks/audit-log interceptors on the same `SaveChanges`; this skill only
  covers the SlimBus side (`AddSlimBusEventPublisher`).
- `dknet-efcore-data-security` — row-level data authorization and column encryption interceptors that
  also run inside the same auto-save `SaveChangesAsync`.
- `dknet-codegen` — `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]`/`[GenerateDto]` and generator
  diagnostics; read it before writing a CRUD slice by hand — this skill only covers overriding one.
- `dknet-aspcore-api` — `Map{Entity}Crud`'s minimal-API layer and how `Result`/`NotFoundError` become
  `ProblemDetails`; this skill only produces what that layer consumes.
- `dknet-idempotency` — making an endpoint that dispatches one of these requests safe under retries.
- `dknet-blob-storage` — blob storage abstraction and adapters, if a handler needs file storage.
- `dknet-services` — encryption, PDF, and template services a handler might call into.
- `dknet-core-utilities` — foundation helpers (`DKNet.Fw.Extensions`) and secure random strings
  (`DKNet.RandomCreator`).
- `dknet-testing` — TestContainers fixtures; the `SlimBus.Extensions.Tests` in-memory-bus fixture
  (`ServiceCollection` + `SlimMessageBus.Host.Memory` + EF Core InMemory) is a good model to copy.

## References

- `references/DKNet.SlimBus.Extensions.md` — full public surface, options, diagnostics, gotchas, and
  testing notes for the CQRS adapter package.
  https://github.com/baoduy/DKNet/blob/dev/docs/Messaging/DKNet.SlimBus.Extensions.md ·
  https://www.nuget.org/packages/DKNet.SlimBus.Extensions · https://baoduy.github.io/DKNet/
- `references/Aspire.Hosting.ServiceBus.md` — full public surface, options, and gotchas for the emulator
  AppHost extension (not published to NuGet — reference the project directly).
  https://github.com/baoduy/DKNet/blob/dev/docs/Aspire/Aspire.Hosting.ServiceBus.md ·
  https://baoduy.github.io/DKNet/
