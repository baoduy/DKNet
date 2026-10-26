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

## Documentation

| Guide | Read when |
|---|---|
| [Package overview](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.Events/index.md) | Start with architecture and the end-to-end flow. |
| [Publish queued events after a successful save](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.Events/publish-domain-events.md) | Connect entity event queues to one or more `IEventPublisher` implementations through the save hook. |
| [Publish payloads declared on entity operations](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.Events/publish-declared-events.md) | Evaluate `[RaisesEvent]` rules before the write, then map qualifying payloads from the entity after a successful save. |
