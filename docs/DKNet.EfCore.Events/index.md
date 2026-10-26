# DKNet.EfCore.Events

Domain-event collection and publishing during successful async saves for .NET developers using entity event queues or RaisesEvent declarations.

## ✨ Why use it?

- Keep transport calls out of domain methods by collecting queued event objects on save.
- Capture declared create/update/delete rules before save and map their payloads afterward.
- Fan the combined batch out to host-supplied publisher implementations.

## 🚀 Quick Start

Use a .NET 10 console project. Install the package, then replace `Program.cs` with the example.

```bash
dotnet new console -f net10.0 -n EfCoreDemo
cd EfCoreDemo
dotnet add package DKNet.EfCore.Events
dotnet add package Microsoft.EntityFrameworkCore.Sqlite --version 10.0.12
```

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

Run with `dotnet run`.

## 🏗️ Architecture

![Entity queues and declarations reach EventHook through Hooks, then EventContext and the optional mapper produce batches for publisher adapters.](diagrams/components.svg)

EventHook runs as a scoped Hooks implementation and reads the shared Extensions snapshot. IEventPublisher implementations are scoped, while the first IMapper is used for mapping-based sources. The package supplies no durable transport, outbox store or background dispatcher.

## 🔄 How it works

![HookRunnerInterceptor invokes EventHook before the write to capture declarations and after success to map, publish and clear event queues.](diagrams/efcore-events-savechanges.svg)

AddEventPublisher registers the publisher and EventHook; AddDbContextWithHook installs the required interceptor. Publisher failures are logged and swallowed individually, while mapping errors propagate. An outer transaction may still be uncommitted when the after-save phase runs.

## 🧩 Features

- [Publish queued events after a successful save](publish-domain-events.md) — Connect entity event queues to one or more `IEventPublisher` implementations through the save hook.
- [Publish payloads declared on entity operations](publish-declared-events.md) — Evaluate `[RaisesEvent]` rules before the write, then map qualifying payloads from the entity after a successful save.

## 🔗 Related packages

- [DKNet.EfCore.Abstractions](../DKNet.EfCore.Abstractions/index.md) — reach for it to define event entities, payload contracts and declarations.
- [DKNet.EfCore.Hooks](../DKNet.EfCore.Hooks/index.md) — reach for it to configure or suppress the shared save pipeline.
- [DKNet.EfCore.Extensions](../DKNet.EfCore.Extensions/index.md) — reach for it to configure entity models and snapshots.
- [DKNet.EfCore.AuditLogs](../DKNet.EfCore.AuditLogs/index.md) — reach for it for field-level audit capture.
- [DKNet.EfCore.DtoGenerator](../DKNet.EfCore.DtoGenerator/index.md) — reach for it to generate and validate declared event payloads.
