# DKNet.EfCore.Repos.Abstractions

> **Retired / obsolete — source-only, not published to NuGet.** `IsPackable` is `false` in this
> project's `.csproj`; it is kept in the source tree only so the sibling `DKNet.EfCore.Repos` project
> still builds. Every interface here (`IReadRepository<TEntity>`, `IWriteRepository<TEntity>`,
> `IRepository<TEntity>`, `IRepositoryFactory`) is marked `[Obsolete]`:
>
> > DKNet.EfCore.Repos.Abstractions is retired. Use DKNet.EfCore.Specifications (IRepositorySpec +
> > SpecSetup) instead. See docs/EfCore/Migrating-Repos-To-Specifications.md.
>
> **Use `IRepositorySpec` from `DKNet.EfCore.Specifications` in new code.**

## What it was

Repository-pattern interface contracts for EF Core, splitting read and write operations for
Domain-Driven Design / Onion Architecture applications:

- `IReadRepository<TEntity>` — `CountAsync`, `ExistsAsync`, `FindAsync` (by key or filter), `Query`
  (queryable and projected)
- `IWriteRepository<TEntity>` — `AddAsync`/`AddRangeAsync`, `Delete`/`DeleteRange`, `UpdateAsync`/
  `UpdateRangeAsync`, `SaveChangesAsync`, `BeginTransactionAsync`, `Entry`
- `IRepository<TEntity>` — combines both of the above
- `IRepositoryFactory` — creates `IRepository<T>` / `IReadRepository<T>` / `IWriteRepository<T>` for an
  entity type at runtime; itself `IDisposable` + `IAsyncDisposable`

## Quick reference (retired API — do not use in new code)

```csharp
#pragma warning disable CS0618 // retired API
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

## Full documentation

<https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/DKNet.EfCore.Repos.Abstractions.md>

## Migrating

See <https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/Migrating-Repos-To-Specifications.md> for the
call-site mapping to `DKNet.EfCore.Specifications`' `IRepositorySpec`.
