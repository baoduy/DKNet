# Customise how concurrency conflicts are resolved

Part of [DKNet.EfCore.Extensions](index.md). Implement `IEfCoreExceptionHandler` and register it per `DbContext` type when the default reload-and-retry behaviour is not what you want.

Use the console-project setup in the [Quick Start](index.md#-quick-start), then replace `Program.cs` with this example.

```csharp
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using DKNet.EfCore.Extensions.Extensions;
using Microsoft.Extensions.DependencyInjection;

using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
connection.Open();
var services = new ServiceCollection();
services.AddDbContext<DemoDb>(o => o.UseSqlite(connection));
services.AddEfCoreExceptionHandler<DemoDb, IgnoreConflictsHandler>();
using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
await using var db = scope.ServiceProvider.GetRequiredService<DemoDb>();
await db.Database.EnsureCreatedAsync();
var handler = scope.ServiceProvider.GetRequiredKeyedService<IEfCoreExceptionHandler>(typeof(DemoDb).FullName);

var item = new Item { Name = "Coffee" };
db.Add(item);
await db.SaveChangesWithConcurrencyHandlingAsync(handler);

// Force a real conflict: change the concurrency-checked column outside this DbContext's tracked state.
await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Items SET Name = 'Changed elsewhere' WHERE Id = {item.Id}");
item.Name = "Coffee beans";
Console.WriteLine(await db.SaveChangesWithConcurrencyHandlingAsync(handler)); // 0: IgnoreChanges fired

public sealed class DemoDb(DbContextOptions<DemoDb> options) : DbContext(options)
{
    public DbSet<Item> Items => Set<Item>();
}
public sealed class Item
{
    public int Id { get; set; }

    [ConcurrencyCheck]
    public string Name { get; set; } = "";
}
public sealed class IgnoreConflictsHandler : IEfCoreExceptionHandler
{
    public int MaxRetryCount => 1;

    public Task<EfConcurrencyResolution> HandlingAsync(
        DbContext context, DbUpdateConcurrencyException exception, CancellationToken cancellationToken = default)
    {
        Console.WriteLine("HandlingAsync fired"); // proves the override actually ran
        return Task.FromResult(EfConcurrencyResolution.IgnoreChanges);
    }
}
```

![AddEfCoreExceptionHandler registers a keyed handler per DbContext type; EfAutoSavePostInterceptor resolves it with an unkeyed fallback while RepositorySpec resolves only the keyed registration, and both feed the chosen handler into the shared retry loop that calls your HandlingAsync override.](diagrams/customise-concurrency-handling.svg)

`AddEfCoreExceptionHandler<TDbContext, TExceptionHandler>()` registers `TExceptionHandler` as a keyed transient `IEfCoreExceptionHandler`, keyed by `typeof(TDbContext).FullName`. A second call for the same `TDbContext` is a no-op: it checks for an existing keyed registration of that exact key and service type first and returns the service collection unchanged instead of adding a second registration. Register a different handler type for a second `DbContext` type by calling it again with that type.

[Retry a save after a concurrency conflict](handle-concurrency.md) documents the retry loop itself (`SaveChangesWithConcurrencyHandlingAsync`) and the shipped default handler. This page is about replacing that handler's decision.

## ⚙️ Configuration reference

| Override point | How you customise it |
|---|---|
| `IEfCoreExceptionHandler.HandlingAsync(DbContext, DbUpdateConcurrencyException, CancellationToken)` | Implement it to decide how a concurrency conflict resolves. Return one of the `EfConcurrencyResolution` values below. |
| `IEfCoreExceptionHandler.MaxRetryCount` | Default interface member, defaults to `3`. Override the property in your implementation to change the retry budget `SaveChangesWithConcurrencyHandlingAsync` enforces for that handler. |
| `AddEfCoreExceptionHandler<TDbContext, TExceptionHandler>()` | Registers `TExceptionHandler` as a keyed transient `IEfCoreExceptionHandler`, keyed by `typeof(TDbContext).FullName`. Call it once per `DbContext` type you want a dedicated handler for. |

| `EfConcurrencyResolution` member | Effect on the retry loop |
|---|---|
| `RetrySaveChanges` | Calls `SaveChangesAsync` again, up to `MaxRetryCount` attempts after the first. |
| `IgnoreChanges` | Stops the loop and returns `0`. Your handler owns detaching or adjusting any entries; the loop does not do this for you. |
| `RethrowException` | Stops the loop and rethrows the original `DbUpdateConcurrencyException`. |

The shipped default, `EfCoreExceptionHandler`, rethrows when `exception.Entries` is empty (nothing structured to recover), otherwise reloads database values into `OriginalValues` while keeping `CurrentValues`, and returns `RetrySaveChanges`. It takes an optional `ILogger<EfCoreExceptionHandler>` and logs a warning per conflict when one is supplied.

## 🧱 Where it fits

| DKNet consumer | Package | What it does with this feature |
|---|---|---|
| `EfAutoSavePostInterceptor` | [DKNet.SlimBus.Extensions](../DKNet.SlimBus.Extensions/auto-save-dbcontext.md) | Resolves the handler keyed by the `DbContext`'s `FullName` first, falling back to an unkeyed `IEfCoreExceptionHandler` registration, before calling `SaveChangesWithConcurrencyHandlingAsync`. |
| `RepositorySpec<TDbContext>` | [DKNet.EfCore.Specifications](../DKNet.EfCore.Specifications/query-and-save.md) | `SaveChangesAsync` resolves only the keyed registration for its `DbContext` type; there is no unkeyed fallback on this path. |

`SaveChangesWithConcurrencyHandlingAsync` itself never reads DI: it uses the handler instance you pass, or constructs a default `EfCoreExceptionHandler` when you pass none. Both consumers above resolve a handler from the service provider and pass the result into that same call — see [Retry a save after a concurrency conflict](handle-concurrency.md).

**Entry point in your app:** `services.AddEfCoreExceptionHandler<TDbContext, TExceptionHandler>()` in your composition root, next to `AddDbContext<TDbContext>()`.

## ⚠️ Gotchas & limits

- **`RepositorySpec` has no unkeyed fallback.** Registering your handler with a plain `services.AddTransient<IEfCoreExceptionHandler, TYourHandler>()` (no `TDbContext` key) is seen by `EfAutoSavePostInterceptor` but not by `RepositorySpec.SaveChangesAsync`. Use `AddEfCoreExceptionHandler<TDbContext, TExceptionHandler>()` if repository saves must pick it up.
- **A second `AddEfCoreExceptionHandler<TDbContext, _>()` call for the same `TDbContext` is silently ignored.** The first registration wins; a later call with a different `TExceptionHandler` for the same context type does not replace it.
- **`MaxRetryCount` is read from the handler instance, not from configuration.** There is no options object; override the property on your implementation.
- **The key is the runtime type, not the type you registered against.** Both consumers key their keyed lookup off `db.GetType().FullName` — the concrete `DbContext` instance DI resolved — while `AddEfCoreExceptionHandler<TDbContext, _>()` keys off the `TDbContext` you passed at compile time. Register against a base `DbContext` type while a derived type is what gets resolved, and the keys will not match.
- **An out-of-range `EfConcurrencyResolution` loops forever.** The retry loop's `switch` has no `default` case; a handler that returns a value outside the three declared members (only reachable through an invalid cast) falls through doing nothing — the retry counter never advances and the loop retries the same save indefinitely.
- **Calling `SaveChangesWithConcurrencyHandlingAsync` directly bypasses both registrations.** Pass your handler explicitly, or resolve it yourself from a keyed/unkeyed service, when not going through `EfAutoSavePostInterceptor` or `RepositorySpec`.
