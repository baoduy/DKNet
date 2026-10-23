# DKNet.EfCore.AuditLogs

Capture entity changes and audit identity for .NET developers who supply their own audit-log publisher.

## ✨ Why use it?

- Record create, update and delete operations without repeating a ChangeTracker walk.
- Redact sensitive property values before they reach your audit sink.
- Attribute changes to the current user while keeping tenant ownership separate.

## 🚀 Quick Start

Use a .NET 10 console project. Install the packages, then replace `Program.cs` with the example.

```bash
dotnet new console -f net10.0 -n EfCoreDemo
cd EfCoreDemo
dotnet add package DKNet.EfCore.AuditLogs
dotnet add package Microsoft.EntityFrameworkCore.Sqlite --version 10.0.12
```

```csharp
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.AuditLogs;
using DKNet.EfCore.Hooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
connection.Open();
var services = new ServiceCollection();
services.AddLogging();
services.AddDbContextWithHook<DemoDb>(o => o.UseSqlite(connection));
services.AddEfCoreAuditLogs<DemoDb, ConsolePublisher>();
services.AddCurrentUserProvider<DemoDb, DemoUser>();
using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
await using var db = scope.ServiceProvider.GetRequiredService<DemoDb>();
await db.Database.EnsureCreatedAsync();
db.Add(new Item { Name = "Coffee" });
await db.SaveChangesAsync(); // Created: Item, by user-42

public sealed class DemoDb(DbContextOptions<DemoDb> options) : DbContext(options)
{
    public DbSet<Item> Items => Set<Item>();
}
public sealed class Item : IAuditedProperties
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedOn { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedOn { get; set; }
}
public sealed class DemoUser : ICurrentUserProvider
{
    public string? GetCurrentUser() => "user-42";
}
public sealed class ConsolePublisher : IAuditLogPublisher
{
    public Task PublishAsync(IEnumerable<AuditLogEntry> logs, CancellationToken cancellationToken = default)
    {
        foreach (var log in logs)
            Console.WriteLine($"{log.Action}: {log.EntityName}, by {log.UpdatedBy ?? log.CreatedBy}");
        return Task.CompletedTask;
    }
}
```

Run with `dotnet run`.

## 🏗️ Architecture

![A hook-enabled DbContext passes snapshots to EfCoreAuditHook, which uses audit options and the current user before passing entries to keyed publishers.](diagrams/components.svg)

`EfCoreAuditHook` and publishers resolve in the context DI scope. The hook keeps pending batches in memory by context instance; the package supplies no durable store.

## 🔄 How it works

![BeforeSaveAsync stamps the current user and captures eligible changes, then a successful EF save triggers publication; excluded entities and failed writes produce no published batch.](diagrams/efcore-auditlogs-capture.svg)

Capture runs before EF writes, and publishing runs after a successful save. Publisher calls are awaited and exceptions are logged and swallowed. An after-save callback can still run inside a caller-owned transaction; this is not a durable delivery guarantee.

## 🧩 Features

- [Capture entity changes](capture-changes.md) — Understand entry metadata and field diffs.
- [Redact sensitive values](redact-sensitive-values.md) — Choose entity and property capture rules.
- [Stamp the current user](stamp-current-user.md) — Fill audit fields before capture.
- [Publish audit batches](publish-audit-logs.md) — Connect one or more scoped sinks.

## 🔗 Related packages

- [DKNet.EfCore.Hooks](../DKNet.EfCore.Hooks/index.md) — use it for custom work around SaveChangesAsync.
- [DKNet.EfCore.Abstractions](../DKNet.EfCore.Abstractions/index.md) — use its audited entity contracts and capture attributes.
- [DKNet.EfCore.DataAuthorization](../DKNet.EfCore.DataAuthorization/index.md) — use it for row ownership and tenant-key fallback.
- [DKNet.EfCore.Encryption](../DKNet.EfCore.Encryption/index.md) — use it to encrypt stored columns as well as redact their audit values.
