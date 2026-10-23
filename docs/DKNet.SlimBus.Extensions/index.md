# DKNet.SlimBus.Extensions

CQRS and messaging glue on top of [SlimMessageBus](https://github.com/zarusz/SlimMessageBus) — fluent command, query
and event-consumer contracts, `FluentResults`-based handlers, lazy Mapster mapping, and an opt-in EF Core auto-save
and domain-event publisher for hosts that use a `DbContext`.

## ✨ Why use it?

- **Request shape says what a message is.** Implementing `Fluents.Requests.*` marks a write, `Fluents.Queries.*`
  marks a read — no attributes, no naming conventions to remember.
- **Expected business failures are results, not exceptions.** Commands return `FluentResults` types, so "product not
  found" is a value the caller inspects rather than a thrown exception to catch.
- **EF Core writes without a `SaveChangesAsync` call.** `AddSlimBusEfCoreInterceptor<TDbContext>()` saves the
  `DbContext` for you after a successful write, and `AddSlimBusEventPublisher<TDbContext>()` forwards the domain
  events that save raised onto the bus.
- **No bundled transport.** There is no one-call setup for the bus itself — you call SlimMessageBus's own
  `AddSlimMessageBus(...)` and pick the provider (memory, Azure Service Bus, Kafka) yourself; the `Fluents` contracts
  and your handlers stay the same either way.

This package already depends on EF Core: it references `X.PagedList.EF` (paged query results) and
`DKNet.EfCore.Events` (the domain-event publisher). If your handlers never touch a `DbContext`, you can still install
it and simply not call the two EF Core registration methods.

## 🚀 Quick Start

Use a .NET 10 console project. Install the packages, then replace `Program.cs` with the example.

```bash
dotnet new console -f net10.0 -n SlimBusDemo
cd SlimBusDemo
dotnet add package DKNet.SlimBus.Extensions
dotnet add package SlimMessageBus.Host.Memory --version 3.5.0
```

```csharp
using DKNet.SlimBus.Extensions;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using SlimMessageBus;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;

var services = new ServiceCollection();
services.AddLogging();
services.AddSlimMessageBus(mbb => mbb
    .AddServicesFromAssembly(typeof(Program).Assembly)
    .AddChildBus("Memory", m => m
        .WithProviderMemory()
        .AutoDeclareFrom(typeof(Program).Assembly)));

await using var provider = services.BuildServiceProvider();
var bus = provider.GetRequiredService<IMessageBus>();

var result = await bus.Send(new Ping("hello"));
Console.WriteLine(result.IsSuccess ? result.Value : "failed");

public record Ping(string Text) : Fluents.Requests.IWitResponse<string>;

internal sealed class PingHandler : Fluents.Requests.IHandler<Ping, string>
{
    public Task<IResult<string>> OnHandle(Ping request, CancellationToken cancellationToken) =>
        Task.FromResult<IResult<string>>(Result.Ok($"pong: {request.Text}"));
}
```

Run with `dotnet run`. Prints `pong: hello`.

The package brings no transport of its own — `SlimMessageBus.Host.Memory` above is the in-process provider for local
development and tests. For a real deployment, swap it for the Azure Service Bus, Kafka, or another SlimMessageBus
provider package and pass it to the same `AddChildBus(...)` call; the `Fluents` contracts and your handlers never
change. No serializer is needed for the memory provider. `AutoDeclareFrom` declares the message types on that child
bus — leaving it out makes `Send` return `null` without ever reaching your handler. `AddServicesFromAssembly`
registers your handler types in DI. Both come from SlimMessageBus's own `SlimMessageBus.Host` /
`SlimMessageBus.Host.Memory` packages, not from this one.

## 🏗️ Architecture

![The host registers IMessageBus and its provider; SlimMessageBus dispatches to your handlers, which implement the Fluents contracts and may use LazyMapper. AddSlimBusEfCoreInterceptor and AddSlimBusEventPublisher are an optional second row that wraps your handlers to save a DbContext and publish its domain events.](diagrams/components.svg)

The top row — `IMessageBus`, the `Fluents` contracts, your handlers, and `LazyMapper` — needs no `DbContext`. The
bottom row is opt-in: nothing wires it up unless the host calls
`AddSlimBusEfCoreInterceptor<TDbContext>()` and/or `AddSlimBusEventPublisher<TDbContext>()`. The diagram cannot show
registration order or DI lifetimes; see [Auto-save after a successful write](auto-save-dbcontext.md) and
[Publish domain events onto the bus](publish-domain-events.md) for those.

## 🔄 How it works

![One write request: the caller sends it through IMessageBus, your own interceptors run first, auto-save wraps the handler last (Order = int.MaxValue), the handler loads and changes entities without calling SaveChangesAsync, and on success auto-save saves the DbContext, which publishes any raised events through SlimBusEventPublisher back onto IMessageBus before the response returns to the caller.](diagrams/slimbus-request-pipeline.svg)

This is the write path with both EF Core add-ons registered — the pipeline a plain `Fluents.Requests` write handler
sees once the host calls `AddSlimBusEfCoreInterceptor<TDbContext>()` and `AddSlimBusEventPublisher<TDbContext>()`.
Without those two calls, `Send` goes straight from your interceptors to the handler and back, with no save and no
event publish. A query (`Fluents.Queries.*`) never enters the auto-save branch at all, whatever it leaves in the
change tracker — see [Run read queries](run-queries.md).

## 🧩 Features

- [Send write commands](send-commands.md) — Declare requests with `Fluents.Requests.INoResponse` / `IWitResponse<T>`
  and implement their handlers.
- [Run read queries](run-queries.md) — Declare queries with `Fluents.Queries.IWitResponse<T>` /
  `IWitPageResponse<T>` and implement their handlers.
- [Consume published events](consume-events.md) — Implement `Fluents.EventsConsumers.IHandler<TEvent>` to react to a
  message on the bus.
- [Auto-save after a successful write](auto-save-dbcontext.md) — Register `AddSlimBusEfCoreInterceptor<TDbContext>()`
  so a successful write saves the `DbContext` for you.
- [Publish domain events onto the bus](publish-domain-events.md) — Register `AddSlimBusEventPublisher<TDbContext>()`
  so events raised by `DKNet.EfCore.Events` reach `IMessageBus`.
- [Map results lazily](map-results-lazily.md) — Defer a Mapster mapping until a handler's result value is actually
  read, with `ILazyMap<T>` and `IMapper.ResultOf<T>`.

## 🔗 Related packages

- [DKNet.SlimBus.Generators](../DKNet.SlimBus.Generators/index.md) — reach for it to generate CRUD requests,
  handlers, and endpoints built on these same `Fluents` interfaces instead of hand-writing them.
- [DKNet.AspCore.Extensions](../DKNet.AspCore.Extensions/index.md) — reach for it to map `Fluents` requests to
  minimal-API endpoints and turn their results into HTTP responses.
- [DKNet.EfCore.Events](../DKNet.EfCore.Events/index.md) — the source of the domain events
  `AddSlimBusEventPublisher<TDbContext>()` forwards onto the bus.
- [DKNet.EfCore.Extensions](../DKNet.EfCore.Extensions/index.md) — supplies `AddNewEntitiesFromNavigations` and
  `SaveChangesWithConcurrencyHandlingAsync`, the save primitives the auto-save interceptor calls.
- [DKNet.EfCore.Hooks](../DKNet.EfCore.Hooks/index.md) — owns `AddDbContextWithHook<TDbContext>()`, the save-hook
  pipeline that domain-event publishing depends on.
- [DKNet.EfCore.Specifications](../DKNet.EfCore.Specifications/index.md) — reach for it (and its `X.PagedList.EF`
  dependency) to build the paged, specification-driven queries a `Fluents.Queries.IPageHandler<,>` returns.
