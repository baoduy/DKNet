# DKNet.EfCore.Specifications

| Field | Value |
|---|---|
| Area | EfCore |
| NuGet | `dotnet add package DKNet.EfCore.Specifications` |
| Docs | https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/DKNet.EfCore.Specifications.md |
| Source | https://github.com/baoduy/DKNet/tree/dev/src/EfCore/DKNet.EfCore.Specifications |
| Depends on (DKNet) | `DKNet.EfCore.Extensions` (project reference; supplies `GlobalQueryFilter` used by `IgnoreQueryFilters()`, `AddNewEntitiesFromNavigations`, `SaveChangesWithConcurrencyHandlingAsync`, and `IEfCoreExceptionHandler`, all consumed by `RepositorySpec<TDbContext>`) |
| Depends on (3rd party) | `LinqKit.Microsoft.EntityFrameworkCore`, `Mapster` (+ `MapsterMapper`), `System.Linq.Dynamic.Core`, `Microsoft.EntityFrameworkCore`, `MR.EntityFrameworkCore.KeysetPagination`, `X.PagedList.EF` |
| Target framework | net10.0 |

## Purpose

The Specification pattern for EF Core: `Specification<TEntity>` bundles a filter, includes, and ordering into
one reusable, testable object, executed through a single non-generic `IRepositorySpec` that serves every
entity type in a `DbContext` (the entity type is inferred per call from the specification passed in). Its
signature feature is the Dynamic Predicate Builder (`DynamicAnd`/`DynamicOr`), which turns runtime
`(propertyName, Ops, value)` triples into type-safe, fail-safe EF Core predicates -- built for search boxes
and query-string filters, not hand-written `Expression<Func<T,bool>>` trees. It is the direct successor to
the removed `DKNet.EfCore.Repos`.

**Not** a general ORM or migrations tool, not a CQRS/mediator layer (that's `DKNet.SlimBus.Extensions`), and
not a replacement for `DbContext` configuration (`OnModelCreating`, global filters) which still lives in
`DKNet.EfCore.Extensions`.

## Entry points

| Call | Exact signature | Called on | Notes |
|---|---|---|---|
| `AddSpecRepo<TDbContext>` | `public static IServiceCollection AddSpecRepo<TDbContext>(this IServiceCollection services) where TDbContext : DbContext` | `IServiceCollection` | Idempotent: uses `TryAddScoped`/`TryAddSingleton` keyed off `IRepositorySpec` alone -- a second call with a *different* `TDbContext` is a silent no-op (single-context-per-process registration). Registers `IRepositorySpec -> RepositorySpec<TDbContext>` (scoped) and `IRepositorySpecFactory -> RepositorySpecFactory` (singleton). Defined in `SpecSetup` (`DKNet.EfCore.Specifications` namespace, project root -- the entry surface). |
| `RepositorySpec<TDbContext>` constructor | `public RepositorySpec(TDbContext dbContext, IServiceProvider? provider = null)` | manual instantiation (tests, or outside DI) | The public ctor resolves `IMapper` from `provider` itself; there is also an `internal` ctor taking `IMapper?` directly, reachable only from the package's own test project. |
| `IRepositorySpecFactory.CreateAsync<TDbContext>` | `IRepositorySpecProvider CreateAsync<TDbContext>() where TDbContext : DbContext` | resolved `IRepositorySpecFactory` (singleton) | Despite the name, it is **synchronous** -- returns `IRepositorySpecProvider` immediately (no `await`, no `Task`). Requires `IDbContextFactory<TDbContext>` registered (e.g. via `AddDbContextFactory<TDbContext>()`). Creates a new DI scope + `DbContext` per call; caller must `Dispose`/`DisposeAsync` the provider. |
| `Specification<TEntity>` protected builders | `WithFilter`, `AddInclude(Expression<Func<TEntity,object?>>)`, `AddInclude(Func<IQueryable<TEntity>,IQueryable<TEntity>>)`, `AddOrderBy` (expression overload, plus a `(string, ListSortDirection)` overload that dispatches internally to ascending/descending), `AddOrderByDescending` (expression overload only -- no string overload), `AsNoTracking()`, `IgnoreQueryFilters()`, `Skip(int)`, `Take(int)`, `CreatePredicate(Expression<Func<TEntity,bool>>? = null)` | called only from inside a `Specification<TEntity>`/`ModelSpecification<TEntity,TModel>` subclass constructor (all `protected`) | `Skip`/`Take` throw `ArgumentOutOfRangeException` for `count <= 0`. Ordering is declared-sequence (mixed ascending/descending applies in the order added, not "all ascending, then all descending"). |
| `DynamicAnd`/`DynamicOr` (triple overload) | `public Expression<Func<T, bool>> DynamicAnd(string propertyName, Ops operation, object? value)` (C# 14 extension member on `ExpressionStarter<T>` and on `Expression<Func<T,bool>>`) | any predicate built with `PredicateBuilder.New<T>()`/`CreatePredicate` | Ambient `LinqKit` namespace -- no extra `using` needed alongside `PredicateBuilder`. Fail-safe: returns the predicate **unchanged** (never throws) on any unusable input. |
| `DynamicAnd`/`DynamicOr` (raw expression overload) | `public ExpressionStarter<T> DynamicAnd(string expression, params object?[] values)` | same targets | Fail-loud: throws `ArgumentException` on a blocklisted substring, otherwise lets `System.Linq.Dynamic.Core` throw its own parse errors. |
| `TryBuildPredicate<T>` | `public static bool TryBuildPredicate<T>(string propertyName, Ops operation, object? value, out Expression<Func<T, bool>>? predicate)` (static method on `DynamicPredicateExtensions`, `LinqKit` namespace) | standalone call, e.g. at an HTTP boundary | Explicit-failure counterpart to the triple overload -- returns `false` instead of silently no-op'ing, so a caller can turn an invalid filter into a 400 instead of quietly ignoring it. |
| `AfterKeyset`/`BeforeKeyset` | `public static IQueryable<TEntity> AfterKeyset<TEntity, TKey>(this IQueryable<TEntity> query, Expression<Func<TEntity, TKey>> keySelector, TKey cursor)` (+ two-key composite overloads, + `BeforeKeyset` mirrors) | any ordered `IQueryable<TEntity>` | Caller owns `OrderBy`; these add only the `WHERE` cursor predicate. Throws `ArgumentNullException` for a null `query`/selector argument. |
| `ToKeysetPageAsync` (arbitrary-arity) | `public static async Task<KeysetPage<TEntity>> ToKeysetPageAsync<TEntity>(this IQueryable<TEntity> query, Action<KeysetPaginationBuilder<TEntity>> configureKeyset, int pageSize, KeysetPaginationDirection direction = KeysetPaginationDirection.Forward, object? reference = null, CancellationToken cancellationToken = default)` | any `IQueryable<TEntity>` | Owns ordering *and* the cursor filter itself (via `configureKeyset`) -- do not chain after your own `OrderBy`. Throws `ArgumentOutOfRangeException` for `pageSize <= 0`. Three round trips (page + `HasPreviousAsync` + `HasNextAsync`); the latter two ignore `cancellationToken` (an `MR.EntityFrameworkCore.KeysetPagination` 1.6.0 limitation). |

## Public surface

### `DKNet.EfCore.Specifications` (project root)

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `SpecSetup` | static class | DI registration entry point | `AddSpecRepo<TDbContext>(this IServiceCollection services) : IServiceCollection` |

### `DKNet.EfCore.Specifications.Definitions`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `ISpecification<TEntity>` | interface | Read-only contract for a specification's filter/include state | `bool IsIgnoreQueryFilters { get; }`; `Expression<Func<TEntity,bool>>? FilterQuery { get; }`; `IReadOnlyCollection<Expression<Func<TEntity,object?>>> IncludeQueries { get; }`; `IReadOnlyCollection<Func<IQueryable<TEntity>,IQueryable<TEntity>>> IncludeBuilders { get; }` (ordering is **not** on this interface -- see Gotchas) |
| `Specification<TEntity>` | abstract class (implements `ISpecification<TEntity>`) | Base class every specification derives from | Ctors: `protected Specification()`, `protected Specification(Expression<Func<TEntity,bool>> query)`, `protected Specification(ISpecification<TEntity> specification)` (copy ctor -- clones filter/includes always; ordering/`Skip`/`Take`/tracking only when `specification` is itself a `Specification<TEntity>`). Public props: `FilterQuery`, `IncludeQueries`, `IncludeBuilders`, `IsIgnoreQueryFilters`. `internal` props (visible only to the package's own test project): `OrderByClauses`, `SkipCount`, `TakeCount`, `IsReadOnly`. Protected builder methods listed in Entry points above. |
| `IModelSpecification<TEntity, TModel>` | interface (extends `ISpecification<TEntity>`) | Marker for a projection-bound specification | No members of its own. |
| `ModelSpecification<TEntity, TModel>` | class (extends `Specification<TEntity>`, implements `IModelSpecification<TEntity,TModel>`) | Base for a specification whose reads project straight to `TModel` | Ctors: `protected ModelSpecification()`, `protected ModelSpecification(Expression<Func<TEntity,bool>> query)`, `protected ModelSpecification(ISpecification<TEntity> copyFrom)`. Adds no new members beyond `Specification<TEntity>`. |
| `OrderClause<TEntity>` | `internal readonly record struct` | One declared ordering step | `Expression<Func<TEntity,object>> KeySelector`, `ListSortDirection Direction` -- internal only, not consumer-visible. |

### `DKNet.EfCore.Specifications.Repositories`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `IRepositorySpec` | interface | Non-generic repository contract, one instance serves every entity type | `ValueTask AddAsync<TEntity>(TEntity, CancellationToken = default)`; `ValueTask AddRangeAsync<TEntity>(IEnumerable<TEntity>, CancellationToken = default)`; `Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken = default)`; `void Delete<TEntity>(TEntity)`; `Task<int> BulkDeleteAsync<TEntity>(Expression<Func<TEntity,bool>>, CancellationToken = default)`; `EntityEntry<TEntity> Entry<TEntity>(TEntity)`; `IQueryable<TEntity> Query<TEntity>(ISpecification<TEntity>)`; `IQueryable<TModel> Query<TEntity,TModel>(ISpecification<TEntity>)`; `Task<int> SaveChangesAsync(CancellationToken = default)`; `Task<int> UpdateAsync<TEntity>(TEntity, CancellationToken = default)`; `Task UpdateRangeAsync<TEntity>(IEnumerable<TEntity>, CancellationToken = default)` |
| `RepositorySpec<TDbContext>` | `sealed class` (implements `IRepositorySpec`) | The DI-registered implementation | `Query<TEntity,TModel>` throws `InvalidOperationException` when no `IMapper` is resolvable. `SaveChangesAsync` calls `AddNewEntitiesFromNavigations`, resolves a keyed `IEfCoreExceptionHandler` (keyed off the `DbContext`'s type full name), and calls `SaveChangesWithConcurrencyHandlingAsync` (both from `DKNet.EfCore.Extensions`). `UpdateAsync` sets `EntityState.Modified` then adds any new entities discovered from navigations, returning how many were added. |
| `IRepositorySpecFactory` | interface | Factory for out-of-scope (background job) repository access | `IRepositorySpecProvider CreateAsync<TDbContext>() where TDbContext : DbContext` |
| `RepositorySpecFactory` | `internal sealed class` (implements `IRepositorySpecFactory`) | DI-registered singleton implementation | `CreateAsync<TDbContext>()` returns a new `RepositorySpecProvider<TDbContext>` |
| `IRepositorySpecProvider` | interface (extends `IAsyncDisposable, IDisposable`) | Owns a scope + `DbContext` + repository | `IRepositorySpec Repository { get; }` |
| `RepositorySpecProvider<TDbContext>` | `internal sealed class` | Implementation | Creates its own `IServiceScope`, resolves `IDbContextFactory<TDbContext>.CreateDbContext()`, builds `Repository` from the new `DbContext` and the scope's provider. `Dispose`/`DisposeAsync` dispose the `DbContext` and the scope together. |

### `DKNet.EfCore.Specifications.Dynamics`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `Ops` | enum | Supported dynamic-predicate operations | `Equal`, `NotEqual`, `GreaterThan`, `GreaterThanOrEqual`, `LessThan`, `LessThanOrEqual`, `Contains`, `NotContains`, `StartsWith`, `EndsWith`, `In`, `NotIn`, `IsNull`, `IsNotNull` -- **`IsNull`/`IsNotNull` are easy to miss**: the published docs page's "Supported operations" table stops at `In`/`NotIn`, and the README does not enumerate `Ops` members at all (see Gotchas). `IsNull`/`IsNotNull` ignore the `value` argument entirely. |
| `DynamicPredicateExtensions` | static class, deliberately declares the **ambient `LinqKit` namespace** (extends LinqKit's own types, so no extra `using` is needed beyond `using LinqKit;`) | Public dynamic-predicate API | `extension<T>(ExpressionStarter<T> predicate)` and `extension<T>(Expression<Func<T,bool>> predicate)` blocks (C# 14 extension members), each with `DynamicAnd(string propertyName, Ops operation, object? value) : Expression<Func<T,bool>>`, `DynamicOr(...)` (same shape), `DynamicAnd(string expression, params object?[] values) : ExpressionStarter<T>`, `DynamicOr(string expression, params object?[] values) : ExpressionStarter<T>`. Static method `TryBuildPredicate<T>(string propertyName, Ops operation, object? value, out Expression<Func<T,bool>>? predicate) : bool`. |
| `DynamicPredicateBuilderExtensions` | `internal static class` | Implementation helpers (validation, coercion, clause building) | `IsValidPropertyName`, `TryNormalizePropertyName`, `ValidateExpression` (throws `ArgumentException` on a blocklisted substring), `AdjustOperationForValueType`, `BuildClause`, `ResolvePropertyType` (memoized), `ValidateArrayValue`, `TryConvertEnum`, `ValidateEnumValue`, `TryCoerceValue`, `TryCoerceArray` -- all `internal`, not consumer-visible, but their behaviour (coercion rules, the injection blocklist) is directly observable and documented under Diagnostics below. |

### `DKNet.EfCore.Specifications.Extensions`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `KeysetPage<TEntity>` | `sealed record` | Result of `ToKeysetPageAsync` (arbitrary-arity overload) | `IReadOnlyList<TEntity> Items`, `bool HasPrevious`, `bool HasNext` |
| `KeysetQueryExtensions` | static class | Keyset (cursor) pagination over a raw `IQueryable<TEntity>` | `AfterKeyset<TEntity,TKey>(IQueryable<TEntity>, Expression<Func<TEntity,TKey>>, TKey)`; `BeforeKeyset<TEntity,TKey>` (same shape); `AfterKeyset<TEntity,TKey1,TKey2>(..., key1Selector, key2Selector, cursor1, cursor2)`; `BeforeKeyset<TEntity,TKey1,TKey2>`; `ToKeysetPageAsync<TEntity>(IQueryable<TEntity>, Action<KeysetPaginationBuilder<TEntity>> configureKeyset, int pageSize, KeysetPaginationDirection direction = Forward, object? reference = null, CancellationToken = default) : Task<KeysetPage<TEntity>>`. Cursor values are boxed behind a private field-access wrapper so EF Core parameterizes rather than inlines them (avoids server-side plan-cache pollution -- two calls with different cursor values produce identical SQL text). |
| `ModelSpecRepoExtensions` | static class | `<TEntity,TModel>` repository extensions for `IModelSpecification` | `extension(IRepositorySpec repo)` block with `FirstAsync<TEntity,TModel>(IModelSpecification<TEntity,TModel>, CancellationToken = default) : Task<TModel>`; `FirstOrDefaultAsync<TEntity,TModel>(...) : Task<TModel?>`; `ToListAsync<TEntity,TModel>(...) : Task<List<TModel>>`; `ToPagedListAsync<TEntity,TModel>(..., int pageNumber, int pageSize, CancellationToken = default) : Task<IPagedList<TModel>>`; `ToPageEnumerable<TEntity,TModel>(IModelSpecification<TEntity,TModel>) : IAsyncEnumerable<TModel>` (calls `EnsureSpecHasOrdering()` first). |
| `PropertyNameExtensions` | static class | Property-path normalization used by both the ordering `(string, ListSortDirection)` overload and the dynamic predicate builder | `ToPascalCase(this string? propertyPath) : string` -- splits dotted paths on `.`, each segment on `_`/`-`, capitalizes the first letter of each word, preserves the rest of the word's casing (`lineITEM` -> `LineITEM`). Returns `string.Empty` for null/whitespace input. |
| `SpecificationExtensions` | `internal static class` | Folds a specification onto an `IQueryable` | `ApplySpecs<TEntity>(this IQueryable<TEntity>, ISpecification<TEntity>) : IQueryable<TEntity>` (throws `ArgumentNullException` for a null specification); `EnsureSpecHasOrdering<TEntity>(this ISpecification<TEntity>)` (throws `NotSupportedException` when no `OrderBy`/`OrderByDescending` was declared). Both `internal` -- reached only through `IRepositorySpec`. |
| `SpecRepoExtensions` | static class | `<TEntity>`-only (non-projected) repository extensions | `extension(IRepositorySpec repo)` block with `AnyAsync<TEntity>`, `CountAsync<TEntity>`, `FirstAsync<TEntity>`, `FirstOrDefaultAsync<TEntity>`, `FirstOrDefaultAsync<TEntity,TModel>`, `ToListAsync<TEntity>`, `ToListAsync<TEntity,TModel>`, `ToPagedListAsync<TEntity>`, `ToPagedListAsync<TEntity,TModel>`, `ToPageEnumerable<TEntity>`, `ToPageEnumerable<TEntity,TModel>`, `ToKeysetPageAsync<TEntity,TKey>(ISpecification<TEntity>, Expression<Func<TEntity,TKey>> keySelector, TKey cursor, int pageSize, CancellationToken = default) : Task<List<TEntity>>`, `ToKeysetPageAsync<TEntity,TKey1,TKey2>` (composite overload). All take `(specification, ..., CancellationToken = default)`; the paged overloads take `(specification, int pageNumber, int pageSize, CancellationToken = default)`. |

### `DKNet.EfCore.Specifications.Paging`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `EfCorePageAsyncEnumerator<TEntity>` | `internal sealed class` (implements `IAsyncEnumerable<TEntity>`) | Streams an `IQueryable<T>` page-by-page via `Skip`/`Take` | Not consumer-constructible directly (`internal`); reached via the extension below. Throws `ArgumentNullException`/`ArgumentOutOfRangeException` (`pageSize <= 0`) from its constructor. |
| `PageAsyncEnumeratorExtensions` | `internal static class` | Entry point for streaming pagination | `internal const int DefaultPageSize = 100`; `ToPageEnumerable<TEntity>(this IQueryable<TEntity> query, int pageSize = DefaultPageSize) : IAsyncEnumerable<TEntity>` -- `internal`, reached only via `IRepositorySpec.ToPageEnumerable`/`ToPageEnumerable<TEntity,TModel>`, not directly callable by a consumer on an arbitrary `IQueryable`. |

## Options & defaults

No options class or configuration section exists -- behaviour is set per specification (constructor calls)
and per DI registration call:

| Option | Type | Default | Effect | Where set |
|---|---|---|---|---|
| `AddSpecRepo<TDbContext>()` | DI call | not registered | Registers `IRepositorySpec`/`IRepositorySpecFactory` for `TDbContext` | `services.AddSpecRepo<AppDbContext>()` |
| `WithFilter(expression)` | specification builder | no filter (`FilterQuery == null`) | The `Where` clause `ApplySpecs` applies | specification constructor |
| `AddInclude(...)` (both overloads) | specification builder | no includes | Adds a single-level include or an `Include`/`ThenInclude` chain | specification constructor |
| `AddOrderBy`/`AddOrderByDescending` | specification builder | unordered | Declared-sequence ordering; mixed directions apply in the order added | specification constructor |
| `AsNoTracking()` | specification builder | tracking on (EF Core default) | `ApplySpecs` calls `.AsNoTracking()` when set. Projected (`TModel`) queries are **always** no-tracking regardless of this flag | specification constructor |
| `IgnoreQueryFilters()` | specification builder | `false` | Bypasses only global filters whose `GlobalQueryFilter.IsIgnorable` is `true` | specification constructor |
| `Skip(count)` / `Take(count)` | specification builder | unset | Applied only via `Query<TEntity>`/`Query<TEntity,TModel>` (i.e. `ApplySpecs`); reading `FilterQuery` etc. off the specification directly skips it. Throws `ArgumentOutOfRangeException` for `count <= 0` | specification constructor |
| `PageAsyncEnumeratorExtensions.DefaultPageSize` | `internal const int` | `100` | Rows fetched per round trip in `ToPageEnumerable`; not exposed as a public parameter | fixed in source |
| `configureKeyset` (`Action<KeysetPaginationBuilder<TEntity>>`) | delegate, required | none | Defines keyset columns + direction for `IQueryable<TEntity>.ToKeysetPageAsync` | call site |

## Usage patterns

### Register the repository for a `DbContext`

**When**: application startup, once per `DbContext` type.

```csharp
using DKNet.EfCore.Specifications;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;

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
```

**Notes**: idempotent -- a second `AddSpecRepo<AppDbContext>()` call is a no-op. Model projections
(`Query<TEntity, TModel>` and the `<TEntity,TModel>` extensions) require a Mapster `IMapper` registered
separately -- omitting it throws `InvalidOperationException` at query time, not at startup.

### Factory usage (background jobs, out-of-scope work)

**When**: a hosted service, queued job, or console tool needs `IRepositorySpec` outside a normal DI scope.

```csharp
using DKNet.EfCore.Specifications;
using DKNet.EfCore.Specifications.Definitions;
using DKNet.EfCore.Specifications.Extensions;
using DKNet.EfCore.Specifications.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

public sealed class AllProductsSpecification : Specification<Product>
{
}

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
    public async Task<int> CountAllAsync(CancellationToken ct)
    {
        await using var provider = factory.CreateAsync<AppDbContext>();
        return await provider.Repository.CountAsync(new AllProductsSpecification(), ct);
    }
}
```

**Notes**: `CreateAsync<TDbContext>()` is synchronous (see Gotchas) and requires
`IDbContextFactory<TDbContext>` registered via `AddDbContextFactory<TDbContext>()`. Disposing the returned
`IRepositorySpecProvider` disposes its own DI scope and the factory-created `DbContext` together --
`Repository` (`IRepositorySpec`) replaces one repository-per-entity-type factory API entirely.

### Define and run a filter/include/order-by specification

**When**: reusable query logic against an aggregate, executed through `IRepositorySpec`.

```csharp
using DKNet.EfCore.Specifications.Definitions;
using DKNet.EfCore.Specifications.Extensions;
using DKNet.EfCore.Specifications.Repositories;
using System.Threading;
using System.Threading.Tasks;

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

public sealed class ProductService(IRepositorySpec repo)
{
    public Task<Product?> FindActiveExpensiveAsync(decimal minPrice, CancellationToken ct) =>
        repo.FirstOrDefaultAsync(new ActiveExpensiveProductsSpec(minPrice), ct);

    public Task<List<Product>> ListActiveExpensiveAsync(decimal minPrice, CancellationToken ct) =>
        repo.ToListAsync(new ActiveExpensiveProductsSpec(minPrice), ct);
}
```

**Notes**: `WithFilter`/`AddInclude`/`AddOrderBy*` are `protected` -- callable only from inside the subclass
constructor, keeping the specification immutable once built. `AddOrderByDescending` then `AddOrderBy`
applies as `OrderByDescending(Price).ThenBy(Name)` because ordering is declared-sequence, not "all
descending, then all ascending".

### Runtime search filter with the Dynamic Predicate Builder

**When**: filter shape (which fields, which operators) is only known at request time -- search boxes,
admin grids, `?field=value` query strings.

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

**Notes**: `.AsExpandable()` is applied automatically inside `RepositorySpec<TDbContext>.Query<TEntity>` --
no extra wiring needed here. `DynamicAnd`/`DynamicOr` never throw on bad input (typo'd property, wrong
type, unresolvable path); they silently return the predicate unchanged, which is why every optional filter
above can be composed with a plain `if`. `nameof(Product.Name)`/`"category.name"`/`"Category.Name"` are
all accepted -- the property-name argument is normalized to PascalCase per dotted segment.

### Validate a single dynamic filter at a trust boundary

**When**: an HTTP endpoint takes raw query-string filters and must reject an invalid one (400) instead of
quietly serving unfiltered data.

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

**Notes**: unlike `DynamicAnd`/`DynamicOr`, `TryBuildPredicate` reports failure explicitly via its `bool`
return -- this is the entry point to use whenever a dropped filter would be a correctness problem (e.g.
handing a caller unfiltered data under a query that looked applied), not just a no-op composition detail.

### Projected reads via `ModelSpecification<TEntity, TModel>`

**When**: a read path that should never materialize the full entity -- list/summary endpoints.

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

**Notes**: requires an `IMapper` (Mapster) registered in DI; `RepositorySpec<TDbContext>.Query<TEntity,TModel>`
throws `InvalidOperationException` at query time if none is found. Projected queries are always
`AsNoTracking()` regardless of whether the specification itself called `AsNoTracking()`.

### Streaming enumeration and keyset pagination

**When**: large result sets -- streaming instead of loading everything, or cursor-based "next page"
navigation that must stay fast as the table grows.

```csharp
using DKNet.EfCore.Specifications.Definitions;
using DKNet.EfCore.Specifications.Extensions;
using DKNet.EfCore.Specifications.Repositories;
using System.Threading.Tasks;

public sealed class ActiveProductsByIdSpec : Specification<Product>
{
    public ActiveProductsByIdSpec()
    {
        WithFilter(p => p.IsActive);
        AddOrderBy(p => p.Id);
    }
}

public sealed class ProductProcessor(IRepositorySpec repo)
{
    public async Task ProcessAllActiveAsync()
    {
        // Requires the specification to declare at least one AddOrderBy/AddOrderByDescending,
        // or this throws NotSupportedException before the first row is fetched.
        await foreach (var product in repo.ToPageEnumerable(new ActiveExpensiveProductsSpec(50m)))
        {
            await ProcessAsync(product);
        }
    }

    // The keySelector's direction must match the specification's own declared order
    // (ascending here, via AddOrderBy) for the cursor comparison to stay consistent
    // with the result order.
    public Task<List<Product>> NextPageByIdAsync(int lastId, int pageSize) =>
        repo.ToKeysetPageAsync(new ActiveProductsByIdSpec(), p => p.Id, lastId, pageSize);

    private static Task ProcessAsync(Product product) => Task.CompletedTask;
}
```

**Notes**: `ToPageEnumerable` throws `NotSupportedException` immediately if the specification has no
ordering (`EnsureSpecHasOrdering`) -- paging an unordered query would return unstable/duplicate rows across
page boundaries. The simple `ToKeysetPageAsync(spec, keySelector, cursor, pageSize, ct)` overload chains
`AfterKeyset` onto the specification's own query; for a raw `IQueryable<TEntity>` with no specification,
`AfterKeyset`/`BeforeKeyset` add only the cursor predicate and you own `OrderBy` yourself. For arbitrary
column counts and directions in one call, `IQueryable<TEntity>.ToKeysetPageAsync(configureKeyset, pageSize,
direction, reference, ct)` owns ordering *and* filtering itself via `configureKeyset` (e.g.
`b => b.Ascending(x => x.Country).Descending(x => x.Revenue).Ascending(x => x.Id)`) and additionally reports
`HasPrevious`/`HasNext` -- do not chain a preceding `OrderBy` in front of it.

## Runtime behaviour

`ApplySpecs<TEntity>` (internal) folds a specification onto an `IQueryable<TEntity>` in a **fixed order**,
applied by `RepositorySpec<TDbContext>.Query<TEntity>`/`Query<TEntity,TModel>` on every read:

1. If `IsIgnoreQueryFilters` is set and `GlobalQueryFilter.IgnorableFilterKeys` is non-empty, calls
   `queryable.IgnoreQueryFilters(ignorableKeys)` -- bypassing only filters registered as ignorable.
2. If `FilterQuery` is non-null, applies `.Where(FilterQuery)`.
3. Applies every `IncludeQueries` entry via `.Include(...)`, in declaration order.
4. Applies every `IncludeBuilders` entry (each a `Func<IQueryable<TEntity>,IQueryable<TEntity>>`, e.g. an
   `Include(...).ThenInclude(...)` chain), in declaration order.
5. If the specification is a `Specification<TEntity>` (foreign `ISpecification<TEntity>` implementations are
   not supported past this point): applies `OrderByClauses` in **declared sequence** -- the first clause
   becomes `OrderBy`/`OrderByDescending`, every subsequent clause becomes `.ThenBy`/`.ThenByDescending`
   regardless of direction mixing.
6. If `IsReadOnly` (`AsNoTracking()` was called), applies `.AsNoTracking()`.
7. If `SkipCount`/`TakeCount` were declared, applies `.Skip(...)`/`.Take(...)` in that order.

`Query<TEntity>` itself first calls `_dbContext.Set<TEntity>().AsExpandable()` before `ApplySpecs`, so a
`DynamicAnd`/`DynamicOr`-built predicate translates correctly without the caller doing anything extra.
`Query<TEntity,TModel>` additionally calls `.AsNoTracking().ProjectToType<TModel>(_mapper.Config)` after
`ApplySpecs` (so a projected read is always non-tracking, on top of whatever `ApplySpecs` itself applied).

`RepositorySpec<TDbContext>.SaveChangesAsync`: (1) calls `_dbContext.AddNewEntitiesFromNavigations(ct)`
(from `DKNet.EfCore.Extensions`) to pick up new entities reachable only through navigation properties; (2)
resolves a keyed `IEfCoreExceptionHandler` for `_dbContext.GetType().FullName` if one is registered; (3)
calls `_dbContext.SaveChangesWithConcurrencyHandlingAsync(handler, ct)`. Any `SaveChanges`
interceptors/hooks registered on the `DbContext` (from `DKNet.EfCore.Hooks`/`Events`/`AuditLogs`/etc.) still
run, since this is the same underlying `DbContext.SaveChangesAsync` pipeline.

`ToPageEnumerable` (either the `IRepositorySpec` extension or the `ModelSpecRepoExtensions` variant):
fetches `DefaultPageSize` (100) rows at a time via `Skip(page * 100).Take(100)`, yields each item, and stops
once a page returns fewer than 100 rows.

## Diagnostics & exceptions

| Exception type | Severity | When | Fix |
|---|---|---|---|
| `ArgumentOutOfRangeException` | error | `Specification<TEntity>.Skip(count)`/`Take(count)` called with `count <= 0` | Pass a positive count. |
| `ArgumentOutOfRangeException` | error | `IRepositorySpec.ToKeysetPageAsync`/`IQueryable.ToKeysetPageAsync` called with `pageSize <= 0` | Pass a positive page size. |
| `ArgumentNullException` | error | A null `specification`/`query`/selector/`configureKeyset` argument on `ApplySpecs`, `AfterKeyset`, `BeforeKeyset`, or `ToKeysetPageAsync` | Don't pass null; these are programmer errors, not user-input errors. |
| `InvalidOperationException` | error | `RepositorySpec<TDbContext>.Query<TEntity,TModel>` called with no `IMapper` resolvable from DI | Register Mapster (`IMapper`) in the service collection before calling a projected (`TModel`) query/repository method. |
| `NotSupportedException` | error | `EnsureSpecHasOrdering` -- `ToPageEnumerable` (either overload) called on a specification with no `AddOrderBy`/`AddOrderByDescending` | Add at least one ordering clause to the specification before streaming it. |
| `ArgumentException` | error | The raw Dynamic LINQ `DynamicAnd`/`DynamicOr(string expression, params object?[] values)` overload -- expression contains a blocklisted substring (`System.`, `Microsoft.`, `Reflection.`, `Process.`, `Assembly.`, `GetType(`, `typeof(`, `Activator.`, `Environment.`, `File.`, `Directory.`, `Path.`, `Stream.`, `SqlCommand`, `SqlConnection`, `DbCommand`, `Runtime.`, `Unsafe.`, `Marshal.`, `AppDomain.`, `Thread.`, `Task.Run`) | Rewrite the expression to avoid the pattern; this overload is fail-loud by design. |
| (parse exception, not thrown to caller) | n/a | The triple-overload `DynamicAnd`/`DynamicOr`/`TryBuildPredicate` catch `System.Linq.Dynamic.Core.Exceptions.ParseException` internally and return `null`/`false` instead of propagating | Not a caller-facing exception -- the triple overload is fail-safe by design (see Gotchas). |

No analyzer / Roslyn `DiagnosticDescriptor` exists in this package -- it ships no source generator or
analyzer.

## Gotchas

- **`Ops.IsNull`/`Ops.IsNotNull` exist but are easy to miss.** The published docs page's "Supported
  operations" table lists only the first 12 members (`Equal` through `NotIn`); the README doesn't enumerate
  `Ops` members at all. The full enum has 14 members. `IsNull`/`IsNotNull` ignore the `value` argument
  entirely and build `{prop} == null`/`{prop} != null` directly -- pass any placeholder (or `null`) as the
  third argument.
- **`IRepositorySpecFactory.CreateAsync<TDbContext>` is synchronous despite the name.** It returns
  `IRepositorySpecProvider` directly, not a `Task<IRepositorySpecProvider>`. Awaiting it
  (`await factory.CreateAsync<AppDbContext>()`) is a compile error, not a race.
- **Filtered `AddInclude` + a tracking query can surface stale children.** A single-level filtered include
  (`AddInclude(p => p.OrderItems.Where(i => i.Quantity > 0))`) on a tracking query can return children that
  don't match the filter, because EF Core's navigation fixup reattaches already-tracked entities regardless
  of the include filter -- use `Query<TEntity,TModel>` (projection) or `AsNoTracking()` when the filter must
  be exact.
- **`Skip`/`Take` only apply through `Query<TEntity>`/`Query<TEntity,TModel>`.** Reading
  `FilterQuery`/`IncludeQueries`/etc. off the specification directly and building your own query skips the
  window entirely -- `SkipCount`/`TakeCount` are only consumed inside `ApplySpecs`.
- **A foreign `ISpecification<TEntity>` implementation (not deriving from `Specification<TEntity>`)
  contributes no ordering, no `Skip`/`Take`, no tracking flag.** `ApplySpecs` only reads those from
  specifications whose runtime type is `Specification<TEntity>`. Always derive from
  `Specification<TEntity>` (or `ModelSpecification<TEntity,TModel>`), never hand-roll `ISpecification<T>`.
- **`ToKeysetPageAsync`'s `HasPreviousAsync`/`HasNextAsync` ignore the `CancellationToken`.** Only the page
  query itself observes cancellation -- a `CancellationToken` passed to the arbitrary-arity
  `ToKeysetPageAsync` cannot cancel the two existence-check round trips. This is an
  `MR.EntityFrameworkCore.KeysetPagination` 1.6.0 API limitation, not a DKNet oversight.
- **`AddOrderBy(string orderBy, ListSortDirection direction)` uses reflection (`Expression.PropertyOrField`),
  not the dynamic-predicate machinery.** It normalizes with the same `ToPascalCase()` as
  `DynamicAnd`/`DynamicOr`, but an unresolvable property here **throws** rather than silently no-op'ing like
  the dynamic predicate triple overload does -- the two "runtime property name" surfaces have different
  failure modes.
- **`ToPageEnumerable`'s page size (100) is not overridable through the public `IRepositorySpec` surface.**
  The only place it can be changed is an `internal` overload a normal consumer cannot reach.

## Anti-patterns & hallucination traps

- `IRepository<T>`, `IReadRepository<T>`, `IWriteRepository<T>`, `IRepositoryFactory`, `SetupRepository`,
  `RepoExtensions`, `Repository<T>`, `ReadRepository<T>`, `WriteRepository<T>`, `RepositoryFactory<TDbContext>`
  -- **do not exist anywhere in the package.** They belonged to the removed `DKNet.EfCore.Repos`/
  `DKNet.EfCore.Repos.Abstractions` packages. Any code referencing them will not compile. Use
  `IRepositorySpec` + `Specification<TEntity>` instead (see
  https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/Migrating-Repos-To-Specifications.md).
- `repo.DeleteRange(entities)` -- **removed with no direct replacement.** Use
  `repo.BulkDeleteAsync<TEntity>(predicate, ct)` (a server-side `ExecuteDeleteAsync`; entities do not need to
  be loaded first) instead of loading and deleting a range.
- `PredicateBuilder.New<Product>().And(p => p.IsActive).DynamicAnd(...)` run directly against a raw
  `_db.Products`/`DbSet<Product>` **without `.AsExpandable()`** -- fails or silently mistranslates. Only
  needed when bypassing `IRepositorySpec`; through the repository, `.AsExpandable()` is already applied.
- Wrapping `DynamicAnd`/`DynamicOr` calls in `if (value != null) predicate.DynamicAnd(...)` -- unnecessary
  and risks skipping a legitimate `null`-equality filter; `Equal`/`NotEqual` against `null` already produce
  `IS NULL`/`IS NOT NULL` internally.
- Calling `.ToList()`/`.ToListAsync()` before the specification/predicate/paging has been composed onto the
  query -- pulls the whole table into memory and evaluates everything client-side. Always materialize
  *after* `Query`/`ToListAsync(spec, ct)`, never before.
- Chaining your own `.OrderBy(...)` in front of `IQueryable<TEntity>.ToKeysetPageAsync(configureKeyset,
  ...)` -- that overload owns ordering itself via `configureKeyset`; a preceding `OrderBy` is
  redundant/conflicting, not additive.
- Expecting `Specification<TEntity>`'s builder methods (`WithFilter`, `AddInclude`, `AddOrderBy`, `Skip`,
  `Take`, `AsNoTracking`, `IgnoreQueryFilters`, `CreatePredicate`) to be callable from outside a subclass --
  they are all `protected`; there is no public mutation API on a built specification.
- Treating `ISpecification<TEntity>` as carrying ordering, `Skip`/`Take`, or `AsNoTracking` state -- it does
  not; only the concrete `Specification<TEntity>` base class does (as `internal` members), and only when the
  runtime type is `Specification<TEntity>` will `ApplySpecs` see them.
- `services.AddSpecRepo<Db1>(); services.AddSpecRepo<Db2>();` expecting both to work -- the second call is a
  silent no-op (`TryAddScoped` keys off `IRepositorySpec` alone), so `IRepositorySpec` in that process only
  ever serves `Db1`. Multi-`DbContext` support is not implemented; don't assume it.
- `Ops.Like`, `Ops.Between`, `Ops.NotNull`, `Ops.IsEmpty` -- none exist. The full enum is `Equal, NotEqual,
  GreaterThan, GreaterThanOrEqual, LessThan, LessThanOrEqual, Contains, NotContains, StartsWith, EndsWith,
  In, NotIn, IsNull, IsNotNull`.
- `IRepositorySpec<TEntity>` -- does not exist; `IRepositorySpec` is non-generic, with the entity type a
  per-method type parameter instead.

## Composes with

| Package | Rule |
|---|---|
| `DKNet.EfCore.Abstractions` | Reach for it to model `TEntity` via `Entity<TKey>`/`Entity` (`Entity<Guid>`, implementing `IEventEntity` for domain events) -- specifications constrain only on `class`, but a DKNet solution's aggregates typically derive from here. There is no shipped `AggregateRoot` type in this package or in `DKNet.EfCore.Abstractions`; it is DDD terminology used in the top-level project documentation for the same concept. |
| `DKNet.EfCore.Extensions` | Required (project reference). Supplies `GlobalQueryFilter` (what `IgnoreQueryFilters()` bypasses), `AddNewEntitiesFromNavigations`, and `SaveChangesWithConcurrencyHandlingAsync`/`IEfCoreExceptionHandler` that `RepositorySpec<TDbContext>.SaveChangesAsync` calls into. Reach for it to register a global query filter or configure the model. |
| `DKNet.EfCore.DtoGenerator` | Reach for it instead of hand-maintaining the `TModel` DTO type that `ModelSpecification<TEntity,TModel>` projects onto. |
| `DKNet.EfCore.DataAuthorization` | Reach for it for row-level ownership/tenant isolation; its filter is deliberately registered non-ignorable, so `IgnoreQueryFilters()` never bypasses it. |
| `DKNet.EfCore.Hooks` / `DKNet.EfCore.Events` / `DKNet.EfCore.AuditLogs` | These attach as `SaveChanges` interceptors on the same `DbContext` -- they fire during `RepositorySpec<TDbContext>.SaveChangesAsync` without any extra wiring in this package. |
| `DKNet.SlimBus.Extensions` | A CQRS handler fetches via `IRepositorySpec`/a `Specification<TEntity>`, mutates the aggregate, and persists via the same `SaveChangesAsync` -- do not call `SaveChangesAsync` more than once per handler invocation. |

## Testing notes

- **Two DB strategies, both without Docker/TestContainers for most tests**: an in-memory SQLite connection
  (`new SqliteConnection("DataSource=:memory:")`, `UseSqlite`, `EnsureCreatedAsync`) for most
  specification/ordering tests, and a shared fixture exposing a seeded test `DbContext` for the broader
  repository tests. One fixture provisions a real PostgreSQL container (`Testcontainers.PostgreSql`) for the
  three-key composite keyset ordering scenario and a null-semantics test suite; every other test file runs
  against in-memory SQLite.
- **Constructing `RepositorySpec<TDbContext>` directly in tests**, not through DI: either the `internal`
  `IMapper?` constructor (test-project-only via `InternalsVisibleTo`) or the public constructor with no
  provider, for tests that don't need Mapster or `IEfCoreExceptionHandler`.
- **SQL assertion pattern**: `repo.Query(spec).ToQueryString()` to assert against generated SQL text
  alongside the materialized rows.
- **Dedicated fail-safe vs. fail-loud coverage**: one suite asserts `TryBuildPredicate` returns
  `false`/`null` (never throws) for unresolvable/mismatched properties; another asserts the raw-expression
  overload throws `ArgumentException` for each blocklisted pattern (e.g. `System.IO.File.Delete(@0)`,
  `typeof(Object).ToString()`, `Activator.CreateInstance(@0)`, `Process.Start(@0)`).
- Test naming follows the repo convention `MethodName_Scenario_ExpectedBehavior` (e.g.
  `AddAsync_WithCancellationToken_ShouldRespectCancellation`, `TryBuildPredicate_UnknownProperty_ReturnsFalse`).
- See the `dknet-testing` skill for the TestContainers fixture patterns and ARM64 image-fallback details that
  apply across the whole DKNet suite.
