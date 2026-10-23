# DKNet.EfCore.DataAuthorization

Filter owned rows and stamp ownership for .NET developers building tenant-aware EF Core applications.

## ✨ Why use it?

- Apply ownership checks to queries without repeating a Where clause.
- Deny owned-row reads when the accessible-key list is empty.
- Fill a new row's blank owner and revert inaccessible ownership changes before saving.

## 🚀 Quick Start

Use a .NET 10 console project. Install the packages, then replace `Program.cs` with the example.

```bash
dotnet new console -f net10.0 -n EfCoreDemo
cd EfCoreDemo
dotnet add package DKNet.EfCore.DataAuthorization
dotnet add package Microsoft.EntityFrameworkCore.Sqlite --version 10.0.12
```

```csharp
using DKNet.EfCore.DataAuthorization;
using DKNet.EfCore.Hooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
connection.Open();
var services = new ServiceCollection();
services.AddLogging();
services.AddDataOwnerProvider<DemoDb, DemoOwner>();
services.AddDbContextWithHook<DemoDb>(o =>
    o.UseSqlite(connection).UseAutoConfigModel<DemoDb>());
using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
await using var db = scope.ServiceProvider.GetRequiredService<DemoDb>();
await db.Database.EnsureCreatedAsync();
db.Add(new Invoice { Name = "Coffee" });
await db.SaveChangesAsync();
db.ChangeTracker.Clear();
var invoice = await db.Invoices.SingleAsync();
Console.WriteLine(invoice.OwnedBy); // tenant-a

public sealed class DemoOwner : IDataOwnerProvider
{
    public string? GetOwnershipKey() => "tenant-a";
}
public sealed class DemoDb(DbContextOptions<DemoDb> options, IDataOwnerProvider owner)
    : DbContext(options), IDataOwnerDbContext
{
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public IEnumerable<string> AccessibleKeys => owner.GetAccessibleKeys();
}
public sealed class Invoice : IOwnedBy
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string OwnedBy { get; private set; } = "";
    public void AssignOwner(string key) => OwnedBy = key;
}
```

Run with `dotnet run`.

## 🏗️ Architecture

![IDataOwnerProvider supplies context access keys and DataOwnerHook, while DataOwnerAuthQuery filters IOwnedBy roots and the optional current user controls audit-field stamping.](diagrams/efcore-dataauthorization-ownership.svg)

The ownership provider is scoped and unkeyed; the first registration wins. The global model-builder registration is process-wide, while the hook is keyed to a context type. No separate service or database policy is installed.

## 🔄 How it works

![AddDataOwnerProvider installs the query builder and save hook, UseAutoConfigModel attaches the filter, and async saves stamp ownership before reads evaluate the context keys.](diagrams/ownership-flow.svg)

Both model configuration and hook wiring are required. Query access comes from `IDataOwnerDbContext`; write stamping and reassignment checks read `IDataOwnerProvider`. Keep those sources consistent.

## 🧩 Features

- [Filter owned rows](filter-owned-rows.md) — Wire the global filter and explicit unrestricted access.
- [Stamp and guard ownership](stamp-ownership.md) — Set owners and compose tenant and user audit identity.

## 🔗 Related packages

- [DKNet.EfCore.Extensions](../DKNet.EfCore.Extensions/index.md) — use it for model configuration and custom global filters.
- [DKNet.EfCore.Hooks](../DKNet.EfCore.Hooks/index.md) — use it for the async save pipeline.
- [DKNet.EfCore.AuditLogs](../DKNet.EfCore.AuditLogs/index.md) — use it for signed-in user attribution and a change trail.
- [DKNet.EfCore.Specifications](../DKNet.EfCore.Specifications/index.md) — use it for reusable queries that preserve this ownership filter.
