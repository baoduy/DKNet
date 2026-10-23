# Publish domain events onto the bus

Part of [DKNet.SlimBus.Extensions](index.md). `AddSlimBusEventPublisher<TDbContext>()` registers
`SlimBusEventPublisher` as the `IEventPublisher` for `TDbContext`, forwarding the domain events
[DKNet.EfCore.Events](../DKNet.EfCore.Events/index.md) collects during a successful save onto `IMessageBus`. An
aggregate raises an event the same way it does anywhere `DKNet.EfCore.Events` is used — by calling `AddEvent(...)`
inside an entity method. After `SaveChangesAsync` succeeds, the save hook hands the raised events to
`SlimBusEventPublisher`, which calls `IMessageBus.Publish` for each one. Consume them with
[Fluents.EventsConsumers.IHandler\<TEvent\>](consume-events.md). Use a .NET 10 console project, then replace
`Program.cs` with this example:

```bash
dotnet new console -f net10.0 -n PublishEventsDemo
cd PublishEventsDemo
dotnet add package DKNet.SlimBus.Extensions
dotnet add package SlimMessageBus.Host.Memory --version 3.5.0
dotnet add package Microsoft.EntityFrameworkCore.Sqlite --version 10.0.12
```

```csharp
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.Abstractions.Events;
using DKNet.EfCore.Hooks;
using DKNet.SlimBus.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlimMessageBus;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;

await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
await connection.OpenAsync();
var services = new ServiceCollection();
services.AddLogging();
// AddDbContextWithHook, not plain AddDbContext — see the Gotcha below.
services.AddDbContextWithHook<AppDbContext>(o => o.UseSqlite(connection));
services.AddSlimBusEventPublisher<AppDbContext>();
services.AddSlimMessageBus(mbb => mbb
    .AddServicesFromAssembly(typeof(Program).Assembly)
    .AddChildBus("Memory", m => m.WithProviderMemory().AutoDeclareFrom(typeof(Program).Assembly)));

await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
await db.Database.EnsureCreatedAsync();

var product = new Product(Guid.NewGuid(), "Announced", 3m);
product.Announce();
db.Products.Add(product);
await db.SaveChangesAsync();

Console.WriteLine($"ProductAnnouncedHandler received: {ProductAnnouncedHandler.LastReceived}, " +
                   $"header tenant={ProductAnnouncedHandler.LastTenantHeader}");

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
}

public sealed class Product : Entity<Guid>
{
    public Product(Guid id, string name, decimal price) : base(id)
    {
        Name = name;
        Price = price;
    }

    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }

    public void Announce()
    {
        var evt = new ProductAnnounced(Name);
        evt.AdditionalData["tenant"] = "acme";
        AddEvent(evt);
    }
}

public sealed record ProductAnnounced(string Name) : EventItem;

public class ProductAnnouncedHandler : Fluents.EventsConsumers.IHandler<ProductAnnounced>
{
    public static string? LastReceived;
    public static string? LastTenantHeader;

    public Task OnHandle(ProductAnnounced message, CancellationToken cancellationToken)
    {
        LastReceived = message.Name;
        LastTenantHeader = message.AdditionalData.TryGetValue("tenant", out var v) ? v : null;
        return Task.CompletedTask;
    }
}
```

Run with `dotnet run`. Prints `ProductAnnouncedHandler received: Announced, header tenant=acme`.

![AddDbContextWithHook registers the DbContext with DKNet.EfCore.Hooks' save pipeline; AddSlimBusEventPublisher registers SlimBusEventPublisher; after a successful SaveChangesAsync the EventHook collects raised events and SlimBusEventPublisher publishes them one at a time, copying AdditionalData onto message headers. A plain AddDbContext call never installs the hook, so no event ever publishes.](diagrams/publish-domain-events.svg)

`Product` derives from `DKNet.EfCore.Abstractions.Entities.Entity<TKey>` and calls its own `AddEvent(...)` — the
entity-level API `DKNet.EfCore.Events`' `EventHook` reads from after a successful save.

When an event implements `IEventItem`, `SlimBusEventPublisher` copies its `AdditionalData` entries onto the published
message as headers, using case-insensitive keys, as shown above.

A collection of events is published one at a time, in order, awaiting each — there is no batching.

`SlimBusEventPublisher` is public and both `PublishAsync` overloads are `virtual`, so you can subclass it — to stamp
extra headers or add logging, for example — and register the subclass directly with `DKNet.EfCore.Events`' own
`AddEventPublisher<TDbContext, TImplementation>()` instead of `AddSlimBusEventPublisher<TDbContext>()`. As a
standalone file alongside the `Program.cs` above:

```csharp
using DKNet.SlimBus.Extensions.Handlers;
using Microsoft.Extensions.Logging;
using SlimMessageBus;

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

Then in `Program.cs`, replace `services.AddSlimBusEventPublisher<AppDbContext>()` with
`services.AddEventPublisher<AppDbContext, LoggingEventPublisher>()`.

## ⚙️ Configuration reference

| Method | Registers | Requires |
|---|---|---|
| `AddSlimBusEventPublisher<TDbContext>()` | `SlimBusEventPublisher` as `IEventPublisher` for `TDbContext`, via `DKNet.EfCore.Events`' `AddEventPublisher<TDbContext, TImplementation>()` | `TDbContext` already registered with `AddDbContextWithHook<TDbContext>()` (`DKNet.EfCore.Hooks`) — a plain `AddDbContext<TDbContext>()` never fires the save hook this depends on |

There is no options type and no `IConfiguration` section. `AddEventPublisher` itself guards against registering the
same publisher implementation twice.

## 🧱 Where it fits

[DKNet.EfCore.Events](../DKNet.EfCore.Events/index.md) owns the entity event queues and the `EventHook` that runs
around a save; this package only supplies the `IEventPublisher` that forwards the batch onto `IMessageBus`.
[DKNet.EfCore.Hooks](../DKNet.EfCore.Hooks/index.md) owns `AddDbContextWithHook<TDbContext>()`, the save-pipeline
registration this feature depends on. [Consume published events](consume-events.md) covers the receiving side.

## ⚠️ Gotchas & limits

- **Without `AddDbContextWithHook<TDbContext>()`, nothing ever publishes.** `AddSlimBusEventPublisher<TDbContext>()`
  registers the publisher and the hook runner, but the hook only actually runs on `SaveChangesAsync` when the
  `DbContext` was registered through `DKNet.EfCore.Hooks`' `AddDbContextWithHook<TDbContext>()`, not a plain
  `AddDbContext<TDbContext>()`. The call compiles and the app starts either way; events simply never reach the bus.
- **Each registered `IEventPublisher` is wrapped in its own try/catch; a failing publisher doesn't stop the others.**
  But `SlimBusEventPublisher.PublishAsync(IEnumerable<object>)` itself has no internal try/catch — it awaits each
  event in a plain loop, so when one `Publish` call throws, the events still queued behind it in that same batch are
  never published. The failure is logged after the fact; there is no retry or durable outbox.
- **Successful `SaveChangesAsync` is not proof that a caller-owned outer transaction has committed.** This is not
  atomic transport delivery across a wider unit of work.
- **Event publishing and auto-save are separate opt-ins.** Registering only
  [`AddSlimBusEfCoreInterceptor<TDbContext>()`](auto-save-dbcontext.md) saves the `DbContext` but publishes nothing;
  you need `AddSlimBusEventPublisher<TDbContext>()` (and the hook wiring above) as well.
