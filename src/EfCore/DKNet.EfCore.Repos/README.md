# DKNet.EfCore.Repos

> ⚠️ **Retired / obsolete.** This project builds with `<IsPackable>false</IsPackable>` — it is **not published to
> NuGet**, ever. It exists only as a source-only compatibility shim for pre-existing consumers. Every public type
> (`ReadRepository<T>`, `WriteRepository<T>`, `Repository<T>`, `RepositoryFactory<T>`, `RepoExtensions`,
> `SetupRepository`) is marked `[Obsolete]` and points to `DKNet.EfCore.Specifications`
> (`IRepositorySpec` + `SpecSetup.AddSpecRepo`). **Do not use this package for new code.**

## What is this project?

The original generic repository pattern implementation over EF Core in DKNet — `IReadRepository<T>` /
`IWriteRepository<T>` / `IRepository<T>` implementations backed directly by a `DbContext`. Superseded by
`DKNet.EfCore.Specifications`'s `IRepositorySpec`, which covers the same read/write/paging surface while composing
directly with `Specification<T>` instead of a second ad-hoc query layer.

## Features (legacy, for existing source consumers only)

- `ReadRepository<TEntity>` — `AsNoTracking()` queries, `FindAsync`, `CountAsync`, `ExistsAsync`, Mapster projection via `Query<TModel>`
- `WriteRepository<TEntity>` — `AddAsync`/`AddRangeAsync`, `Delete`/`DeleteRange`, `UpdateAsync`/`UpdateRangeAsync`, `SaveChangesAsync`, `BeginTransactionAsync`, `Entry`
- `Repository<TEntity>` — combines the above with a **tracked** `Query()` override for read-then-update workflows
- `RepositoryFactory<TDbContext>` — creates repositories against a factory-owned `DbContext` (e.g. for background workers)
- `SetupRepository` — `AddGenericRepositories<TDbContext>()` / `AddRepoFactory<TDbContext>()` DI registration
- `RepoExtensions` (in `RepoSpecExtensions.cs`) — bridges a repository to `DKNet.EfCore.Specifications`'s `ISpecification<T>` (`QuerySpecs`, `SpecsCountAsync`, `SpecsListAsync`, `SpecsToPageListAsync`, ...)

## Quick start (legacy usage)

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString));
services.AddGenericRepositories<AppDbContext>(); // registers IReadRepository<>, IWriteRepository<>, IRepository<>

public sealed class ProductService(IRepository<Product> repo)
{
    public async Task<Guid> CreateAsync(Product product, CancellationToken ct)
    {
        await repo.AddAsync(product, ct);
        await repo.SaveChangesAsync(ct);
        return product.Id;
    }
}
```

## Migration — namespace changes in this release

Root types were grouped into concern folders; the namespace of each moved type now ends
with its folder name. This is an import-only source break: no type was renamed, removed,
resignatured, or had its behaviour changed — update the `using` line and you're done.

| Type | Old namespace | New namespace |
|---|---|---|
| `ReadRepository<T>`, `WriteRepository<T>`, `Repository<T>`, `RepositoryFactory<T>` | `DKNet.EfCore.Repos` | `DKNet.EfCore.Repos.Repositories` |

`SetupRepository` (ambient, namespace `Microsoft.Extensions.DependencyInjection`) and
`RepoExtensions`/`RepoSpecExtensions.cs` — the package's registration point and its
spec-application concern — stay at `DKNet.EfCore.Repos`.

## Full docs and migration

- Full feature reference: [`docs/EfCore/DKNet.EfCore.Repos.md`](https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/DKNet.EfCore.Repos.md)
- Migration path: [`docs/EfCore/Migrating-Repos-To-Specifications.md`](https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/Migrating-Repos-To-Specifications.md)
