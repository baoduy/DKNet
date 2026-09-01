# DKNet.EfCore.Repos

Retired generic-repository implementations over EF Core (`ReadRepository<T>`, `WriteRepository<T>`, `Repository<T>`,
`RepositoryFactory<TDbContext>`), kept in source only for readers maintaining code that still references them.

> ⚠️ **Retired / obsolete — source-only, not published to NuGet.**
> The project sets `<IsPackable>false</IsPackable>`, so it is **never packed or published**; it exists purely so
> existing source consumers keep compiling. Every public type — `ReadRepository<TEntity>`, `WriteRepository<TEntity>`,
> `Repository<TEntity>`, `RepositoryFactory<TDbContext>`, `RepoExtensions`, `SetupRepository` — carries an
> `[Obsolete(...)]` attribute. Most read:
> `"DKNet.EfCore.Repos is retired. Use DKNet.EfCore.Specifications (IRepositorySpec + SpecSetup) instead. See docs/EfCore/Migrating-Repos-To-Specifications.md."`
> `SetupRepository` reads the same message but names the exact replacement entry point:
> `"DKNet.EfCore.Repos is retired. Use DKNet.EfCore.Specifications (IRepositorySpec + SpecSetup.AddSpecRepo) instead. See docs/EfCore/Migrating-Repos-To-Specifications.md."`
>
> **Do not start new code against this package.** Use `DKNet.EfCore.Specifications` (`IRepositorySpec` +
> `SpecSetup.AddSpecRepo`) instead — see [Migrating from DKNet.EfCore.Repos to DKNet.EfCore.Specifications](./Migrating-Repos-To-Specifications.md)
> and [DKNet.EfCore.Specifications](./DKNet.EfCore.Specifications.md). This page documents the retired API only for
> teams that still reference it from source.

## ✨ Why use it?

You should not — this package is retired, and every public type in it is `[Obsolete]`. It is documented here for one
reader: someone maintaining code that still injects these repositories. What it solved, and why that no longer needs
solving:

- **Persistence ignorance per aggregate** — application services depended on `IReadRepository<TEntity>` /
  `IWriteRepository<TEntity>` / `IRepository<TEntity>` rather than on `DbContext` or `DbSet<TEntity>`, so they could
  be unit-tested against a fake.
- **CRUD, projection, and transaction primitives in one injectable** — `FindAsync`, `Query`, `Query<TModel>`,
  `AddAsync`, `UpdateAsync`, `Delete`, `BeginTransactionAsync`, `SaveChangesAsync` on a single per-entity surface.
- **A runtime factory seam** — `RepositoryFactory<TDbContext>` produced a repository for an arbitrary entity type
  without injecting one `IRepository<T>` per entity.
- **A bridge to specifications** — `RepoExtensions` accepted `Specification<TEntity>` objects against these
  repositories once that pattern arrived, which is the seam the replacement package grew out of.

That shape was right while queries were ad-hoc `Expression<Func<TEntity, bool>>` filters passed into
`Query(filter)` / `FindAsync(filter)`. Once [DKNet.EfCore.Specifications](./DKNet.EfCore.Specifications.md)
introduced `Specification<TEntity>` (filter + includes + order-by as one composable, reusable object) and
`IRepositorySpec` (one non-generic repository whose entity type is inferred per call from the specification), this
second query layer on top of EF Core became redundant. **New code should use `DKNet.EfCore.Specifications`
directly**; existing consumers should follow
[Migrating from DKNet.EfCore.Repos to DKNet.EfCore.Specifications](./Migrating-Repos-To-Specifications.md).

## 🚀 Quick Start

This package is **not on NuGet** — `<IsPackable>false</IsPackable>` in `DKNet.EfCore.Repos.csproj` means it is never
packed or published. The only way to consume it is a source/project reference from within this repository (or a
fork of it), e.g.:

```xml
<ProjectReference Include="..\DKNet.EfCore.Repos\DKNet.EfCore.Repos.csproj" />
```

Minimum DI registration is the `IServiceCollection` extension method in `SetupRepository`
(namespace `Microsoft.Extensions.DependencyInjection`):

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString));

// Registers IReadRepository<>, IWriteRepository<>, IRepository<> as scoped, backed by AppDbContext.
services.AddGenericRepositories<AppDbContext>();
```

`AddGenericRepositories<TDbContext>()` is idempotent (skips re-registration if `IReadRepository<>` is already
registered) and also exposes `TDbContext` as the base `DbContext` service if nothing already provides it. There is
a second, independent registration for the factory flavor (see below):

```csharp
services.AddRepoFactory<AppDbContext>();
```

## 🧩 Features

### `ReadRepository<TEntity>` — query-only access

`ReadRepository<TEntity>(DbContext dbContext, IEnumerable<IMapper>? mappers = null) : IReadRepository<TEntity>`

All queries default to `AsNoTracking()`. Projection requires an `IMapper` (Mapster) — pass one via the constructor
or DI; without it, `Query<TModel>` throws `InvalidOperationException`.

```csharp
public sealed class ProductQueryService(IReadRepository<Product> repo)
{
    public Task<int> CountActiveAsync(CancellationToken ct) =>
        repo.CountAsync(p => p.IsActive, ct);

    public Task<bool> SkuExistsAsync(string sku, CancellationToken ct) =>
        repo.ExistsAsync(p => p.Sku == sku, ct);

    public ValueTask<Product?> GetByIdAsync(Guid id, CancellationToken ct) =>
        repo.FindAsync(id, ct);

    public Task<Product?> GetBySkuAsync(string sku, CancellationToken ct) =>
        repo.FindAsync(p => p.Sku == sku, ct);

    // Untracked IQueryable for further composition.
    public IQueryable<Product> ActiveProducts() => repo.Query(p => p.IsActive);

    // Mapster projection — throws InvalidOperationException if no IMapper is registered.
    public IQueryable<ProductDto> ActiveProductDtos() =>
        repo.Query<ProductDto>(p => p.IsActive);
}
```

### `WriteRepository<TEntity>` — add/update/delete/save

`WriteRepository<TEntity>(DbContext dbContext, IServiceProvider? provider = null) : ReadRepository<TEntity>(dbContext), IWriteRepository<TEntity>`

Inherits every read method above, plus:

```csharp
public sealed class ProductCommandService(IWriteRepository<Product> repo)
{
    public async Task<Guid> CreateAsync(Product product, CancellationToken ct)
    {
        await repo.AddAsync(product, ct);
        await repo.SaveChangesAsync(ct);
        return product.Id;
    }

    public async Task CreateManyAsync(IEnumerable<Product> products, CancellationToken ct)
    {
        await repo.AddRangeAsync(products, ct);
        await repo.SaveChangesAsync(ct);
    }

    public async Task RenameAsync(Product product, string name, CancellationToken ct)
    {
        product.Rename(name);
        await repo.UpdateAsync(product, ct);   // marks Modified + adds any new navigation entities
        await repo.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(Product product, CancellationToken ct)
    {
        repo.Delete(product);                  // synchronous — stages removal
        await repo.SaveChangesAsync(ct);
    }

    public async Task TransferStockAsync(Product from, Product to, int qty, CancellationToken ct)
    {
        await using var tx = await repo.BeginTransactionAsync(ct);
        from.Reduce(qty);
        to.Increase(qty);
        await repo.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
```

Notes on `SaveChangesAsync`: it first calls `dbContext.AddNewEntitiesFromNavigations(ct)` (auto-attaches new
entities reachable from navigation properties), then delegates to
`dbContext.SaveChangesWithConcurrencyHandlingAsync(handler, ct)`, resolving an optional keyed
`IEfCoreExceptionHandler` registered under `dbContext.GetType().FullName`. `Entry(entity)` exposes the underlying
`EntityEntry<TEntity>` for direct change-tracking inspection when needed.

### `Repository<TEntity>` — read + write combined

`Repository<TEntity> : WriteRepository<TEntity>, IRepository<TEntity>` (sealed) adds nothing new API-wise beyond
`WriteRepository<TEntity>` except overriding `Query()` to return a **tracked** queryable
(`dbContext.Set<TEntity>()`, no `AsNoTracking()`), since read-then-update workflows need change tracking on the
materialized entities:

```csharp
public sealed class OrderService(IRepository<Order> repo)
{
    public async Task ApplyDiscountAsync(Guid orderId, decimal percent, CancellationToken ct)
    {
        // Tracked query — mutating the result and calling SaveChangesAsync persists it.
        var order = await repo.Query().FirstAsync(o => o.Id == orderId, ct);
        order.ApplyDiscount(percent);
        await repo.SaveChangesAsync(ct);
    }
}
```

It has two constructors: `Repository(DbContext dbContext, IServiceProvider? provider = null)` (resolves `IMapper`
from `provider` if present — the one used by DI) and an `internal Repository(DbContext dbContext, IMapper? mapper = null)` used by `RepositoryFactory<TDbContext>`.

### `RepositoryFactory<TDbContext>` — repository-per-DbContext-instance factory

`RepositoryFactory<TDbContext>(IDbContextFactory<TDbContext> dbFactory, IServiceProvider provider) : IRepositoryFactory`, `where TDbContext : DbContext`.

Use this when you need a repository backed by a **freshly created, factory-owned `DbContext`** instead of the
ambient scoped one — e.g. background workers or fan-out parallel work outside a request scope. The factory creates
one `TDbContext` via `dbFactory.CreateDbContext()` on construction and owns its lifetime.

```csharp
services.AddDbContextFactory<AppDbContext>(o => o.UseSqlServer(connectionString));
services.AddRepoFactory<AppDbContext>();

public sealed class BackgroundExportJob(IRepositoryFactory factory)
{
    public async Task RunAsync(CancellationToken ct)
    {
        await using var repoFactory = (RepositoryFactory<AppDbContext>)factory; // or inject IRepositoryFactory directly
        var readRepo = factory.CreateRead<Product>();
        var writeRepo = factory.CreateWrite<Order>();
        var repo = factory.Create<Customer>();
        // ... use repositories; the factory disposes its owned DbContext via Dispose()/DisposeAsync().
    }
}
```

`Create<TEntity>()` returns `IRepository<TEntity>`, `CreateRead<TEntity>()` returns `IReadRepository<TEntity>`,
`CreateWrite<TEntity>()` returns `IWriteRepository<TEntity>`. `Dispose()` / `DisposeAsync()` dispose the
factory-owned `DbContext` — dispose the factory (or the resolving scope) when done, not the individual repositories.

### `RepoExtensions` (in `RepoSpecExtensions.cs`) — the bridge to `DKNet.EfCore.Specifications`

This is the deliberate seam between the two packages: extension methods on `IReadRepository<TEntity>` that accept an
`ISpecification<TEntity>` from `DKNet.EfCore.Specifications` and apply its `FilterQuery`, `IncludeQueries`/`IncludeBuilders`,
`OrderByQueries`/`OrderByDescendingQueries`, and `IsIgnoreQueryFilters` flag — a **local copy** of logic that is
`internal` inside `DKNet.EfCore.Specifications`, kept in sync only for as long as this retired package ships.

```csharp
using DKNet.EfCore.Repos;          // brings in the RepoExtensions extension methods
using DKNet.EfCore.Specifications; // ISpecification<T>, Specification<T>

public sealed class ActiveProductsSpec : Specification<Product>
{
    public ActiveProductsSpec()
    {
        WithFilter(p => p.IsActive);
        AddOrderBy(p => p.Name);
    }
}

public sealed class ProductSpecQueryService(IReadRepository<Product> repo)
{
    public IQueryable<Product> Query() => repo.QuerySpecs(new ActiveProductsSpec());

    public Task<int> CountAsync(CancellationToken ct) =>
        repo.SpecsCountAsync(new ActiveProductsSpec(), ct);

    public Task<bool> AnyAsync(CancellationToken ct) =>
        repo.SpecsAnyAsync(new ActiveProductsSpec(), ct);

    public Task<Product> FirstAsync(CancellationToken ct) =>
        repo.SpecsFirstAsync(new ActiveProductsSpec(), ct);

    public Task<Product?> FirstOrDefaultAsync(CancellationToken ct) =>
        repo.SpecsFirstOrDefaultAsync(new ActiveProductsSpec(), ct);

    public Task<IList<Product>> ListAsync(CancellationToken ct) =>
        repo.SpecsListAsync(new ActiveProductsSpec(), ct);

    // Requires the specification to define at least one OrderBy/OrderByDescending, or throws NotSupportedException.
    public IAsyncEnumerable<Product> StreamAsync() =>
        repo.SpecsToPageEnumerable(new ActiveProductsSpec());

    public Task<X.PagedList.IPagedList<Product>> PageAsync(int page, int size, CancellationToken ct) =>
        repo.SpecsToPageListAsync(new ActiveProductsSpec(), page, size, ct);
}
```

`SpecsToPageEnumerable` streams results in fixed-size pages of 100 (`ToPageEnumerable(pageSize: 100)` internally,
not configurable from the public surface) rather than materializing the whole result set, and requires ordering —
call `EnsureSpecHasOrdering()` semantics apply: no `OrderBy`/`OrderByDescending` on the specification throws
`NotSupportedException`. `SpecsToPageListAsync` returns an `X.PagedList.IPagedList<TEntity>` via
`X.PagedList.EF`'s `ToPagedListAsync`.

## ⚙️ Configuration reference

There is no options object or configuration section — behavior is fixed by construction:

| Aspect | Default | How to change it |
|---|---|---|
| Read tracking | `ReadRepository<T>.Query()` is `AsNoTracking()` | Use `Repository<T>.Query()` (tracked) instead, or track manually via `dbContext.Entry` |
| Mapster mapper | First `IMapper` from the injected `IEnumerable<IMapper>` (or `IServiceProvider.GetService(typeof(IMapper))` for `Repository<T>`/`RepositoryFactory<T>`) | Register exactly one `IMapper` (e.g. via Mapster's `services.AddMapster()`) — `Query<TModel>` throws `InvalidOperationException` if none is found |
| Duplicate registration | `AddGenericRepositories<TDbContext>()` no-ops if `IReadRepository<>` is already registered | N/A — call it once per `TDbContext` |
| `IRepositoryFactory` registration | `AddRepoFactory<TDbContext>()` no-ops if `IRepositoryFactory` is already registered | N/A |
| Exception handling in `SaveChangesAsync` | Optional keyed `IEfCoreExceptionHandler` resolved by `dbContext.GetType().FullName` | Register a keyed service under that key to intercept concurrency/db exceptions |
| Specification paging page size (`SpecsToPageEnumerable`) | Fixed at 100 | Not configurable — use `DKNet.EfCore.Specifications`'s own paging APIs directly if you need a different size |

## 🧱 Where it fits

- **`DKNet.EfCore.Repos.Abstractions`** — defines `IReadRepository<T>`, `IWriteRepository<T>`, `IRepository<T>`,
  `IRepositoryFactory` that every type here implements.
- **`DKNet.EfCore.Extensions`** — `WriteRepository<T>.SaveChangesAsync` calls its
  `AddNewEntitiesFromNavigations` / `GetNewEntitiesFromNavigations` / `SaveChangesWithConcurrencyHandlingAsync`
  extension methods.
- **`DKNet.EfCore.Specifications`** — `RepoExtensions` (`RepoSpecExtensions.cs`) is the bridge: it accepts
  `ISpecification<TEntity>` / `Specification<TEntity>` from that package and applies it to a repository's queryable,
  duplicating (locally, deliberately) logic that package keeps `internal`.
- **Mapster / `MapsterMapper`** — `Query<TModel>` / `Repository<TEntity>.Query<TModel>` project via
  `IMapper.Config` and `ProjectToType<TModel>()`.
- **`X.PagedList.EF`** — backs `SpecsToPageListAsync`'s `IPagedList<TEntity>` result.

## ⚠️ Gotchas & limits

- **This package is obsolete and unpublished.** It is source-only (`IsPackable=false`), every public type is
  `[Obsolete]`, and it exists solely to keep pre-existing source consumers compiling. **Migrate to
  `DKNet.EfCore.Specifications`** (`IRepositorySpec` + `SpecSetup.AddSpecRepo`) — see
  [Migrating from DKNet.EfCore.Repos to DKNet.EfCore.Specifications](./Migrating-Repos-To-Specifications.md) for the
  full call-site mapping, and [DKNet.EfCore.Specifications](./DKNet.EfCore.Specifications.md) for the replacement
  package's own docs.
- Any code that still references `IReadRepository<T>` / `IWriteRepository<T>` / `IRepository<T>` /
  `IRepositoryFactory` compiles only with `CS0618` (obsolete) warnings suppressed — the source files here suppress
  it locally (`#pragma warning disable CS0618`) around each obsolete type; consumers outside this repo will see the
  warning (and it becomes a build error under `TreatWarningsAsErrors`, DKNet's own solution-wide setting).
  Suppress deliberately at the call site, don't disable the warning class-wide.
- **Never NuGet-installable.** There is no `DKNet.EfCore.Repos` package on nuget.org — only a project/source
  reference works, and only within (or forked from) this repository.
- `Repository<TEntity>.Query()` is **tracked**, while `ReadRepository<TEntity>.Query()` is **not** — mixing the two
  through `IReadRepository<T>` vs `IRepository<T>` injections in the same unit of work is an easy source of
  "why isn't my mutation being saved" or "why is this untracked entity throwing on `SaveChanges`" bugs.
  `DKNet.EfCore.Specifications`'s `IRepositorySpec` does not have this split — same call surface for both.
- `Query<TModel>()` on either class throws `InvalidOperationException` at call time (not at DI composition time) if
  no `IMapper` is registered — a startup validation for this is *not* provided.
- `RepositoryFactory<TDbContext>` owns and disposes its own `DbContext` — repositories it creates become unusable
  once the factory is disposed; don't let a repository outlive its factory.
- `RepoExtensions`' specification-application logic (`ApplySpecs`, `EnsureSpecHasOrdering`, `ToPageEnumerable`) is a
  **local copy** of internal logic in `DKNet.EfCore.Specifications` — it is not guaranteed to track future changes
  to that package's internal behavior, since this package is retired and receives maintenance only incidentally.

## 🔗 Related packages

- [DKNet.EfCore.Specifications](./DKNet.EfCore.Specifications.md) – the active replacement. Reach for
  `IRepositorySpec` + `Specification<TEntity>` and `SpecSetup.AddSpecRepo` for all new work.
- [Migrating-Repos-To-Specifications](./Migrating-Repos-To-Specifications.md) – the per-call mapping off this package.
  Reach for it when moving existing code.
- [DKNet.EfCore.Repos.Abstractions](./DKNet.EfCore.Repos.Abstractions.md) – the retired interfaces every type here
  implements. Reach for it to read the contracts rather than the implementations.
- [DKNet.EfCore.Extensions](./DKNet.EfCore.Extensions.md) – supplies the navigation-scanning and
  concurrency-handling extension methods `WriteRepository<T>.SaveChangesAsync` calls. Still current.
- [DKNet.EfCore.Abstractions](./DKNet.EfCore.Abstractions.md) – the `Entity`/`AuditedEntity` types these repositories
  were used against. Still current.
