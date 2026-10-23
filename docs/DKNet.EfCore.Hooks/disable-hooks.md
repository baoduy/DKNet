# Suppress hooks for a bounded async flow

Part of [DKNet.EfCore.Hooks](index.md). Wrap saves in a disposable `DisableHooks()` scope when a batch must bypass every hook for that context type.

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
services.AddHook<DemoDb, ReportHook>();
using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
await using var db = scope.ServiceProvider.GetRequiredService<DemoDb>();
await db.Database.EnsureCreatedAsync();
using (db.DisableHooks())
{
    db.Add(new Item { Name = "Seed" });
    await db.SaveChangesAsync(); // no hook output
}
db.Add(new Item { Name = "Normal" });
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

![DisableHooks increments the context-type count in an AsyncLocal immutable map and disposal decrements it, restoring hooks after the outermost scope ends.](diagrams/disable-hooks.svg)

Suppression is keyed by the concrete context type full name and follows the current logical execution context. Nested scopes are reference-counted; synchronous and asynchronous disposal are supported and repeated disposal is a no-op.

## 🧱 Where it fits

The shared hook pipeline means one scope also suppresses Events, AuditLogs and DataAuthorization hooks registered for that concrete context type in the inherited execution flow.

![The package component map shows the host, contracts and consumers involved in this capability.](diagrams/components.svg)

## ⚠️ Gotchas & limits

- Every instance of the same context type in the inherited async flow is affected. Unrelated concurrent flows are isolated; child flows inherit the execution context.
- This suppresses audit, events, ownership and custom hooks together. Pending entity event queues are not drained by the skipped event hook.
- Disposal leaves the DbContext alive. Keep the scope around the work and dispose it in the flow where it was opened.
