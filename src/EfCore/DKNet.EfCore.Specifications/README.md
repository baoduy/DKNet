# DKNet.EfCore.Specifications

Compose reusable EF Core queries, runtime predicates and paging through one repository for .NET developers.

## ✨ Why use it?

- Name reusable filter, include and ordering logic in a specification.
- Use one repository per configured DbContext for reads and tracked writes.
- Build runtime filters with explicit validation and choose offset, streamed or cursor paging.

## 🚀 Quick Start

Use a .NET 10 console project. Install the packages, then replace `Program.cs` with the example.

```bash
dotnet new console -f net10.0 -n EfCoreDemo
cd EfCoreDemo
dotnet add package DKNet.EfCore.Specifications
dotnet add package Microsoft.EntityFrameworkCore.Sqlite --version 10.0.12
```

```csharp
using DKNet.EfCore.Specifications;
using DKNet.EfCore.Specifications.Definitions;
using DKNet.EfCore.Specifications.Extensions;
using DKNet.EfCore.Specifications.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
connection.Open();
var services = new ServiceCollection();
services.AddDbContext<DemoDb>(o => o.UseSqlite(connection));
services.AddSpecRepo<DemoDb>();
using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
await using var db = scope.ServiceProvider.GetRequiredService<DemoDb>();
await db.Database.EnsureCreatedAsync();
var repo = scope.ServiceProvider.GetRequiredService<IRepositorySpec>();
await repo.AddRangeAsync(new[]
{
    new Product { Name = "Coffee", Price = 12 },
    new Product { Name = "Tea", Price = 6 }
});
await repo.SaveChangesAsync();
var rows = await repo.ToListAsync(new MinimumPrice(10));
Console.WriteLine(rows.Single().Name); // Coffee

public sealed class DemoDb(DbContextOptions<DemoDb> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
}
public sealed class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Price { get; set; }
}
public sealed class MinimumPrice : Specification<Product>
{
    public MinimumPrice(int minimum)
    {
        WithFilter(p => p.Price >= minimum);
        AddOrderBy(p => p.Id);
        AsNoTracking();
    }
}
```

Run with `dotnet run`.

## Documentation

| Guide | Read when |
|---|---|
| [Package overview](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.Specifications/index.md) | Start with architecture and the end-to-end flow. |
| [Compose reusable queries](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.Specifications/compose-specifications.md) | Configure filters, includes, ordering and tracking. |
| [Query and save through a repository](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.Specifications/query-and-save.md) | Register repositories, persist changes and own background scopes. |
| [Build runtime predicates](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.Specifications/build-dynamic-predicates.md) | Validate caller filters before composing LINQ expressions. |
| [Project queries into models](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.Specifications/project-models.md) | Use Mapster-backed non-tracking model queries. |
| [Page and stream results](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.EfCore.Specifications/paginate-results.md) | Choose offset pages, buffered enumeration or keysets. |
