# DKNet.EfCore.Repos.Abstractions

Retired repository-pattern contracts (`IReadRepository<T>`, `IWriteRepository<T>`, `IRepository<T>`,
`IRepositoryFactory`) kept in source only, for readers maintaining code that still references them.

> **Retired / obsolete — source-only, not published to NuGet.**
> `DKNet.EfCore.Repos.Abstractions.csproj` sets `<IsPackable>false</IsPackable>`. It is no longer packed
> or advertised as a NuGet package; it stays in the source tree only so the sibling `DKNet.EfCore.Repos`
> project (also unpublished) still builds. Every public interface in this package carries this exact
> `[Obsolete]` message:
>
> ```
> DKNet.EfCore.Repos.Abstractions is retired. Use DKNet.EfCore.Specifications (IRepositorySpec + SpecSetup)
> instead. See docs/EfCore/Migrating-Repos-To-Specifications.md.
> ```
>
> **New code must use `IRepositorySpec` from [`DKNet.EfCore.Specifications`](./DKNet.EfCore.Specifications.md)
> instead.** Existing consumers should follow
> [`docs/EfCore/Migrating-Repos-To-Specifications.md`](./Migrating-Repos-To-Specifications.md) to move off
> these interfaces.

## ✨ Why use it?

Before `DKNet.EfCore.Specifications` existed, this package supplied the repository-pattern *contracts* used
throughout the DKNet framework to keep domain/application code independent of EF Core:

- **CQRS-style split** — `IReadRepository<TEntity>` for queries/projections, `IWriteRepository<TEntity>`
  for mutations and transactions, `IRepository<TEntity>` when a consumer genuinely needs both.
- **Persistence ignorance** — application services depended on these interfaces, not on `DbContext`
  or `DbSet<TEntity>` directly, so they could be unit-tested with a mock/fake repository.
- **A factory seam** — `IRepositoryFactory` let code obtain a repository for an arbitrary entity type
  at runtime without injecting one `IRepository<T>` per entity.

`DKNet.EfCore.Specifications`' `IRepositorySpec` now covers the same read/write/paging surface as
`IRepository<TEntity>`, but composes directly with `Specification<TEntity>` objects (filter + include +
order-by in one reusable unit) instead of loose `Expression<Func<TEntity, bool>>` filters, and is not
generic over the entity — one injected `IRepositorySpec` instance serves every aggregate in the
`DbContext`. That is a strictly larger feature set, which is why this package is retired rather than
extended.

## 🚀 Quick Start

Not applicable — this package is **not published to NuGet**. It exists only as project source
(`src/EfCore/DKNet.EfCore.Repos.Abstractions`), consumed via `ProjectReference` by
`DKNet.EfCore.Repos` inside this solution. Do not add a `PackageReference` to it; there is no package
to restore.

For new work, install the active replacement instead:

```bash
dotnet add package DKNet.EfCore.Specifications
```

## 🧩 Features

All four types below are decorated with `[Obsolete("DKNet.EfCore.Repos.Abstractions is retired. Use
DKNet.EfCore.Specifications (IRepositorySpec + SpecSetup) instead. See
docs/EfCore/Migrating-Repos-To-Specifications.md.")]`. Referencing any of them from non-obsolete code
produces a compiler warning (which is an error under this repo's `TreatWarningsAsErrors=true`).

### `IReadRepository<TEntity>` — queries and projections

```csharp
public interface IReadRepository<TEntity> where TEntity : class
{
    Task<int> CountAsync(Expression<Func<TEntity, bool>> filter, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> filter, CancellationToken cancellationToken = default);
    ValueTask<TEntity?> FindAsync(object keyValue, CancellationToken cancellationToken = default);
    ValueTask<TEntity?> FindAsync(object[] keyValues, CancellationToken cancellationToken = default);
    Task<TEntity?> FindAsync(Expression<Func<TEntity, bool>> filter, CancellationToken cancellationToken = default);
    IQueryable<TEntity> Query(Expression<Func<TEntity, bool>> filter);
    IQueryable<TEntity> Query();
    IQueryable<TModel> Query<TModel>(Expression<Func<TEntity, bool>> filter) where TModel : class;
}
```

Note the doc comment on the parameterless `Query()`: tracking behavior is **implementation-defined** —
a `ReadRepository<TEntity>` typically returns `AsNoTracking()`, while a `Repository<TEntity>` used for
read-then-update workflows may return a tracked queryable. Callers must not assume one tracking mode
from the interface alone.

```csharp
#pragma warning disable CS0618 // retired API, kept for illustration only
public sealed class ProductQueryService(IReadRepository<Product> repo)
{
    public Task<int> CountActiveAsync(CancellationToken ct) =>
        repo.CountAsync(p => p.IsActive, ct);

    public Task<Product?> FindByIdAsync(Guid id, CancellationToken ct) =>
        repo.FindAsync((object)id, ct).AsTask();

    public Task<Product?> FindBySkuAsync(string sku, CancellationToken ct) =>
        repo.FindAsync(p => p.Sku == sku, ct);

    public IQueryable<ProductSummary> ActiveSummaries() =>
        repo.Query<ProductSummary>(p => p.IsActive);
}
#pragma warning restore CS0618
```

### `IWriteRepository<TEntity>` — mutations and transactions

```csharp
public interface IWriteRepository<TEntity> where TEntity : class
{
    ValueTask AddAsync(TEntity entity, CancellationToken cancellationToken = default);
    ValueTask AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    void Delete(TEntity entity);
    void DeleteRange(IEnumerable<TEntity> entities);
    EntityEntry<TEntity> Entry(TEntity entity);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<int> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);
    Task UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);
}
```

```csharp
#pragma warning disable CS0618
public sealed class ProductWriteService(IWriteRepository<Product> repo)
{
    public async Task CreateAsync(Product product, CancellationToken ct)
    {
        await repo.AddAsync(product, ct);
        await repo.SaveChangesAsync(ct);
    }

    public async Task DeactivateAsync(Product product, CancellationToken ct)
    {
        await using var tx = await repo.BeginTransactionAsync(ct);
        product.Deactivate();
        await repo.UpdateAsync(product, ct);
        await repo.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
#pragma warning restore CS0618
```

`Delete`/`DeleteRange` are synchronous (they mark tracked entities for removal; the actual delete happens
on `SaveChangesAsync`). `AddAsync`/`AddRangeAsync` return `ValueTask`, not `Task`.

### `IRepository<TEntity>` — the combined surface

```csharp
public interface IRepository<TEntity> : IReadRepository<TEntity>, IWriteRepository<TEntity>
    where TEntity : class;
```

A pure marker/union interface — no members of its own, just the combined read + write surface for
consumers that need both:

```csharp
#pragma warning disable CS0618
public sealed class ProductService(IRepository<Product> repo)
{
    public async Task<Product?> RenameAsync(Guid id, string newName, CancellationToken ct)
    {
        var product = await repo.FindAsync((object)id, ct);
        if (product is null) return null;

        product.Rename(newName);
        await repo.UpdateAsync(product, ct);
        await repo.SaveChangesAsync(ct);
        return product;
    }
}
#pragma warning restore CS0618
```

### `IRepositoryFactory` — a repository per entity type at runtime

```csharp
public interface IRepositoryFactory : IDisposable, IAsyncDisposable
{
    IRepository<TEntity> Create<TEntity>() where TEntity : class;
    IReadRepository<TEntity> CreateRead<TEntity>() where TEntity : class;
    IWriteRepository<TEntity> CreateWrite<TEntity>() where TEntity : class;
}
```

```csharp
#pragma warning disable CS0618
public sealed class BulkImportService(IRepositoryFactory factory)
{
    public async Task ImportAsync<TEntity>(IEnumerable<TEntity> rows, CancellationToken ct)
        where TEntity : class
    {
        var repo = factory.CreateWrite<TEntity>();
        await repo.AddRangeAsync(rows, ct);
        await repo.SaveChangesAsync(ct);
    }
}
#pragma warning restore CS0618
```

`IRepositoryFactory` implements both `IDisposable` and `IAsyncDisposable`, so callers obtained from DI
should dispose it (or let the DI container scope do so) rather than holding it indefinitely.

## 🧱 Where it fits

- **Implemented by [`DKNet.EfCore.Repos`](./DKNet.EfCore.Repos.md)** — the concrete
  `ReadRepository<TEntity>`/`Repository<TEntity>`/`RepositoryFactory` classes that back these interfaces
  against a real `DbContext`. That package is retired for the same reason and carries the same
  `[Obsolete]` markers.
- **Superseded by [`DKNet.EfCore.Specifications`](./DKNet.EfCore.Specifications.md)** — `IRepositorySpec`
  replaces `IRepository<TEntity>` end-to-end (see the call-site mapping table in the migration guide),
  registered via `services.AddSpecRepo<TDbContext>()` instead of `AddGenericRepositories<TDbContext>()` /
  `AddRepoFactory<TDbContext>()`.
- **Entities** consumed through these interfaces are the `Entity`/`AuditedEntity` types defined in
  `DKNet.EfCore.Abstractions`; persistence of domain events raised on those aggregates during
  `SaveChangesAsync` is handled separately by `DKNet.EfCore.Events` / SaveChanges hooks — this package
  has no involvement in event dispatch itself.

## ⚠️ Gotchas & limits

- **There is nothing to configure.** This package is pure interface definitions — no options classes, no
  `AddXxx(...)` DI extension methods, no configurable defaults. Anything you might expect to configure
  (tracking mode, transaction scope) is decided entirely by the concrete implementation in
  `DKNet.EfCore.Repos`.
- **It is obsolete and unpublished.** Do not add a `PackageReference` to `DKNet.EfCore.Repos.Abstractions`
  in new projects — there is no NuGet package to reference. Any remaining internal usage inside this
  solution compiles only because of scattered `#pragma warning disable CS0618` markers around the retired
  call sites.
- **Migrate, don't extend.** Follow
  [`docs/EfCore/Migrating-Repos-To-Specifications.md`](./Migrating-Repos-To-Specifications.md) for the
  registration change (`AddSpecRepo<TDbContext>()`) and the per-call mapping from `IRepository<T>`
  members to `IRepositorySpec` + `Specification<T>` equivalents. See
  [`docs/EfCore/DKNet.EfCore.Specifications.md`](./DKNet.EfCore.Specifications.md) for the replacement
  API in full.
- **`Query()` tracking behavior is not guaranteed by the interface** — it depends on which concrete
  implementation is injected (see the note under `IReadRepository<TEntity>` above). Don't assume
  `AsNoTracking()` just because you're holding an `IReadRepository<TEntity>`.
- **`IRepositoryFactory` is disposable** (`IDisposable` + `IAsyncDisposable`) — leaking instances outside
  a DI scope leaks whatever `DbContext`/connection resources the concrete factory holds.

## 🔗 Related packages

- [DKNet.EfCore.Specifications](./DKNet.EfCore.Specifications.md) – the active replacement. Reach for
  `IRepositorySpec` + `Specification<TEntity>` for all new work; it covers the same read/write/paging
  surface without being generic over the entity.
- [Migrating-Repos-To-Specifications](./Migrating-Repos-To-Specifications.md) – the per-call mapping from
  these interfaces to their `IRepositorySpec` equivalents. Reach for it when moving existing code off this
  package.
- [DKNet.EfCore.Repos](./DKNet.EfCore.Repos.md) – the concrete implementations of these contracts, retired
  for the same reason. Reach for it only to understand behaviour code you still maintain depends on.
- [DKNet.EfCore.Abstractions](./DKNet.EfCore.Abstractions.md) – the `Entity`/`AuditedEntity` types the
  retired interfaces were used against; still current.
