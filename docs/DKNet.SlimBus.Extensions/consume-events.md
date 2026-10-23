# Consume published events

Part of [DKNet.SlimBus.Extensions](index.md). `Fluents.EventsConsumers.IHandler<TEvent>` is a thin alias over
SlimMessageBus's `IConsumer<TEvent>` for reacting to a message on the bus. Use the console-project setup in the
[Quick Start](index.md#-quick-start), then replace `Program.cs` with this example:

```csharp
using DKNet.SlimBus.Extensions;
using Microsoft.Extensions.DependencyInjection;
using SlimMessageBus;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;

var services = new ServiceCollection();
services.AddLogging();
services.AddSlimMessageBus(mbb => mbb
    .AddServicesFromAssembly(typeof(Program).Assembly)
    .AddChildBus("Memory", m => m.WithProviderMemory().AutoDeclareFrom(typeof(Program).Assembly)));

await using var provider = services.BuildServiceProvider();
var bus = provider.GetRequiredService<IMessageBus>();

await bus.Publish(new ProductCreatedEvent(Guid.NewGuid()));
Console.WriteLine($"ProductCreatedHandler received: {ProductCreatedHandler.LastReceived}");

public sealed record ProductCreatedEvent(Guid ProductId);

public class ProductCreatedHandler : Fluents.EventsConsumers.IHandler<ProductCreatedEvent>
{
    public static Guid? LastReceived;

    public Task OnHandle(ProductCreatedEvent message, CancellationToken cancellationToken)
    {
        LastReceived = message.ProductId;
        return Task.CompletedTask;
    }
}
```

Run with `dotnet run`. Prints `ProductCreatedHandler received: <guid>`.

Publish with `IMessageBus.Publish(...)` from anywhere that holds an `IMessageBus` — a handler, a background job, or
[SlimBusEventPublisher](publish-domain-events.md) forwarding an EF Core domain event automatically after a save.

![IMessageBus.Publish routes the event by type to the registered Fluents.EventsConsumers.IHandler<TEvent>, whose OnHandle runs and returns no response.](diagrams/consume-events.svg)

## 🧱 Where it fits

[Publish domain events onto the bus](publish-domain-events.md) is the usual source of events this handler consumes
when the host uses EF Core — `SlimBusEventPublisher` calls the same `IMessageBus.Publish` shown above.

## ⚠️ Gotchas & limits

- **`OnHandle` returns no value.** An event consumer cannot signal success or failure back to the publisher the way
  a `Fluents.Requests` handler returns a `FluentResults` value.
- **A missing declaration is silent, not an error.** Without `AutoDeclareFrom` covering the assembly that declares
  the event, `Publish` completes without throwing and the consumer is simply never invoked.
