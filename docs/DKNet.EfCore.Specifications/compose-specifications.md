# Compose reusable queries

Part of [DKNet.EfCore.Specifications](index.md). Replace the Quick Start specification with a mixed-order, bounded read:

```csharp
public sealed class MinimumPrice : Specification<Product>
{
    public MinimumPrice(int minimum)
    {
        WithFilter(p => p.Price >= minimum);
        AddOrderByDescending(p => p.Price);
        AddOrderBy(p => p.Id);
        AsNoTracking();
        Take(20);
    }
}
```

![A Specification stores its filter, includes and ordered clauses, and ApplySpecs folds them into an IQueryable before applying tracking and offset-window flags.](diagrams/compose-specifications.svg)

Derive from `Specification<TEntity>` and use the protected builders in your constructor. `WithFilter` replaces the previous `FilterQuery`; combine predicates yourself when several conditions belong together. `CreatePredicate` returns a LinqKit starter, optionally initialized with an expression.

`AddInclude(Expression<Func<TEntity, object?>>)` supports single-navigation and filtered includes. `AddInclude(Func<IQueryable<TEntity>, IQueryable<TEntity>>)` supports `Include`/`ThenInclude` chains and deeper filtering. Ascending and descending order clauses are applied in declaration order as `OrderBy` followed by `ThenBy`.

`ISpecification<TEntity>` exposes `FilterQuery`, `IncludeQueries`, `IncludeBuilders` and `IsIgnoreQueryFilters`. Ordering, tracking and window state are recognized only on the `Specification<TEntity>` base. Its copy constructor accepts an existing specification and copies all base state, including flags and windowing when available.

## ⚙️ Configuration reference

| Builder | Type | Default | Effect |
|---|---|---|---|
| `AsNoTracking()` | Flag | Tracking | Read-only entity query. |
| `IgnoreQueryFilters()` | Flag | `false` | Disable only named filters registered as ignorable. |
| `Skip(count)` / `Take(count)` | Positive `int` | Unset | Offset/limit applied during query composition; `count <= 0` throws. |
| `AddOrderBy(name, direction)` | `string`, `ListSortDirection` | No order | Normalize a runtime property name and construct an ordering expression. |

There is no options object. A null specification is rejected by query composition.

## 🧱 Where it fits

The [query architecture](index.md#-architecture) shows the repository applying these inert declarations. [DataAuthorization](../DKNet.EfCore.DataAuthorization/filter-owned-rows.md) marks ownership filters non-ignorable, so the specification bypass flag preserves them.

## ⚠️ Gotchas & limits

- `ApplySpecs` is internal; consumers execute via `IRepositorySpec.Query` or its public materialization helpers.
- A runtime sort name is not the validated dynamic-filter path. Unknown names can throw; dotted paths are not built as chained member access by this ordering overload. Validate sort choices in the caller.
- Tracking navigation fixup can reattach previously tracked children outside a filtered include. Use a no-tracking query or projection for exact filtered children.
- Reading only `FilterQuery` ignores includes, ordering and flags. Execute the full specification rather than reconstructing part of it.
