# DKNet.EfCore.DataAuthorization

Row-level, ownership-based data authorization for EF Core: automatic global query filtering plus SaveChanges-time
ownership stamping, so multi-tenant or per-user data isolation doesn't have to be repeated in every query.

## 1. Problem it solves / when to reach for it

Any application where rows belong to a principal — a tenant, a user, a branch, a department — has to answer the
same question at every read: "does the caller own this row, or is it in the set of rows the caller may see?" Doing
that by hand means every `Where(...)` clause across the codebase has to remember to add the ownership predicate, and
every insert has to remember to stamp the owner. Miss one query and you leak another tenant's data; miss one insert
and the row becomes unowned/orphaned.

`DKNet.EfCore.DataAuthorization` centralizes both halves of that problem as infrastructure:

- **Reads** — an EF Core global query filter is applied once, at model-build time, to every entity that opts in
  (`IOwnedBy`). No call site needs to add or remember a `Where` clause.
- **Writes** — a `SaveChanges` hook stamps the current owner onto newly added entities, and reverts any attempt to
  silently move an existing row to an owner the current caller isn't allowed to write as.

Reach for this package when you have row-level or multi-tenant ownership rules to enforce and you want them applied
uniformly by the persistence layer rather than by convention in application code.

## 2. Install and minimum wiring

```bash
dotnet add package DKNet.EfCore.DataAuthorization
```

The package brings in `DKNet.EfCore.Extensions` (global query filter plumbing) and `DKNet.EfCore.Hooks`
(`SaveChanges` pipeline) as project/package references — you don't add those separately for this feature.

Minimum wiring, from the real signatures in `EfCoreDataAuthSetup`, `DataOwnerAuthQuery`, and `SetupEfCoreHook`:

```csharp
// 1. Entity opts into ownership
public class Invoice : IOwnedBy
{
    public string OwnedBy { get; private set; } = string.Empty;
    // ... other members
}

// 2. DbContext exposes the current caller's access
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IDataOwnerDbContext
{
    public IEnumerable<string> AccessibleKeys { get; init; } = [];
    // IsUnrestrictedAccess defaults to false via the interface — override only for admin/system contexts.

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.UseAutoConfigModel(); // required — see "How it composes" below
}

// 3. Provider supplies the current owner key and the caller's accessible keys
public sealed class TenantOwnerProvider(ICurrentTenant currentTenant) : IDataOwnerProvider
{
    public string? GetOwnershipKey() => currentTenant.TenantId;
    // Default GetAccessibleKeys() wraps GetOwnershipKey() into a single-key collection;
    // override it if a caller may see more than one key (see section 3).
}

// 4. Registration
services
    .AddDataOwnerProvider<AppDbContext, TenantOwnerProvider>()
    .AddDbContextWithHook<AppDbContext>(options => options.UseSqlServer(connectionString));
```

`AddDataOwnerProvider<TDbContext, TProvider>()` is an `extension(IServiceCollection)` member on
`EfCoreDataAuthSetup` (C# 14 extension members). It:

1. Registers `DataOwnerAuthQuery` as a global model builder (`AddGlobalModelBuilder<DataOwnerAuthQuery>()`) — a
   no-op if it's already registered (checked via `IsRegistered<IDataOwnerProvider>()`).
2. Registers `TProvider` as scoped `IDataOwnerProvider`.
3. Registers `DataOwnerHook` as a keyed hook for `TDbContext` (`AddHook<TDbContext, DataOwnerHook>()`).

Two things it does **not** do for you, because they belong to the packages it builds on:

- Call `UseAutoConfigModel(...)` on your `DbContextOptionsBuilder` / inside `OnModelCreating` — without it the
  global query filter is never applied to the model (see section 5 and the gotchas below).
- Wire `UseHooks<TDbContext>(provider)` into your `DbContextOptionsBuilder` — `AddDbContextWithHook<TDbContext>`
  (from `DKNet.EfCore.Hooks`) does this for you; if you build the `DbContext` another way, add
  `options.UseHooks<TDbContext>(provider)` yourself or `DataOwnerHook` is registered in DI but never runs.

## 3. Features

### 3.1 `IOwnedBy` — ownership marker

```csharp
public interface IOwnedBy
{
    string OwnedBy { get; }
}
```

Implement this on any entity that should be subject to ownership filtering and stamping. Only entities that
implement `IOwnedBy` are touched by the filter or the hook — everything else in the model is unaffected. The
getter-only shape signals intent: consumers should mutate `OwnedBy` through a domain method or a private setter, not
assign it arbitrarily (the hook and its reassignment guard, section 3.3, assume that discipline).

### 3.2 Automatic global query filter (`DataOwnerAuthQuery`)

Registering the provider (section 2) applies a global EF Core query filter to every entity type in the model that
implements `IOwnedBy` (excluding TPH-discriminated subtypes — `GetDiscriminatorValue() == null` — since EF Core
already applies a base type's filter down the hierarchy). The filter, evaluated per query against your
`IDataOwnerDbContext`:

```csharp
x => capturedContext.IsUnrestrictedAccess
     || capturedContext.AccessibleKeys.Contains(((IOwnedBy)x).OwnedBy);
```

What this buys you: no repository, handler, or LINQ query anywhere in the app needs a `Where(x => x.OwnedBy == ...)`
— every `DbSet<T>` query against an `IOwnedBy` entity is scoped automatically, translated to a SQL `IN` clause (see
the gotcha in section 6 about why this only works because `AccessibleKeys` is `IEnumerable<string>`).

Key behaviors, verified from `DataOwnerAuthQuery`:

- **Deny-by-default.** An empty `AccessibleKeys` collection means the filter matches nothing — the caller sees zero
  owned rows. Empty must never be read as "unrestricted"; that has to be requested explicitly (next point).
- **Explicit unrestricted-access escape hatch.** `IDataOwnerDbContext.IsUnrestrictedAccess` (default `false`) is the
  only way to bypass the filter entirely. Set it to `true` only on system/admin contexts.
- **Not ignorable.** `DataOwnerAuthQuery.IsIgnorable => false`. If your app uses `DKNet.EfCore.Specifications`,
  a specification's `IsIgnoreQueryFilters` flag — which can bypass "ignorable" filters such as soft-delete — has no
  effect on this one. Row-level ownership isolation cannot be turned off from a query-time flag.
- **Per-query evaluation.** The filter closes over the `DbContext` instance (`capturedContext`), so
  `AccessibleKeys`/`IsUnrestrictedAccess` are read fresh on every query, not fixed once at model-build time — a
  scoped `IDataOwnerDbContext` implementation naturally gets per-request/per-scope values.

### 3.3 Ownership stamping and reassignment guard (`DataOwnerHook`)

`DataOwnerHook` implements `IBeforeSaveHookAsync` and runs inside the `SaveChanges` pipeline for every registered
`TDbContext` (see section 5 for how the hook actually gets invoked). For every tracked entity it:

- **On `Added` entities** (when `IDataOwnerProvider.GetOwnershipKey()` returns a non-empty key):
  - Sets `IOwnedBy.OwnedBy` to the current owner key — but only if it's not already set. This is idempotent: an
    entity created with `OwnedBy` already assigned (e.g. by a domain factory method) is left alone.
  - If the entity also implements `DKNet.EfCore.Abstractions.Entities.IAuditedProperties`, stamps `CreatedBy` (to
    the same owner key) and `CreatedOn` (`DateTimeOffset.UtcNow`) when `CreatedBy` is blank — tying ownership
    stamping to the audited-entity convention used elsewhere in DKNet.
  - Property values are set by walking up the type hierarchy for a writable accessor (handles private setters
    declared on a base class, e.g. `AuditedEntity<TKey>`), so this works with the "private setter + intention-
    revealing method" entity style the rest of the framework uses.
- **On `Modified` entities**, guards against silent ownership reassignment: if `OwnedBy` changed from its original
  tracked value and the new value is **not** one of the current context's `GetAccessibleKeys()`, the hook reverts
  `OwnedBy` back to its original value before save. This stops a row from being transferred to another tenant/owner
  or orphaned by an errant assignment, without throwing — the save proceeds with ownership unchanged.

This saves you from writing that stamping/guard logic in every aggregate's constructor or every command handler —
it happens once, uniformly, for anything that implements `IOwnedBy`.

### 3.4 `IDataOwnerProvider.GetAccessibleKeys()` default

```csharp
public ICollection<string> GetAccessibleKeys()
{
    var key = GetOwnershipKey();
    return string.IsNullOrEmpty(key) ? [] : [key];
}
```

Most providers only need to implement `GetOwnershipKey()` (the key stamped on new rows) — the default
`GetAccessibleKeys()` wraps it into a single-key collection, which is also what `DataOwnerHook` uses for the
reassignment guard. Override `GetAccessibleKeys()` directly when a caller can legitimately see/write more than one
key — e.g. a head-office user who spans several branch keys.

Note this is a different member than `IDataOwnerDbContext.AccessibleKeys` (section 3.2): the provider supplies the
data used by both the DbContext's `AccessibleKeys` property (your implementation typically just forwards
`_provider.GetAccessibleKeys()`) and the hook's reassignment guard — the DbContext is what the query filter reads.

## 4. Configuration options and defaults

There is no `appsettings.json`-driven configuration — everything is expressed through the three interfaces you
implement:

| Setting | Where | Default | Effect |
|---|---|---|---|
| `IDataOwnerDbContext.IsUnrestrictedAccess` | your `DbContext` | `false` (interface default) | `false`: rows restricted to `AccessibleKeys`. `true`: query filter bypassed entirely for this context. |
| `IDataOwnerDbContext.AccessibleKeys` | your `DbContext` | you supply it | Empty collection ⇒ deny all `IOwnedBy` rows (not "allow all"). |
| `IDataOwnerProvider.GetAccessibleKeys()` | your provider | wraps `GetOwnershipKey()` into one key, or `[]` | Override for multi-key callers. |
| `IDataOwnerProvider.GetOwnershipKey()` | your provider | required, no default | Owner key stamped on new `IOwnedBy` entities; blank/null ⇒ hook skips stamping. |
| `DataOwnerAuthQuery.FilterKey` | fixed | `nameof(DataOwnerAuthQuery)` | Named EF Core 10 query filter key; used internally, not configurable. |
| `DataOwnerAuthQuery.IsIgnorable` | fixed | `false` | Cannot be bypassed via `ISpecification.IsIgnoreQueryFilters`. |

## 5. How it composes with other DKNet packages

This package is a consumer of two other EF Core building blocks, not a standalone interceptor:

- **`DKNet.EfCore.Extensions` — global query filter plumbing.** `DataOwnerAuthQuery` derives from
  `DKNet.EfCore.Extensions.Configurations.GlobalQueryFilter` and is registered via
  `services.AddGlobalModelBuilder<DataOwnerAuthQuery>()`. That registration only takes effect once your
  `DbContext` calls `modelBuilder`/`optionsBuilder.UseAutoConfigModel(...)` — `AutoConfigModelCustomizer` is what
  invokes `RegisterGlobalModelBuilders`, which instantiates every registered `IGlobalModelBuilder` (including
  `DataOwnerAuthQuery`) and calls `Apply(modelBuilder, dbContext)` for each `IOwnedBy` entity type in your model.
  Skip `UseAutoConfigModel` and the filter is simply never applied — see the gotcha below.
- **`DKNet.EfCore.Hooks` — the `SaveChanges` pipeline.** `DataOwnerHook` implements `IBeforeSaveHookAsync` from
  `DKNet.EfCore.Hooks` and is registered as a keyed hook via `services.AddHook<TDbContext, DataOwnerHook>()`. It
  only actually runs if `HookRunnerInterceptor` is attached to the `DbContext`'s options
  (`options.UseHooks<TDbContext>(provider)`), which `AddDbContextWithHook<TDbContext>(...)` does for you. If you
  configure the `DbContext` with plain `AddDbContext` instead, add `options.UseHooks<TDbContext>(provider)`
  yourself inside the options delegate, or the hook is registered in DI but never invoked.
- **`DKNet.EfCore.Abstractions` entities.** The hook special-cases entities that also implement
  `IAuditedProperties` (from `DKNet.EfCore.Abstractions.Entities`) to stamp `CreatedBy`/`CreatedOn` alongside
  `OwnedBy`, so ownership and audit stamping stay consistent for entities that use both conventions.
- **`DKNet.EfCore.Specifications`** (if used in the same app): its `IsIgnoreQueryFilters` flag can bypass
  "ignorable" global filters, but `DataOwnerAuthQuery.IsIgnorable => false` means row-level ownership isolation
  is exempt from that bypass by design.

## 6. Gotchas and limits

- **`IDataOwnerDbContext.AccessibleKeys` must be `IEnumerable<string>`, never `ICollection<string>`.** EF Core's
  query-filter translator can turn `Enumerable.Contains` over an `IEnumerable<string>` into a SQL `IN (...)`
  clause, but it cannot translate `ICollection<string>.Contains` inside a query filter — it throws at query time
  with `Translation of method 'System.Linq.Enumerable.Contains' failed`. This is exactly why the interface is
  declared as `IEnumerable<string>` today; if you implement `IDataOwnerDbContext` yourself, expose `AccessibleKeys`
  as `IEnumerable<string>` (backing it with a `List<string>` or `string[]` is fine — just don't widen the
  property's declared type back to `ICollection<string>`, which silently reintroduces the untranslatable query).
- **Empty `AccessibleKeys` denies access — it does not mean "unrestricted".** Older revisions of this package used
  `!AccessibleKeys.Any() || AccessibleKeys.Contains(...)`, i.e. an empty collection meant "no restriction, see
  everything." The current filter is deny-by-default: an empty collection matches nothing, and the only way to see
  everything is the explicit `IsUnrestrictedAccess` opt-in. If you're upgrading from that older behavior, audit any
  context that relied on "empty keys ⇒ full access" — it will now return zero rows instead.
- **Forgetting `UseAutoConfigModel` disables filtering silently.** The query filter is only attached to the model
  when the DbContext calls `UseAutoConfigModel(...)`. There is no runtime error if you skip it — `IOwnedBy` entities
  simply have no query filter and every query returns all owners' rows.
- **A `DbContext` that doesn't implement `IDataOwnerDbContext` fails open in Release builds.** `HasQueryFilter`
  guards with `Debug.Fail(...)` when `context is not IDataOwnerDbContext`, then returns `null` (no filter). `Debug.Fail`
  is compiled out in Release builds, so in Release this path is a silent no-op: the entity type ends up with **no**
  ownership filter at all, and every caller sees every row. Always implement `IDataOwnerDbContext` on the exact
  `DbContext` type you register with `AddDataOwnerProvider<TDbContext, TProvider>()`.
- **Forgetting `UseHooks<TDbContext>` means new rows are never stamped.** `AddHook<TDbContext, DataOwnerHook>()`
  registers the hook in DI, but `HookRunnerInterceptor` only invokes it if the `DbContext`'s options include
  `UseHooks<TDbContext>(provider)` — use `AddDbContextWithHook<TDbContext>(...)` or add that call yourself.
  Symptom: new `IOwnedBy` rows are saved with a blank `OwnedBy`, and the deny-by-default filter then hides them
  from everyone, including their creator.
- **The reassignment guard only sees changes EF Core's `ChangeTracker` sees.** `GuardOwnedByReassignment` inspects a
  tracked `Modified` entry's original vs. current `OwnedBy` value. Raw SQL, bulk-update libraries, or any write path
  that bypasses `SaveChanges`/the `ChangeTracker` is not covered — the guard is not a database-level constraint.
- **TPH (table-per-hierarchy) entities:** the filter is only registered for entity types where
  `GetDiscriminatorValue() == null`. If `IOwnedBy` is implemented on a base type in a TPH hierarchy, the filter is
  registered once for the root type; EF Core applies a base type's query filter to derived types in the same
  hierarchy automatically, so this is expected behavior, not a limitation — but a derived type that implements
  `IOwnedBy` independently of its base (uncommon) will not get its own filter registration.
