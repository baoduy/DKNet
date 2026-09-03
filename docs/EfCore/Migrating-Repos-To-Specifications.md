# Migrating from DKNet.EfCore.Repos to DKNet.EfCore.Specifications

`DKNet.EfCore.Repos` and `DKNet.EfCore.Repos.Abstractions` were **removed** in this release — the projects, their
solution entries, and their test project are gone from the source tree entirely. They previously shipped
unpublished (`IsPackable=false`) with every public type marked `[Obsolete]`; there is no source or project
reference left to fall back on. The public types that no longer exist anywhere in the codebase: `IRepository<T>`,
`IReadRepository<T>`, `IWriteRepository<T>`, `IRepositoryFactory`, `SetupRepository`, `RepoExtensions`, and the
`Repository<T>`/`ReadRepository<T>`/`WriteRepository<T>`/`RepositoryFactory<TDbContext>` implementations. Any code
still referencing them will not compile. This page maps every removed member onto its
`DKNet.EfCore.Specifications` equivalent.

The same commit also renamed `DKNet.EfCore.DtoEntities` to `EfCore.DtoGenerator.TestEntities`
(`src/EfCore/EfCore.DtoGenerator.TestEntities/`). That project was never a published package or a public API — it
is an internal test-fixture project consumed only by the `DKNet.EfCore.DtoGenerator` test suites — so it was
renamed, not removed, and there is nothing here for a consumer to migrate: no external code ever referenced it.

## Why

`IRepositorySpec` covers the same read/write/paging surface as `IRepository<T>` while composing directly with
`Specification<T>` (filter + include + order-by in one object), so callers no longer need a second query layer
bolted on top of the repository. See [`DKNet.EfCore.Specifications`](./DKNet.EfCore.Specifications.md) for the
package's own docs.

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

`AddSpecRepo<TDbContext>()` registers both `IRepositorySpec` (scoped) and `IRepositorySpecFactory` (singleton) in
one call — there is no separate factory registration to opt into.

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

| `DKNet.EfCore.Repos` (removed) | `DKNet.EfCore.Specifications` replacement |
|---|---|
| `repo.Query(filter)` / `repo.Query()` | `repo.Query(spec)` — define the filter on a `Specification<T>` |
| `repo.Query<TModel>(filter)` (Mapster projection) | `repo.Query<TEntity, TModel>(spec)`, or the `ToListAsync<TEntity, TModel>` / `FirstOrDefaultAsync<TEntity, TModel>` extensions below |
| `repo.FindAsync(keyValue, ct)` / `FindAsync(keyValues, ct)` | no direct equivalent — write a `Specification<T>` that filters on the key and call `repo.FirstOrDefaultAsync(spec, ct)` |
| `repo.FindAsync(filter, ct)` | `repo.FirstOrDefaultAsync(spec, ct)` |
| `repo.ExistsAsync(filter, ct)` | `repo.AnyAsync(spec, ct)` |
| `repo.CountAsync(filter, ct)` | `repo.CountAsync(spec, ct)` |
| `repo.Delete(entity)` | `repo.Delete(entity)` — unchanged |
| `repo.DeleteRange(entities)` | **removed, no direct replacement.** `IRepositorySpec.DeleteRange` was also removed in this pass — use `repo.BulkDeleteAsync<TEntity>(predicate, ct)` instead (a server-side `ExecuteDeleteAsync`, so entities do not need to be loaded first) |
| `RepoExtensions.QuerySpecs(spec)` | `repo.Query(spec)` |
| `RepoExtensions.SpecsAnyAsync(spec, ct)` | `repo.AnyAsync(spec, ct)` |
| `RepoExtensions.SpecsCountAsync(spec, ct)` | `repo.CountAsync(spec, ct)` |
| `RepoExtensions.SpecsFirstAsync(spec, ct)` | `repo.FirstAsync(spec, ct)` |
| `RepoExtensions.SpecsFirstOrDefaultAsync(spec, ct)` | `repo.FirstOrDefaultAsync(spec, ct)` |
| `RepoExtensions.SpecsListAsync(spec, ct)` — returned `Task<IList<T>>` | `repo.ToListAsync(spec, ct)` — returns **`Task<List<T>>`**, not `IList<T>` |
| `RepoExtensions.SpecsToPageEnumerable(spec)` | `repo.ToPageEnumerable(spec)` — default page size 100, requires the specification to declare an `OrderBy`/`OrderByDescending` |
| `RepoExtensions.SpecsToPageListAsync(spec, page, size, ct)` | `repo.ToPagedListAsync(spec, page, size, ct)` |

## Writes

`IRepositorySpec` carries the same write surface as `IWriteRepository<T>` — `AddAsync`/`AddRangeAsync`,
`UpdateAsync`/`UpdateRangeAsync`, `SaveChangesAsync`, `BeginTransactionAsync`, `Delete`, `Entry` — just generic
per call instead of per injected instance, since one `IRepositorySpec` serves every entity type. The one exception
is `DeleteRange`, which was removed with no direct replacement — see the mapping table above for
`BulkDeleteAsync`.

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

## Factory usage (background jobs, out-of-scope work)

**Before**

```csharp
services.AddDbContextFactory<AppDbContext>(o => o.UseSqlServer(connectionString));
services.AddRepoFactory<AppDbContext>();

public sealed class BackgroundExportJob(IRepositoryFactory factory)
{
    public async Task RunAsync(CancellationToken ct)
    {
        var readRepo = factory.CreateRead<Product>();
        var writeRepo = factory.CreateWrite<Order>();
        // ... one repository per entity type
    }
}
```

**After**

```csharp
services.AddDbContextFactory<AppDbContext>(o => o.UseSqlServer(connectionString));
services.AddSpecRepo<AppDbContext>();   // also registers IRepositorySpecFactory

public sealed class BackgroundExportJob(IRepositorySpecFactory factory)
{
    public async Task RunAsync(CancellationToken ct)
    {
        await using var provider = factory.CreateAsync<AppDbContext>();
        var repo = provider.Repository;   // one IRepositorySpec serves every entity type
        // ... repo.Query(spec), repo.AddAsync(entity, ct), etc.
    }
}
```

`IRepositorySpecFactory.CreateAsync<TDbContext>()` returns one `IRepositorySpecProvider` owning its own DI scope
and a factory-created `DbContext`; its single `Repository` (`IRepositorySpec`) replaces
`CreateRead<T>()`/`CreateWrite<T>()`/`Create<T>()` returning a repository per entity type. Dispose the provider
(`IDisposable`/`IAsyncDisposable`) when done — disposing it disposes the scope and the `DbContext` together, same
as the old factory.

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
- Source of record: `src/EfCore/DKNet.EfCore.Specifications/Repositories/IRepositorySpec.cs`,
  `src/EfCore/DKNet.EfCore.Specifications/SpecSetup.cs`
