# The disposable handle behind a disabled-hooks scope

Part of [DKNet.EfCore.Hooks](index.md). `IHookDisablingContext` is the disposable handle `DisableHooks()` returns; it is not something you implement, only something you hold and dispose.

Use the console-project setup in the [Quick Start](index.md#-quick-start), then replace `Program.cs` with this example.

```csharp
using Microsoft.EntityFrameworkCore;
using DKNet.EfCore.Hooks;
using DKNet.EfCore.Hooks.Internals;
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

IHookDisablingContext handle = db.DisableHooks();
Console.WriteLine(handle is IDisposable); // True
db.Add(new Item { Name = "Seed" });
await db.SaveChangesAsync(); // no hook output: handle is still active
handle.Dispose();
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

None — no DI registration needed; `DisableHooks()` is an extension method you call directly, and `IHookDisablingContext` is simply the disposable handle it returns to you.

![DisableHooks() creates HookDisablingContext, which increments a ref-count for the DbContext type; the caller holds the returned IHookDisablingContext until it disposes it, decrementing the count and letting HookRunnerInterceptor run hooks again.](diagrams/hook-disabling-scope.svg)

`IHookDisablingContext` declares no members beyond `IDisposable` and `IAsyncDisposable`; it exists so a caller has something concrete to hold and dispose, not so a caller implements it. The only implementation, `HookDisablingContext`, is `internal sealed` — you receive an instance from `DisableHooks()`, you never construct or subclass one yourself. See [Suppress hooks for a bounded async flow](disable-hooks.md) for the ref-counting and `AsyncLocal` scoping behaviour this handle drives.

## ⚙️ Configuration reference

None — `IHookDisablingContext` reads no options and exposes no override point. Its only two members, `Dispose()` and `DisposeAsync()`, are not virtual, and there is no second implementation to register.

## 🧱 Where it fits

None — no DKNet package calls `DisableHooks()` or resolves an `IHookDisablingContext`; you receive one from your own call and dispose it in your own code. `HookRunnerInterceptor` checks suppression through the separate internal `HookDisablingContext.IsHookDisabled(DbContext)` method, not through this interface — see [Suppress hooks for a bounded async flow](disable-hooks.md) for that mechanism.

**Entry point in your app:** the `using` (or `using var`) statement around `context.DisableHooks()` in the code that needs hooks suppressed, as shown above and in [Suppress hooks for a bounded async flow](disable-hooks.md).

## ⚠️ Gotchas & limits

- **Disposing the handle does not dispose the `DbContext`.** `IHookDisablingContext.Dispose()` only decrements the suppression ref-count; the context you called `DisableHooks()` on stays open and usable.
- **There is nothing to implement.** A type that needs "suppress hooks" behaviour calls `DisableHooks()` for a handle; it does not implement `IHookDisablingContext` itself — the only implementation is internal to this package.
- **Repeated disposal is a no-op, not an error.** Calling `Dispose()` or `DisposeAsync()` more than once on the same handle does not throw and does not double-decrement the count.
- **Letting the handle go out of scope without disposing it leaks the suppression.** `IHookDisablingContext` is not a `SafeHandle` with a finalizer. `DisableHooks()` is synchronous, so its ref-count increment lands in the `AsyncLocal` value of the method that called it; an un-disposed handle keeps hooks disabled for that `DbContext` type for every awaited continuation of that method (and anything it calls), until the enclosing async method returns and the logical call context unwinds.
