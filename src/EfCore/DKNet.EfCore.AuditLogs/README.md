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

## 📚 Documentation

| Read next | What it covers |
|---|---|
| [Package guide](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.AuditLogs/index.md) | Architecture, save or query flow, and related packages. |
| [Capture entity changes](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.AuditLogs/capture-changes.md) | Understand entry metadata and field diffs. |
| [Redact sensitive values](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.AuditLogs/redact-sensitive-values.md) | Choose entity and property capture rules. |
| [Stamp the current user](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.AuditLogs/stamp-current-user.md) | Fill audit fields before capture. |
| [Publish audit batches](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.AuditLogs/publish-audit-logs.md) | Connect one or more scoped sinks. |
