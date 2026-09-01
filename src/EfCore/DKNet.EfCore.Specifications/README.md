# DKNet.EfCore.Specifications

The Specification pattern for EF Core, plus a runtime dynamic predicate builder — the flagship successor to the
retired `DKNet.EfCore.Repos`. Encapsulate filter, include, and order-by logic in reusable `Specification<TEntity>`
classes, and execute them through a single non-generic `IRepositorySpec` that serves every entity in your `DbContext`.

## Install

```bash
dotnet add package DKNet.EfCore.Specifications
```

```csharp
services.AddSpecRepo<AppDbContext>();
```

## Features

- **`Specification<TEntity>`** — filter (`WithFilter`), includes (`AddInclude`, single-level or `Include`/`ThenInclude`
  chains), and ordering (`AddOrderBy`/`AddOrderByDescending`, declared-sequence, mixed-direction) in one object.
- **`IRepositorySpec`** — one injected, non-generic repository for reads (`FirstAsync`, `ToListAsync`,
  `ToPagedListAsync`, `AnyAsync`, `CountAsync`) and writes (`AddAsync`, `UpdateAsync`, `SaveChangesAsync`,
  `BulkDeleteAsync`, `BeginTransactionAsync`) driven entirely by the specification passed to each call.
- **Dynamic Predicate Builder** (`DynamicAnd`/`DynamicOr`) — build EF Core predicates from
  `(propertyName, Ops, value)` triples for runtime-driven search filters, with automatic type/enum coercion,
  camelCase/snake_case/kebab-case property-name normalization, and fail-safe (silently-skipped) invalid input.
- **`ModelSpecification<TEntity, TModel>`** — the same builders, projected straight to a DTO via Mapster.
- **Keyset (cursor) pagination** — `AfterKeyset`/`BeforeKeyset`, `ToKeysetPageAsync`, and streaming
  `ToPageEnumerable` for large result sets without `Skip`/`Take` scan cost.

## Quick start

```csharp
using LinqKit;
using DKNet.EfCore.Specifications.Definitions;
using DKNet.EfCore.Specifications.Dynamics;
using DKNet.EfCore.Specifications.Repositories;

public sealed class ProductSearchSpecification : Specification<Product>
{
    public ProductSearchSpecification(string? name, decimal? minPrice)
    {
        var predicate = CreatePredicate(p => p.IsActive);

        if (name is not null)
            predicate = predicate.DynamicAnd(nameof(Product.Name), Ops.Contains, name);

        if (minPrice is not null)
            predicate = predicate.DynamicAnd(nameof(Product.Price), Ops.GreaterThanOrEqual, minPrice);

        WithFilter(predicate);
        AddOrderBy(p => p.Name);
    }
}

public sealed class ProductService(IRepositorySpec repo)
{
    public Task<IList<Product>> SearchAsync(string? name, decimal? minPrice, CancellationToken ct) =>
        repo.ToListAsync(new ProductSearchSpecification(name, minPrice), ct);
}
```

`.AsExpandable()` is applied automatically when querying through `IRepositorySpec` — no extra wiring needed for the
dynamic predicate above to translate to SQL.

## Customisation reference

There is no options class and no configuration section — behaviour is set per specification and per registration.

| Knob | Where | Default | Effect |
|---|---|---|---|
| `AddSpecRepo<TDbContext>()` | DI registration | not registered | Registers `IRepositorySpec` for that `DbContext`. Idempotent — calling it twice is a no-op. |
| `WithFilter(expression)` | specification constructor | no filter | The `Where` clause applied by `ApplySpecs`. |
| `AddInclude(expression)` / `AddInclude(builder)` | specification constructor | no includes | Single-level includes, or an `Include`/`ThenInclude` chain that may filter at any level. |
| `AddOrderBy` / `AddOrderByDescending` | specification constructor | unordered | Applied in the sequence declared, so mixed directions survive. |
| `AddOrderBy(string, ListSortDirection)` | specification constructor | — | Property-name overload; the name is normalised to PascalCase first. |
| `AsNoTracking()` | specification constructor | tracking on, matching EF Core | Runs that specification's entity query without change tracking. Projected `TModel` queries are always no-tracking. |
| `IgnoreQueryFilters()` | specification constructor | `false` | Bypasses only filters whose `GlobalQueryFilter.IsIgnorable` is `true`; ownership isolation is never bypassed. |
| `Skip(count)` / `Take(count)` | specification constructor | unset | Must be greater than zero. Applied only when the specification runs through `Query<TEntity>` / `Query<TEntity, TModel>`. |
| `CreatePredicate(expression?)` | specification constructor | empty predicate | Starts a LinqKit `ExpressionStarter<TEntity>` for `DynamicAnd`/`DynamicOr` composition. |
| `ToPageEnumerable` page size | internal constant | `100` rows per round trip | Not exposed as a parameter. |
| Keyset ordering | `configureKeyset` delegate | none — required | A keyset page with no ordering throws `NotSupportedException`. |

`ApplySpecs` composes in a fixed order: ignore-filters, `Where`, includes, include builders, ordering, then
`AsNoTracking`, `Skip` and `Take`. Reading `FilterQuery`/`IncludeQueries` yourself skips all of it.

## Migration — namespace changes in this release

Root types were grouped into concern folders; the namespace of each moved type now ends
with its folder name. This is an import-only source break: no type was renamed, removed,
resignatured, or had its behaviour changed — update the `using` line and you're done.

| Type | Old namespace | New namespace |
|---|---|---|
| `Specification<TEntity>`, `ModelSpecification<TEntity, TModel>`, `OrderClause` | `DKNet.EfCore.Specifications` | `DKNet.EfCore.Specifications.Definitions` |
| `IRepositorySpec`, `IRepositorySpecFactory`, `IRepositorySpecProvider` (+ their implementations) | `DKNet.EfCore.Specifications` | `DKNet.EfCore.Specifications.Repositories` |
| `PageAsyncEnumerator` | `DKNet.EfCore.Specifications` | `DKNet.EfCore.Specifications.Paging` |

`SpecSetup` — the package's `AddSpecRepo` registration point — stays at `DKNet.EfCore.Specifications`.
`Dynamics/` and `Extensions/` (including the ambient `LinqKit`-namespaced `DynamicPredicateExtensions`)
are unchanged.

## Learn more

Full feature guide, configuration, composition with other DKNet packages, and gotchas:
https://github.com/baoduy/DKNet/blob/main/docs/EfCore/DKNet.EfCore.Specifications.md

Migrating from `DKNet.EfCore.Repos`? See
https://github.com/baoduy/DKNet/blob/main/docs/EfCore/Migrating-Repos-To-Specifications.md
