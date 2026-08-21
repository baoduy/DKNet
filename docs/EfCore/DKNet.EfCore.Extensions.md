## DKNet.EfCore.Extensions

`DKNet.EfCore.Extensions` is the wiring layer for the EF Core area of DKNet. On its own it provides
convention-based entity configuration discovery, global query filters, data seeding, time-ordered GUID
keys, SQL sequence support and a couple of `DbContext`/`EntityEntry` helper extensions. Just as importantly,
it defines the `SnapshotContext` type that `DKNet.EfCore.Hooks` (and, through it, `DKNet.EfCore.Events`,
`DKNet.EfCore.AuditLogs` and `DKNet.EfCore.DataAuthorization`) pass into every save-pipeline hook — so this
package is a dependency of most of the rest of the EfCore family even when you never call its APIs directly.

### When to reach for it

- You want entity configurations (`IEntityTypeConfiguration<T>`) applied automatically from one or more
  assemblies instead of calling `modelBuilder.ApplyConfigurationsFromAssembly(...)` yourself in every
  `OnModelCreating`.
- You want a common base (`DefaultEntityTypeConfiguration<T>`) that wires up primary keys, GUID v7 value
  generation, audit columns and row-version concurrency tokens by convention.
- You need cross-cutting query filters (soft delete, tenant/ownership isolation) applied to every matching
  entity type without repeating `HasQueryFilter` in each configuration.
- You want structured, idempotent-by-design data seeding that plugs into EF Core's native
  `UseSeeding`/`UseAsyncSeeding` pipeline.
- You need SQL Server/PostgreSQL sequences declared from an enum instead of raw migrations SQL.
- You are building another EF Core add-on (a hook, an audit log, a data-authorization filter) and need the
  shared `SnapshotContext` abstraction that the rest of the family already speaks.

### Install and minimum registration

```bash
dotnet add package DKNet.EfCore.Extensions
```

The package depends on `DKNet.EfCore.Abstractions` and `DKNet.Fw.Extensions`; both come along transitively.

The entry point is `UseAutoConfigModel`, an extension on `DbContextOptionsBuilder` declared in
`EfCoreSetup.cs`:

```csharp
public static DbContextOptionsBuilder<TContext> UseAutoConfigModel<TContext>(
    this DbContextOptionsBuilder<TContext> @this,
    params Assembly[]? assemblies)
    where TContext : DbContext;

public static DbContextOptionsBuilder UseAutoConfigModel(
    this DbContextOptionsBuilder @this,
    Assembly[] assemblies);
```

Minimum registration — no assemblies means "scan the assembly the `DbContext` lives in":

```csharp
services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString)
           .UseAutoConfigModel<AppDbContext>());
```

Multi-assembly (modular/bounded-context) registration:

```csharp
options.UseSqlServer(connectionString)
       .UseAutoConfigModel<AppDbContext>(
           typeof(Product).Assembly,
           typeof(Customer).Assembly);
```

`UseAutoConfigModel` does not add entity configuration logic to `OnModelCreating` itself — it stores the
assembly list as a `IDbContextOptionsExtension` and replaces EF Core's `IModelCustomizer` with
`AutoConfigModelCustomizer`, which runs the discovery/registration steps below once per model build, then
delegates to the original customizer (so it composes with a provider's own customizer, e.g. Npgsql's).

### Features

#### 1. Automatic entity configuration discovery

`AutoConfigModelCustomizer.Customize` calls `modelBuilder.ApplyConfigurationsFromAssembly(assembly)` (EF
Core's own scanner) for every assembly registered via `UseAutoConfigModel`. In practice this means: write
one `IEntityTypeConfiguration<T>` per entity (anywhere in a scanned assembly) and it is picked up without an
explicit call in `OnModelCreating`. This package does **not** invent entities out of thin air — an entity
still needs either an explicit `IEntityTypeConfiguration<T>` or a `DbSet<T>` property for EF Core to know
about it; "auto configuration" only automates *applying the configuration classes you already wrote*.

`DefaultEntityTypeConfiguration<TEntity>` (in `Configurations/DefaultEntityTypeConfiguration.cs`) is a base
class for those configuration classes that wires up conventions so you don't repeat them per entity:

```csharp
public abstract class DefaultEntityTypeConfiguration<TEntity> : IEntityTypeConfiguration<TEntity>
    where TEntity : class
{
    public virtual void Configure(EntityTypeBuilder<TEntity> builder);
}
```

It, based on reflection over `TEntity`:

- Sets the primary key from an `Id` property if one exists; numeric ids get `ValueGeneratedOnAdd()`, `Guid`
  ids get `ValueGeneratedOnAdd().HasValueGenerator<GuidV7ValueGenerator>()`.
- Configures `CreatedBy`/`CreatedOn` (required, ignored after save) and `UpdatedBy`/`UpdatedOn` when the
  entity implements `IAuditedProperties`.
- Configures `RowVersion` as a concurrency token / row-version column when the entity implements
  `IConcurrencyEntity<T>`.

```csharp
public class ProductConfiguration : DefaultEntityTypeConfiguration<Product>
{
    public override void Configure(EntityTypeBuilder<Product> builder)
    {
        base.Configure(builder); // Id, audit columns, concurrency token
        builder.Property(p => p.Name).HasMaxLength(255).IsRequired();
        builder.HasIndex(p => p.Sku).IsUnique();
    }
}
```

#### 2. Global query filters

`IGlobalModelBuilder` (`Configurations/IGlobalModelBuilder.cs`) is the extension point:

```csharp
public interface IGlobalModelBuilder
{
    void Apply(ModelBuilder modelBuilder, DbContext context);
}
```

Any non-abstract implementation found while scanning the registered assemblies is instantiated
(`Activator.CreateInstance`) and applied automatically during model build — no separate registration call is
required for assembly-discovered filters. You can additionally register one explicitly (useful when the
implementation needs constructor arguments EF's parameterless `Activator.CreateInstance` can't supply, or
when it lives outside the scanned assemblies) via:

```csharp
services.AddGlobalModelBuilder<MySoftDeleteFilter>();
```

`GlobalQueryFilter` (`Configurations/GlobalQueryFilter.cs`) is an abstract base that turns the low-level
`IGlobalModelBuilder.Apply` into a simpler per-entity-type contract:

```csharp
public abstract class GlobalQueryFilter : IGlobalModelBuilder
{
    public abstract string FilterKey { get; }
    public virtual bool IsIgnorable => true;

    protected abstract IEnumerable<IMutableEntityType> GetEntityTypes(ModelBuilder modelBuilder);
    protected abstract Expression<Func<TEntity, bool>>? HasQueryFilter<TEntity>(DbContext context)
        where TEntity : class;
}
```

`FilterKey` is EF Core 10's *named* query filter key (`HasQueryFilter(key, expression)`), which lets a
DbContext carry multiple, independently-toggleable filters on the same entity. `IsIgnorable` records (in a
static registry exposed as `GlobalQueryFilter.IgnorableFilterKeys`) whether specification code is allowed to
bypass this particular filter — see the DataAuthorization example below, which sets it to `false` so
row-level ownership can never be silently skipped.

```csharp
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

#### 3. Data seeding

`IDataSeedingConfiguration` (`Configurations/IDataSeedingConfiguration.cs`) describes a seed unit; the
abstract `DataSeedingConfiguration<TEntity>` base does the plumbing so you only implement `GetDataAsync`:

```csharp
public abstract class DataSeedingConfiguration<TEntity> : IDataSeedingConfiguration where TEntity : class
{
    public virtual int Order => 0;
    protected abstract ValueTask<ICollection<TEntity>> GetDataAsync(CancellationToken cancellation = default);
}
```

```csharp
public sealed class CountrySeed : DataSeedingConfiguration<Country>
{
    protected override ValueTask<ICollection<Country>> GetDataAsync(CancellationToken cancellation = default) =>
        ValueTask.FromResult<ICollection<Country>>([new Country("VN"), new Country("US")]);
}
```

Wire seeding in separately from `UseAutoConfigModel` — it is its own opt-in via
`EfCoreDataSeedingExtensions.UseAutoDataSeeding`:

```csharp
options.UseSqlServer(connectionString)
       .UseAutoConfigModel<AppDbContext>()
       .UseAutoDataSeeding([typeof(AppDbContext).Assembly]);
```

`UseAutoDataSeeding` discovers `IDataSeedingConfiguration` implementations in the given assemblies and
attaches them to EF Core's native `UseSeeding`/`UseAsyncSeeding` hooks (run by `EnsureCreated`/migration
flows), so seeding runs through the same mechanism as any other EF Core seed data, not a bespoke one.

#### 4. GUID v7 keys

`GuidV7ValueGenerator` (`Convertors/GuidV7ValueGenerator.cs`) is a `ValueGenerator<Guid>` whose `Next`
returns `Guid.CreateVersion7()` — a time-ordered GUID (RFC 9562 v7), which avoids the index-fragmentation
cost of random `Guid.NewGuid()` primary keys on typical clustered-index setups. You rarely construct it
directly: `DefaultEntityTypeConfiguration<TEntity>` attaches it automatically to any `Guid` `Id` property via
`.HasValueGenerator<GuidV7ValueGenerator>()`. To use it on an entity that isn't going through
`DefaultEntityTypeConfiguration`, attach it explicitly:

```csharp
builder.Property(e => e.Id).HasValueGenerator<GuidV7ValueGenerator>();
```

#### 5. Sequence support

`[SqlSequenceAttribute(schema)]` (on an enum, from `DKNet.EfCore.Abstractions`) plus `[SequenceAttribute]`
(on each enum member) declare one SQL sequence per member. `SequenceExtensions.RegisterSequences` (internal,
invoked automatically by `AutoConfigModelCustomizer` when the provider is SQL Server or Npgsql) turns them
into `modelBuilder.HasSequence(...)` calls:

```csharp
[SqlSequence("billing")]
public enum InvoiceSequences
{
    [Sequence(typeof(long), StartAt = 1000, IncrementsBy = 1)]
    InvoiceNumber
}
```

Read the next value at runtime with the `DbContext` extensions in `EfCoreExtensions.cs`:

```csharp
long? next = await db.NextSeqValue<InvoiceSequences, long>(InvoiceSequences.InvoiceNumber);
string formatted = await db.NextSeqValueWithFormat(InvoiceSequences.InvoiceNumber); // uses [Sequence].FormatString
```

`NextSeqValue` issues a raw `SELECT NEXT VALUE FOR ...` (SQL Server) or `SELECT nextval(...)` (Npgsql)
against `context.Database.GetDbConnection()`; it throws `NotSupportedException` on any other provider.

#### 6. Navigation helpers

`NavigationExtensions.cs` adds a handful of `EntityEntry`/`DbContext` extensions used to work with
change-tracked graphs without hand-rolled reflection:

- `EntityEntry.IsNewEntity()` — true when detached/added or when original key values are unset.
- `EntityEntry.HasProperty`, `GetOriginalValue`, `GetOriginalKeyValues`, `GetCurrentValue`,
  `GetCurrentKeyValues`.
- `DbContext.GetCollectionNavigations(Type)`, `GetNewEntitiesFromNavigations(...)` and
  `AddNewEntitiesFromNavigations(cancellationToken)` — walks tracked roots' collection navigations and
  `Add`s any reachable child that is still "new", so you don't need to call `.Add()` on every nested
  aggregate member by hand:

```csharp
db.Orders.Add(order); // order.Items contains brand-new OrderItem instances
await db.AddNewEntitiesFromNavigations(); // finds and stages order.Items automatically
await db.SaveChangesAsync();
```

#### 7. Exception handling (concurrency)

`IEfCoreExceptionHandler` / `EfCoreExceptionHandler` (`Extensions/EfCoreExceptionHandler.cs`) classify a
`DbUpdateConcurrencyException` into `RetrySaveChanges`, `IgnoreChanges` or `RethrowException`. The default
implementation retries (after reloading the DB's current values into `OriginalValues`) only when the
exception message contains `"but actually affected 0 row(s)"`; anything else is rethrown.
`EfSaveChangesExtension.SaveChangesWithConcurrencyHandlingAsync` drives the retry loop, bounded by
`IEfCoreExceptionHandler.MaxRetryCount` (default 3):

```csharp
var rows = await db.SaveChangesWithConcurrencyHandlingAsync(); // uses EfCoreExceptionHandler by default
```

Register a custom, per-`DbContext` handler through DI (keyed by the `DbContext`'s full type name):

```csharp
services.AddEfCoreExceptionHandler<AppDbContext, MyConcurrencyHandler>();
```

#### 8. SnapshotContext / SnapshotEntityEntry

`SnapshotContext` (`Snapshots/SnapshotContext.cs`) wraps a `DbContext` and, once `Initialize()` is called,
captures every `Added`/`Modified`/`Deleted` `EntityEntry` at that moment into a read-only list of
`SnapshotEntityEntry` (`Entry`, `Entity`, `OriginalState`). It's a plain point-in-time capture, not a
diffing/change-detection utility — `Initialize()` calls `ChangeTracker.DetectChanges()` once and stores the
result; `Entities` throws `InvalidOperationException` if read before `Initialize()`, and the type itself
throws `ObjectDisposedException` once disposed.

```csharp
await using var snapshot = new SnapshotContext(db);
snapshot.Initialize();
foreach (var e in snapshot.Entities)
    Console.WriteLine($"{e.Entity.GetType().Name}: {e.OriginalState}");
```

You will rarely construct this yourself in application code — see the next section for who does.

### Configuration options and defaults

| Setting | Default | Where |
|---|---|---|
| Assemblies scanned by `UseAutoConfigModel<TContext>()` (no args) | `[typeof(TContext).Assembly]` | `EfCoreSetup.UseAutoConfigModel` |
| `IEfCoreExceptionHandler.MaxRetryCount` | `3` | `EfCoreExceptionHandler`/interface default |
| `GlobalQueryFilter.IsIgnorable` | `true` (filter may be bypassed by spec code) | `GlobalQueryFilter` |
| `DataSeedingConfiguration<T>.Order` | `0` | `IDataSeedingConfiguration` |
| `SequenceAttribute.IncrementsBy` / `Min` / `Max` / `StartAt` | `-1` = "leave to the database default"; only values `> 0` are applied | `RegisterSequencesFromEnumType` |
| `SequenceAttribute.Cyclic` | `true` | `SequenceAttribute` |
| `SqlSequenceAttribute.Schema` | `"seq"` | `SqlSequenceAttribute` |
| Sequence registration | Only runs when `context.IsSqlServer()` or `context.IsNpgsql()` | `AutoConfigModelCustomizer` |

### How it composes with other packages

- **`DKNet.EfCore.Hooks`** is the primary consumer of `SnapshotContext`. `HookContext` (its internal
  save-pipeline coordinator) constructs `new SnapshotContext(db)` once per `SaveChanges` call and passes it
  to every registered hook through the interfaces declared in `IHook.cs`:

  ```csharp
  public interface IBeforeSaveHookAsync : IHookBaseAsync
  {
      Task BeforeSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default);
  }

  public interface IAfterSaveHookAsync : IHookBaseAsync
  {
      Task AfterSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default);
  }
  ```

  `DKNet.EfCore.Events` (`EventContext`/`EventHook`), `DKNet.EfCore.AuditLogs` (`EfCoreAuditHook`) and
  `DKNet.EfCore.DataAuthorization` (`DataOwnerHook`) are all built on top of these two interfaces, so the
  same `SnapshotContext` capture is what every one of those packages inspects — this package owns that
  shared vocabulary even though it has no hook logic of its own.
- **`DKNet.EfCore.DataAuthorization`** plugs directly into `GlobalQueryFilter`: its internal
  `DataOwnerAuthQuery : GlobalQueryFilter` sets `IsIgnorable = false` (row-level ownership must never be
  bypassable the way an ordinary spec-level `IsIgnoreQueryFilters` flag can bypass an ignorable filter) and
  filters every `IOwnedBy` entity type by `IDataOwnerDbContext.AccessibleKeys`/`IsUnrestrictedAccess`.
- **`DKNet.EfCore.Abstractions`**: `GuidV7ValueGenerator` is what makes `Entity : Entity<Guid>` (the
  Guid-keyed aggregate root base class in Abstractions) get sequential rather than random GUIDs, but only
  when the entity's `IEntityTypeConfiguration<T>` derives from this package's
  `DefaultEntityTypeConfiguration<T>` (or attaches the generator itself) — it is not automatic for every
  `Guid` property in the model. `DefaultEntityTypeConfiguration<T>` in turn depends on Abstractions'
  `IAuditedProperties` and `IConcurrencyEntity<T>` marker interfaces to decide what to configure.
- **`DKNet.EfCore.Repos`**: consumes whatever model this package builds (entity configurations, global
  filters, sequences already applied) — it doesn't call into this package's APIs directly.

### Gotchas and limits

- **Auto-configuration ≠ auto-discovery of entities.** Assembly scanning only finds and *applies*
  `IEntityTypeConfiguration<T>` classes (via EF Core's own `ApplyConfigurationsFromAssembly`); an entity with
  no configuration class and no `DbSet<T>` is simply not in the model.
- **Data seeding is a separate opt-in.** `UseAutoConfigModel` does not wire up seeding (the customizer's
  seeding line is intentionally left commented out in source) — call `UseAutoDataSeeding(assemblies)`
  explicitly.
- **`DataSeedingConfiguration<T>` dedupes with `EqualityComparer<TEntity>.Default`.** Unless `TEntity`
  overrides `Equals`/`GetHashCode` for value equality, this falls back to reference equality, so every
  freshly constructed seed instance will look "new" against the entities already loaded from the database —
  seeding can then try to re-insert the same logical row (and fail on a unique/PK constraint) on every
  startup. Give seeded entity types value equality (or a stable natural key comparer) if you rely on the
  built-in dedupe.
- **Sequences only work against SQL Server and Npgsql.** `RegisterSequences` silently does nothing on any
  other provider (e.g. SQLite/InMemory in tests); `NextSeqValue` throws `NotSupportedException` at runtime
  on unsupported providers instead of failing at model-build time.
- **`EfCoreExceptionHandler`'s retry heuristic string-matches the exception message** (`"but actually
  affected 0 row(s)"`), which is the message .NET's SQL Server/relational providers currently produce for
  a zero-row concurrency conflict — it is not guaranteed stable across provider versions. Anything else is
  rethrown as-is, and retries are capped by `MaxRetryCount` (default `3`), after which the method silently
  returns `0` rather than throwing.
- **`SnapshotContext` is a one-shot capture, not live tracking.** It must be `Initialize()`d before
  `Entities` is readable, and it only records `Added`/`Modified`/`Deleted` entries present at that instant —
  entities added to the context afterward are not retroactively included.
- **`IgnoreEntityAttribute`** (defined in `DKNet.EfCore.Abstractions`) exists but is not currently
  referenced anywhere in this package's discovery/customizer code — it isn't wired into
  `AutoConfigModelCustomizer`, so decorating an entity with it has no effect on auto-configuration today.
- **`AddGlobalModelBuilder<T>()` and assembly-scanned filters are merged and de-duplicated by type** (`Union(...).Distinct()`), so registering a filter both ways is harmless, but each filter is instantiated via `Activator.CreateInstance` — implementations needing constructor dependencies must be registered by hand and cannot rely on assembly scanning.
