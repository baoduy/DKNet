# Run custom work before and after an async save

Part of [DKNet.EfCore.Hooks](index.md). Implement a hook interface or derive from `HookAsync` to inspect the shared snapshot around a save.

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

![AddHook registers ReportHook for DemoDb and HookRunnerInterceptor invokes BeforeSaveAsync before writing and AfterSaveAsync after successful completion.](diagrams/write-save-hooks.svg)

`IHookBaseAsync` is the marker; choose `IBeforeSaveHookAsync`, `IAfterSaveHookAsync` or combined `IHookAsync`. `HookAsync` provides no-op virtual methods. Hooks are keyed scoped services resolved from the context application provider, including registrations for its base DbContext types.

## ⚙️ Configuration reference

| Registration | Lifetime / default | Effect |
|---|---|---|
| `AddDbContextWithHook<T>(builder, contextLifetime, optionLifetime)` | both Scoped | Register context and attach interceptor; overloads accept provider-aware or typed options callbacks. |
| `AddHook<TDbContext, THook>()` | keyed Scoped | Register hook once per context/hook pair and ensure runner services exist. |
| `UseHooks<TDbContext>(provider)` | explicit options call | Attach an already registered keyed runner when using plain AddDbContext. |
| Hook runner | keyed Singleton | One interceptor per registered context type. |

Manual setup must both register hooks and call UseHooks inside a provider-aware AddDbContext options callback. AddHook alone does not attach the interceptor.

## 🧱 Where it fits

Custom hooks share the same interceptor and snapshot with EventHook, EfCoreAuditHook and DataOwnerHook. Keep domain operations on entities and use these infrastructure hooks for work tied to persistence.

![The package component map shows the host, contracts and consumers involved in this capability.](diagrams/components.svg)

## ⚠️ Gotchas & limits

- Use SaveChangesAsync. Synchronous SaveChanges throws `NotSupportedException` unless hooks are disabled.
- Before-hook failures propagate and prevent the normal save; after-hook failures propagate after the write has succeeded. A caller-controlled transaction can still be uncommitted at this point.
- Within one key, hooks follow DI enumeration order. There is no priority option; avoid relying on ordering across base/derived registrations.
- The fixed snapshot excludes newly added entries from later hooks. A hook that changes shared live entries can affect later hook behavior.
- The interceptor tracks save contexts in a weak-keyed stack to preserve outer state on nested saves. Hook implementations still need to manage their own mutable cross-phase state and prevent recursive save loops.
