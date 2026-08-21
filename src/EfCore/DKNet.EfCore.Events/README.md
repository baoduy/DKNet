# DKNet.EfCore.Events

[![NuGet](https://img.shields.io/nuget/v/DKNet.EfCore.Events)](https://www.nuget.org/packages/DKNet.EfCore.Events/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](../../../../LICENSE)

Dispatches domain events raised by entities during EF Core `SaveChanges`. An entity queues a plain object from a
business method (`AddEvent(...)`); this package publishes it after the save has actually committed — never
before, never for a save that fails.

## Features

- `AddEvent(object)` / `AddEvent<TEvent>()` on any `IEventEntity` (already implemented by `Entity<TKey>` /
  `Entity` in `DKNet.EfCore.Abstractions`) to queue events from domain methods.
- Automatic dispatch after a successful `SaveChanges`, via an `EventHook` that plugs into the same save pipeline
  as `DKNet.EfCore.Hooks`.
- `[RaisesEvent]` declared events — raise events by attribute instead of by hand, with no `IEventEntity`
  requirement (needs `DKNet.EfCore.DtoGenerator` for the payload).
- `IEventPublisher` / `DefaultEventPublisher` abstraction — plug in any messaging technology; multiple publishers
  can be registered and all run per save.
- `EventException` surfaces missing `IMapper` registrations and unresolved declared-event payloads instead of
  failing silently.

## Install

```bash
dotnet add package DKNet.EfCore.Events
```

## Quick start

```csharp
public record OrderPlacedEvent(Guid OrderId, decimal Total);

public class Order : Entity<Guid>
{
    public Order(Guid id, decimal total) : base(id)
    {
        Total = total;
        AddEvent(new OrderPlacedEvent(id, total)); // queued, not dispatched yet
    }

    public decimal Total { get; private set; }
}

public sealed class LoggingEventPublisher(ILogger<LoggingEventPublisher> logger) : DefaultEventPublisher
{
    public override Task PublishAsync(object eventObj, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Publishing {EventType}", eventObj.GetType().Name);
        return Task.CompletedTask;
    }
}

// Startup — DbContext must be hook-aware for events to dispatch at all
services.AddDbContextWithHook<AppDbContext>((_, o) => o.UseSqlServer(connectionString));
services.AddEventPublisher<AppDbContext, LoggingEventPublisher>();

// Usage
db.Orders.Add(new Order(Guid.NewGuid(), 42.00m));
await db.SaveChangesAsync(); // OrderPlacedEvent reaches LoggingEventPublisher only after this commits
```

## Full documentation

Registration details, the complete dispatch lifecycle, `[RaisesEvent]` declared events, configuration, and how
this composes with `DKNet.EfCore.Abstractions` and `DKNet.EfCore.Hooks`:
https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/DKNet.EfCore.Events.md
