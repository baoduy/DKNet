# DKNet.SlimBus.Extensions.EfCore

The EF Core add-on for [DKNet.SlimBus.Extensions](./DKNet.SlimBus.Extensions.md) — automatic `SaveChanges` after a
successful write request and domain events forwarded onto the bus.

## ✨ Why use it?

- **Handlers stop calling `SaveChangesAsync`.** A write request that returns a successful `IResult` gets its
  `DbContext` saved by an interceptor, using the same concurrency-aware save path as the rest of DKNet's EF Core stack.
- **Domain events reach the bus without handler code.** Events raised by aggregates through
  [DKNet.EfCore.Events](../DKNet.EfCore.Events/index.md) are published to `IMessageBus` after the save that made them
  true.
- **One call for the common case.** `AddSlimBusForEfCore<TDbContext>(assembly)` is the core MediatR-style setup plus
  auto-save plus event publishing.

If your handlers don't touch EF Core, use [DKNet.SlimBus.Extensions](./DKNet.SlimBus.Extensions.md) alone.

## 🚀 Quick Start

```bash
dotnet add package DKNet.SlimBus.Extensions.EfCore
```

It references `DKNet.SlimBus.Extensions` and `DKNet.EfCore.Events`, so the `Fluents` contracts come with it.

```csharp
using Microsoft.Extensions.DependencyInjection;

builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString));

// Memory bus + handler discovery + auto-save + domain events onto the bus.
builder.Services.AddSlimBusForEfCore<AppDbContext>(typeof(Program).Assembly);
```

That is equivalent to:

```csharp
services.AddSlimBusForMediator(typeof(Program).Assembly)   // DKNet.SlimBus.Extensions
    .AddSlimBusEfCoreInterceptor<AppDbContext>()            // auto-save
    .AddSlimBusEventPublisher<AppDbContext>();              // domain events → IMessageBus
```

Then send a command — the handler adds the entity, the interceptor saves it:

```csharp
var result = await bus.Send(new CreateProduct("Widget", 9.99m), cancellationToken);
if (result.IsSuccess) return TypedResults.Created($"/products/{result.Value}");
```

```csharp
internal sealed class CreateProductHandler(AppDbContext db)
    : Fluents.Requests.IHandler<CreateProduct, Guid>
{
    public async Task<IResult<Guid>> OnHandle(CreateProduct request, CancellationToken cancellationToken)
    {
        var product = new Product(request.Name, request.Price);
        await db.Products.AddAsync(product, cancellationToken);
        // No SaveChangesAsync call here — the auto-save interceptor does it after this returns Ok.
        return Result.Ok(product.Id);
    }
}
```

### A real transport, or more than one `DbContext`

`AddSlimBusForEfCore<T>` sits on the memory-only core setup. When you configure SlimMessageBus yourself (Azure Service
Bus, Kafka, child buses), call the two building blocks directly instead:

```csharp
using SlimMessageBus.Host;
using SlimMessageBus.Host.Serialization.SystemTextJson;

services.AddSlimBusEfCoreInterceptor<AppDbContext>();   // auto-save
services.AddSlimBusEventPublisher<AppDbContext>();      // optional: domain events onto the bus

services.AddSlimMessageBus(mbb => mbb
    .AddJsonSerializer()
    .AddServicesFromAssembly(typeof(Program).Assembly)
    .AddChildBus("Bus", bus => bus /* .WithProviderServiceBus(...) */
        .AutoDeclareFrom(typeof(Program).Assembly)));
```

For a second `DbContext`, add `AddSlimBusEfCoreInterceptor<OtherDbContext>()` (and
`AddSlimBusEventPublisher<OtherDbContext>()`) after `AddSlimBusForEfCore<AppDbContext>(...)` — never call
`AddSlimBusForEfCore` twice, because that sets up the bus twice.

## 🧩 Features

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
`AddSlimBusEfCoreInterceptor<TDbContext>()` (or `AddSlimBusForEfCore<TDbContext>()`).

![Sequence diagram of one write request: the auto-save interceptor wraps the handler and saves the DbContext when the result succeeded and the ChangeTracker has changes. That save publishes the raised events through SlimBusEventPublisher back onto IMessageBus.](../diagrams/slimbus-request-pipeline.svg)

Read the wrapping order from the diagram rather than the bullet list: because auto-save is registered last, the
handler's result passes through it on the way out, which is where the save — and therefore the event publish — actually
happens. Event publishing is a second, separate opt-in when you use the building blocks: without
`AddSlimBusEventPublisher<TDbContext>()` the save still runs and nothing reaches the bus.

### Domain event publishing

`AddSlimBusEventPublisher<TDbContext>()` registers `SlimBusEventPublisher` as an `IEventPublisher` for `TDbContext` via
`DKNet.EfCore.Events`' `AddEventPublisher<TDbContext, TImplementation>()`, which wires that package's event hook into
the context's save pipeline. After a successful `SaveChangesAsync`, the hook collects the events the aggregates raised
and hands each to the publisher, which forwards it to `IMessageBus.Publish`. When an event implements `IEventItem`, its
`AdditionalData` entries are copied onto the message as headers with case-insensitive keys. Consume them with
`Fluents.EventsConsumers.IHandler<TEvent>`.

`SlimBusEventPublisher` is public and both `PublishAsync` overloads are `virtual`, so you can subclass it — to stamp
extra headers, for example — and register the subclass instead:

```csharp
using DKNet.SlimBus.Extensions.Handlers;

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

## ⚙️ Configuration reference

There is no options type, no `IConfiguration` section, and no builder. What you can vary is which registration you
call and the `DbContext` you call it with.

### Registration surface (`SlimBusEfCoreSetup`)

| Method | Constraint | Registers | Repeat call |
|---|---|---|---|
| `AddSlimBusForEfCore<TDbContext>(params Assembly[] handlerAssemblies)` | `TDbContext : DbContext` | `AddSlimBusForMediator(handlerAssemblies)` + the two methods below | Not supported — once per host; add further contexts with the two methods below |
| `AddSlimBusEfCoreInterceptor<TDbContext>()` | `TDbContext : DbContext` | `IRequestHandlerInterceptor<,>` → the internal auto-save interceptor (Scoped) | Adds `TDbContext` to the save registry; the interceptor itself is registered only once |
| `AddSlimBusEventPublisher<TDbContext>()` | `TDbContext : DbContext` | `SlimBusEventPublisher` as `IEventPublisher` for `TDbContext`, via `DKNet.EfCore.Events` | Delegated to `AddEventPublisher` |

All three sit in the ambient `Microsoft.Extensions.DependencyInjection` namespace and are declared inside a C# 14
`extension(IServiceCollection)` block — a consumer at `LangVersion` 13 calls them as ordinary extension methods.

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
| `SlimBusEventPublisher` (`DKNet.SlimBus.Extensions.Handlers`) | `public class`, both `PublishAsync` overloads `virtual` | Subclass to add headers or logging, then register the subclass. |

The auto-save interceptor (`EfAutoSavePostInterceptor<,>`) and its `DbContext` registry are `internal`.

## 🧱 Where it fits

- **[DKNet.SlimBus.Extensions](./DKNet.SlimBus.Extensions.md)** — the EF-free core: `Fluents` contracts, lazy mapping,
  and the memory-bus setup this package builds on.
- **[DKNet.EfCore.Events](../DKNet.EfCore.Events/index.md)** — the source of the domain events this package forwards;
  `AddSlimBusEventPublisher<TDbContext>()` is the bridge between its hook and the bus.
- **[DKNet.EfCore.Abstractions](../DKNet.EfCore.Abstractions/index.md)** — supplies the `IEventItem` / `IEventPublisher`
  contracts `SlimBusEventPublisher` implements.
- **[DKNet.EfCore.Specifications](../DKNet.EfCore.Specifications/index.md)** — the recommended query/mutation surface
  inside handlers; auto-save doesn't care how entities were changed, only that the change tracker has changes.
- **[DKNet.SlimBus.Generators](./DKNet.SlimBus.Generators.md)** — generated `[CrudAction]` slices rely on auto-save, so
  their hosts reference this package.
- **`DKNet.EfCore.Extensions`** — provides `AddNewEntitiesFromNavigations` and
  `SaveChangesWithConcurrencyHandlingAsync`, the save primitives the interceptor calls.

## ⚠️ Gotchas & limits

- **Query handlers that mutate the `DbContext` are not saved.** Auto-save only fires for `INoResponse` /
  `IWitResponse<T>` requests — by design, but easy to trip over when a "read" handler writes.
- **Raw SlimMessageBus requests bypass auto-save entirely.** A handler implementing `IRequestHandler<,>` directly, not
  through `Fluents.Requests`, is never recognized as a write.
- **No cross-`DbContext` transaction.** Each registered context with pending changes is saved independently in a loop;
  if a later save throws, earlier ones have already committed.
- **The `DbContext` type registry is scoped to the `IServiceCollection`.** Each registration is resolved from the
  provider built out of that collection, so separately built providers (parallel test fixtures, for instance) never
  see each other's registered types — each one has to call `AddSlimBusEfCoreInterceptor<TDbContext>()` for itself.
- **A `null` or failed response silently skips the save**, including an exception the handler caught and turned into
  `Result.Fail(...)`. Unhandled exceptions propagate through SlimMessageBus's own pipeline; this package adds no
  exception handling around the handler call.
- **Auto-save runs inside the interceptor, after the handler returns.** A `DbUpdateException` surfaces from the
  interceptor rather than from the handler, so handler-local try/catch will not see it.

## 🔗 Related packages

- [DKNet.SlimBus.Extensions](./DKNet.SlimBus.Extensions.md) – the EF-free contracts and MediatR-style setup.
- [DKNet.SlimBus.Generators](./DKNet.SlimBus.Generators.md) – reach for it to generate CRUD requests, handlers, and
  endpoints instead of hand-writing the repetitive slices.
- [DKNet.EfCore.Events](../DKNet.EfCore.Events/index.md) – reach for it to raise domain events from aggregates in the
  first place.
- [DKNet.EfCore.Specifications](../DKNet.EfCore.Specifications/index.md) – reach for it to express handler queries as
  reusable specifications.
