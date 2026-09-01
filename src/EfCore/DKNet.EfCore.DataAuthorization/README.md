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
}

public sealed class TenantOwnerProvider(ICurrentTenant currentTenant) : IDataOwnerProvider
{
    public string? GetOwnershipKey() => currentTenant.TenantId;
}

services
    .AddDataOwnerProvider<AppDbContext, TenantOwnerProvider>()
    .AddDbContextWithHook<AppDbContext>(options =>
        options.UseSqlServer(connectionString)
               .UseAutoConfigModel<AppDbContext>()); // required for the global query filter to apply
```

`AddDataOwnerProvider<TDbContext, TProvider>()` is declared `where TDbContext : DbContext, IDataOwnerDbContext`, so a
`DbContext` without the interface does not compile — and a context that reaches the query filter without it throws
`InvalidOperationException` at model-build time rather than quietly applying no ownership filter.

## Customisation reference

There is no options class and nothing bound from `appsettings.json` — the whole surface is the three interfaces
you implement plus one DI call.

| Knob | Where | Default | Effect |
|---|---|---|---|
| `AddDataOwnerProvider<TDbContext, TProvider>()` | `IServiceCollection` | not registered | Registers `DataOwnerAuthQuery` as a global model builder, `TProvider` as a scoped `IDataOwnerProvider`, and `DataOwnerHook` as a keyed hook for `TDbContext`. Constrained to `DbContext, IDataOwnerDbContext`. |
| `IDataOwnerDbContext.AccessibleKeys` | your `DbContext` | you supply it | The keys the caller may read. **Empty denies every owned row** — it never means "all". |
| `IDataOwnerDbContext.IsUnrestrictedAccess` | your `DbContext` | `false` (interface default) | `true` bypasses the filter completely for that context. The only escape hatch. |
| `IDataOwnerProvider.GetOwnershipKey()` | your provider | required, no default | The owner key stamped on new `IOwnedBy` rows. Null or blank means the hook stamps nothing. |
| `IDataOwnerProvider.GetAccessibleKeys()` | your provider | wraps `GetOwnershipKey()` into a single-key collection, or empty | The keys ownership may be reassigned to. Override for callers that span several owners. |
| `DataOwnerAuthQuery.FilterKey` | fixed | `nameof(DataOwnerAuthQuery)` | Named EF Core query-filter key; not configurable. |
| `DataOwnerAuthQuery.IsIgnorable` | fixed | `false` | A specification's `IgnoreQueryFilters()` can never bypass ownership isolation. |

The hook stamps `OwnedBy` (and `CreatedBy`/`CreatedOn` on audited entities) only when they are still blank, keeps
an explicit `SetUpdatedBy` from being overwritten, and reverts an `OwnedBy` change that targets a key outside
`GetAccessibleKeys()`.

Full documentation, configuration options, and gotchas:
[DKNet.EfCore.DataAuthorization docs](https://github.com/baoduy/DKNet/blob/main/docs/EfCore/DKNet.EfCore.DataAuthorization.md)
