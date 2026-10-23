# Retry a save after a concurrency conflict

Part of [DKNet.EfCore.Extensions](index.md). Wrap an async save in a bounded retry loop using `IEfCoreExceptionHandler`.

Use the console-project setup in the [Quick Start](index.md#-quick-start), then replace `Program.cs` with this example.

```csharp
using Microsoft.EntityFrameworkCore;
using DKNet.EfCore.Extensions.Extensions;

using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
connection.Open();
var o = new DbContextOptionsBuilder<DemoDb>();
o.UseSqlite(connection);
await using var db = new DemoDb(o.Options);
await db.Database.EnsureCreatedAsync();
db.Add(new Item { Name = "Coffee" });
Console.WriteLine(await db.SaveChangesWithConcurrencyHandlingAsync()); // 1

public sealed class DemoDb(DbContextOptions<DemoDb> options) : DbContext(options)
{
    public DbSet<Item> Items => Set<Item>();
}
public sealed class Item
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}
```

![SaveChangesWithConcurrencyHandlingAsync saves normally, asks the handler on DbUpdateConcurrencyException and retries, returns zero or rethrows according to its decision.](diagrams/handle-concurrency.svg)

The default `EfCoreExceptionHandler` rethrows conflicts with no entries. Otherwise it reloads database values into OriginalValues while preserving CurrentValues, then requests another save. It uses the structured Entries collection, not message matching.

## ⚙️ Configuration reference

| Option | Type | Default | Effect |
|---|---|---|---|
| `handler` | `IEfCoreExceptionHandler?` | new default handler | Handles concurrency exceptions only. |
| `MaxRetryCount` | `int` | `3` on interface | Retry limit after initial save. |
| `cancellationToken` | `CancellationToken` | `default` | Forwarded to save and handler. |
| Default handler logger | `ILogger<EfCoreExceptionHandler>?` | `null` | Log conflict warning when supplied. |

`EfConcurrencyResolution` contains `RethrowException`, `IgnoreChanges` and `RetrySaveChanges`. Pass a custom handler explicitly for direct calls.

## ⚠️ Gotchas & limits

- The default maximum is 3 retries after the first attempt. Exhaustion returns 0; it does not throw the last concurrency exception.
- `IgnoreChanges` also returns 0 but does not itself detach entries or clear pending changes. Your handler owns any state adjustment.
- `AddEfCoreExceptionHandler<TDbContext,THandler>()` registers a keyed transient service. This direct save helper uses the handler argument or constructs its default; it does not resolve that registration automatically.
- A missing database row leaves its entry unchanged during reload, so repeated conflict can exhaust retries. Other exception types propagate.
