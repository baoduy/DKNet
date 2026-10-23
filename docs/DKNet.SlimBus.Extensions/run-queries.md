# Run read queries

Part of [DKNet.SlimBus.Extensions](index.md). Mark a read with `Fluents.Queries.IWitResponse<TResponse>` (a single
item) or `IWitPageResponse<TResponse>` (a page), then implement its handler. The return type is `TResponse?`, not a
`FluentResults` wrapper — a query answers "found / not found", it does not carry a business-failure result the way a
write does.

`IWitPageResponse<TResponse>` returns `X.PagedList`'s `IPagedList<T>`. This package already references
`X.PagedList.EF`, so an EF Core `IQueryable` can page server-side with `ToPagedListAsync`. Use a .NET 10 console
project, then replace `Program.cs` with this example:

```bash
dotnet new console -f net10.0 -n RunQueriesDemo
cd RunQueriesDemo
dotnet add package DKNet.SlimBus.Extensions
dotnet add package SlimMessageBus.Host.Memory --version 3.5.0
dotnet add package Microsoft.EntityFrameworkCore.Sqlite --version 10.0.12
```

```csharp
using DKNet.SlimBus.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlimMessageBus;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;
using X.PagedList;
using X.PagedList.EF;

await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
await connection.OpenAsync();
var services = new ServiceCollection();
services.AddLogging();
services.AddDbContext<AppDbContext>(o => o.UseSqlite(connection));
services.AddSlimMessageBus(mbb => mbb
    .AddServicesFromAssembly(typeof(Program).Assembly)
    .AddChildBus("Memory", m => m.WithProviderMemory().AutoDeclareFrom(typeof(Program).Assembly)));

await using var provider = services.BuildServiceProvider();
await using (var scope = provider.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    db.Products.Add(new Product(Guid.NewGuid(), "Gadget", 5m));
    db.Products.Add(new Product(Guid.NewGuid(), "Gizmo", 7m));
    await db.SaveChangesAsync();
}

var bus = provider.GetRequiredService<IMessageBus>();
var page = await bus.Send(new GetProductsPage(1, 1));
Console.WriteLine($"Page 1 of size 1 -> TotalItemCount={page.TotalItemCount}, PageCount={page.PageCount}, " +
                   $"Item={page.First().Name}");

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
}

public sealed class Product
{
    public Product(Guid id, string name, decimal price)
    {
        Id = id;
        Name = name;
        Price = price;
    }

    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public sealed record ProductDto(Guid Id, string Name);

public record GetProductsPage(int PageIndex, int PageSize) : Fluents.Queries.IWitPageResponse<ProductDto>;

internal sealed class GetProductsPageHandler(AppDbContext db)
    : Fluents.Queries.IPageHandler<GetProductsPage, ProductDto>
{
    public Task<IPagedList<ProductDto>> OnHandle(GetProductsPage request, CancellationToken cancellationToken) =>
        db.Products.AsNoTracking()
            .Select(p => new ProductDto(p.Id, p.Name))
            .ToPagedListAsync(request.PageIndex, request.PageSize, null, cancellationToken);
}
```

Run with `dotnet run`. Prints `Page 1 of size 1 -> TotalItemCount=2, PageCount=2, Item=Gadget`.

A single-item query (`Fluents.Queries.IWitResponse<TResponse>`) follows the same shape — implement
`Fluents.Queries.IHandler<TQuery, TResponse>` and return `null` for a miss instead of `Result.Fail(...)`.

![A query implementing IWitResponse<T> or IWitPageResponse<T> is sent through IMessageBus, the matching IHandler or IPageHandler runs and returns T? or IPagedList<T> directly, and queries are never auto-saved even if the handler changed the DbContext.](diagrams/run-queries.svg)

For an in-memory source instead of an EF Core `IQueryable`, build the page from any `IEnumerable<T>` with
`X.PagedList`'s own `ToPagedList(...)`.

## 🧱 Where it fits

[DKNet.EfCore.Specifications](../DKNet.EfCore.Specifications/index.md) is the recommended way to express the query
behind a page handler as a reusable specification. `DKNet.AspCore.Extensions` maps `IWitResponse<T>` to `MapGet`
(200/404) and `IWitPageResponse<T>` to `MapGetPage` (200 with a `PagedResponse<T>` envelope).

## ⚠️ Gotchas & limits

- **Queries are never auto-saved**, whatever a handler leaves pending in the change tracker — see
  [Auto-save after a successful write](auto-save-dbcontext.md). A query that must persist something has to call
  `SaveChangesAsync` itself.
- **`Fluents.Queries.IWitResponse<T>` and `Fluents.Requests.IWitResponse<T>` are different interfaces** with the same
  name in different nested classes. Import them explicitly enough to keep them apart.
- **A missing item is `null`, not a thrown exception.** `OnHandle` returning `null` is the expected "not found" path,
  not an error condition to guard against.
