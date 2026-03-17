# DKNet.EfCore.Repos.Abstractions — AI Skill File

> **Package**: `DKNet.EfCore.Repos.Abstractions`  
> **Minimum Version**: `10.0.0`  
> **Minimum .NET**: `net10.0`  
> **Source**: `src/EfCore/DKNet.EfCore.Repos.Abstractions/`

---

## Purpose

Defines the `IReadRepository<T>`, `IWriteRepository<T>`, and `IRepository<T>` contracts that all DKNet data-access code is written against, enabling CQRS-aligned separation between queries and commands without coupling handlers to EF Core directly.

---

## When To Use

- ✅ Injecting read-only data access into query handlers — use `IReadRepository<T>`
- ✅ Injecting write (create/update/delete) access into command handlers — use `IWriteRepository<T>`
- ✅ When a class needs both read and write — use `IRepository<T>` (extends both)
- ✅ Defining custom repository interfaces that extend these contracts

## When NOT To Use

- ❌ Injecting `DbContext` directly into handlers — use repositories instead
- ❌ Calling `.ToList()` before filtering — always push filters to the database via `IQueryable`

---

## Installation

```bash
dotnet add package DKNet.EfCore.Repos.Abstractions
```

---

## Setup

No DI registration here. Register implementations via `DKNet.EfCore.Repos`:

```csharp
services.AddGenericRepositories<AppDbContext>();
```

---

## Key API Surface

### `IReadRepository<TEntity>`

| Method | Signature | Notes |
|---|---|---|
| `FindAsync` | `ValueTask<TEntity?> FindAsync(object keyValue, ct)` | Find by primary key |
| `FindAsync` | `Task<TEntity?> FindAsync(Expression<Func<TEntity,bool>> filter, ct)` | Find by predicate |
| `Query()` | `IQueryable<TEntity>` | Full queryable — chain `.Where`, `.Select`, `.OrderBy` |
| `Query(filter)` | `IQueryable<TEntity>` | Pre-filtered queryable |
| `Query<TModel>(filter)` | `IQueryable<TModel>` | Mapster projection queryable |
| `CountAsync` | `Task<int> CountAsync(filter, ct)` | Count matching rows |
| `ExistsAsync` | `Task<bool> ExistsAsync(filter, ct)` | Check existence |

### `IWriteRepository<TEntity>`

| Method | Signature | Notes |
|---|---|---|
| `AddAsync` | `ValueTask AddAsync(entity, ct)` | Track new entity |
| `AddRangeAsync` | `ValueTask AddRangeAsync(entities, ct)` | Track many |
| `UpdateAsync` | `Task<int> UpdateAsync(entity, ct)` | Apply changes |
| `Delete` | `void Delete(entity)` | Mark for deletion |
| `DeleteRange` | `void DeleteRange(entities)` | Mark many |
| `SaveChangesAsync` | `Task<int> SaveChangesAsync(ct)` | Persist (called automatically by SlimBus auto-save) |
| `BeginTransactionAsync` | `Task<IDbContextTransaction> BeginTransactionAsync(ct)` | Explicit transaction |

---

## Usage Pattern

```csharp
// ── Command handler — write only ─────────────────────────────────────────
public class DeleteProductHandler(IWriteRepository<Product> repo)
    : Fluents.Requests.IHandler<DeleteProductCommand>
{
    public async Task<IResultBase> Handle(
        DeleteProductCommand request, CancellationToken cancellationToken)
    {
        var product = await repo.FindAsync(request.Id, cancellationToken);  // ← uses IReadRepository via IWriteRepository (IRepository extends both)
        if (product is null) return Results.Fail("Product not found");

        repo.Delete(product);
        // SaveChanges called automatically by EfAutoSave interceptor
        return Results.Ok();
    }
}

// ── Query handler — read only ─────────────────────────────────────────────
public class GetProductHandler(IReadRepository<Product> repo)
    : Fluents.Queries.IHandler<GetProductQuery, ProductDto>
{
    public async Task<ProductDto?> Handle(
        GetProductQuery request, CancellationToken cancellationToken)
        => await repo.Query(p => p.Id == request.Id)
                     .AsNoTracking()
                     .Select(p => new ProductDto(p.Id, p.Name, p.Price))
                     .FirstOrDefaultAsync(cancellationToken);
}
```

---

## Anti-Patterns

```csharp
// ❌ WRONG — injecting DbContext directly into a handler
public class MyHandler(AppDbContext db) : ...
{
    public async Task<...> Handle(...)
        => await db.Products.Where(p => p.IsActive).ToListAsync();
}

// ✅ CORRECT — inject repository abstraction
public class MyHandler(IReadRepository<Product> repo) : ...

// ❌ WRONG — in-memory filtering (materialise then filter)
var all = await repo.Query().ToListAsync();
var active = all.Where(p => p.IsActive);    // ← filter happens in .NET, not SQL

// ✅ CORRECT — push filter to database
var active = await repo.Query(p => p.IsActive).ToListAsync(cancellationToken);

// ❌ WRONG — calling SaveChangesAsync manually when using SlimBus auto-save
await repo.AddAsync(product, ct);
await repo.SaveChangesAsync(ct);    // ← double-save risk with EfAutoSave interceptor

// ✅ CORRECT — omit SaveChangesAsync inside SlimBus handlers
await repo.AddAsync(product, ct);
// auto-save fires after handler returns
```

---

## Composes With

| Library | How |
|---|---|
| `DKNet.EfCore.Repos` | Provides concrete implementations — `AddGenericRepositories<TDbContext>()` |
| `DKNet.EfCore.Specifications` | Use `IRepositorySpec` (extends read repo) with `Specification<T>` for complex queries |
| `DKNet.SlimBus.Extensions` | Inject into command/query handlers |

---

## Test Example

```csharp
[Fact]
public async Task FindAsync_ExistingId_ReturnsEntity()
{
    // Arrange — uses TestContainers SQL Server, NOT InMemoryDatabase
    await using var db = CreateDbContext(_container.GetConnectionString());
    IReadRepository<Product> repo = new ReadRepository<Product>(db);
    var product = new Product { Name = "Widget", Price = 9.99m };
    db.Products.Add(product);
    await db.SaveChangesAsync();

    // Act
    var found = await repo.FindAsync(product.Id);

    // Assert
    found.ShouldNotBeNull();
    found!.Name.ShouldBe("Widget");
}
```

---

## Quick Decision Guide

- Inject `IReadRepository<T>` for query handlers.
- Inject `IWriteRepository<T>` for command handlers.
- Inject `IRepository<T>` only when one component truly needs both concerns.

---

## Version

| Version | Notes |
|---|---|
| `10.0.0` | Initial documentation |
