# DKNet.EfCore.Specifications — AI Skill File

> **Package**: `DKNet.EfCore.Specifications`  
> **Minimum Version**: `10.0.0`  
> **Minimum .NET**: `net10.0`  
> **Source**: `src/EfCore/DKNet.EfCore.Specifications/`

---

## Purpose

Encapsulates all query logic (filter, includes, ordering, pagination) into reusable, composable `Specification<T>` classes that execute entirely on the database, eliminating duplicated LINQ and preventing in-memory filtering.

---

## When To Use

- ✅ Any query that needs a filter, related-entity includes, or a sort order
- ✅ Reusable queries used in more than one place
- ✅ Dynamic runtime filters (search term, price range, status) via `DynamicPredicateBuilder`
- ✅ Paginated queries via `repo.ToPagedListAsync(spec, page, size, ct)`

## When NOT To Use

- ❌ Trivial single-field primary-key lookups — use `repo.FindAsync(id)` directly
- ❌ Queries that project to a DTO immediately — use `IReadRepository<T>.Query<TModel>(filter)` with Mapster for simpler projections

---

## Installation

```bash
dotnet add package DKNet.EfCore.Specifications
```

---

## Setup / DI Registration

```csharp
// Program.cs
using DKNet.EfCore.Specifications;

// Registers IRepositorySpec<T> on top of existing IReadRepository<T>
services.AddSpecifications<AppDbContext>();
```

---

## Key API Surface

### `Specification<TEntity>` (base class — inherit this)

| Member | Role |
|---|---|
| `WithFilter(expr)` | Set the WHERE clause |
| `AddInclude(expr)` | Add an `.Include(...)` eager load |
| `AddOrderBy(expr)` | Add ascending ORDER BY |
| `AddOrderByDescending(expr)` | Add descending ORDER BY |
| `AddOrderBy(propertyName, direction)` | Dynamic string-based ORDER BY |
| `IgnoreQueryFilters()` | Bypass soft-delete / multi-tenant global filters |
| `CreatePredicate(expr?)` | Returns a `LinqKit.ExpressionStarter<T>` for composing dynamic predicates |

### Extension methods on `IRepositorySpec`

| Method | Role |
|---|---|
| `repo.ToListAsync<T>(spec, ct)` | Execute spec and return list |
| `repo.FirstOrDefaultAsync<T>(spec, ct)` | Execute spec and return first or null |
| `repo.AnyAsync<T>(spec, ct)` | Execute spec and return bool |
| `repo.CountAsync<T>(spec, ct)` | Execute spec and return count |
| `repo.ToPagedListAsync<T>(spec, page, size, ct)` | Execute spec and return paged list |

### Dynamic predicates (LinqKit + System.Linq.Dynamic.Core)

| Member | Role |
|---|---|
| `PredicateBuilder.New<T>()` | Create a composable predicate starter |
| `.DynamicAnd(builder => builder.With("Prop", Op, value))` | Add null-safe AND condition |
| `.DynamicOr(builder => builder.With("Prop", Op, value))` | Add null-safe OR condition |
| `.AsExpandable()` | **Required** on queryable before `.Where(predicate)` with LinqKit |

---

## Usage Pattern

```csharp
// ── Static specification ──────────────────────────────────────────────────
/// <summary>Specification for active products in a category, ordered by name.</summary>
public class ActiveProductsByCategorySpec : Specification<Product>
{
    /// <summary>Initializes the specification with a category filter.</summary>
    public ActiveProductsByCategorySpec(Guid categoryId)
    {
        WithFilter(p => p.IsActive && !p.IsDeleted && p.CategoryId == categoryId);
        AddInclude(p => p.Category);
        AddOrderBy(p => p.Name);
    }
}

// Usage in query handler
public class ListProductsHandler(IRepositorySpec repo)
    : Fluents.Queries.IPageHandler<ListProductsQuery, ProductDto>
{
    public async Task<IPagedList<ProductDto>> Handle(
        ListProductsQuery request, CancellationToken cancellationToken)
    {
        var spec = new ActiveProductsByCategorySpec(request.CategoryId);
        var products = await repo.ToPagedListAsync<Product>(
            spec, request.Page, request.PageSize, cancellationToken);
        return products.ToMappedPagedList<ProductDto>();
    }
}

// ── Dynamic specification ─────────────────────────────────────────────────
/// <summary>Specification for searching products with optional runtime filters.</summary>
public class ProductSearchSpec : Specification<Product>
{
    public ProductSearchSpec(string? searchTerm, decimal? minPrice, Guid? categoryId)
    {
        var predicate = CreatePredicate(p => !p.IsDeleted);

        if (!string.IsNullOrWhiteSpace(searchTerm))
            predicate = predicate.DynamicAnd(b => b
                .With("Name", FilterOperations.Contains, searchTerm));

        if (minPrice.HasValue)
            predicate = predicate.DynamicAnd(b => b
                .With("Price", FilterOperations.GreaterThanOrEqual, minPrice.Value));

        if (categoryId.HasValue)
            predicate = predicate.DynamicAnd(b => b
                .With("CategoryId", FilterOperations.Equal, categoryId.Value));

        WithFilter(predicate);
        AddOrderBy(p => p.Name);
    }
}
```

---

## Anti-Patterns

```csharp
// ❌ WRONG — filtering in memory after materialisation
var all = await repo.ToListAsync<Product>(new AllProductsSpec(), ct);
var filtered = all.Where(p => p.Price > 50);   // ← SQL loads all rows

// ✅ CORRECT — embed filter in the specification
public class ProductsAbovePriceSpec : Specification<Product>
{
    public ProductsAbovePriceSpec(decimal minPrice)
        => WithFilter(p => p.Price > minPrice);
}

// ❌ WRONG — forgetting .AsExpandable() when using dynamic predicates outside a spec
var predicate = PredicateBuilder.New<Product>().And(p => p.IsActive);
var results = db.Products.Where(predicate).ToList();  // ← will throw or silently ignore

// ✅ CORRECT — add .AsExpandable() before .Where()
var results = db.Products.AsExpandable().Where(predicate).ToList();

// ❌ WRONG — calling WithFilter multiple times (last call wins, first is discarded)
WithFilter(p => p.IsActive);
WithFilter(p => p.Price > 0);   // ← overwrites the first filter!

// ✅ CORRECT — compose into a single expression
WithFilter(p => p.IsActive && p.Price > 0);
// OR use CreatePredicate for dynamic composition
```

---

## Composes With

| Library | How |
|---|---|
| `DKNet.EfCore.Repos` / `DKNet.EfCore.Repos.Abstractions` | `IRepositorySpec` is the spec-aware read repo; `AddSpecifications<T>()` extends the existing repo registration |
| `DKNet.SlimBus.Extensions` | Inject `IRepositorySpec` into query handlers |

---

## Test Example

```csharp
// Uses TestContainers.MsSql-backed integration test environment.
[Fact]
public async Task ActiveProductsByCategorySpec_ReturnsOnlyMatchingProducts()
{
    // Arrange
    await using var db = CreateDbContext(_container.GetConnectionString());
    var categoryId = Guid.NewGuid();
    db.Products.AddRange(
        new Product { Name = "A", CategoryId = categoryId, IsActive = true },
        new Product { Name = "B", CategoryId = categoryId, IsActive = false }, // excluded
        new Product { Name = "C", CategoryId = Guid.NewGuid(), IsActive = true }); // excluded
    await db.SaveChangesAsync();
    IRepositorySpec repo = new SpecRepository(db);

    // Act
    var spec = new ActiveProductsByCategorySpec(categoryId);
    var results = await repo.ToListAsync<Product>(spec);

    // Assert
    results.Count.ShouldBe(1);
    results[0].Name.ShouldBe("A");
}
```

---

## Quick Decision Guide

- Use a dedicated `Specification<T>` when a query has reusable filter/order/include logic.
- Use dynamic predicates when filter inputs are runtime-dependent.
- Call `.AsExpandable()` whenever LinqKit-composed predicates are applied directly to queryables.

---

## Version

| Version | Notes |
|---|---|
| `10.0.0` | Initial documentation — `Specification<T>`, dynamic predicate builder, `IRepositorySpec` extensions |
