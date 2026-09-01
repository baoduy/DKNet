# DKNet.EfCore.DataAuthorization

Row-level, ownership-based data authorization for EF Core: an automatic global query filter plus a `SaveChanges`
hook, so multi-tenant or per-user data isolation is enforced by the persistence layer instead of by convention in
every query.

## Install

```bash
dotnet add package DKNet.EfCore.DataAuthorization
```

## Features

- **`IOwnedBy`** — marker interface entities implement to opt into ownership-based filtering and stamping.
- **Automatic global query filter** — every `IOwnedBy` entity is scoped to `IDataOwnerDbContext.AccessibleKeys`
  automatically; deny-by-default when empty, with an explicit `IsUnrestrictedAccess` escape hatch for admin/system
  contexts. Not bypassable via specification `IsIgnoreQueryFilters`.
- **Automatic ownership stamping** — a `SaveChanges` hook stamps the current owner key onto newly added `IOwnedBy`
  entities (and `CreatedBy`/`CreatedOn` when the entity is also audited), and reverts any attempt to silently
  reassign an existing row's owner to a key the caller can't access.
- **One DI call to wire it up** — `AddDataOwnerProvider<TDbContext, TProvider>()` registers the query filter, the
  hook, and your `IDataOwnerProvider` implementation together. `TDbContext` must implement `IDataOwnerDbContext`;
  the requirement is compile-enforced by the method's generic constraint.

## Quick start

```csharp
public class Invoice : IOwnedBy
{
    public string OwnedBy { get; private set; } = string.Empty;
}

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IDataOwnerDbContext
{
    public IEnumerable<string> AccessibleKeys { get; init; } = [];

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.UseAutoConfigModel(); // required for the global query filter to apply
}

public sealed class TenantOwnerProvider(ICurrentTenant currentTenant) : IDataOwnerProvider
{
    public string? GetOwnershipKey() => currentTenant.TenantId;
}

services
    .AddDataOwnerProvider<AppDbContext, TenantOwnerProvider>()
    .AddDbContextWithHook<AppDbContext>(options => options.UseSqlServer(connectionString));
```

`AddDataOwnerProvider<TDbContext, TProvider>()` is declared `where TDbContext : DbContext, IDataOwnerDbContext`, so a
`DbContext` without the interface does not compile — and a context that reaches the query filter without it throws
`InvalidOperationException` at model-build time rather than quietly applying no ownership filter.

Full documentation, configuration options, and gotchas:
[DKNet.EfCore.DataAuthorization docs](https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/DKNet.EfCore.DataAuthorization.md)
