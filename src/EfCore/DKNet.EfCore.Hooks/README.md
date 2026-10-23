# DKNet.EfCore.Hooks

A before/after SaveChangesAsync hook pipeline for .NET developers adding cross-cutting persistence work.

## ✨ Why use it?

- Share one captured change set between audit, event, ownership and custom hooks.
- Resolve hooks from the context DI scope and inherit hook registrations from base context types.
- Suppress all hooks for a bounded seeding or migration flow.

## 🚀 Quick Start

Use a .NET 10 console project. Install the package, then replace `Program.cs` with the example.

```bash
dotnet new console -f net10.0 -n EfCoreDemo
cd EfCoreDemo
dotnet add package DKNet.EfCore.Hooks
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
services.AddHook<DemoDb, ReportHook>();
using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
await using var db = scope.ServiceProvider.GetRequiredService<DemoDb>();
await db.Database.EnsureCreatedAsync();
db.Add(new Item { Name = "Coffee" });
await db.SaveChangesAsync(); // before: 1, then after: 1

public sealed class DemoDb(DbContextOptions<DemoDb> options) : DbContext(options)
{
    public DbSet<Item> Items => Set<Item>();
}
public sealed class Item
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}
public sealed class ReportHook : HookAsync
{
    public override Task BeforeSaveAsync(DKNet.EfCore.Extensions.Snapshots.SnapshotContext snapshot,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"before: {snapshot.Entities.Count}");
        return Task.CompletedTask;
    }
    public override Task AfterSaveAsync(DKNet.EfCore.Extensions.Snapshots.SnapshotContext snapshot,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"after: {snapshot.Entities.Count}");
        return Task.CompletedTask;
    }
}
```

Run with `dotnet run`.

## Documentation

| Guide | Read when |
|---|---|
| [Package overview](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.Hooks/index.md) | Start with architecture and the end-to-end flow. |
| [Run custom work before and after an async save](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.Hooks/write-save-hooks.md) | Implement a hook interface or derive from `HookAsync` to inspect the shared snapshot around a save. |
| [Suppress hooks for a bounded async flow](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.Hooks/disable-hooks.md) | Wrap saves in a disposable `DisableHooks()` scope when a batch must bypass every hook for that context type. |
