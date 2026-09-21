---
name: dknet-efcore-specifications
description: Covers DKNet.EfCore.Specifications -- Specification<TEntity>/ModelSpecification<TEntity,TModel> (WithFilter, AddInclude, AddOrderBy/AddOrderByDescending, Skip/Take, AsNoTracking, IgnoreQueryFilters), the non-generic IRepositorySpec (Query, ToPagedListAsync, ToPageEnumerable, BulkDeleteAsync, SaveChangesAsync) via AddSpecRepo<TDbContext>(), IRepositorySpecFactory for background jobs, and the Dynamic Predicate Builder: PredicateBuilder.New<T>().DynamicAnd/DynamicOr(propertyName, Ops, value), AsExpandable, LinqKit, TryBuildPredicate, the Ops enum (Equal..IsNotNull), plus keyset pagination (AfterKeyset/BeforeKeyset, ToKeysetPageAsync). Use for a runtime search/filter endpoint from a query string, paging/streaming a list, replacing a removed IRepository<T>/generic repository from DKNet.EfCore.Repos, or ModelSpecification projection with Mapster's IMapper.
license: MIT
metadata:
  author: baoduy
  version: "0.1.0"
  packages: "DKNet.EfCore.Specifications"
---

# DKNet specifications, IRepositorySpec and dynamic predicates

This skill teaches `DKNet.EfCore.Specifications`: the Specification pattern over EF Core (`Specification<TEntity>` / `ModelSpecification<TEntity,TModel>`), the single non-generic `IRepositorySpec` that executes them, and the Dynamic Predicate Builder that turns a runtime `(propertyName, Ops, value)` triple into a type-safe EF Core predicate. It is the direct successor to the removed `DKNet.EfCore.Repos`. Open [references/DKNet.EfCore.Specifications.md](references/DKNet.EfCore.Specifications.md) for the full API surface, diagnostics, and every gotcha with its source-verified evidence.

## Packages

| Package | Install | What it gives you | Depends on | Reference |
|---|---|---|---|---|
| `DKNet.EfCore.Specifications` | `dotnet add package DKNet.EfCore.Specifications` | `Specification<TEntity>` / `ModelSpecification<TEntity,TModel>`, the non-generic `IRepositorySpec` + `AddSpecRepo<TDbContext>()`, the Dynamic Predicate Builder (`DynamicAnd`/`DynamicOr`/`TryBuildPredicate`/`Ops`), offset paging (`ToPagedListAsync`) and keyset paging (`AfterKeyset`/`ToKeysetPageAsync`) | `DKNet.EfCore.Extensions` (project reference; supplies `GlobalQueryFilter`, `AddNewEntitiesFromNavigations`, `SaveChangesWithConcurrencyHandlingAsync`) | [references/DKNet.EfCore.Specifications.md](references/DKNet.EfCore.Specifications.md) |

## Quick start

```csharp
using DKNet.EfCore.Specifications;
using DKNet.EfCore.Specifications.Definitions;
using DKNet.EfCore.Specifications.Extensions;
using DKNet.EfCore.Specifications.Repositories;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

public sealed class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsActive { get; set; }
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
}

public sealed class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
}

// A Specification<TEntity> bundles filter + include + order-by. Builder methods
// (WithFilter, AddInclude, AddOrderBy...) are protected -- only callable from the
// subclass's own constructor.
public sealed class ActiveExpensiveProductsSpec : Specification<Product>
{
    public ActiveExpensiveProductsSpec(decimal minPrice)
    {
        WithFilter(p => p.IsActive && p.Price >= minPrice);
        AddInclude(p => p.Category);
        AddOrderByDescending(p => p.Price);
        AddOrderBy(p => p.Name);
    }
}

public static class PersistenceSetup
{
    public static IServiceCollection AddAppPersistence(this IServiceCollection services)
    {
        services.AddDbContext<AppDbContext>();
        services.AddSpecRepo<AppDbContext>();

        // Only needed for projected (TModel) queries -- Query<TEntity,TModel> throws
        // InvalidOperationException at query time if no IMapper is registered.
        services.AddSingleton<IMapper>(new Mapper(new TypeAdapterConfig()));
        return services;
    }
}

public sealed class ProductService(IRepositorySpec repo)
{
    public Task<List<Product>> ListActiveExpensiveAsync(decimal minPrice, CancellationToken ct) =>
        repo.ToListAsync(new ActiveExpensiveProductsSpec(minPrice), ct);
}
```

## Rules

1. Derive every specification from `Specification<TEntity>` (or `ModelSpecification<TEntity,TModel>`) -- never implement `ISpecification<TEntity>` directly. `ApplySpecs` only reads ordering, `Skip`/`Take`, and the `AsNoTracking` flag off the concrete `Specification<TEntity>` type.
2. Call `WithFilter`, `AddInclude`, `AddOrderBy`/`AddOrderByDescending`, `Skip`, `Take`, `AsNoTracking`, `IgnoreQueryFilters`, `CreatePredicate` only from inside your specification's own constructor -- they are `protected`. There is no public mutation API on a built specification.
3. Register with `services.AddSpecRepo<TDbContext>()` once per process. A second call with a *different* `TDbContext` is a silent no-op (`TryAddScoped` keys off `IRepositorySpec` alone) -- `IRepositorySpec` then only ever serves the first `TDbContext` registered.
4. Run a `DynamicAnd`/`DynamicOr`-built predicate only against a query wrapped in `.AsExpandable()`. `IRepositorySpec.Query` already applies it; only add it yourself when you bypass the repository and query a raw `DbSet`/`IQueryable` directly.
5. Do not null-guard before calling the triple-overload `DynamicAnd`/`DynamicOr`. It is fail-safe: an unresolvable property, a wrong-typed value, or a `null` value all just return the predicate unchanged, and `Equal`/`NotEqual` against `null` already compile to `IS NULL`/`IS NOT NULL`.
6. Use `TryBuildPredicate<T>` -- not `DynamicAnd`/`DynamicOr` -- at a trust boundary where a dropped filter must become a 400, not silently unfiltered data. It is the only Dynamic Predicate Builder entry point that reports failure through its `bool` return instead of no-op'ing.
7. Call `repo.BulkDeleteAsync<TEntity>(predicate, ct)` for a bulk delete. `DeleteRange` no longer exists on `IRepositorySpec` -- there is no load-then-delete-range replacement by design; `BulkDeleteAsync` is a server-side `ExecuteDeleteAsync`.
8. Add at least one `AddOrderBy`/`AddOrderByDescending` to any specification passed to `ToPageEnumerable`. It throws `NotSupportedException` immediately otherwise (`EnsureSpecHasOrdering`) -- paging an unordered query would return unstable or duplicate rows across page boundaries.
9. Treat `IRepositorySpecFactory.CreateAsync<TDbContext>()` as synchronous. It returns `IRepositorySpecProvider` directly, not `Task<IRepositorySpecProvider>` -- `await`ing it is a compile error, not a race.
10. `await using`/`Dispose` the `IRepositorySpecProvider` from `IRepositorySpecFactory.CreateAsync`. Disposing it disposes its own DI scope and factory-created `DbContext` together.
11. Match a keyset `keySelector`'s direction to the specification's own declared order. `AfterKeyset` assumes ascending (`WHERE key > cursor`); pair it with `AddOrderBy`, not `AddOrderByDescending`, or the cursor comparison and the result order disagree.

## How to ...

### Access the repository from a background job (no DI scope)

When: a hosted service, queued job, or console tool needs `IRepositorySpec` outside a normal request scope.

```csharp
using DKNet.EfCore.Specifications;
using DKNet.EfCore.Specifications.Extensions;
using DKNet.EfCore.Specifications.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

public static class BackgroundJobSetup
{
    public static IServiceCollection AddBackgroundJobSupport(this IServiceCollection services)
    {
        services.AddDbContextFactory<AppDbContext>();
        services.AddSpecRepo<AppDbContext>(); // idempotent; also registers IRepositorySpecFactory
        return services;
    }
}

public sealed class BackgroundExportJob(IRepositorySpecFactory factory)
{
    public async Task<int> CountActiveExpensiveAsync(CancellationToken ct)
    {
        await using var provider = factory.CreateAsync<AppDbContext>();
        return await provider.Repository.CountAsync(new ActiveExpensiveProductsSpec(50m), ct);
    }
}
```

Notes:
- `CreateAsync<TDbContext>()` is synchronous (see Rules #9); `await using` disposes the scope it creates.
- Requires `IDbContextFactory<AppDbContext>` registered via `AddDbContextFactory<AppDbContext>()`.

### Look up a single row via a reusable specification

When: a lookup that should stay declarative and testable instead of an inline LINQ expression.

```csharp
using DKNet.EfCore.Specifications.Extensions;
using DKNet.EfCore.Specifications.Repositories;
using System.Threading;
using System.Threading.Tasks;

public sealed class ProductLookupService(IRepositorySpec repo)
{
    public Task<Product?> FindActiveExpensiveAsync(decimal minPrice, CancellationToken ct) =>
        repo.FirstOrDefaultAsync(new ActiveExpensiveProductsSpec(minPrice), ct);
}
```

Notes:
- `FirstOrDefaultAsync` returns `null` on no match; `FirstAsync` throws `InvalidOperationException` instead.

### Build a runtime search filter from a query string

When: the filter shape (which fields, which operators) is only known at request time -- search boxes, admin grids, `?field=value` query strings.

```csharp
using DKNet.EfCore.Specifications.Definitions;
using DKNet.EfCore.Specifications.Dynamics;
using DKNet.EfCore.Specifications.Extensions;
using DKNet.EfCore.Specifications.Repositories;
using LinqKit;
using System.Threading;
using System.Threading.Tasks;

public sealed class ProductSearchSpecification : Specification<Product>
{
    public ProductSearchSpecification(string? name, decimal? minPrice, string? categoryName)
    {
        var predicate = CreatePredicate(p => p.IsActive);

        if (name is not null)
            predicate = predicate.DynamicAnd(nameof(Product.Name), Ops.Contains, name);

        if (minPrice is not null)
            predicate = predicate.DynamicAnd(nameof(Product.Price), Ops.GreaterThanOrEqual, minPrice);

        if (categoryName is not null)
            predicate = predicate.DynamicAnd("Category.Name", Ops.Equal, categoryName);

        WithFilter(predicate);
        AddOrderBy(p => p.Name);
    }
}

public sealed class ProductSearchService(IRepositorySpec repo)
{
    public Task<List<Product>> SearchAsync(string? name, decimal? minPrice, string? categoryName,
        CancellationToken ct) =>
        repo.ToListAsync(new ProductSearchSpecification(name, minPrice, categoryName), ct);
}
```

Notes:
- `.AsExpandable()` is applied automatically inside `IRepositorySpec.Query` -- no extra wiring here.
- Every optional filter composes with a plain `if`: an unresolvable property, wrong type, or `null` value makes `DynamicAnd` a no-op instead of throwing (Rules #5).
- `nameof(Product.Name)`, `"category.name"`, and `"Category.Name"` are all accepted -- the property-name argument is normalized to PascalCase per dotted segment.

### Reject an invalid filter at a trust boundary

When: an HTTP endpoint takes a raw query-string filter and must return 400 on an invalid one instead of quietly serving unfiltered data.

```csharp
using System.Linq.Expressions;
using DKNet.EfCore.Specifications.Dynamics;
using LinqKit;

public static class NameFilterValidator
{
    public static bool TryValidateNameFilter(string? rawValue, out Expression<Func<Product, bool>>? predicate)
    {
        if (rawValue is null)
        {
            predicate = null;
            return false;
        }

        return DynamicPredicateExtensions.TryBuildPredicate<Product>(
            nameof(Product.Name), Ops.Contains, rawValue, out predicate);
    }
}
```

Notes:
- Unlike `DynamicAnd`/`DynamicOr`, `TryBuildPredicate` reports failure explicitly through its `bool` return -- use it wherever a dropped filter would be a correctness problem, not just a no-op composition detail.

### Project straight to a DTO instead of the entity

When: a read path that should never materialize the full entity -- list/summary endpoints.

```csharp
using DKNet.EfCore.Specifications.Definitions;
using DKNet.EfCore.Specifications.Extensions;
using DKNet.EfCore.Specifications.Repositories;
using System.Threading;
using System.Threading.Tasks;

public sealed class ProductSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public sealed class ActiveProductSummariesSpec : ModelSpecification<Product, ProductSummaryDto>
{
    public ActiveProductSummariesSpec()
    {
        WithFilter(p => p.IsActive);
        AddOrderBy(p => p.Name);
    }
}

public sealed class ProductSummaryService(IRepositorySpec repo)
{
    public Task<List<ProductSummaryDto>> ListAsync(CancellationToken ct) =>
        repo.ToListAsync<Product, ProductSummaryDto>(new ActiveProductSummariesSpec(), ct);
}
```

Notes:
- Requires a Mapster `IMapper` registered in DI (Quick start above); `Query<TEntity,TModel>` throws `InvalidOperationException` at query time, not at startup, when none is found.
- A projected read is always `AsNoTracking()` regardless of whether the specification itself called it.
- Prefer `dknet-codegen`'s `[GenerateDto]` over hand-writing the `TModel` DTO.

### Page or stream a large result set

When: an API needs an offset page (`pageNumber`/`pageSize` with `IPagedList<T>`), or a job needs to walk every row without loading them all into memory at once.

```csharp
using DKNet.EfCore.Specifications.Extensions;
using DKNet.EfCore.Specifications.Repositories;
using System.Threading;
using System.Threading.Tasks;
using X.PagedList;

public sealed class ProductPagingService(IRepositorySpec repo)
{
    public Task<IPagedList<Product>> PageAsync(decimal minPrice, int pageNumber, int pageSize,
        CancellationToken ct) =>
        repo.ToPagedListAsync(new ActiveExpensiveProductsSpec(minPrice), pageNumber, pageSize, ct);

    public async Task<int> CountAllViaStreamingAsync(decimal minPrice, CancellationToken ct)
    {
        var count = 0;
        await foreach (var product in repo.ToPageEnumerable(new ActiveExpensiveProductsSpec(minPrice)))
        {
            ct.ThrowIfCancellationRequested();
            count++;
        }

        return count;
    }
}
```

Notes:
- `ToPageEnumerable` throws `NotSupportedException` immediately if the specification declares no ordering (Rules #8), and fetches 100 rows per round trip (fixed, not overridable through `IRepositorySpec`).
- `ToPagedListAsync` counts the whole filtered set for `IPagedList<T>.TotalItemCount` -- expect one extra `COUNT` query per call.

### Cursor (keyset) pagination for stable next-page links

When: "next page" links must stay fast and stable as the table grows -- offset paging degrades because `Skip(n)` still scans `n` rows.

```csharp
using DKNet.EfCore.Specifications.Definitions;
using DKNet.EfCore.Specifications.Extensions;
using DKNet.EfCore.Specifications.Repositories;
using System.Threading;
using System.Threading.Tasks;

public sealed class ActiveProductsByIdSpec : Specification<Product>
{
    public ActiveProductsByIdSpec()
    {
        WithFilter(p => p.IsActive);
        AddOrderBy(p => p.Id);
    }
}

public sealed class ProductKeysetPagingService(IRepositorySpec repo)
{
    public Task<List<Product>> NextPageAsync(int lastId, int pageSize, CancellationToken ct) =>
        repo.ToKeysetPageAsync(new ActiveProductsByIdSpec(), p => p.Id, lastId, pageSize, ct);
}
```

Notes:
- `keySelector` must match the specification's own declared order direction (Rules #11) -- ascending here, via `AddOrderBy`.
- For a raw `IQueryable<TEntity>` (no specification), `AfterKeyset`/`BeforeKeyset` add only the `WHERE` cursor predicate -- you own `OrderBy` yourself; see [references/DKNet.EfCore.Specifications.md](references/DKNet.EfCore.Specifications.md) for the composite (two-key) and arbitrary-arity (`ToKeysetPageAsync` with `KeysetPaginationBuilder`) overloads.

### Migrate a call site off the removed generic repository

When: existing code still calls the removed `IRepository<T>`/`IRepositoryFactory` from `DKNet.EfCore.Repos` and needs the `DKNet.EfCore.Specifications` equivalent.

```csharp
using DKNet.EfCore.Specifications.Definitions;
using DKNet.EfCore.Specifications.Extensions;
using DKNet.EfCore.Specifications.Repositories;
using System.Threading;
using System.Threading.Tasks;

public sealed class OrderByIdSpecification : Specification<Product>
{
    public OrderByIdSpecification(int id)
    {
        WithFilter(p => p.Id == id);
    }
}

public sealed class ProductLegacyLookupService(IRepositorySpec repo)
{
    // Old: IRepository<Product> repo; repo.FindAsync(id, ct)
    public Task<Product?> FindAsync(int id, CancellationToken ct) =>
        repo.FirstOrDefaultAsync(new OrderByIdSpecification(id), ct);
}
```

Notes:
- There is no direct `FindAsync(keyValue, ct)` equivalent -- write a specification that filters on the key and call `FirstOrDefaultAsync`.
- Full call-site mapping table: https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/Migrating-Repos-To-Specifications.md

## Runtime behaviour

1. `IRepositorySpec.Query<TEntity>` wraps `_dbContext.Set<TEntity>()` in `.AsExpandable()` *before* applying the specification, so a `DynamicAnd`/`DynamicOr`-built predicate always translates correctly.
2. Applying a specification (internal `ApplySpecs`) runs in a fixed order: ignorable query filters (if `IgnoreQueryFilters()` was called and any ignorable filter keys are registered) -> `WithFilter`'s `Where` -> `AddInclude` expressions -> `AddInclude` chains -> declared-sequence `OrderBy`/`OrderByDescending`/`ThenBy`/`ThenByDescending` -> `AsNoTracking()` -> `Skip`/`Take`.
3. `Query<TEntity,TModel>` additionally calls `.AsNoTracking().ProjectToType<TModel>(mapper.Config)` after step 2 -- always non-tracking, regardless of whether the specification itself called `AsNoTracking()`.
4. `RepositorySpec<TDbContext>.SaveChangesAsync` calls `AddNewEntitiesFromNavigations`, then resolves a keyed `IEfCoreExceptionHandler` for the `DbContext`'s type name (if registered), then calls `SaveChangesWithConcurrencyHandlingAsync`. Hooks/Events/AuditLogs interceptors on the same `DbContext` still run -- it is the same underlying `DbContext.SaveChangesAsync` pipeline.
5. `ToPageEnumerable` fetches 100 rows per round trip (`Skip(page * 100).Take(100)`) and stops once a page returns fewer than 100 rows.

## Gotchas

- **`IRepositorySpecFactory.CreateAsync<TDbContext>()` is synchronous despite the name** -> `await`ing it is a compile error, not a race -> call it without `await` and dispose the returned provider (Rules #9-10).
- **`Ops.IsNull`/`Ops.IsNotNull` exist but ignore the `value` argument entirely** -> they build `{prop} == null`/`{prop} != null` directly -> pass any placeholder (or `null`) as the third argument.
- **A filtered `AddInclude` on a *tracking* query can surface children that don't match the filter** -> EF Core's navigation fixup reattaches already-tracked entities regardless of the include filter -> use a projected (`TModel`) read or `AsNoTracking()` when the filtered include must be exact.
- **A foreign `ISpecification<TEntity>` (not deriving from `Specification<TEntity>`) contributes no ordering, `Skip`/`Take`, or tracking flag** -> `ApplySpecs` only reads those from the concrete `Specification<TEntity>` type -> always derive from `Specification<TEntity>`/`ModelSpecification<TEntity,TModel>`.
- **`AddOrderBy(string, ListSortDirection)` throws on an unresolvable property; the Dynamic Predicate Builder does not** -> the string-ordering overload uses reflection (`Expression.PropertyOrField`) and throws, while `DynamicAnd`/`DynamicOr`/`TryBuildPredicate` silently no-op or return `false` -> the two "runtime property name" surfaces have different failure modes; do not assume one behaves like the other.
- **`ToKeysetPageAsync`'s `HasPreviousAsync`/`HasNextAsync` ignore the `CancellationToken`** -> only the page query itself observes cancellation (an `MR.EntityFrameworkCore.KeysetPagination` 1.6.0 limitation, not a DKNet bug) -> don't rely on cancelling those two existence checks.
- **`ToPageEnumerable`'s page size (100) is not overridable through `IRepositorySpec`** -> the only overload that takes an explicit page size is `internal` -> if 100 is wrong for your workload, page manually with `ToPagedListAsync`/`Skip`/`Take` instead.

## Do not

- `IRepository<T>`, `IReadRepository<T>`, `IWriteRepository<T>`, `IRepositoryFactory`, `SetupRepository`, `RepoExtensions`, `Repository<T>`, `ReadRepository<T>`, `WriteRepository<T>`, `RepositoryFactory<TDbContext>` -- none exist. They belonged to the removed `DKNet.EfCore.Repos`/`DKNet.EfCore.Repos.Abstractions`. Use `IRepositorySpec` + `Specification<TEntity>` instead.
- `IRepositorySpec<TEntity>` -- does not exist. `IRepositorySpec` is non-generic; the entity type is a per-method type parameter.
- `repo.DeleteRange(entities)` / `IRepositorySpec.DeleteRangeAsync` -- removed, no direct replacement. Use `repo.BulkDeleteAsync<TEntity>(predicate, ct)`.
- `Ops.Like`, `Ops.Between`, `Ops.NotNull`, `Ops.IsEmpty` -- none exist. The full enum is `Equal, NotEqual, GreaterThan, GreaterThanOrEqual, LessThan, LessThanOrEqual, Contains, NotContains, StartsWith, EndsWith, In, NotIn, IsNull, IsNotNull`.
- `services.AddSpecifications<TDbContext>()`, `services.AddRepositorySpec<TDbContext>()` -- wrong name; it is `services.AddSpecRepo<TDbContext>()`.
- `spec.OrderBy(...)`, `spec.Where(...)`, `spec.Include(...)` -- wrong names. The protected builders are `AddOrderBy`/`AddOrderByDescending`, `WithFilter`, `AddInclude`.
- Calling a specification's builder methods (`WithFilter`, `AddInclude`, `AddOrderBy*`, `Skip`, `Take`, `AsNoTracking`, `IgnoreQueryFilters`, `CreatePredicate`) from outside its own constructor -- they are all `protected`; there is no public mutation API on a built specification.
- ```csharp
  // no-compile
  var predicate = PredicateBuilder.New<Product>()
      .And(p => p.IsActive)
      .DynamicAnd("Price", Ops.GreaterThan, 100m);
  var results = await _db.Products.Where(predicate).ToListAsync(); // missing .AsExpandable()
  ```
  Running a `DynamicAnd`/`DynamicOr` predicate against a raw `DbSet`/`IQueryable` without `.AsExpandable()` fails or silently mistranslates. Only needed when bypassing `IRepositorySpec` -- the repository already applies it.
- `if (value != null) predicate.DynamicAnd(...)` guards before every dynamic filter -- unnecessary, and it risks dropping a legitimate `null`-equality filter (`Equal`/`NotEqual` against `null` already compile to `IS NULL`/`IS NOT NULL`).
- `await factory.CreateAsync<TDbContext>()` -- `CreateAsync` is synchronous; awaiting its return value is a compile error, not a race (Rules #9).
- `services.AddSpecRepo<Db1>(); services.AddSpecRepo<Db2>();` expecting both to work -- the second call is a silent no-op; `IRepositorySpec` in that process only ever serves `Db1` (Rules #3).

## Related skills

- `dknet-packages` -- confirm this is the right package, and the wiring order for a new project, before reaching for specifications directly.
- `dknet-efcore-domain-model` -- entity base classes (`Entity<TKey>`) and `DbContext` model wiring that a `Specification<TEntity>` targets.
- `dknet-efcore-save-pipeline` -- what runs inside `RepositorySpec<TDbContext>.SaveChangesAsync` (hooks, domain events, audit logs) beyond this package's own concerns.
- `dknet-efcore-data-security` -- the row-level filter `IgnoreQueryFilters()` never bypasses, and column encryption on the entities a specification queries.
- `dknet-codegen` -- generate the `TModel` DTO a `ModelSpecification<TEntity,TModel>` projects onto instead of hand-writing it.
- `dknet-slimbus-cqrs` -- the CQRS handler that fetches via `IRepositorySpec`/a specification, mutates the aggregate, and persists through the same `SaveChangesAsync`.
- `dknet-aspcore-api` -- turn a specification-backed query into a minimal-API endpoint.
- `dknet-testing` -- TestContainers fixtures and the `ToQueryString()` SQL-assertion pattern used to test specifications.

## References

- [references/DKNet.EfCore.Specifications.md](references/DKNet.EfCore.Specifications.md) -- full public surface, options, usage patterns, runtime behaviour, diagnostics, gotchas, anti-patterns, composition, and testing notes for the package.
- Docs: https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/DKNet.EfCore.Specifications.md
- Migration guide: https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/Migrating-Repos-To-Specifications.md
- Web docs: https://baoduy.github.io/DKNet/
- NuGet: https://www.nuget.org/packages/DKNet.EfCore.Specifications
