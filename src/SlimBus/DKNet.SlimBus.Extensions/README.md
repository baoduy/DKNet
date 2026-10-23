# DKNet.SlimBus.Extensions

CQRS and messaging glue on top of [SlimMessageBus](https://github.com/zarusz/SlimMessageBus) — fluent command, query
and event-consumer contracts, `FluentResults`-based handlers, lazy Mapster mapping, and an opt-in EF Core auto-save
and domain-event publisher.

## ✨ Why use it?

- **Request shape says what a message is.** Implementing `Fluents.Requests.*` marks a write, `Fluents.Queries.*`
  marks a read — no attributes, no naming conventions to remember.
- **Expected business failures are results, not exceptions.** Commands return `FluentResults` types, so "product not
  found" is a value the caller inspects rather than a thrown exception to catch.
- **EF Core writes without a `SaveChangesAsync` call.** `AddSlimBusEfCoreInterceptor<TDbContext>()` saves the
  `DbContext` for you after a successful write, and `AddSlimBusEventPublisher<TDbContext>()` forwards the domain
  events that save raised onto the bus.
- **No bundled transport.** You call SlimMessageBus's own `AddSlimMessageBus(...)` and pick the provider (memory,
  Azure Service Bus, Kafka) yourself; the `Fluents` contracts and your handlers stay the same either way.

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

This package already depends on EF Core — it references `X.PagedList.EF` and `DKNet.EfCore.Events` — but nothing
needs an actual database unless you call the two EF Core registration methods
(`AddSlimBusEfCoreInterceptor<TDbContext>()`, `AddSlimBusEventPublisher<TDbContext>()`), which are opt-in on top of
the contracts shown above.

## Documentation

| Guide | Read when |
|---|---|
| [Package overview](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.SlimBus.Extensions/index.md) | Start with architecture and the end-to-end flow. |
| [Send write commands](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.SlimBus.Extensions/send-commands.md) | Declare requests with `Fluents.Requests.INoResponse` / `IWitResponse<T>` and implement their handlers. |
| [Run read queries](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.SlimBus.Extensions/run-queries.md) | Declare queries with `Fluents.Queries.IWitResponse<T>` / `IWitPageResponse<T>` and implement their handlers. |
| [Consume published events](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.SlimBus.Extensions/consume-events.md) | Implement `Fluents.EventsConsumers.IHandler<TEvent>` to react to a message on the bus. |
| [Auto-save after a successful write](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.SlimBus.Extensions/auto-save-dbcontext.md) | Register `AddSlimBusEfCoreInterceptor<TDbContext>()` so a successful write saves the `DbContext` for you. |
| [Publish domain events onto the bus](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.SlimBus.Extensions/publish-domain-events.md) | Register `AddSlimBusEventPublisher<TDbContext>()` so events raised by `DKNet.EfCore.Events` reach `IMessageBus`. |
| [Map results lazily](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.SlimBus.Extensions/map-results-lazily.md) | Defer a Mapster mapping until a handler's result value is actually read. |
