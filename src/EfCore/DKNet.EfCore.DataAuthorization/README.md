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

## Documentation

| Guide | Read when |
|---|---|
| [Package overview](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.DataAuthorization/index.md) | Start with architecture and the end-to-end flow. |
| [Filter owned rows](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.DataAuthorization/filter-owned-rows.md) | Wire the global filter and explicit unrestricted access. |
| [Stamp and guard ownership](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.DataAuthorization/stamp-ownership.md) | Set owners and compose tenant and user audit identity. |
