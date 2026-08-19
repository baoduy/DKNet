# Migrating from DKNet.EfCore.Repos to DKNet.EfCore.Specifications

`DKNet.EfCore.Repos`, `DKNet.EfCore.Repos.Abstractions`, and `DKNet.EfCore.DtoEntities` are retired: they stay in the
source tree (unpublished, `IsPackable=false`) for source builders, but are no longer packed or advertised as NuGet
packages. Their public types now carry `[Obsolete]` and point here. New code — and any code still referencing
the retired interfaces — should move to the specification repository in `DKNet.EfCore.Specifications`.

## Why

`IRepositorySpec` covers the same read/write/paging surface as `IRepository<T>` while composing directly with
`Specification<T>` (filter + include + order-by in one object), so callers no longer need a second query layer
bolted on top of the repository.

## Registration

**Before**

```csharp
services.AddGenericRepositories<AppDbContext>();
// or
services.AddRepoFactory<AppDbContext>();
```

**After**

```csharp
services.AddSpecRepo<AppDbContext>();
```

## Injecting the repository

**Before**

```csharp
public sealed class OrderService(IRepository<Order> repo)
{
    public Task<Order?> FindAsync(Guid id, CancellationToken ct) => repo.FindAsync(id, ct);
}
```

**After**

```csharp
public sealed class OrderService(IRepositorySpec repo)
{
    public Task<Order?> FindAsync(Guid id, CancellationToken ct) =>
        repo.FirstOrDefaultAsync(new OrderByIdSpecification(id), ct);
}
```

`IRepositorySpec` is not generic over the entity — the entity type is inferred from the `Specification<TEntity>`
passed to each call, so one injected instance serves every aggregate in the `DbContext`.

## Query call-site mapping

| `DKNet.EfCore.Repos` (retired) | `DKNet.EfCore.Specifications` replacement |
|---|---|
| `repo.Query(filter)` / `repo.Query()` | `repo.Query(spec)` / define the filter on a `Specification<T>` |
| `repo.FindAsync(filter, ct)` | `repo.FirstOrDefaultAsync(spec, ct)` |
| `repo.ExistsAsync(filter, ct)` | `repo.AnyAsync(spec, ct)` |
| `repo.CountAsync(filter, ct)` | `repo.CountAsync(spec, ct)` |
| `repo.Query<TModel>(filter)` (projection) | `repo.ToListAsync<TEntity, TModel>(spec, ct)` / `FirstOrDefaultAsync<TEntity, TModel>(spec, ct)` |
| `RepoExtensions.SpecsListAsync` / `SpecsToPageListAsync` | `repo.ToListAsync(spec, ct)` / `repo.ToPagedListAsync(spec, page, size, ct)` |

## Writes

`IRepositorySpec` carries the same write surface as `IWriteRepository<T>` — `AddAsync`/`AddRangeAsync`,
`UpdateAsync`/`UpdateRangeAsync`, `SaveChangesAsync`, `BeginTransactionAsync` — just generic per call instead
of per injected instance, since one `IRepositorySpec` serves every entity type.

**Before**

```csharp
public sealed class OrderService(IRepository<Order> repo)
{
    public async Task AddAsync(Order order, CancellationToken ct)
    {
        await repo.AddAsync(order, ct);
        await repo.SaveChangesAsync(ct);
    }
}
```

**After**

```csharp
public sealed class OrderService(IRepositorySpec repo)
{
    public async Task AddAsync(Order order, CancellationToken ct)
    {
        await repo.AddAsync(order, ct);
        await repo.SaveChangesAsync(ct);
    }
}
```

## Example: converting an ad-hoc filter to a specification

**Before**

```csharp
var expensiveActive = await repo.Query(p => p.IsActive && p.Price > 100m).ToListAsync(ct);
```

**After**

```csharp
private sealed class ExpensiveActiveProductsSpecification : Specification<Product>
{
    public ExpensiveActiveProductsSpecification()
    {
        WithFilter(p => p.IsActive && p.Price > 100m);
        AddOrderBy(p => p.Name);
    }
}

var expensiveActive = await repo.ToListAsync(new ExpensiveActiveProductsSpecification(), ct);
```

## See also

- `DKNet.EfCore.Specifications` package docs: [`DKNet.EfCore.Specifications.md`](./DKNet.EfCore.Specifications.md)
- Source of record: `src/EfCore/DKNet.EfCore.Specifications/IRepositorySpec.cs`, `SpecSetup.cs`
