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

## 🏗️ Architecture

![Application code passes a specification to IRepositorySpec, whose EF query composition and optional Mapster projection execute through the configured DbContext and relational provider.](diagrams/components.svg)

`IRepositorySpec` is scoped and uses the registered DbContext. `IRepositorySpecFactory` is singleton; each provider it creates owns a new scope and factory-created context. Query operations remain deferred until materialized.

## 🔄 How it works

![ApplySpecs composes ignorable-filter bypass, Where, includes, declared ordering, tracking and window flags before EF executes the query.](diagrams/efcore-specifications-query-composition.svg)

`RepositorySpec.Query` starts with `AsExpandable()` and then applies the specification. Query composition does not execute SQL; a terminal operation such as `ToListAsync` does. Null specifications throw, and database translation/execution failures propagate.

## 🧩 Features

- [Compose reusable queries](compose-specifications.md) — Configure filters, includes, ordering and tracking.
- [Query and save through a repository](query-and-save.md) — Register repositories, persist changes and own background scopes.
- [Build runtime predicates](build-dynamic-predicates.md) — Validate caller filters before composing LINQ expressions.
- [Project queries into models](project-models.md) — Use Mapster-backed non-tracking model queries.
- [Page and stream results](paginate-results.md) — Choose offset pages, buffered enumeration or keysets.

## 🔗 Related packages

- [DKNet.EfCore.Abstractions](../DKNet.EfCore.Abstractions/index.md) — use it to model entities; specifications also accept any class entity.
- [DKNet.EfCore.Extensions](../DKNet.EfCore.Extensions/index.md) — use it for global filters and concurrency-aware persistence helpers.
- [DKNet.EfCore.DtoGenerator](../DKNet.EfCore.DtoGenerator/index.md) — use it to generate model types for projections.
- [DKNet.EfCore.DataAuthorization](../DKNet.EfCore.DataAuthorization/index.md) — use it for non-ignorable row ownership filtering.
- [Repository migration guide](../EfCore/Migrating-Repos-To-Specifications.md) — use it to move from the removed Repos packages.
