# DKNet.EfCore.Repos — AI Skill File

> **Package**: `DKNet.EfCore.Repos`  
> **Minimum Version**: `10.0.0`  
> **Minimum .NET**: `net10.0`  
> **Source**: `src/EfCore/DKNet.EfCore.Repos/`

---

## Purpose

Provides concrete EF Core implementations of `IReadRepository<T>`, `IWriteRepository<T>`, and `IRepository<T>`, plus a single `AddGenericRepositories<TDbContext>()` extension that auto-registers all three for every entity in the context.

---

## When To Use

- ✅ Registering the repository implementations in DI — always pair with the Abstractions package
- ✅ When you need Mapster projections via `IRepository<T>.Query<TModel>(filter)` without writing manual `Select` projections
- ✅ When you need transaction management via `BeginTransactionAsync`

## When NOT To Use

- ❌ For complex filtered queries — use `DKNet.EfCore.Specifications` on top of this
- ❌ Bypassing abstractions by injecting the concrete class (e.g., `EfReadRepository<T>`) directly — always inject the interface

---

## Installation

```bash
dotnet add package DKNet.EfCore.Repos
```

---

## Setup / DI Registration

```csharp
// Program.cs
using DKNet.EfCore.Repos;

services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(configuration.GetConnectionString("Default")));

// Registers IRepository<T>, IReadRepository<T>, IWriteRepository<T> for every entity
services.AddGenericRepositories<AppDbContext>();

// Optional: Mapster configuration for projections
services.AddMapster();
```

---

## Key API Surface

| Method | Role |
|---|---|
| `services.AddGenericRepositories<TDbContext>()` | Auto-registers all three repo interfaces for every `DbSet<T>` in the context |
| `IRepository<T>.Query<TModel>(filter)` | Returns a Mapster-projected `IQueryable<TModel>` — efficient SELECT |

---

## Usage Pattern

```csharp
// Query handler with Mapster projection
public class ListProductsHandler(IReadRepository<Product> repo)
    : Fluents.Queries.IPageHandler<ListProductsQuery, ProductDto>
{
    public async Task<IPagedList<ProductDto>> Handle(
        ListProductsQuery request, CancellationToken cancellationToken)
        // Mapster projects directly in SQL — no full entity load
        => await repo.Query<ProductDto>(p => p.IsActive)
                     .OrderBy(p => p.Name)
                     .ToPagedListAsync(request.Page, request.PageSize, cancellationToken);
}

// Command handler with transaction
public class TransferStockHandler(IWriteRepository<Product> repo)
    : Fluents.Requests.IHandler<TransferStockCommand>
{
    public async Task<IResultBase> Handle(
        TransferStockCommand request, CancellationToken cancellationToken)
    {
        await using var tx = await repo.BeginTransactionAsync(cancellationToken);
        try
        {
            var source = await repo.FindAsync(request.SourceId, cancellationToken);
            var target = await repo.FindAsync(request.TargetId, cancellationToken);
            if (source is null || target is null) return Results.Fail("Product not found");

            source.Stock -= request.Quantity;
            target.Stock += request.Quantity;
            await repo.UpdateAsync(source, cancellationToken);
            await repo.UpdateAsync(target, cancellationToken);
            await repo.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return Results.Ok();
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
```

---

## Anti-Patterns

```csharp
// ❌ WRONG — injecting the concrete type
public class MyHandler(EfReadRepository<Product> repo) ...   // ← concrete class

// ✅ CORRECT — inject the interface
public class MyHandler(IReadRepository<Product> repo) ...

// ❌ WRONG — loading all then filtering
var all = await repo.Query().ToListAsync(ct);
var cheap = all.Where(p => p.Price < 10);   // ← SQL loads everything

// ✅ CORRECT — filter in the query
var cheap = await repo.Query(p => p.Price < 10).ToListAsync(ct);

// ❌ WRONG — using Mapster projection on the IWriteRepository (not available)
var dto = await _writeRepo.Query<ProductDto>(...);  // IWriteRepository has no Query<TModel>

// ✅ CORRECT — inject IReadRepository or IRepository for projection queries
var dto = await _readRepo.Query<ProductDto>(...);
```

---

## Composes With

| Library | How |
|---|---|
| `DKNet.EfCore.Repos.Abstractions` | **Required** — this package implements those interfaces |
| `DKNet.EfCore.Specifications` | Use `IRepositorySpec` (registered by Specifications package) for spec-based queries |
| `DKNet.SlimBus.Extensions` | Inject repos into command/query handlers |

---

## Test Example

```csharp
// Uses TestContainers.MsSql-backed integration test environment.
[Fact]
public async Task AddAsync_ThenQuery_ReturnsProjectedDto()
{
    // Arrange
    await using var db = CreateDbContext(_container.GetConnectionString());
    db.RegisterMapsterConfig();  // map Product → ProductDto
    services.AddGenericRepositories<AppDbContext>();
    var repo = new EfRepository<Product>(db);

    // Act
    await repo.AddAsync(new Product { Name = "Gadget", Price = 29.99m, IsActive = true });
    await db.SaveChangesAsync();

    var dtos = await repo.Query<ProductDto>(p => p.IsActive).ToListAsync();

    // Assert
    dtos.ShouldNotBeEmpty();
    dtos[0].Name.ShouldBe("Gadget");
}
```

---

## Quick Decision Guide

- Use this package to register and consume concrete repo implementations.
- Pair with `DKNet.EfCore.Repos.Abstractions` in all handler/service code.
- Move complex filtering into `DKNet.EfCore.Specifications` instead of ad-hoc LINQ chains.

---

## Version

| Version | Notes |
|---|---|
| `10.0.0` | Initial documentation |
