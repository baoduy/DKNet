# DKNet.EfCore.DataAuthorization

| Field | Value |
|---|---|
| Area | EfCore |
| NuGet | `dotnet add package DKNet.EfCore.DataAuthorization` |
| Docs | https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/DKNet.EfCore.DataAuthorization.md |
| Source | https://github.com/baoduy/DKNet/tree/dev/src/EfCore/DKNet.EfCore.DataAuthorization |
| Depends on (DKNet) | `DKNet.EfCore.Extensions`, `DKNet.EfCore.Hooks`, `DKNet.EfCore.AuditLogs` (pulled in transitively) |
| Depends on (3rd party) | none at runtime |
| Target framework | `net10.0` |

## Purpose

Row-level, ownership-based data authorization for EF Core: a global query filter that scopes every read of an
`IOwnedBy` entity to the caller's `AccessibleKeys`, plus a `SaveChanges` hook that stamps the owner key onto new
rows and reverts any attempt to silently reassign an existing row to an owner the caller cannot access. Both sides
read their keys from interfaces you implement (`IDataOwnerDbContext`, `IDataOwnerProvider`) — the package supplies
the mechanism, not the tenant/user model itself.

**Not** a general row-level-security or column-encryption feature (see `DKNet.EfCore.Encryption` for that), not a
replacement for audit-property change tracking (it only fills `CreatedBy`/`UpdatedBy` as a byproduct when no
signed-in-user provider is present), and not a database-level constraint — it only sees writes that go through EF
Core's `ChangeTracker`/`SaveChanges`; raw SQL or bulk-update libraries bypass it entirely.

## Entry points

| Call | Exact signature | Called on | Notes (lifetime, ordering, prerequisites) |
|---|---|---|---|
| `AddDataOwnerProvider<TDbContext, TProvider>()` | `public IServiceCollection AddDataOwnerProvider<TDbContext, TProvider>() where TDbContext : DbContext, IDataOwnerDbContext where TProvider : class, IDataOwnerProvider` (extension member on `IServiceCollection`, in `EfCoreDataAuthSetup`) | `IServiceCollection` | Registers `DataOwnerAuthQuery` as a global model builder, `TProvider` as **scoped** `IDataOwnerProvider` (first call wins — see Gotchas), and `DataOwnerHook` as a **keyed hook for `TDbContext`** via `AddHook<TDbContext, DataOwnerHook>()`. Does **not** call `UseAutoConfigModel` or `UseHooks` for you. |
| `UseAutoConfigModel<TContext>(params Assembly[]? assemblies)` | `public static DbContextOptionsBuilder<TContext> UseAutoConfigModel<TContext>(this DbContextOptionsBuilder<TContext> @this, params Assembly[]? assemblies) where TContext : DbContext` (from `DKNet.EfCore.Extensions`, ambient namespace `Microsoft.EntityFrameworkCore`) | `DbContextOptionsBuilder<TContext>` | **Required** for the global query filter to attach to the model — the auto-config model customizer invokes every registered global model builder, including `DataOwnerAuthQuery`. Skip it and the filter never applies (silent). |
| `AddDbContextWithHook<TDbContext>(...)` | overloads taking `Action<IServiceProvider, DbContextOptionsBuilder>` or `Action<DbContextOptionsBuilder<TDbContext>>`, plus `ServiceLifetime contextLifetime = Scoped, ServiceLifetime optionLifetime = Scoped` (from `DKNet.EfCore.Hooks`, `SetupEfCoreHook`) | `IServiceCollection` | Wires `options.UseHooks<TDbContext>(provider)` for you so `DataOwnerHook` actually runs. If you register the `DbContext` with plain `AddDbContext` instead, you must call `options.UseHooks<TDbContext>(provider)` yourself or the hook is registered in DI but never invoked. |
| `IOwnedBy` | `interface IOwnedBy { string OwnedBy { get; } }` | entity class | Marker interface; only entities that implement it are touched by the filter or the hook. |
| `IDataOwnerDbContext` | `interface IDataOwnerDbContext { IEnumerable<string> AccessibleKeys { get; } bool IsUnrestrictedAccess => false; }` | `DbContext` subclass | Mandatory on the `TDbContext` passed to `AddDataOwnerProvider` (compile-enforced generic constraint) and on any other `DbContext` in the same process whose model contains an `IOwnedBy` entity (see Gotchas — the filter registration is process-wide). |
| `IDataOwnerProvider` | `interface IDataOwnerProvider { ICollection<string> GetAccessibleKeys() /* default body */; string? GetOwnershipKey(); }` | your provider class | Register the implementing type as `TProvider` in `AddDataOwnerProvider`. |

## Public surface

### `DKNet.EfCore.DataAuthorization` (namespace)

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `IOwnedBy` | interface | Marker for entities subject to ownership filtering/stamping. | `string OwnedBy { get; }` (getter-only by design) |
| `IDataOwnerProvider` | interface | Supplies the current owner key and the caller's accessible keys. | `ICollection<string> GetAccessibleKeys()` (default: wraps `GetOwnershipKey()` into a single-element collection, or `[]` if blank); `string? GetOwnershipKey()` (no default — must implement) |
| `IDataOwnerDbContext` | interface | `DbContext` contract the query filter reads. | `IEnumerable<string> AccessibleKeys { get; }` (no default — must implement, and **must** stay `IEnumerable<string>`, never widen to `ICollection<string>`); `bool IsUnrestrictedAccess => false` (default provided) |
| `EfCoreDataAuthSetup` | static class (C# `extension` members) | DI wiring. | `IServiceCollection AddDataOwnerProvider<TProvider>()` (private, extension on `IServiceCollection`, dedup-guarded); `IServiceCollection AddDataOwnerProvider<TDbContext, TProvider>()` (public, the entry point) |

### Internals (not directly consumable, but explain observable behaviour)

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `DataOwnerAuthQuery` | `internal sealed class : GlobalQueryFilter` | Attaches the global query filter to every `IOwnedBy` entity type at model-build time. | `override string FilterKey => nameof(DataOwnerAuthQuery)`; `override bool IsIgnorable => false`; `GetEntityTypes` (every `IOwnedBy` type where the discriminator value is null — root types only); `HasQueryFilter<TEntity>(DbContext)` — throws `InvalidOperationException` if the context is not `IDataOwnerDbContext`; otherwise returns `x => capturedContext.IsUnrestrictedAccess \|\| capturedContext.AccessibleKeys.Contains(((IOwnedBy)x).OwnedBy)` |
| `DataOwnerHook` | `internal sealed class(IDataOwnerProvider, ICurrentUserProvider? = null) : IBeforeSaveHookAsync` | `SaveChanges`-time owner stamping and reassignment guard. | `Task BeforeSaveAsync(SnapshotContext, CancellationToken)`; private `UpdatingOwner`, `StampAddedEntity`, `GuardOwnedByReassignment` |

## Options & defaults

No options class and nothing bound from `appsettings.json` — the whole surface is the two interfaces you implement
plus one DI call.

| Option | Type | Default | Effect | Where set |
|---|---|---|---|---|
| `IDataOwnerDbContext.AccessibleKeys` | `IEnumerable<string>` | you supply it (no default; interface has no body) | Empty ⇒ deny every `IOwnedBy` row. Must stay `IEnumerable<string>` — EF Core cannot translate `ICollection<string>.Contains` in a query filter. | your `DbContext` |
| `IDataOwnerDbContext.IsUnrestrictedAccess` | `bool` | `false` (interface default) | `true` bypasses the filter entirely for that context — the only escape hatch. | your `DbContext` (override the default) |
| `IDataOwnerProvider.GetOwnershipKey()` | `string?` | no default, must implement | Key stamped on new `IOwnedBy` rows, and (absent a signed-in-user provider) on `CreatedBy`/`UpdatedBy`. Null/blank ⇒ hook stamps nothing on `Added`. | your provider |
| `IDataOwnerProvider.GetAccessibleKeys()` | `ICollection<string>` | wraps `GetOwnershipKey()` into a one-element collection, or `[]` if blank | Keys the reassignment guard treats as legal new owners; typically also what your `AccessibleKeys` property forwards. | your provider (override for multi-key callers) |
| `DataOwnerAuthQuery.FilterKey` | `string` | `nameof(DataOwnerAuthQuery)` (fixed) | Named EF Core query-filter key. | not configurable |
| `DataOwnerAuthQuery.IsIgnorable` | `bool` | `false` (fixed) | A specification's `IgnoreQueryFilters()` can never bypass this filter. | not configurable |

## Usage patterns

### Wiring ownership on a DbContext, provider, and entity

**When**: standard multi-tenant setup — every query and every insert against `IOwnedBy` entities should be scoped
to the caller automatically.

```csharp
using DKNet.EfCore.DataAuthorization;
using DKNet.EfCore.Hooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public class Invoice : IOwnedBy
{
    public Guid Id { get; private set; }
    public string OwnedBy { get; private set; } = string.Empty;

    public static Invoice Create(string ownerKey) => new() { Id = Guid.NewGuid(), OwnedBy = ownerKey };

    public void Reassign(string ownerKey) => OwnedBy = ownerKey;
}

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IDataOwnerDbContext
{
    public DbSet<Invoice> Invoices => Set<Invoice>();

    public IEnumerable<string> AccessibleKeys { get; init; } = [];
    // IsUnrestrictedAccess defaults to false via the interface.
}

public interface ICurrentTenant
{
    string? TenantId { get; }
}

public sealed class MyCurrentTenant : ICurrentTenant
{
    public string? TenantId => "tenant-a";
}

public sealed class TenantOwnerProvider(ICurrentTenant currentTenant) : IDataOwnerProvider
{
    public string? GetOwnershipKey() => currentTenant.TenantId;
    // Default GetAccessibleKeys() wraps GetOwnershipKey() into a single-key collection.
}

public static class Startup
{
    public static void ConfigureServices(IServiceCollection services, string connectionString)
    {
        services
            .AddScoped<ICurrentTenant, MyCurrentTenant>()
            .AddDataOwnerProvider<AppDbContext, TenantOwnerProvider>()
            .AddDbContextWithHook<AppDbContext>(options =>
                options.UseSqlServer(connectionString)
                       .UseAutoConfigModel<AppDbContext>());
    }
}
```

**Notes**: `UseAutoConfigModel<AppDbContext>()` is mandatory — without it `DataOwnerAuthQuery` is registered in DI
but never applied to the model, and every query returns every owner's rows with no runtime error. `AppDbContext`
must implement `IDataOwnerDbContext` on the exact type passed as `TDbContext`, or the call does not compile.

### Multi-key caller (head-office user spanning several branches)

**When**: a caller may legitimately read/write more than one owner key.

```csharp
using DKNet.EfCore.DataAuthorization;
using System.Linq;

public interface ICurrentUserBranches
{
    string? PrimaryBranchId { get; }
    IReadOnlyCollection<string> AllBranchIds { get; }
}

public sealed class HeadOfficeOwnerProvider(ICurrentUserBranches branches) : IDataOwnerProvider
{
    public string? GetOwnershipKey() => branches.PrimaryBranchId;

    public ICollection<string> GetAccessibleKeys() => branches.AllBranchIds.ToList();
}
```

**Notes**: `GetOwnershipKey()` still decides what gets stamped on a brand-new row; `GetAccessibleKeys()` decides
what the query filter (via your `IDataOwnerDbContext.AccessibleKeys`, which should forward this) and the
reassignment guard treat as legal.

### Admin/system context that must see every row

**When**: a background job, migration, or admin API that needs unrestricted read access.

```csharp
using DKNet.EfCore.DataAuthorization;
using Microsoft.EntityFrameworkCore;

public class AdminDbContext(DbContextOptions<AdminDbContext> options)
    : DbContext(options), IDataOwnerDbContext
{
    public IEnumerable<string> AccessibleKeys { get; init; } = [];
    public bool IsUnrestrictedAccess => true; // deliberate, greppable opt-in
}
```

**Notes**: this is the *only* supported bypass. An empty `AccessibleKeys` with `IsUnrestrictedAccess` left `false`
denies every row rather than allowing them — do not rely on "empty means all".

### Guarding against silent ownership reassignment

**When**: proving/relying on the built-in guard rather than re-implementing it in a command handler.

```csharp
public static class ReassignmentDemo
{
    public static async Task AttemptReassignAsync(AppDbContext db, Invoice entity)
    {
        // entity.OwnedBy was "tenant-a"; this tries to move it to a tenant the caller cannot access.
        entity.Reassign("tenant-b");
        await db.SaveChangesAsync();
        // entity.OwnedBy is back to "tenant-a" once persisted: DataOwnerHook reverted the change
        // before it reached the database. No exception is thrown.
    }
}
```

**Notes**: the guard only sees changes EF Core's `ChangeTracker` sees on a tracked `Modified` entry — raw SQL or a
bulk-update library bypassing `SaveChanges` is not covered. Reassigning to a key that **is** in
`GetAccessibleKeys()` persists normally (no guard fires).

### Auditing composes with a signed-in-user provider

**When**: `CreatedBy`/`UpdatedBy` should record the human, not the tenant key, while `OwnedBy` still records the
tenant.

```csharp
using DKNet.EfCore.AuditLogs;
using Microsoft.AspNetCore.Http;

public sealed class SignedInUserProvider(IHttpContextAccessor accessor) : ICurrentUserProvider
{
    public string? GetCurrentUser() => accessor.HttpContext?.User?.FindFirst("sub")?.Value;
}
```

```csharp
using DKNet.EfCore.AuditLogs;
using DKNet.EfCore.DataAuthorization;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddDataOwnerProvider<AppDbContext, TenantOwnerProvider>()
    .AddCurrentUserProvider<AppDbContext, SignedInUserProvider>();
```

**Notes**: when `GetCurrentUser()` returns a non-empty value for a save, `DataOwnerHook` stamps `OwnedBy` only and
`EfCoreAuditHook` stamps `CreatedBy`/`UpdatedBy` from that value. When no provider is registered, or one returns
null/empty, `DataOwnerHook` keeps filling the audit fields from the ownership key exactly as it always did — no
code change required for existing apps.

### Excluding a DbContext from the process-wide filter (audit/reporting side effects)

**When**: a second `DbContext` in the same process contains `IOwnedBy` entities but was never passed to
`AddDataOwnerProvider`.

```csharp
using DKNet.EfCore.DataAuthorization;
using Microsoft.EntityFrameworkCore;

// AppDbContext registered AddDataOwnerProvider<AppDbContext, TenantOwnerProvider>() elsewhere in the process.
// ReportingDbContext must ALSO implement IDataOwnerDbContext if its model contains IOwnedBy entities and it
// calls UseAutoConfigModel(), because DataOwnerAuthQuery was registered into a static, process-wide bag.
public class ReportingDbContext(DbContextOptions<ReportingDbContext> options)
    : DbContext(options), IDataOwnerDbContext
{
    public IEnumerable<string> AccessibleKeys { get; init; } = [];
    public bool IsUnrestrictedAccess => true; // a reporting context typically wants everything
}
```

**Notes**: see Gotchas — this is not optional once any `AddDataOwnerProvider` call has run in the process and the
second context's model has an `IOwnedBy` entity plus `UseAutoConfigModel()`.

## Runtime behaviour

**Model build** (`DbContext.OnModelCreating`, via `UseAutoConfigModel`):
1. The auto-config model customizer runs its global-model-builder registration step, instantiating every
   registered `IGlobalModelBuilder` — including `DataOwnerAuthQuery` if any `AddDataOwnerProvider` call has
   executed anywhere in the process.
2. `GlobalQueryFilter.Apply(modelBuilder, context)` calls `GetEntityTypes` (every entity type implementing
   `IOwnedBy` whose discriminator value is null) and, via reflection, `HasQueryFilter<TEntity>(context)` for each.
3. `HasQueryFilter` casts `context` to `IDataOwnerDbContext`; if that fails, it throws `InvalidOperationException`
   immediately (model build fails, the `DbContext` cannot be used at all). Otherwise it captures `context` in a
   closure and returns `x => capturedContext.IsUnrestrictedAccess || capturedContext.AccessibleKeys.Contains(((IOwnedBy)x).OwnedBy)`.
4. `modelBuilder.Entity<TEntity>().HasQueryFilter(FilterKey, filter)` attaches the named filter.

**Every query** against a `DbSet<T>` for an `IOwnedBy` `T`: EF Core evaluates the captured closure fresh — reading
`IsUnrestrictedAccess`/`AccessibleKeys` from the live, scoped `DbContext` instance — and, because `AccessibleKeys`
is `IEnumerable<string>`, expands `.Contains(...)` into a literal SQL `IN (...)` list.

**`SaveChangesAsync`** (when `UseHooks<TDbContext>` is wired):
1. The hook runner interceptor invokes every registered before-save hook, including `DataOwnerHook.BeforeSaveAsync`.
2. `DataOwnerHook` forces `ChangeTracker.AutoDetectChangesEnabled = true` for the duration (restoring the caller's
   prior value in a `finally`), reads `GetOwnershipKey()`, `GetAccessibleKeys()`, and whether a registered
   `ICurrentUserProvider.GetCurrentUser()` is non-empty (`stampAuditFromOwner`).
3. For each tracked entry by original state:
   - `Added` (with a non-empty owner key): stamps `CreatedBy`/`CreatedOn` when `stampAuditFromOwner`, then (if the
     entity is `IOwnedBy` and `OwnedBy` is still blank) sets `OwnedBy` to the owner key.
   - `Modified`: compares the tracked property's original value to the entity's current `OwnedBy`; if changed and
     the new value is not in `accessibleKeys` (or is blank), resets the tracked property's current value back to
     the original. Then, if the owner key is non-empty and `stampAuditFromOwner`, stamps `UpdatedBy`/`UpdatedOn`
     unless a domain method already changed them from their original values.
4. The property stamper prefers EF Core's compiled property accessor (reaches private setters/init-only/shadow
   properties); it falls back to a reflection walk up the type hierarchy only when the property is not part of the
   EF model at all, and throws `ArgumentException` if no writable property is found anywhere.

## Diagnostics & exceptions

| Exception type | Severity | When | Fix |
|---|---|---|---|
| `InvalidOperationException` | Fatal (model build fails) | `DataOwnerAuthQuery.HasQueryFilter<TEntity>` runs against a `DbContext` that does not implement `IDataOwnerDbContext` — e.g. a second `DbContext` in the same process that has an `IOwnedBy` entity and calls `UseAutoConfigModel()` but was never passed to `AddDataOwnerProvider`. Message names both `IDataOwnerDbContext` and the offending context's type name. | Implement `IDataOwnerDbContext` on that `DbContext`, or keep `IOwnedBy` entities out of its model. |
| `ArgumentException` (property stamper) | Fatal (surfaces from `SaveChangesAsync`) | `OwnedBy` (or `CreatedBy`/`CreatedOn`/`UpdatedBy`/`UpdatedOn`) is not part of the EF model **and** has no writable property anywhere in the type's hierarchy — e.g. `OwnedBy` implemented as a computed getter-only property with no backing field. | Give the property a real (even private) setter, or map it into the EF model. |
| Translation failure: `Translation of method 'System.Linq.Enumerable.Contains' failed` (EF Core, not this package) | Runtime, at query time | `IDataOwnerDbContext.AccessibleKeys` is implemented/exposed as `ICollection<string>` instead of `IEnumerable<string>` — EF Core cannot translate `ICollection<string>.Contains` inside a query filter. | Declare `AccessibleKeys` as `IEnumerable<string>` (a `List<string>`/`string[]` backing field is fine). |
| (compile error, no exception type) | Compile time | `TDbContext` passed to `AddDataOwnerProvider<TDbContext, TProvider>()` does not implement `IDataOwnerDbContext`. | Implement the interface on that exact `DbContext` type. |

No `DiagnosticDescriptor`-based analyzer ships in this package — the compile-time enforcement above is an ordinary
generic constraint, not a Roslyn analyzer.

## Gotchas (source-verified)

- **`AccessibleKeys` must stay `IEnumerable<string>`, never widen to `ICollection<string>`.** EF Core's filter
  translator turns `Enumerable.Contains` over `IEnumerable<string>` into SQL `IN (...)`, but cannot translate
  `ICollection<string>.Contains` inside a query filter. `IDataOwnerDbContext.AccessibleKeys` is declared as
  `IEnumerable<string>` for exactly this reason, and `DataOwnerAuthQuery.HasQueryFilter` calls `.Contains` on it
  directly.
- **Empty `AccessibleKeys` denies access — it is not "unrestricted".** The generated predicate is
  `IsUnrestrictedAccess || AccessibleKeys.Contains(...)`; an empty collection makes the `Contains` branch always
  false, and only `IsUnrestrictedAccess == true` bypasses it. A half-initialized principal fails closed, not open.
- **Forgetting `UseAutoConfigModel` disables filtering with no runtime error.** The filter is only attached inside
  `GlobalQueryFilter.Apply`, invoked from the auto-config model customizer, which only runs when
  `UseAutoConfigModel<TContext>()` was called on the options builder. `AddDataOwnerProvider` alone never triggers
  it. Symptom: every `IOwnedBy` query returns all owners' rows, silently.
- **Forgetting `UseHooks<TDbContext>` means new rows are never stamped.** `AddHook<TDbContext, DataOwnerHook>()`
  only registers the hook in DI; the hook runner interceptor invokes it only if the `DbContext`'s options include
  `UseHooks<TDbContext>(provider)`. `AddDbContextWithHook<TDbContext>(...)` does this automatically; plain
  `AddDbContext` does not. Symptom: new `IOwnedBy` rows save with a blank `OwnedBy`, then the deny-by-default
  filter hides them from everyone, including their creator.
- **The filter registration is process-wide, not per-`TDbContext`.** `AddDataOwnerProvider` adds
  `DataOwnerAuthQuery` to a static, process-wide bag of global model builders. Any other `DbContext` in that
  process whose model has an `IOwnedBy` entity and that calls `UseAutoConfigModel()` gets this filter applied too
  — with no compile-time warning, since it was never passed to `AddDataOwnerProvider`. If it doesn't implement
  `IDataOwnerDbContext`, its model build now throws.
- **A registered `ICurrentUserProvider` changes audit-field behavior application-wide, silently.**
  `AddCurrentUserProvider<TDbContext, TProvider>()` registers it un-keyed; once *any* call registers one, every
  `DataOwnerHook` instance in the process stops stamping `CreatedBy`/`UpdatedBy` from the ownership key for saves
  where `GetCurrentUser()` returns a value. `OwnedBy` stamping and the query filter are unaffected. Symptom of an
  *unintended* registration: `CreatedBy` starts holding a user id where a report expected the tenant key.
- **`GetAccessibleKeys()`'s default silently returns `[]` for a blank ownership key.** If `GetOwnershipKey()`
  returns null/empty, the default `IDataOwnerProvider.GetAccessibleKeys()` returns an empty collection rather than
  throwing — combined with the deny-by-default filter, a misconfigured/uninitialized provider quietly produces
  zero visible rows rather than an error. The same blank key also makes new-entity stamping a no-op, so new rows
  save with `OwnedBy` blank.
- **The reassignment guard only sees changes EF Core's own `ChangeTracker` sees.** The guard reads a tracked
  `Modified` entry's original value. Raw SQL, `ExecuteUpdate`/bulk-update helpers, or any write path bypassing
  `SaveChanges` is not covered; it is not a database-level constraint.
- **TPH (table-per-hierarchy) subtypes rely on EF Core's own filter inheritance, not a re-registration per
  subtype.** `DataOwnerAuthQuery.GetEntityTypes` filters to root types only (discriminator value is null). A
  derived type that implements `IOwnedBy` independently of its base — uncommon — does not get its own
  registration.

## Anti-patterns & hallucination traps

- **`services.AddDataOwnerFilter(...)` / `AddOwnershipFilter(...)` / `services.AddRowLevelSecurity(...)`** — do not
  exist. The one DI call is `AddDataOwnerProvider<TDbContext, TProvider>()`.
- **`modelBuilder.HasDataOwnerFilter<T>()` or any `ModelBuilder` extension in this package** — does not exist.
  `DataOwnerAuthQuery` is applied automatically by `UseAutoConfigModel`; there is no per-entity opt-in call to
  write in `OnModelCreating`.
- **Setting `IDataOwnerDbContext.AccessibleKeys` as `ICollection<string>` or `List<string>` on the interface
  itself** — the interface member is `IEnumerable<string>`; widening your implementation's declared property type
  reintroduces the untranslatable `Contains` call at query time.
- **Assuming an empty `AccessibleKeys` means "see everything"** — that was true in an older revision of this
  package and is false today; the only bypass is `IsUnrestrictedAccess = true`.
- **`new DataOwnerAuthQuery()` or `new DataOwnerHook(...)` constructed directly by application code** — both types
  are `internal sealed`; a consumer never instantiates them. Register via `AddDataOwnerProvider` and let DI/the
  model builder create them.
- **Calling `AddHook<TDbContext, DataOwnerHook>()` yourself** — `DataOwnerHook` is `internal`, so it cannot be
  referenced from outside the assembly anyway; `AddDataOwnerProvider` already does this.
- **Expecting `AddDataOwnerProvider<TDbContext, TProvider>()` to work on a `DbContext` that only implements
  `IEntity`/`IAuditedEntity`** — the generic constraint is `DbContext, IDataOwnerDbContext` specifically; nothing
  else satisfies it, and the call is a compile error otherwise.
- **Relying on hook run order between `AddDataOwnerProvider` and `AddCurrentUserProvider`** — each hook decides
  independently from `ICurrentUserProvider.GetCurrentUser()`'s own return value for that save, never from which
  `Add...` call ran first.

## Composes with

| Package | Rule |
|---|---|
| `DKNet.EfCore.Extensions` | Supplies `GlobalQueryFilter` (the base `DataOwnerAuthQuery` derives from) and the `UseAutoConfigModel`/global-model-builder wiring this package depends on. Reach for it directly to write a global filter of your own (e.g. soft-delete). |
| `DKNet.EfCore.Hooks` | Supplies `IBeforeSaveHookAsync`, `AddHook`, `AddDbContextWithHook`, `UseHooks` — the pipeline `DataOwnerHook` runs in. Reach for it for a custom before/after-save hook. |
| `DKNet.EfCore.AuditLogs` | Supplies `ICurrentUserProvider`, `AddCurrentUserProvider<TDbContext, TProvider>()`, and the internal property stamper both `DataOwnerHook` and the audit hook stamp through. Reach for it when `CreatedBy`/`UpdatedBy` should name the signed-in user rather than the tenant key, or when you need a change trail. |
| `DKNet.EfCore.Abstractions` | Supplies audited-entity contracts, which `DataOwnerHook` special-cases to also stamp `CreatedBy`/`CreatedOn`/`UpdatedBy`/`UpdatedOn`. Reach for it for entity base classes and audit attributes. |
| `DKNet.EfCore.Specifications` | Its `IgnoreQueryFilters()` can bypass filters whose `IsIgnorable` is `true`, but `DataOwnerAuthQuery.IsIgnorable => false` means row-level ownership isolation is exempt from that bypass by design — use Specifications for the query surface on top of the filter, not to defeat it. |

## Testing notes

- Tests use **SQLite in-memory** (an open in-memory connection kept for the fixture's lifetime) rather than
  TestContainers/SQL Server — appropriate here because the feature under test is EF Core query-filter/hook
  behavior, not SQL-Server-specific SQL.
- Fixture pattern: `IAsyncLifetime` fixtures build a `ServiceProvider` via `AddDataOwnerProvider<TDbContext,
  TProvider>().AddDbContextWithHook<TDbContext>(b => b.UseSqlite(connection).UseAutoConfigModel())`, then
  `EnsureCreatedAsync()`. Some hook-only fixtures deliberately omit `.UseAutoConfigModel()` when they exercise only
  `DataOwnerHook`'s stamping behaviour, not the query filter.
- Tests that push `SaveChangesAsync` into throwing build their own connection/DI per test rather than sharing a
  fixture, because a shared context would carry a poisoned entity into the next test.
- Canonical entity shape for tests: an audited base type implementing `IOwnedBy` with a private setter on
  `OwnedBy` and an intention-revealing method that mutates it — mirrors the "private setter + domain method"
  convention this framework favors elsewhere.
- A test `DbContext` forwards `AccessibleKeys` from an injected `IDataOwnerProvider` (first-wins across multiple
  registrations), and an "unrestricted" subclass overrides `IsUnrestrictedAccess => true` to exercise the bypass
  path.
- `DKNet.EfCore.Specifications`'s `RepositorySpec<TDbContext>` and `Specification<TEntity>.IgnoreQueryFilters()`
  are used to prove `IsIgnorable => false` holds even when a spec asks to bypass filters.
- Assertion style: Shouldly (`.ShouldBe`, `.ShouldBeOfType`, `.ShouldNotBeEmpty`, `.ShouldAllBe`) plus
  `db.Set<T>().AsNoTracking().FirstAsync(...)` reloads to assert on what actually persisted, not just the
  in-memory entity state.
