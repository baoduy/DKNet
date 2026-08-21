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
using DKNet.EfCore.Specifications;
using DKNet.EfCore.Specifications.Dynamics;

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

## Learn more

Full feature guide, configuration, composition with other DKNet packages, and gotchas:
https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/DKNet.EfCore.Specifications.md

Migrating from `DKNet.EfCore.Repos`? See
https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/Migrating-Repos-To-Specifications.md
