# DKNet.EfCore.Extensions

| Field | Value |
|---|---|
| Area | EfCore |
| Install | `dotnet add package DKNet.EfCore.Extensions` |
| NuGet | https://www.nuget.org/packages/DKNet.EfCore.Extensions |
| Docs | https://github.com/baoduy/DKNet/blob/main/docs/EfCore/DKNet.EfCore.Extensions.md |
| Source | https://github.com/baoduy/DKNet/tree/main/src/EfCore/DKNet.EfCore.Extensions |
| Depends on (DKNet) | `DKNet.Fw.Extensions` (Core), `DKNet.EfCore.Abstractions` |
| Depends on (3rd party) | `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Relational`, `System.ComponentModel.Annotations` |
| Target framework | `net10.0` |

## Purpose

`DKNet.EfCore.Extensions` is the registration/glue layer for the DKNet EF Core stack: it replaces EF Core's
`IModelCustomizer` so entity configurations, cross-cutting query filters, and SQL sequences are discovered by
assembly scan instead of hand-called in `OnModelCreating`. It also ships small, independent utilities used
elsewhere in the family — GUID v7 key generation, change-tracked-graph helpers, a concurrency-retry
`SaveChanges` wrapper, the `SnapshotContext` type every save-pipeline hook package consumes, and an opt-in
role-aware JSON property filter.

It is NOT a repository/query layer (that's `DKNet.EfCore.Specifications`), NOT a save-pipeline hook runner
(that's `DKNet.EfCore.Hooks`), and it does not invent entities: an entity still needs a `DbSet<T>` or an
`IEntityTypeConfiguration<T>` for EF Core to know about it at all — this package only automates *applying*
configuration classes you already wrote.

## Entry points

| Call | Exact signature | Called on | Notes (lifetime, ordering, prerequisites) |
|---|---|---|---|
| `UseAutoConfigModel<TContext>` | `DbContextOptionsBuilder<TContext> UseAutoConfigModel<TContext>(this DbContextOptionsBuilder<TContext> @this, params Assembly[]? assemblies) where TContext : DbContext` | `DbContextOptionsBuilder<TContext>` | No/empty `assemblies` defaults to `[typeof(TContext).Assembly]`. Needs a `DbContextOptionsBuilder<TContext>` receiver — reachable from a directly-constructed `new DbContextOptionsBuilder<TContext>()`, **not** from `AddDbContext`'s own `options =>` lambda, whose parameter is always the non-generic `DbContextOptionsBuilder` below. |
| `UseAutoConfigModel` (non-generic) | `DbContextOptionsBuilder UseAutoConfigModel(this DbContextOptionsBuilder @this, Assembly[] assemblies)` | `DbContextOptionsBuilder` | Throws `ArgumentNullException` if `@this` is null. Stores assemblies via `IDbContextOptionsExtension`; does not itself scan anything until model build. |
| `UseAutoDataSeeding` | `DbContextOptionsBuilder UseAutoDataSeeding(this DbContextOptionsBuilder @this, Assembly[] assemblies)` | `DbContextOptionsBuilder` | Separate opt-in from `UseAutoConfigModel` — the customizer's own seeding call is commented out. Wires EF Core's native `UseSeeding`/`UseAsyncSeeding`. Throws `ArgumentNullException` on a null `@this` or `assemblies`. |
| `AddGlobalModelBuilder<TImplementation>` | `IServiceCollection AddGlobalModelBuilder<TImplementation>() where TImplementation : class, IGlobalModelBuilder` | `IServiceCollection` (extension member) | Adds to a static registry, merged+deduped with assembly-scanned filters at model build. Escape hatch for a filter *outside* the scanned assemblies — it is still instantiated via `Activator.CreateInstance`, so it does **not** solve the constructor-argument problem either (see Gotchas). |
| `AddEfCoreExceptionHandler<TDbContext, TExceptionHandler>` | `IServiceCollection AddEfCoreExceptionHandler<TDbContext, TExceptionHandler>() where TDbContext : DbContext where TExceptionHandler : class, IEfCoreExceptionHandler` | `IServiceCollection` (extension member) | Registers a keyed transient (`AddKeyedTransient`) keyed by `typeof(TDbContext).FullName`. Idempotent — a second call for the same `TDbContext` is a no-op. |
| `DefaultEntityTypeConfiguration<TEntity>` | `abstract class DefaultEntityTypeConfiguration<TEntity> : IEntityTypeConfiguration<TEntity> where TEntity : class` | Subclass, implement `Configure` | Call `base.Configure(builder)` first; it configures `Id`, audit columns, and `RowVersion` by reflection before your own mapping runs. |
| `GlobalQueryFilter` | `abstract class GlobalQueryFilter : IGlobalModelBuilder` | Subclass, override `FilterKey`/`GetEntityTypes`/`HasQueryFilter<TEntity>` | Discovered automatically by assembly scan (must be non-abstract, parameterless-constructible) or registered via `AddGlobalModelBuilder<T>()`. |
| `DataSeedingConfiguration<TEntity>` | `abstract class DataSeedingConfiguration<TEntity> : IDataSeedingConfiguration where TEntity : class` | Subclass, implement `GetDataAsync` | Discovered by `UseAutoDataSeeding`'s assembly scan; `Order` is declared but never read — seeders run in scan order. |
| `[SqlSequenceAttribute]` / `[SequenceAttribute]` | Attributes (declared in `DKNet.EfCore.Abstractions`) | `enum` type / each member | Only registered when `context.IsSqlServer()` or `context.IsNpgsql()`; silently ignored on every other provider. |
| `UseRoleAwareSensitiveData` | `JsonSerializerOptions UseRoleAwareSensitiveData(this JsonSerializerOptions options, ISensitiveDataPrincipalAccessor accessor)` | `JsonSerializerOptions` | Must run before the options instance's first serialize (`System.Text.Json` freezes it), typically in host startup. Throws `ArgumentNullException` on null `options`/`accessor`. |
| `SaveChangesWithConcurrencyHandlingAsync` | `Task<int> SaveChangesWithConcurrencyHandlingAsync(this DbContext dbContext, IEfCoreExceptionHandler? handler = null, CancellationToken cancellationToken = default)` | `DbContext` | Defaults to `new EfCoreExceptionHandler()` when `handler` is null. Loops on `DbUpdateConcurrencyException`, bounded by `handler.MaxRetryCount`. |
| `AddNewEntitiesFromNavigations` | `Task<int> AddNewEntitiesFromNavigations(this TDbContext context, CancellationToken cancellationToken = default) where TDbContext : DbContext` | `DbContext` | Runs `ChangeTracker.DetectChanges()` internally; call before `SaveChangesAsync`. |
| `NextSeqValue<TEnum,TValue>` / `NextSeqValue<TEnum>` / `NextSeqValueWithFormat<TEnum>` | see Public surface | `DbContext` | Issue raw SQL against `context.Database.GetDbConnection()`; throw `NotSupportedException` on unsupported providers. |

## Public surface

### `Microsoft.EntityFrameworkCore` (ambient namespace — extends EF Core's own so callers need no extra `using`)

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `EfCoreSetup` | static class | Model-build registration entry points | `UseAutoConfigModel<TContext>(params Assembly[]?)`; `UseAutoConfigModel(Assembly[])`; `extension(IServiceCollection)`: `AddEfCoreExceptionHandler<TDbContext,TExceptionHandler>()`, `AddGlobalModelBuilder<TImplementation>()` |
| `EfCoreExtensions` | static class | Key/table lookups, provider checks, SQL sequence reads | `GetEntityKeyValues(this EntityEntry) : Dictionary<string,object?>`; `extension(DbContext)`: `GetPrimaryKeyProperties<TEntity>()`, `GetPrimaryKeyValues(object)`, `IsSqlServer()`, `IsNpgsql()`, `NextSeqValue<TEnum,TValue>(TEnum, CancellationToken)`, `NextSeqValue<TEnum>(TEnum, CancellationToken)`, `NextSeqValueWithFormat<TEnum>(TEnum, CancellationToken)` |
| `NavigationExtensions` | static class | Change-tracked graph helpers | `GetNavigationValues(this object, INavigation)`; `extension(EntityEntry)`: `IsNewEntity()`, `HasProperty(string)`, `GetOriginalValue(string)`, `GetOriginalKeyValues()`, `GetCurrentValue(string)`, `GetCurrentKeyValues()`; `extension<TDbContext>(TDbContext)`: `AddNewEntitiesFromNavigations(CancellationToken)`, `GetPossibleUpdatingEntities()`, `GetNewEntitiesFromNavigations(EntityEntry)`, `GetNewEntitiesFromNavigations()`, `GetCollectionNavigations(Type)` |

### `DKNet.EfCore.Extensions.Configurations`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `DefaultEntityTypeConfiguration<TEntity>` | abstract class | Convention base: Id/audit/concurrency by reflection | `virtual void Configure(EntityTypeBuilder<TEntity> builder)` |
| `IGlobalModelBuilder` | interface | Extension point for global model-build logic | `void Apply(ModelBuilder, DbContext)` |
| `GlobalQueryFilter` | abstract class | Per-entity-type `HasQueryFilter` base with a named-filter-key registry | `abstract string FilterKey { get; }`; `virtual bool IsIgnorable => true`; `static IReadOnlyCollection<string> IgnorableFilterKeys { get; }`; `void Apply(ModelBuilder, DbContext)` (not virtual — implements `IGlobalModelBuilder.Apply`); `protected abstract IEnumerable<IMutableEntityType> GetEntityTypes(ModelBuilder)`; `protected abstract Expression<Func<TEntity,bool>>? HasQueryFilter<TEntity>(DbContext) where TEntity : class` |
| `IDataSeedingConfiguration` | interface | Seed-unit contract | `int Order { get; }`; `Func<DbContext,bool,CancellationToken,Task>? SeedAsync { get; }`; `Type EntityType { get; }` |
| `DataSeedingConfiguration<TEntity>` | abstract class | Seed-unit base; dedupes by primary key | `Type EntityType => typeof(TEntity)`; `virtual int Order => 0`; `virtual Func<DbContext,bool,CancellationToken,Task> SeedAsync { get; }`; `protected abstract ValueTask<ICollection<TEntity>> GetDataAsync(CancellationToken = default)` |

### `DKNet.EfCore.Extensions.Extensions`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `EfConcurrencyResolution` | enum | Concurrency-conflict resolution outcome | `IgnoreChanges`, `RetrySaveChanges`, `RethrowException` |
| `IEfCoreExceptionHandler` | interface | Pluggable concurrency-conflict handler | `int MaxRetryCount => 3` (default interface member); `Task<EfConcurrencyResolution> HandlingAsync(DbContext, DbUpdateConcurrencyException, CancellationToken = default)` |
| `EfCoreExceptionHandler` | sealed class | Default handler: reload-then-retry | `EfCoreExceptionHandler(ILogger<EfCoreExceptionHandler>? logger = null)`; `Task<EfConcurrencyResolution> HandlingAsync(...)` — see Runtime behaviour |
| `EfSaveChangesExtension` | static class | Retry-loop driver | `static Task<int> SaveChangesWithConcurrencyHandlingAsync(this DbContext, IEfCoreExceptionHandler? = null, CancellationToken = default)` |
| `EfCoreDataSeedingExtensions` | static class | Discovers and wires `IDataSeedingConfiguration` | `static DbContextOptionsBuilder UseAutoDataSeeding(this DbContextOptionsBuilder, Assembly[])` |
| `SequenceExtensions` | *internal* static class | `[SqlSequence]`/`[Sequence]` → `modelBuilder.HasSequence(...)` | Not public; explains why sequences appear on the model — see Runtime behaviour |

### `DKNet.EfCore.Extensions.Convertors`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `GuidV7ValueGenerator` | sealed class (`ValueGenerator<Guid>`) | Time-ordered GUID key generation | `override bool GeneratesTemporaryValues => false`; `override Guid Next(EntityEntry entry) => Guid.CreateVersion7()` |

### `DKNet.EfCore.Extensions.Snapshots`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `SnapshotContext` | sealed class (`IAsyncDisposable`, `IDisposable`) | One-shot point-in-time capture of Added/Modified/Deleted entries | `SnapshotContext(DbContext context)`; `DbContext DbContext { get; }` (throws `ObjectDisposedException` if disposed); `IReadOnlyCollection<SnapshotEntityEntry> Entities { get; }` (throws `InvalidOperationException` if read before `Initialize()`); `void Initialize()`; `void Dispose()`; `ValueTask DisposeAsync()` |
| `SnapshotEntityEntry` | sealed class | Immutable wrapper around one captured `EntityEntry` | `SnapshotEntityEntry(EntityEntry entry)`; `object Entity { get; }`; `EntityEntry Entry { get; }`; `EntityState OriginalState { get; }` |

### `DKNet.EfCore.Extensions.Serialization`

| Type | Kind | Purpose | Key members |
|---|---|---|---|
| `ISensitiveDataPrincipalAccessor` | interface | Seam supplying the caller principal, keeping this package free of `Microsoft.AspNetCore.*` | `ClaimsPrincipal? Current { get; }` |
| `SensitiveDataJsonExtensions` | static class | Role-gated `[SensitiveData]` JSON filtering | `static JsonSerializerOptions UseRoleAwareSensitiveData(this JsonSerializerOptions, ISensitiveDataPrincipalAccessor)`; `internal static bool IsPermitted(ClaimsPrincipal?, IReadOnlyList<string>)` |

`DKNet.EfCore.Extensions.Internal` holds the internal `IModelCustomizer` plumbing (`EntityAutoConfigRegister`,
`EntityConfigExtensionInfo`, `AutoConfigModelCustomizer`) that makes `UseAutoConfigModel` work — not part of the
public surface, listed in Runtime behaviour only because it explains observable ordering.

## Options & defaults

There is no options class — every knob is a registration argument, an attribute, or a virtual/default-interface member.

| Option | Type | Default | Effect | Where set |
|---|---|---|---|---|
| Assemblies scanned by `UseAutoConfigModel<TContext>()` | `Assembly[]` | `[typeof(TContext).Assembly]` when no/empty array passed | Which assemblies are scanned for `IEntityTypeConfiguration<T>`, `IGlobalModelBuilder`, `[SqlSequence]` enums | `EfCoreSetup.UseAutoConfigModel<TContext>`; independently re-defaulted to `dbContext.GetType().Assembly` inside the internal customizer if the extension is somehow missing |
| `IEfCoreExceptionHandler.MaxRetryCount` | `int` | `3` | Retry cap for `SaveChangesWithConcurrencyHandlingAsync`; past it, returns `0` instead of throwing | Default interface member on `IEfCoreExceptionHandler` |
| `GlobalQueryFilter.IsIgnorable` | `bool` | `true` | Whether `ISpecification.IsIgnoreQueryFilters()` may bypass this filter | `GlobalQueryFilter` virtual property, override per filter |
| `IDataSeedingConfiguration.Order` | `int` | `0` | **Declared but never read.** `UseAutoDataSeeding` runs discovered seeders in assembly-scan order | `IDataSeedingConfiguration`/`DataSeedingConfiguration<T>` |
| Sequence registration gate | — | runs only when `context.IsSqlServer()` or `context.IsNpgsql()` | Any other provider (SQLite, InMemory) gets no sequences on the model | the internal customizer's model-creating step |
| `[Sequence]` fields (`Type`, `StartAt`, `IncrementsBy`, `Min`, `Max`, `Cyclic`, `FormatString`) / `[SqlSequence]` field (`Schema`) | attribute properties | declared in `DKNet.EfCore.Abstractions` (not this package) | `SequenceExtensions` only applies a value `> 0` for `StartAt`/`IncrementsBy`/`Min`/`Max`; always calls `seq.IsCyclic(fieldAtt.Cyclic)`; `fieldAtt.Type` feeds `HasSequence(...)`'s data-type argument and `FormatString` is read only by `NextSeqValueWithFormat` | `SequenceExtensions` (consumer of the Abstractions attribute) |
| Role-aware sensitive-property filtering | — | **off** | Applies only to the exact `JsonSerializerOptions` instance passed to `UseRoleAwareSensitiveData` | `SensitiveDataJsonExtensions.UseRoleAwareSensitiveData` |
| `[SensitiveData]` with no role named | — | any **authenticated** caller (`IsAuthenticated == true`) | An unauthenticated caller (or a null principal) is refused regardless | `SensitiveDataJsonExtensions.IsPermitted` |

## Usage patterns

### Auto-apply entity configurations from one or more assemblies

**When**: standard registration — you want every `IEntityTypeConfiguration<T>` in the DbContext's assembly (or named module assemblies) applied without an explicit `ApplyConfigurationsFromAssembly` call.

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;
using DKNet.EfCore.Extensions.Configurations;

public class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class ProductConfiguration : DefaultEntityTypeConfiguration<Product>
{
    public override void Configure(EntityTypeBuilder<Product> builder)
    {
        base.Configure(builder); // Id -> GuidV7ValueGenerator (Product.Id is a Guid)
        builder.Property(p => p.Name).HasMaxLength(255).IsRequired();
    }
}

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
}

public static class Registration
{
    // Registration
    public static void ConfigureServices()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o =>
            // AddDbContext's lambda hands you the non-generic DbContextOptionsBuilder, so pass the
            // assembly explicitly here rather than reaching for the zero-arg UseAutoConfigModel<TContext>().
            o.UseSqlServer("Server=.;Database=App;Trusted_Connection=True;")
             .UseAutoConfigModel([typeof(AppDbContext).Assembly]));
    }
}
```

**Notes**: an entity with no `IEntityTypeConfiguration<T>` and no `DbSet<T>` is simply not in the model — this
package never fabricates entities. Inside `AddDbContext`'s own `options =>` lambda the parameter is always the
non-generic `DbContextOptionsBuilder` — the zero-arg convenience `UseAutoConfigModel<TContext>()` needs a
`DbContextOptionsBuilder<TContext>` receiver, which only a directly-constructed `new DbContextOptionsBuilder<TContext>()`
gives you (see the "Auto-discover" recipe in the `dknet-efcore-domain-model` SKILL.md), so call the array-of-assemblies
overload there instead. Multi-assembly (modular) apps pass every assembly explicitly the same way:
`o.UseAutoConfigModel([typeof(Product).Assembly, typeof(Customer).Assembly])`.

### Cross-cutting query filter (soft delete)

**When**: an entity-marker interface should be filtered out of every query, without repeating `HasQueryFilter` per entity configuration.

```csharp
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using DKNet.EfCore.Extensions.Configurations;

public interface ISoftDelete
{
    bool IsDeleted { get; }
}

internal sealed class SoftDeleteFilter : GlobalQueryFilter
{
    public override string FilterKey => nameof(SoftDeleteFilter);

    protected override IEnumerable<IMutableEntityType> GetEntityTypes(ModelBuilder modelBuilder) =>
        modelBuilder.Model.GetEntityTypes()
            .Where(t => typeof(ISoftDelete).IsAssignableFrom(t.ClrType));

    protected override Expression<Func<TEntity, bool>>? HasQueryFilter<TEntity>(DbContext context) =>
        e => !((ISoftDelete)e).IsDeleted;
}
```

**Notes**: `SoftDeleteFilter` needs no explicit registration — a non-abstract, parameterless-constructible
`IGlobalModelBuilder` found in a scanned assembly is instantiated via `Activator.CreateInstance` automatically.
A filter needing constructor arguments must instead be registered with
`services.AddGlobalModelBuilder<MyFilter>()`, which cannot supply those arguments either (it also uses
`Activator.CreateInstance`) — so that escape hatch only helps for filters *outside* the scanned assemblies, not
ones with real dependencies. Set `IsIgnorable => false` for a filter (e.g. row-level ownership) that must never
be bypassable via `ISpecification.IsIgnoreQueryFilters()`.

### Data seeding through EF Core's native seeding hooks

**When**: idempotent startup/migration seed data, wired through `UseSeeding`/`UseAsyncSeeding` instead of a bespoke seeding path.

```csharp
using Microsoft.EntityFrameworkCore;
using DKNet.EfCore.Extensions.Configurations;
using DKNet.EfCore.Extensions.Extensions;

public class Country
{
    public string Code { get; set; } = string.Empty;
}

public sealed class CountrySeed : DataSeedingConfiguration<Country>
{
    protected override ValueTask<ICollection<Country>> GetDataAsync(CancellationToken cancellation = default) =>
        ValueTask.FromResult<ICollection<Country>>([new Country { Code = "VN" }, new Country { Code = "US" }]);
}

public static class SeedRegistration
{
    // Registration (separate opt-in from UseAutoConfigModel; reuses AppDbContext from the example above)
    public static void ConfigureServices()
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=.;Database=App;Trusted_Connection=True;")
            .UseAutoConfigModel<AppDbContext>()
            .UseAutoDataSeeding([typeof(CountrySeed).Assembly]);
    }
}
```

**Notes**: `DataSeedingConfiguration<T>`'s built-in `SeedAsync` dedupes candidates against the database **by
primary key** (reads only the key columns via a projection, using `EF.Property<object>` so shadow keys work
too), not by `TEntity` equality — running the same seeder twice does not re-insert rows even when `Country`
has no `Equals`/`GetHashCode` override. `Order` is declared on the interface but ignored: seeders run in
assembly-scan order, so cross-seed dependencies must be handled inside one seeder, not via `Order`.

### Retry a save on an optimistic-concurrency conflict

**When**: a `RowVersion`-tracked entity might be updated concurrently and a "someone else already changed this row" conflict should reload and retry rather than crash the request.

```csharp
using Microsoft.EntityFrameworkCore;
using DKNet.EfCore.Extensions.Extensions;

await using var db = new AppDbContext(
    new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlServer("Server=.;Database=App;Trusted_Connection=True;")
        .Options);

// db has at least one IConcurrencyEntity<byte[]>-tracked change pending
var rowsAffected = await db.SaveChangesWithConcurrencyHandlingAsync();
```

**Notes**: the default `EfCoreExceptionHandler` returns `RethrowException` when
`DbUpdateConcurrencyException.Entries.Count == 0` (nothing structured to recover from), and otherwise
**always** reloads each entry's current database values into `OriginalValues` and returns
`RetrySaveChanges` — it does not inspect the exception message at all. The retry loop
(`SaveChangesWithConcurrencyHandlingAsync`) stops and returns `0` once `retryCount` exceeds
`handler.MaxRetryCount` (default `3`), rather than throwing.

### Stage new child entities reachable from a tracked aggregate

**When**: an aggregate root's collection navigation holds newly constructed children that were never explicitly `Add`ed.

```csharp
using Microsoft.EntityFrameworkCore;
using DKNet.EfCore.Extensions.Extensions;

public class OrderItem
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
}

public class PurchaseOrder
{
    public int Id { get; set; }
    public List<OrderItem> Items { get; set; } = [];
}

public class PurchaseDbContext(DbContextOptions<PurchaseDbContext> options) : DbContext(options)
{
    public DbSet<PurchaseOrder> Orders => Set<PurchaseOrder>();
}

public static class PurchaseOrderApp
{
    public static async Task RunAsync()
    {
        await using var db = new PurchaseDbContext(
            new DbContextOptionsBuilder<PurchaseDbContext>()
                .UseSqlServer("Server=.;Database=App;Trusted_Connection=True;")
                .Options);

        // order.Items contains brand-new OrderItem instances that were never added individually
        var order = new PurchaseOrder();
        order.Items.Add(new OrderItem { Sku = "WIDGET-1" });

        db.Set<PurchaseOrder>().Add(order);
        await db.AddNewEntitiesFromNavigations();   // walks tracked roots' collection navigations, adds new children
        await db.SaveChangesAsync();
    }
}
```

**Notes**: `AddNewEntitiesFromNavigations` calls `ChangeTracker.DetectChanges()` internally and only walks
`Detached`/`Modified` roots — `Unchanged` roots are skipped, so a freshly-loaded, untouched aggregate's
navigations are not scanned. "New" is decided by `EntityEntry.IsNewEntity()`: `Detached` state, an unset key,
`Added` state, or (for anything else) all-null original key values.

### Withhold a sensitive property from unauthorized callers

**When**: one property (e.g. a supplier cost) must be present in a JSON response only for callers holding a given role.

```csharp
using System.Security.Claims;
using DKNet.EfCore.Abstractions.Attributes;
using DKNet.EfCore.Extensions.Serialization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

public class PricingItem
{
    public string Name { get; set; } = string.Empty;

    [SensitiveData("pricing")]
    public decimal SupplierCostPrice { get; set; }
}

internal sealed class HttpContextSensitiveDataPrincipalAccessor(IHttpContextAccessor httpContextAccessor)
    : ISensitiveDataPrincipalAccessor
{
    public ClaimsPrincipal? Current => httpContextAccessor.HttpContext?.User;
}

public static class SensitiveDataStartup
{
    // Startup, before the JsonOptions instance ever serializes anything
    public static void ConfigureServices(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<ISensitiveDataPrincipalAccessor, HttpContextSensitiveDataPrincipalAccessor>();
        builder.Services.AddSingleton<IConfigureOptions<JsonOptions>>(sp =>
            new ConfigureOptions<JsonOptions>(o =>
                o.SerializerOptions.UseRoleAwareSensitiveData(
                    sp.GetRequiredService<ISensitiveDataPrincipalAccessor>())));
    }
}
```

**Notes**: the property is **absent** from the payload when withheld — not `null`, not redacted text. The
decision runs per-property, per-serialization (never cached on `JsonTypeInfo`), and fails closed: no ambient
principal, or an unauthenticated one, means every `[SensitiveData]` property is withheld — including ones with
no role named. This package takes no `Microsoft.AspNetCore.*` dependency; the accessor is written by the host.

## Runtime behaviour

**Model build** (once per `DbContext` model, triggered by swapping in the internal `AutoConfigModelCustomizer`
as `IModelCustomizer`):
1. Resolves the registered assembly list off the `DbContext`'s `IDbContextOptions`, falling back to
   `dbContext.GetType().Assembly` if none was registered.
2. For each assembly, calls `modelBuilder.ApplyConfigurationsFromAssembly(assembly)` — EF Core's own scanner —
   applying every `IEntityTypeConfiguration<T>` found (including `DefaultEntityTypeConfiguration<T>`
   subclasses, which set `Id`/audit columns/`RowVersion` by reflection first via `base.Configure`).
3. Unions assembly-scanned `IGlobalModelBuilder` implementations with the ones added via
   `AddGlobalModelBuilder<T>()`, de-duplicates by type, instantiates each via `Activator.CreateInstance`, and
   calls `.Apply(modelBuilder, dbContext)`.
4. If `dbContext.IsSqlServer()` or `dbContext.IsNpgsql()`, finds every `[SqlSequence]`-decorated enum and turns
   each `[Sequence]` member into a `modelBuilder.HasSequence(...)` call.
5. Delegates to the original `ModelCustomizer` (so a provider's own customizer, e.g. Npgsql's, still runs).

**`SaveChangesWithConcurrencyHandlingAsync`**: calls `SaveChangesAsync`; on `DbUpdateConcurrencyException`,
asks the handler for a resolution — `RethrowException` re-throws, `IgnoreChanges` returns `0`,
`RetrySaveChanges` increments a retry counter and loops (bounded by `MaxRetryCount`, past which it returns `0`
silently). The default `EfCoreExceptionHandler` reloads each conflicting entry's database values into
`OriginalValues` before the retry.

**`SnapshotContext.Initialize()`**: calls `ChangeTracker.DetectChanges()` once, then captures every
`Added`/`Modified`/`Deleted` `EntityEntry` at that instant into `SnapshotEntityEntry` records (entry + entity +
captured `OriginalState`). It is a one-shot capture: entities added to the context afterward are not
retroactively included, and `Entities` throws until `Initialize()` has run. `DKNet.EfCore.Hooks` constructs one
`SnapshotContext` per `SaveChanges` call and hands it to every registered `IBeforeSaveHookAsync`/
`IAfterSaveHookAsync` — see `dknet-efcore-save-pipeline` for that side of it.

**`UseRoleAwareSensitiveData`**: wraps (does not replace) the options' `TypeInfoResolver` with an added
modifier. For every object `JsonTypeInfo`, every property carrying `[SensitiveData]` gets a `ShouldSerialize`
callback that re-evaluates `accessor.Current` fresh on each write via `SensitiveDataJsonExtensions.IsPermitted`.

## Diagnostics & exceptions

| Exception type | Severity | When | Fix |
|---|---|---|---|
| `ArgumentNullException` | Error | `UseAutoConfigModel`, `UseAutoDataSeeding`, `UseRoleAwareSensitiveData`, `GetPrimaryKeyValues`, `HasProperty`/`GetOriginalValue`/`GetCurrentValue`/`GetPrimaryKeyProperties` called with a null required argument | Pass a non-null value; these are all argument-validation guards, not configuration errors |
| `NotSupportedException` | Error | `NextSeqValue<TEnum>` called against a provider that is neither SQL Server nor Npgsql | Only call sequence APIs against a supported relational provider; the enum's `[SqlSequence]` is still applied to the model but produces no runtime sequence to read on unsupported providers |
| `InvalidOperationException` | Error | `NextSeqValue<TEnum>` — `ExecuteScalarAsync` returns null (unexpected empty result); or `SnapshotContext.Entities` read before `Initialize()` | Ensure the sequence/schema exists as configured; call `Initialize()` before reading `Entities` |
| `ObjectDisposedException` | Error | `SnapshotContext.DbContext` or `.Initialize()` called after `Dispose()`/`DisposeAsync()` | Don't reuse a disposed `SnapshotContext`; construct a new one per save |
| `DbUpdateConcurrencyException` | Error (rethrown, not swallowed) | `SaveChangesWithConcurrencyHandlingAsync` when the handler resolves to `RethrowException` (including the built-in handler's "no `Entries`" case) — each retry attempt has its own `try`/`catch`, so the instance rethrown is whichever attempt's exception triggered it. Exhausting `MaxRetryCount` does **not** raise this exception — the loop returns `0` silently instead (see Gotchas) | Handle it at the caller like any unresolved concurrency conflict, or supply a custom `IEfCoreExceptionHandler` |

No analyzer `DiagnosticDescriptor`s exist in this package — it has no Roslyn analyzer/generator component.

## Gotchas (source-verified)

- **Auto-configuration finds configurations, not entities.** An entity with no `IEntityTypeConfiguration<T>`
  and no `DbSet<T>` is invisible to the model regardless of `UseAutoConfigModel` — `context.Set<T>()` throws
  `InvalidOperationException` even for a type carrying `[IgnoreEntity]`, because it simply has no config/DbSet,
  not because the attribute excluded it.
- **`[IgnoreEntity]` (from `DKNet.EfCore.Abstractions`) has no effect in this package.** It is never referenced
  by the auto-config discovery code — decorating an entity with it changes nothing here.
- **Audit columns are capped at 255 chars, not the 500 the interface annotates.** `IAuditedProperties.CreatedBy`/
  `UpdatedBy` carry `[MaxLength(500)]`, but `DefaultEntityTypeConfiguration<TEntity>.Configure` calls
  `.HasMaxLength(255)` on both — the fluent call wins.
- **`DataSeedingConfiguration<T>.Order` is decorative.** `UseAutoDataSeeding` never sorts by it — seeders run
  in the order the assembly scan produces them.
- **Sequences are SQL Server/Npgsql only, silently.** Sequence registration only runs when
  `context.IsSqlServer() || context.IsNpgsql()` — on SQLite/InMemory (common in unit tests) the `[SqlSequence]`
  enum contributes nothing to the model, and `NextSeqValue` then throws `NotSupportedException` at call time
  rather than failing at model build.
- **`AddGlobalModelBuilder<T>()` cannot supply constructor arguments either.** Both the assembly-scanned path
  and the DI-registered path go through `Activator.CreateInstance(filter) as IGlobalModelBuilder` — a filter
  with no public parameterless constructor fails the same way either path: `Activator.CreateInstance` throws
  `MissingMethodException` *before* the `as` cast ever runs, crashing model build the first time the
  `DbContext`'s model is touched. This is a loud failure, not a silent "filter not applied".
- **`GlobalQueryFilter.IgnorableFilterKeys` only reflects filters that have actually built their model at
  least once.** The cache is populated inside `Apply`, itself only called during model build — reading
  `IgnorableFilterKeys` before any `DbContext`'s `Model` has been touched returns an empty array even if
  filters are registered.
- **`UseAutoConfigModel`'s "no assemblies" default is applied twice, independently.** The generic overload
  defaults to `[typeof(TContext).Assembly]` at registration time, and the internal customizer *also* falls
  back to `dbContext.GetType().Assembly` if the extension's `Assemblies` array is empty — harmless today (both
  land on the same assembly), but a future change to one default without the other would silently diverge.
- **A frozen `JsonSerializerOptions` fails at opt-in time, not at serialize time.** Calling
  `UseRoleAwareSensitiveData` on an options instance that has already serialized something throws
  `InvalidOperationException` from `System.Text.Json` itself — this package adds no extra guard, so the error
  surfaces from inside the `TypeInfoResolver` assignment.
- **`SnapshotContext` never touches `ChangeTracker.AutoDetectChangesEnabled` or any other tracker setting.**
  If a caller needs detection suppressed while the snapshot is held, it must manage that itself — the type's
  own XML doc says so explicitly.
- **`EfCoreExceptionHandler`'s retry heuristic does not string-match the exception message**, despite what
  older docs/README text may say. It checks only `exception.Entries.Count == 0` — if there are any entries, it
  unconditionally reloads database values and returns `RetrySaveChanges`, regardless of the exception's message
  text. Treat the source behaviour above as authoritative.
- **`DataSeedingConfiguration<T>` does not dedupe with `EqualityComparer<TEntity>.Default`**, despite what
  older docs/README text may say. It compares candidates against the database **by primary key value**, read
  via an `EF.Property<object>` projection — reference/default equality is never used, so an entity with no
  `Equals`/`GetHashCode` override still dedupes correctly across repeated runs.

## Anti-patterns & hallucination traps

- **`services.AddDbContext<T>().UseAutoConfigModel()` as a fluent chain off `AddDbContext` itself** — wrong.
  `UseAutoConfigModel` extends `DbContextOptionsBuilder`, so it belongs *inside* the `options =>` lambda passed
  to `AddDbContext`/`UseSqlServer(...)`, not chained onto the `IServiceCollection` result.
  ```csharp
  // no-compile
  // WRONG: UseAutoConfigModel does not exist on IServiceCollection
  services.AddDbContext<AppDbContext>().UseAutoConfigModel<AppDbContext>();
  ```
  Correct form: `services.AddDbContext<AppDbContext>(o => o.UseSqlServer(cs).UseAutoConfigModel([typeof(AppDbContext).Assembly]));`
  — note the array-of-assemblies overload, not `UseAutoConfigModel<AppDbContext>()`. Inside `AddDbContext`'s
  lambda, `o` is the non-generic `DbContextOptionsBuilder`, so the zero-arg generic overload (which needs a
  `DbContextOptionsBuilder<TContext>` receiver) does not resolve there — the compiler error reads "does not
  contain a definition for 'UseAutoConfigModel'" if you try it. That convenience form only works on a directly
  constructed `new DbContextOptionsBuilder<TContext>()`, as in the Quick start / "Auto-discover" recipes.
- **Calling `UseAutoConfigModel<TContext>()` expecting it to also seed data.** It does not — seeding requires
  the separate `UseAutoDataSeeding(assemblies)` call; the customizer's seeding line is deliberately commented
  out.
- **Expecting `GlobalQueryFilter` subclasses to need DI registration.** Assembly-scanned filters need no
  `AddGlobalModelBuilder<T>()` call at all — adding it too is harmless (de-duplicated by type) but not
  required, and does not fix a filter that needs constructor arguments (see Gotchas).
- **Assuming `IDataSeedingConfiguration.Order` controls seeding sequence.** It is read nowhere in this
  package; do not rely on it for seed ordering — see Gotchas.
- **Assuming `[IgnoreEntity]` excludes an entity from the model here.** It is a marker this package's discovery
  code never inspects; do not reach for it to hide a `DbSet`-mapped or configured entity from
  `UseAutoConfigModel` — remove the `DbSet`/configuration instead.
- **Reaching for `DKNet.EfCore.Repos`/`DKNet.EfCore.Repos.Abstractions`.** Those packages were removed from the
  solution; the persistence entry point is `DKNet.EfCore.Specifications`'s spec repository (see
  `dknet-efcore-specifications`), not a hand-rolled or generated repository interface from this package.
- **Calling `EfCoreExceptionHandler.HandlingAsync` and expecting it to inspect the exception message.** It does
  not string-match anything — it decides purely from `exception.Entries.Count` (see Runtime behaviour). Do not
  write a custom handler assuming the shipped one needs a specific message text to trigger a retry.
- **Passing a plain `Guid.NewGuid()`-style value generator name — there is no `GuidValueGenerator` here.**
  The only value generator this package ships is `GuidV7ValueGenerator`; don't invent a sibling type name for a
  random (non-v7) generator.

## Composes with

| Package | Rule |
|---|---|
| `DKNet.EfCore.Abstractions` | Reach for it first to define the domain model itself — `IAuditedProperties`, `IConcurrencyEntity<T>`, `Entity<T>`/`AuditedEntity<T>`, `[SqlSequence]`/`[Sequence]`, `[SensitiveData]`. This package's conventions read those types by reflection but does not define them. |
| `DKNet.EfCore.Hooks` | Reach for it when you want logic to run around `SaveChanges` — it is the primary consumer of this package's `SnapshotContext`, constructing one per save and handing it to every `IBeforeSaveHookAsync`/`IAfterSaveHookAsync`. |
| `DKNet.EfCore.Events` | Built on `DKNet.EfCore.Hooks`; reach for it when aggregates raise domain events that must publish after a successful save. |
| `DKNet.EfCore.AuditLogs` | Also hook-based; reach for it for a property-level change record, not just who/when. |
| `DKNet.EfCore.DataAuthorization` | Reach for it instead of writing row-level ownership filtering yourself — it plugs into `GlobalQueryFilter` with `IsIgnorable = false` so ownership can never be bypassed the way an ignorable filter can. |
| `DKNet.EfCore.Specifications` | The query/repository layer that runs on top of the model this package builds; consumes the concurrency-handling save extension but otherwise doesn't call this package's APIs directly. |
| `DKNet.EfCore.DtoGenerator` | Reach for it so `[SensitiveData]` is carried from the entity onto the generated response DTO automatically, instead of re-declaring the attribute by hand. |
| `DKNet.Fw.Extensions` | The reflection/type-scanning helpers this package's discovery code is written against. |

## Testing notes

Patterns actually used in this package's own test suite:

- **In-memory provider for pure model/behaviour tests**: `UseInMemoryDatabase("name")` for data-seeding,
  exception-handler, and snapshot tests that don't depend on real SQL. Use a fresh, unique database name per
  test (`+ Guid.NewGuid()`) rather than a shared fixture when isolation matters.
- **SQLite for model-build/query-filter tests**: `UseSqlite("Data Source=:memory:")` when the test needs a real
  relational `ModelBuilder`/`OnModelCreating` pass (e.g. triggering `GlobalQueryFilter.Apply` by touching
  `context.Model`).
- **A shared class fixture** for the snapshot/navigation tests, per project convention of `IAsyncLifetime`
  fixtures only when isolation matters — a plain class fixture is fine when it isn't.
- **Real-provider coverage** for the recoverable `DbUpdateConcurrencyException.Entries`-populated branch of
  `EfCoreExceptionHandler`, which cannot be constructed from a test double because `Entries` wraps an internal
  EF Core type — that branch is deliberately split out to a real-database test.
- **Test entity model**: `User : BaseEntity : AuditedEntity<int>, IConcurrencyEntity<byte[]>`;
  `GuidEntity : Entity<Guid>` for GUID-key generator coverage; a `[SqlSequence]`-decorated test enum; an
  `[IgnoreEntity]`-decorated entity specifically to prove the attribute has no effect here.
- **Assertion style**: Shouldly (`ShouldBe`, `ShouldContain`, `ShouldThrowAsync`, `Should.Throw`), xUnit `[Fact]`,
  naming `MethodName_Scenario_ExpectedBehavior` per repo convention.
