# DKNet.EfCore.Specifications

The Specification pattern for EF Core — filter, includes, and order-by as one reusable object, executed through a
single non-generic `IRepositorySpec`, plus a runtime dynamic predicate builder.

## ✨ Why use it?

- **Query logic gets a name and a home** — "active customers in a region" becomes a `Specification<TEntity>` class
  you can reuse and unit-test, instead of a new repository method per filter combination or an `IQueryable<T>` leaking
  out of the persistence layer.
- **Filters compose instead of being copy-pasted** — filter, include, and order-by live on one object, so a building
  block can be reused across several queries without duplicating the predicate.
- **One repository for every aggregate** — `IRepositorySpec` is not generic over the entity; the entity type is
  inferred per call from the specification passed in, so one injected instance serves the whole `DbContext`.
- **Runtime criteria without string SQL** — the dynamic predicate builder turns
  `(propertyName, operation, value)` triples into type-safe EF Core predicates, which is what search boxes,
  query-string filters, and admin grids actually need.
- **Keyset pagination and streaming built in** — cursor pagination and page-at-a-time async enumeration are part of
  the surface rather than something each caller reimplements.

Reach for this package whenever you need reusable, testable query logic against an EF Core `DbContext` — especially
when some of the filter criteria are supplied by a caller at runtime.

It is the successor to the retired `DKNet.EfCore.Repos` and `DKNet.EfCore.Repos.Abstractions`, whose types carry
`[Obsolete]` and point here. `IRepositorySpec` covers the same read/write/paging surface as the old `IRepository<T>`.
See [`Migrating-Repos-To-Specifications.md`](./Migrating-Repos-To-Specifications.md) for a full call-site mapping.

## 🚀 Quick Start

```bash
dotnet add package DKNet.EfCore.Specifications
```

Register the repository against your `DbContext` type:

```csharp
using DKNet.EfCore.Specifications;

services.AddSpecRepo<AppDbContext>();
```

`SpecSetup.AddSpecRepo<TDbContext>(this IServiceCollection services)` is idempotent — it no-ops if `IRepositorySpec`
is already registered — and wires up two services:

- `IRepositorySpec` → `RepositorySpec<TDbContext>` (scoped), the workhorse used directly by application code.
- `IRepositorySpecFactory` (singleton) → creates an `IRepositorySpecProvider` per `CreateAsync<TDbContext>()` call,
  each owning its own DI scope and `DbContext` instance (via `IDbContextFactory<TDbContext>`) — useful outside a
  normal per-request scope (background jobs, message handlers). Dispose the provider (`IDisposable`/`IAsyncDisposable`)
  when done; disposing it disposes its scope and `DbContext`.

Model projection (`Query<TEntity, TModel>`, and every `*Async<TEntity, TModel>` repository extension) additionally
requires a Mapster `IMapper` registered in DI — `RepositorySpec<TDbContext>` resolves it via
`IServiceProvider.GetService<IMapper>()` and throws `InvalidOperationException` at query time if none is found.

## 🧩 Features

### `Specification<TEntity>` — filter, include, and order-by in one object

`Specification<TEntity>` is the abstract base you derive from. Configure it entirely from the constructor, using its
`protected` builder methods — they are not callable from outside the subclass, which is what keeps a specification's
query logic immutable and self-contained once constructed:

```csharp
public sealed class ActiveExpensiveProductsSpec : Specification<Product>
{
    public ActiveExpensiveProductsSpec(decimal minPrice)
    {
        WithFilter(p => p.IsActive && p.Price >= minPrice);   // FilterQuery
        AddInclude(p => p.Category);                          // IncludeQueries
        AddOrderByDescending(p => p.Price);                    // OrderByClauses, declared-sequence
        AddOrderBy(p => p.Name);                               // OrderByClauses, applied after Price
    }
}
```

The public surface a repository (or your own `IQueryable` code, via `ApplySpecs`) reads back is `ISpecification<TEntity>`:

| Member | Meaning |
|---|---|
| `FilterQuery` | `Expression<Func<TEntity, bool>>?` — set once via `WithFilter`. |
| `IncludeQueries` | Single-level `Expression<Func<TEntity, object?>>` includes added via `AddInclude(Expression<...>)`, e.g. `AddInclude(p => p.Category)`. Supports one level of filtered include, e.g. `AddInclude(p => p.OrderItems.Where(i => i.Quantity > 0))` — see the tracking caveat below. |
| `IncludeBuilders` | `Func<IQueryable<TEntity>, IQueryable<TEntity>>` chains added via `AddInclude(Func<...>)`, for `Include(...).ThenInclude(...)` chains or per-navigation `Where`/`OrderBy`/`Skip`/`Take`. |
| `IsIgnoreQueryFilters` | Set via `IgnoreQueryFilters()` — see "Configuration options and defaults". |

Ordering is not exposed on `ISpecification<TEntity>`. Only `Specification<TEntity>` (the abstract base every
specification derives from) carries it, as an `internal` declared-sequence list — foreign `ISpecification<TEntity>`
implementations that don't derive from `Specification<TEntity>` are not supported and contribute no ordering.

Additional protected builders on `Specification<TEntity>`:

- `AddOrderBy(string orderBy, ListSortDirection direction)` — orders by a **property name string** (normalized to
  PascalCase the same way the dynamic predicate builder does — see *Dynamic Predicate Builder* below), for when the sort column itself is
  runtime-supplied (e.g. an `?orderBy=` query parameter). Builds an `Expression<Func<TEntity, object>>` via
  reflection (`Expression.PropertyOrField`) and routes it through the same ordering path as the expression overload.
- `AddOrderBy` / `AddOrderByDescending` (expression overloads) record ordering **in declaration order**, so
  mixed-direction ordering (`OrderByDescending(Price).ThenBy(Name)`) applies exactly as declared — not "all
  ascending, then all descending".
- `AsNoTracking()` — marks the specification's query read-only; `ApplySpecs` calls `.AsNoTracking()` on the
  `IQueryable` when this is set.
- `Skip(int count)` / `Take(int count)` — declare an offset window; both throw `ArgumentOutOfRangeException` for
  values `<= 0`.
- `IgnoreQueryFilters()` — see "Configuration options and defaults".
- `CreatePredicate(Expression<Func<TEntity, bool>>? expression = null)` — returns a LinqKit
  `ExpressionStarter<TEntity>` (via `PredicateBuilder.New<TEntity>()` or `PredicateBuilder.New(expression)`), the
  idiomatic starting point for combining static predicates with the dynamic predicate builder inside a
  specification's constructor (see *Dynamic Predicate Builder* below).
- A copy constructor, `Specification(ISpecification<TEntity> specification)`, clones filter/include/order-by state
  from an existing specification — useful for `ModelSpecification<TEntity, TModel>` (see *projections* below) built from an
  existing entity specification.

### `IRepositorySpec` — the non-generic repository surface

`IRepositorySpec` is injected once and used for every entity type in the `DbContext`; the entity type comes from the
`ISpecification<TEntity>` (or explicit type argument) passed to each call:

```csharp
public sealed class ProductService(IRepositorySpec repo)
{
    public Task<Product?> FindActiveExpensiveAsync(decimal minPrice, CancellationToken ct) =>
        repo.FirstOrDefaultAsync(new ActiveExpensiveProductsSpec(minPrice), ct);

    public async Task CreateAsync(Product product, CancellationToken ct)
    {
        await repo.AddAsync(product, ct);
        await repo.SaveChangesAsync(ct);
    }
}
```

Core interface members: `AddAsync`/`AddRangeAsync`, `Delete`, `BulkDeleteAsync<TEntity>(predicate, ct)`
(server-side `ExecuteDeleteAsync` — the replacement for the removed `DeleteRange`), `Entry<TEntity>`,
`Query<TEntity>(spec)` / `Query<TEntity, TModel>(spec)`, `SaveChangesAsync`, `UpdateAsync`/`UpdateRangeAsync`, and
`BeginTransactionAsync`.

Query execution goes through `Extensions/SpecRepoExtensions.cs` and `Extensions/ModelSpecRepoExtensions.cs`, both
implemented as extension members on `IRepositorySpec`:

| Method | Returns |
|---|---|
| `AnyAsync<TEntity>(spec, ct)` | `Task<bool>` |
| `CountAsync<TEntity>(spec, ct)` | `Task<int>` |
| `FirstAsync<TEntity>(spec, ct)` | `Task<TEntity>` (throws if empty) |
| `FirstOrDefaultAsync<TEntity>(spec, ct)` | `Task<TEntity?>` |
| `FirstAsync<TEntity, TModel>` / `FirstOrDefaultAsync<TEntity, TModel>(spec, ct)` | projected model (see *`ModelSpecification<TEntity, TModel>` — projections*) |
| `ToListAsync<TEntity>(spec, ct)` / `ToListAsync<TEntity, TModel>(spec, ct)` | `Task<List<T>>` |
| `ToPagedListAsync<TEntity>(spec, pageNumber, pageSize, ct)` / `<TEntity, TModel>` overload | `Task<IPagedList<T>>` (X.PagedList) |
| `ToPageEnumerable<TEntity>(spec)` / `<TEntity, TModel>` overload | `IAsyncEnumerable<T>`, internally paged (see *Keyset (cursor) pagination and streaming enumeration*) |
| `ToKeysetPageAsync<TEntity, TKey>(spec, keySelector, cursor, pageSize, ct)` / two-key overload | `Task<List<TEntity>>` (see *Keyset (cursor) pagination and streaming enumeration*) |

`repo.Query<TEntity>(spec)` and `Query<TEntity, TModel>(spec)` also return the raw `IQueryable<T>` — call
`.ToQueryString()` on it to inspect generated SQL, the pattern used throughout the test suite.

### Dynamic Predicate Builder — the signature feature

For filters whose shape is only known at runtime (search boxes, `?field=value` query strings, admin grids), build a
predicate from `(propertyName, operation, value)` triples instead of hand-writing `Expression<Func<T, bool>>` trees.
`DynamicAnd`/`DynamicOr` are extension members (defined in the `LinqKit` namespace, so no extra `using` is needed
alongside `PredicateBuilder`) on both `ExpressionStarter<T>` and plain `Expression<Func<T, bool>>`:

```csharp
using LinqKit;
using DKNet.EfCore.Specifications.Dynamics;

public sealed class ProductSearchSpecification : Specification<Product>
{
    public ProductSearchSpecification(string? name, decimal? minPrice, string? category)
    {
        var predicate = CreatePredicate(p => p.IsActive);   // ExpressionStarter<Product>

        if (name is not null)
            predicate = predicate.DynamicAnd(nameof(Product.Name), Ops.Contains, name);

        if (minPrice is not null)
            predicate = predicate.DynamicAnd(nameof(Product.Price), Ops.GreaterThanOrEqual, minPrice);

        if (category is not null)
            predicate = predicate.DynamicAnd("Category.Name", Ops.Equal, category); // nested/dotted path

        WithFilter(predicate);
    }
}
```

Executing it through `IRepositorySpec` needs nothing extra — `RepositorySpec<TDbContext>.Query<TEntity>` already
calls `.AsExpandable()` internally (`_dbContext.Set<TEntity>().AsExpandable().ApplySpecs(spec)`) before applying the
specification, so `repo.ToListAsync(new ProductSearchSpecification(...), ct)` just works. `.AsExpandable()` only
needs to be added by hand when you build and execute a `DynamicAnd`/`DynamicOr` predicate directly against an
`IQueryable`/`DbSet` outside `IRepositorySpec` — see "Gotchas and limits".

**Supported operations (`Ops` enum, `DKNet.EfCore.Specifications.Dynamics` namespace):**

| `Ops` member | SQL shape | Notes |
|---|---|---|
| `Equal` / `NotEqual` | `= @0` / `<> @0` | `null` value → `IS NULL` / `IS NOT NULL`, not a parameterized comparison |
| `GreaterThan` / `GreaterThanOrEqual` / `LessThan` / `LessThanOrEqual` | `>`, `>=`, `<`, `<=` | |
| `Contains` / `NotContains` | `LIKE '%..%'` / negated | Auto-converted to `Equal`/`NotEqual` on non-string properties |
| `StartsWith` / `EndsWith` | `LIKE '..%'` / `LIKE '%..'` | Auto-converted to `Equal` on non-string properties |
| `In` / `NotIn` | `IN (...)` / `NOT IN (...)` | Value must be a non-empty `IEnumerable` that is not itself a `string`; invalid values are rejected (see below) |

**Property paths.** The property name argument is normalized with `PropertyNameExtensions.ToPascalCase()` — segments
separated by `_` or `-` are treated as word boundaries, and dotted paths (`"category.name"`, `"customer_profile.city"`)
are normalized segment-by-segment (`Category.Name`, `CustomerProfile.City`) so callers can pass camelCase,
snake_case, kebab-case, or already-PascalCase names interchangeably.

**Fail-safe, not fail-loud, for the triple overload.** `DynamicAnd(propertyName, operation, value)` /
`DynamicOr(...)` **silently return the predicate unchanged** — they do not throw — when:

- the property name is invalid or unsafe (fails the internal path-validation regex),
- the property doesn't resolve on `TEntity` (typo, wrong nesting),
- `value` fails to coerce to the property's CLR type (e.g. a non-numeric string against an `int` property),
- `value` is invalid for an enum property (wrong enum type, or a non-enum-typed array for `In`/`NotIn`), or
- `value` is invalid for `In`/`NotIn` (`null`, empty, or a `string`/non-enumerable).

This makes the triple overload safe to wire straight to unvalidated user input: a bad filter is dropped rather than
crashing the request. Scalar values are coerced automatically to numeric types, `bool`, `DateTime`, `DateOnly`,
`TimeOnly`, `Guid`, and enums (so a query-string `"true"`/`"2024-01-01"`/`"Active"` reaches the database as the right
CLR type) — this coercion is what can fail and trigger the silent skip above.

There is also a **raw Dynamic LINQ overload** — `DynamicAnd(string expression, params object?[] values)` /
`DynamicOr(...)` — for expressions the triple shape can't express (e.g. `"Price * Quantity > @0"`). Unlike the triple
overload, this one is fail-loud: it validates the expression against a blocklist of dangerous substrings
(`System.`, `Reflection.`, `Process.`, `File.`, `SqlCommand`, `Environment.`, …) and throws `ArgumentException` if
one is found, and lets `System.Linq.Dynamic.Core` parse/throw normally otherwise.

### `ModelSpecification<TEntity, TModel>` — projections

For read paths that should never materialize the full entity, derive from `ModelSpecification<TEntity, TModel>`
instead of `Specification<TEntity>`. It adds no new members — same protected builders — but flags the specification
for projection, and pairs with the `<TEntity, TModel>` repository overloads (`FirstOrDefaultAsync`, `ToListAsync`,
`ToPagedListAsync`, `ToPageEnumerable`) that call `Query<TEntity, TModel>` under the hood:

```csharp
public sealed class ActiveProductSummariesSpec : ModelSpecification<Product, ProductSummaryDto>
{
    public ActiveProductSummariesSpec()
    {
        WithFilter(p => p.IsActive);
        AddOrderBy(p => p.Name);
    }
}

List<ProductSummaryDto> summaries =
    await repo.ToListAsync<Product, ProductSummaryDto>(new ActiveProductSummariesSpec(), ct);
```

`RepositorySpec<TDbContext>.Query<TEntity, TModel>` maps via Mapster (`ProjectToType<TModel>(_mapper.Config)`) on top
of `.AsNoTracking()` — projected reads are always non-tracking regardless of whether the specification called
`AsNoTracking()` itself.

### Keyset (cursor) pagination and streaming enumeration

**`ToPageEnumerable`** (in the `IRepositorySpec` table above) streams a specification's results as an `IAsyncEnumerable<T>`, fetching
pages of 100 rows internally (`Skip`/`Take`) rather than materializing the whole result set:

```csharp
await foreach (var product in repo.ToPageEnumerable(new ActiveExpensiveProductsSpec(50m)))
{
    await ProcessAsync(product);
}
```

It requires the specification to declare at least one `OrderBy`/`OrderByDescending` — `EnsureSpecHasOrdering` throws
`NotSupportedException` up front otherwise, since paging an unordered query would return unstable/duplicate rows
across page boundaries.

**Keyset pagination** trades `Skip`/`Take` (which scans and discards every preceding row) for an index seek on the
ordering column(s) — it stays fast as tables grow, where offset pagination degrades. Three layers, from simplest to
richest:

1. **`IQueryable<TEntity>.AfterKeyset` / `.BeforeKeyset`** (single-key or composite two-key overloads, in
   `Extensions/KeysetQueryExtensions.cs`) add only a `WHERE` predicate — you own the `OrderBy` yourself:

   ```csharp
   var nextPage = await context.Orders
       .OrderBy(o => o.CreatedDate).ThenBy(o => o.Id)
       .AfterKeyset(o => o.CreatedDate, o => o.Id, lastDate, lastId)
       .Take(pageSize)
       .ToListAsync();
   // WHERE CreatedDate > @date OR (CreatedDate = @date AND Id > @id)
   // equivalent to the row-value comparison (CreatedDate, Id) > (@date, @id)
   ```

2. **`repo.ToKeysetPageAsync<TEntity, TKey>(spec, keySelector, cursor, pageSize, ct)`** (and the `TKey1, TKey2`
   composite overload) is the `IRepositorySpec` convenience wrapper: it applies the specification, chains
   `AfterKeyset`, and takes `pageSize` rows. The specification still owns `OrderBy` — declare it there so results
   stay ordered consistently with the cursor comparison.

3. **`IQueryable<TEntity>.ToKeysetPageAsync(configureKeyset, pageSize, direction, reference, ct)`** is the
   arbitrary-arity surface, backed by `MR.EntityFrameworkCore.KeysetPagination`. It owns *both* ordering and the
   cursor filter — do not chain it after your own `OrderBy` — and returns a `KeysetPage<TEntity>` record
   (`Items`, `HasPrevious`, `HasNext`) instead of a bare list:

   ```csharp
   var page = await context.Merchants.ToKeysetPageAsync(
       b => b.Ascending(m => m.Country).Descending(m => m.Revenue).Ascending(m => m.Id),
       pageSize: 20,
       direction: KeysetPaginationDirection.Forward,
       reference: lastSeenMerchant); // null for the first page

   if (page.HasNext) { /* show a "next" control */ }
   ```

   `reference` only needs an object whose property names match the configured keyset columns — it does not have to
   be a `TEntity`. This overload costs three round trips: the page query, plus one each for the `HasPrevious`/
   `HasNext` existence checks (and per `MR.EntityFrameworkCore.KeysetPagination` 1.6.0, those two checks do not
   accept a `CancellationToken`; only the page query itself observes it).

## ⚙️ Configuration reference

There is no options object or configuration section — behaviour is set per specification and per registration:

| Knob | Where | Default | Effect |
|---|---|---|---|
| `AddSpecRepo<TDbContext>()` | DI | not registered | Registers `IRepositorySpec` for `TDbContext`. Idempotent. |
| `AsNoTracking()` | specification constructor | tracking on (EF Core default) | Opts that specification's entity query into a no-tracking query. Projected (`TModel`) queries are always no-tracking. |
| `IgnoreQueryFilters()` | specification constructor | `false` | Bypasses global query filters whose `GlobalQueryFilter.IsIgnorable` is `true`; a filter overriding it to `false` is never bypassed. |
| `Skip` / `Take` | specification constructor | unset | Applied only when the specification is executed via `Query<TEntity>` / `Query<TEntity, TModel>` (see the note below). |
| `ToPageEnumerable` page size | internal constant `PageAsyncEnumeratorExtensions.DefaultPageSize` | `100` | Rows fetched per round trip while streaming. Not exposed as a parameter. |
| Keyset ordering | `configureKeyset` delegate | none — required | Defines the keyset columns and direction. A specification with no ordering throws `NotSupportedException`. |

The same points in full:

- **`AddSpecRepo<TDbContext>` is idempotent** — safe to call more than once; it checks `IsRegistered<IRepositorySpec>()`
  first and no-ops if already registered.
- **Tracking**: queries track by default, matching plain EF Core; call `AsNoTracking()` inside a specification's
  constructor to opt into read-only queries. Projected (`TModel`) queries are always `AsNoTracking()` regardless.
- **`IsIgnoreQueryFilters`**: `IgnoreQueryFilters()` bypasses only global query filters registered as *ignorable*
  (`GlobalQueryFilter.IsIgnorable`, default `true` — e.g. a soft-delete filter). A filter author can opt a filter out
  of this bypass entirely by overriding `IsIgnorable => false` (e.g. row-level tenant/ownership isolation) — that
  filter is never bypassed by a specification's flag, no matter which application calls it.
- **Streaming page size**: `ToPageEnumerable` fetches 100 rows per round trip; this is an internal constant
  (`PageAsyncEnumeratorExtensions.DefaultPageSize`), not currently exposed as a parameter on the public
  `IRepositorySpec` extension methods.
- **`Skip`/`Take`** on a specification apply only through `SpecificationExtensions.ApplySpecs` — i.e. only when the
  specification is executed via `Query<TEntity>`/`Query<TEntity, TModel>` (directly or through the `IRepositorySpec`
  extensions above), not when you read `FilterQuery`/`IncludeQueries`/etc. and build the query yourself.

## 🧱 Where it fits

A specification is inert data until `ApplySpecs` folds it onto an `IQueryable`, and it does that in a fixed order —
filters, then includes, then ordering, then the tracking/window flags:

![Data-flow diagram of ApplySpecs: the specification is applied to the queryable as IgnoreQueryFilters and Where, then Include and ThenInclude chains, then OrderBy and ThenBy in declaration sequence, and finally AsNoTracking, Skip and Take before EF Core executes the query.](../diagrams/efcore-specifications-query-composition.svg)

- **Replaces `DKNet.EfCore.Repos`.** `IRepositorySpec` is the direct successor to `IRepository<T>` /
  `IReadRepository<T>` / `IWriteRepository<T>` — see
  [`Migrating-Repos-To-Specifications.md`](./Migrating-Repos-To-Specifications.md) for the full call-site mapping and
  a converted before/after example.
- **Targets entities from `DKNet.EfCore.Abstractions`.** `Specification<TEntity>`/`ModelSpecification<TEntity,TModel>`
  only constrain `TEntity : class` — they work with any EF Core entity — but in a DKNet solution `TEntity` is
  typically an `Entity`/`Entity<TKey>`/aggregate root from `DKNet.EfCore.Abstractions`, keeping specification-based
  query reuse aligned with the same domain model the rest of the framework builds on.
- **Global query filters** come from `DKNet.EfCore.Extensions` (`GlobalQueryFilter`); `IsIgnoreQueryFilters` only
  interacts with filters registered through that mechanism.
- **Writes flow through the same `SaveChangesAsync`/`UpdateAsync` pipeline** as the rest of the EF Core stack —
  `RepositorySpec<TDbContext>.SaveChangesAsync` calls `AddNewEntitiesFromNavigations` and
  `SaveChangesWithConcurrencyHandlingAsync` (resolving a keyed `IEfCoreExceptionHandler` for the `DbContext` type if
  one is registered), so hooks/interceptors registered on the `DbContext` still run.
- **Model projections need Mapster** (`Mapster`/`MapsterMapper`, referenced by this package) registered in DI for
  `Query<TEntity, TModel>` and every `*<TEntity, TModel>` repository extension to work.

## ⚠️ Gotchas & limits

- **Forgetting `.AsExpandable()` breaks dynamic predicate translation — but only outside `IRepositorySpec`.**
  `RepositorySpec<TDbContext>.Query<TEntity>` already calls `.AsExpandable()` before applying the specification, so
  code that only ever queries through `IRepositorySpec` never needs to think about this. If you build a
  `DynamicAnd`/`DynamicOr` predicate and run it against a raw `DbSet`/`IQueryable` yourself (bypassing the
  repository), you must call `.AsExpandable()` first — LinqKit cannot translate/expand the predicate into SQL without
  it, and the query fails or silently mistranslates at execution time.
- **Do not reintroduce manual null checks around `DynamicAnd`/`DynamicOr`.** They already null-handle internally —
  `Equal`/`NotEqual` against a `null` value produce `IS NULL`/`IS NOT NULL`, and invalid values are dropped rather
  than throwing (see *Dynamic Predicate Builder* below). Wrapping calls in `if (value != null)` before calling `DynamicAnd` only hides the
  library's own handling and risks accidentally skipping a legitimate `null`-equality filter.
- **Materializing before `.Where(...)` gives wrong results and kills performance.** Call `ToListAsync()`/
  `ToListAsync<TEntity, TModel>()` (or any other terminal operator) only *after* the specification/predicate has been
  applied. Calling `ToList()`/`ToListAsync()` earlier in a query chain — before the filter, dynamic predicate, or
  paging is composed — pulls the unfiltered table into memory and applies everything client-side instead of pushing
  it to the database, which is both incorrect for large filtered subsets (memory blow-up) and far slower.
- **`ToPageEnumerable` requires ordering.** A specification with no `OrderBy`/`OrderByDescending` throws
  `NotSupportedException` immediately, rather than streaming unstably-ordered pages.
- **Filtered `AddInclude` + tracking can surface stale children.** A single-level filtered include
  (`AddInclude(p => p.OrderItems.Where(i => i.Quantity > 0))`) on a *tracking* query can return children that don't
  match the filter, because EF Core's navigation fixup reattaches already-tracked entities regardless of the include
  filter. Use a projection (`Query<TEntity, TModel>`) or `AsNoTracking()` when the include filter must be exact — see
  the [EF Core filtered-include docs](https://learn.microsoft.com/ef/core/querying/related-data/eager#filtered-include).
- **The raw Dynamic LINQ expression overload can still throw.** Unlike the triple (`propertyName, operation, value`)
  overload, `DynamicAnd`/`DynamicOr(string expression, params object?[] values)` throws `ArgumentException` for a
  blocklisted pattern and otherwise lets `System.Linq.Dynamic.Core` throw its own parse errors — treat it as
  fail-loud, not fail-safe.
- **`ToKeysetPageAsync` (arbitrary-arity, `IQueryable` overload) owns ordering.** Don't chain it after your own
  `OrderBy`/`OrderByDescending` — pass the ordering into `configureKeyset` instead — and remember its
  `HasPrevious`/`HasNext` checks add two extra round trips per call.

## 🔗 Related packages

- [DKNet.EfCore.Abstractions](./DKNet.EfCore.Abstractions.md) – the `Entity`/`AuditedEntity` aggregates
  specifications are usually written against. Reach for it when modelling the entities themselves.
- [DKNet.EfCore.Extensions](./DKNet.EfCore.Extensions.md) – owns `GlobalQueryFilter`, the mechanism
  `IgnoreQueryFilters()` interacts with, plus the concurrency-handling save extension `RepositorySpec` calls. Reach
  for it to register a global filter or configure the model.
- [Migrating-Repos-To-Specifications](./Migrating-Repos-To-Specifications.md) – the per-call mapping from the removed
  repository interfaces onto `IRepositorySpec`. Reach for it when converting existing code, and for the record of what
  `DKNet.EfCore.Repos` and `DKNet.EfCore.Repos.Abstractions` used to offer — those packages no longer ship.
- [DKNet.EfCore.DtoGenerator](./DKNet.EfCore.DtoGenerator.md) – generates the DTO types
  `ModelSpecification<TEntity, TModel>` projects onto. Reach for it so the projection target does not have to be
  hand-maintained.
- [DKNet.EfCore.DataAuthorization](./DKNet.EfCore.DataAuthorization.md) – row-level ownership filtering. Reach for it
  for multi-tenant isolation, and note its filter is deliberately exempt from `IgnoreQueryFilters()`.
