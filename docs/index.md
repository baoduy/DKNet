# DKNet Framework

[![codecov](https://codecov.io/github/baoduy/DKNet/graph/badge.svg?token=xtNN7AtB1O)](https://codecov.io/github/baoduy/DKNet)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](../LICENSE)

A suite of 28 independent .NET 10 NuGet packages for building enterprise applications around **Domain-Driven Design**
and **Onion Architecture**.

## ✨ Why use it?

- **Take only what your `DbContext` or API needs.** Each package is opt-in and registers itself: there is no
  `AddDKNet()` and no package that only aggregates others.
- **One repository for every entity.** `DKNet.EfCore.Specifications` gives you one injected `IRepositorySpec` that
  takes a reusable specification, instead of a hand-rolled repository per entity.
- **Cross-cutting rules stay out of domain code.** Domain events, audit logs, row-level data authorization and column
  encryption attach to the `DbContext` through the `SaveChanges` hook pipeline or a value converter.
- **CQRS without MediatR.** `DKNet.SlimBus.Extensions` builds commands, queries and event consumers on
  SlimMessageBus, saves the `DbContext` after a successful write and forwards domain events onto the bus.
- **ASP.NET Core and service glue included.** Idempotent endpoints, startup background tasks, minimal-API endpoint
  groups, blob storage adapters, PDF generation and .NET Aspire integrations ship as their own packages.

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

Run with `dotnet run`. Prints `Coffee`.

Not sure which packages you need? The
[**Which package do I need?**](README.md#which-package-do-i-need) table maps problems to packages. For a complete
reference application, see the [SlimBus.ApiEndpoints template](https://github.com/baoduy/DKNet.Templates).

## Documentation

![The DKNet onion: presentation packages on top, the application ring below, the EF Core infrastructure ring in the middle, and DKNet.EfCore.Abstractions plus the dependency-free foundation packages at the centre. Every arrow is a project reference pointing inward.](diagrams/dknet-layers.svg)

| Guide | Read when |
|---|---|
| [Package index](README.md) | Find a package by area or by problem: all 28 published packages plus the source-only `Aspire.Hosting.ServiceBus`, each linking its own docs folder. |
| [Getting Started](Getting-Started.md) | Install the first packages and wire up a first working setup. |
| [Architecture Guide](Architecture.md) | See the onion rings, the package dependency graph, and a request and a domain event traced end to end. |
| [Configuration](Configuration.md) | Learn the registration conventions the packages share and read a worked `Program.cs`. |
| [Examples](Examples/README.md) | Copy a CRUD slice, domain events, specifications or a multi-tenant setup. |
| [FAQ](FAQ.md) | Look up a common question or a troubleshooting step. |
| [Migration Guide](Migration-Guide.md) | Upgrade between versions or move off a retired package. |
| [Testing Strategy](Testing-Strategy.md) · [Security](Security.md) · [Changelog](CHANGELOG.md) | Check the test stack, the supported versions, or what changed in a release. |
| [`dknet-skills` plugin](../plugins/dknet-skills/README.md) | Teach your coding agent (Claude Code, Cursor, Codex, Copilot and others) every DKNet package: `npx skills add baoduy/DKNet`, or `claude plugin marketplace add baoduy/DKNet && claude plugin install dknet-skills@dknet` in Claude Code. |
| [CONTRIBUTING.md](../CONTRIBUTING.md) · [SECURITY.md](../SECURITY.md) | Set up for development and open a pull request, or report a vulnerability. |
| [MIT License](../LICENSE) | Check the licence terms. |
