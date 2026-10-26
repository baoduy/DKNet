# Publish queued events after a successful save

Part of [DKNet.EfCore.Events](index.md). Connect entity event queues to one or more `IEventPublisher` implementations through the save hook.

Use the console-project setup in the [Quick Start](index.md#-quick-start), then replace `Program.cs` with this example.

```csharp
using Microsoft.EntityFrameworkCore;
using DKNet.EfCore.Hooks;
using Microsoft.Extensions.DependencyInjection;

using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
connection.Open();
var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
services.AddLogging();
services.AddDbContextWithHook<DemoDb>(o => o.UseSqlite(connection));
services.AddEventPublisher<DemoDb, ConsolePublisher>();
using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
await using var db = scope.ServiceProvider.GetRequiredService<DemoDb>();
await db.Database.EnsureCreatedAsync();
var item = new Item();
item.Announce();
db.Add(item);
await db.SaveChangesAsync();

public sealed class DemoDb(DbContextOptions<DemoDb> options) : DbContext(options)
{
    public DbSet<Item> Items => Set<Item>();
}
public sealed class Item : DKNet.EfCore.Abstractions.Entities.Entity<int>
{
    public string Name { get; set; } = "Coffee";
    public void Announce() => AddEvent(new ItemCreated(Name));
}
public sealed record ItemCreated(string Name);
public sealed class ConsolePublisher : DKNet.EfCore.Abstractions.Events.DefaultEventPublisher
{
    public override Task PublishAsync(object value, CancellationToken cancellationToken = default)
    {
        Console.WriteLine(value); // ItemCreated { Name = Coffee }
        return Task.CompletedTask;
    }
}
```

![EventContext collects entity event queues, EventHook sends the combined batch to each publisher, and it clears queues after attempting all publishers.](diagrams/publish-domain-events.svg)

Only entities in the changed-entry snapshot participate in manual queue dispatch. Object events pass through; queued event types map from the live entity. EventContext de-duplicates each entity queue using object equality and stamps `sourceType` on manually queued `IEventItem` payloads.

## ⚙️ Configuration reference

| Registration | Lifetime / default | Effect |
|---|---|---|
| `AddEventPublisher<TDbContext, TPublisher>()` | Scoped publisher | Add publisher and EventHook for this context. |
| `AddDbContextWithHook<TDbContext>()` | Scoped context by default | Install async-save interceptor. |
| `MapsterMapper.IMapper` | host-supplied, first resolved | Needed for queued event types and declared events. |
| `ILogger<EventHook>` | optional | Information per after-save pass and errors for publisher failures. |

## 🧱 Where it fits

Abstractions defines the entity queues and publisher contracts. Hooks invokes EventHook around the context save, and the host publisher implementation owns transport or external side effects.

![The package component map shows the host, contracts and consumers involved in this capability.](diagrams/components.svg)

## ⚠️ Gotchas & limits

- An event on an Unchanged entity alone does not make it part of the save snapshot. A failed save does not publish.
- Each publisher exception is caught and logged, and later publishers still run. Queues are cleared after the loop even when publishers fail; no retry or durable outbox is supplied.
- Mapping happens before that publisher try/catch. Missing IMapper throws EventException after the write and before cleanup; mapping exceptions also propagate.
- Publisher registrations are shared IEventPublisher services, not keyed by context. Different implementation registrations all receive the batch. Re-registering the same implementation is guarded.
- Successful SaveChangesAsync is not proof that a caller-owned outer transaction has committed. This hook is not atomic transport delivery.
- DefaultEventPublisher publishes a batch sequentially, but the package gives no business ordering guarantee across entities or declared events.
