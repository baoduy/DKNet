---
name: dknet-efcore-domain-model
description: "Covers DKNet.EfCore.Abstractions, DKNet.EfCore.Extensions and DKNet.EfCore.Relational.Helpers: Entity<TKey>/Entity/AuditedEntity base classes, IConcurrencyEntity/ISoftDeletableEntity, GUID v7 keys, and the [RaisesEvent] declaration, [Sequence]/[SqlSequence], [AuditLog]/[SensitiveData] attributes; UseAutoConfigModel, IEntityTypeConfiguration discovery via DefaultEntityTypeConfiguration, global query filter (GlobalQueryFilter), data seeding, SQL sequences (NextSeqValue), SnapshotContext, SaveChangesWithConcurrencyHandlingAsync, role-aware [SensitiveData] JSON, and CreateTableAsync/TableExistsAsync for schema work outside migrations. Use when modeling an entity or aggregate, wiring DbContext model discovery, adding a concurrency token or soft delete, seeding reference data, generating sequence-backed codes, or provisioning a table without a migration."
license: MIT
metadata:
  author: baoduy
  version: "0.1.0"
  packages: "DKNet.EfCore.Abstractions,DKNet.EfCore.Extensions,DKNet.EfCore.Relational.Helpers"
---

# DKNet entity base classes and EF Core model wiring

This skill teaches the entity/audit/concurrency/soft-delete/event contracts a DKNet aggregate is built from, and how `DbContext` model-building, query filters, data seeding, sequences and schema bookkeeping wire up around them, without a hand-called `OnModelCreating`. It does not cover dispatching the events an entity queues, the audit-log trail, row-level ownership, column encryption, querying/persisting the model, or the CRUD-attribute generators — each has its own reference under `references/` with full member tables, diagnostics and gotchas; open the matching one before writing non-trivial code against a given package.

## Packages

| Package | Install | What it gives you | Depends on | Reference |
|---|---|---|---|---|
| `DKNet.EfCore.Abstractions` | `dotnet add package DKNet.EfCore.Abstractions` | `Entity<TKey>`/`Entity`/`AuditedEntity` base classes; `IConcurrencyEntity<T>`/`ISoftDeletableEntity`/`IAuditedProperties` contracts; `[RaisesEvent]`, `[Sequence]`/`[SqlSequence]`, `[AuditLog]`/`[SensitiveData]`, `[IgnoreEntity]` attributes | none | [references/DKNet.EfCore.Abstractions.md](references/DKNet.EfCore.Abstractions.md) |
| `DKNet.EfCore.Extensions` | `dotnet add package DKNet.EfCore.Extensions` | `UseAutoConfigModel`/`UseAutoDataSeeding`, `DefaultEntityTypeConfiguration<T>`, `GlobalQueryFilter`, `DataSeedingConfiguration<T>`, GUID v7 keys, SQL sequences (`NextSeqValue`), `SnapshotContext`, `SaveChangesWithConcurrencyHandlingAsync`, `AddNewEntitiesFromNavigations`, role-aware `[SensitiveData]` JSON filtering | `DKNet.Fw.Extensions`, `DKNet.EfCore.Abstractions` | [references/DKNet.EfCore.Extensions.md](references/DKNet.EfCore.Extensions.md) |
| `DKNet.EfCore.Relational.Helpers` | `dotnet add package DKNet.EfCore.Relational.Helpers` | `CreateTableAsync`/`TableExistsAsync`/`GetTableName`/`GetDbConnection` — schema bookkeeping outside migrations | `DKNet.EfCore.Extensions` | [references/DKNet.EfCore.Relational.Helpers.md](references/DKNet.EfCore.Relational.Helpers.md) |

## Quick start

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.Extensions.Configurations;

public class Product : Entity // Entity<Guid>
{
    private Product() { } // EF Core

    public Product(string name) : base(Guid.NewGuid()) => Name = name;

    public string Name { get; private set; } = string.Empty;
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

public static class QuickStartApp
{
    public static async Task RunAsync()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o =>
            // AddDbContext's lambda hands you the non-generic DbContextOptionsBuilder, so pass the assembly
            // explicitly — the zero-arg UseAutoConfigModel<AppDbContext>() needs a generic receiver you don't have here.
            o.UseSqlServer("Server=.;Database=App;Trusted_Connection=True;")
             .UseAutoConfigModel([typeof(AppDbContext).Assembly]));

        await using var provider = services.BuildServiceProvider();
        var db = provider.GetRequiredService<AppDbContext>();

        db.Products.Add(new Product("Widget"));
        await db.SaveChangesAsync();
    }
}
```

`ProductConfiguration` is discovered and applied automatically — nothing calls `modelBuilder.ApplyConfiguration` by hand. `base.Configure(builder)` is what wires the `Guid` key to `GuidV7ValueGenerator`; skip it and `Id` stays unconfigured.

## Rules

1. Derive aggregates/entities from `Entity<TKey>`/`Entity` or `AuditedEntity<TKey>`/`AuditedEntity` — never `AggregateRoot`. It does not exist anywhere in this codebase, no matter what an older doc or memory says.
2. `UseAutoConfigModel` is a `DbContextOptionsBuilder` extension, called inside the `options =>` lambda passed to `AddDbContext`/`UseSqlServer` — never chained onto the `IServiceCollection` result. That lambda's parameter is always the non-generic `DbContextOptionsBuilder`, so only the array-of-assemblies overload resolves there (`options.UseAutoConfigModel([typeof(TContext).Assembly])`); the zero-arg generic convenience `UseAutoConfigModel<TContext>()` needs a `DbContextOptionsBuilder<TContext>` receiver, which only a directly-constructed `new DbContextOptionsBuilder<TContext>()` gives you.
3. An entity needs a `DbSet<T>` property or an `IEntityTypeConfiguration<T>` to enter the model. Auto-configuration discovers *configurations*, not entities, and `[IgnoreEntity]` excludes nothing here.
4. Always call `base.Configure(builder)` first inside a `DefaultEntityTypeConfiguration<TEntity>` override — it wires the key's value generator, the audit columns, and the concurrency token before your own mapping runs.
5. `AddEvent`/`[RaisesEvent]` only queue an event on the entity. Dispatch is `DKNet.EfCore.Events`' job (see `dknet-efcore-save-pipeline`) — referencing only the packages this skill owns compiles and runs, but publishes nothing.
6. This skill's contracts ship no query filter. Write your own `GlobalQueryFilter` for `ISoftDeletableEntity` — there is no built-in soft-delete filter to opt into.
7. `[Sequence]`/`[SqlSequence]` take effect only on SQL Server or Npgsql (`context.IsSqlServer()`/`IsNpgsql()`). Every other provider gets no sequence at model build, and `NextSeqValue` then throws `NotSupportedException` at call time, not at startup.
8. `[SensitiveData]` changes nothing about API responses until the host calls `UseRoleAwareSensitiveData` on its `JsonSerializerOptions` — and once it does, a property with no role named still serializes for *any* authenticated caller, not "no one".
9. `TableExistsAsync<TEntity>` throws `InvalidOperationException` for an entity that is not part of the model — it does not return `false`.
10. `CreateTableAsync<TEntity>` is not a migration and is not scoped to `TEntity`: if any table in the model is missing, it creates every missing table; if `TEntity`'s own table already exists, it returns immediately without checking whether some other, newer entity type still has no table.
11. Never hand-roll a repository interface on these packages. `DKNet.EfCore.Repos`/`.Repos.Abstractions` are removed from the solution — the persistence entry point is `DKNet.EfCore.Specifications` (see `dknet-efcore-specifications`).
12. Don't hand-write the request/handler/endpoint for a `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]`-marked member — that belongs to `dknet-codegen`; this skill only supplies the attribute types.

## How to

### Model an aggregate that raises domain events

When: state changes on an entity should queue a domain event, explicitly or by declared convention.

```csharp
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.Abstractions.Events;

[RaisesEvent(EventOperations.Created)]                                  // -> OrderCreatedEvent
[RaisesEvent("Shipped", EventOperations.Updated, nameof(Order.Status))] // -> OrderShippedStatusUpdatedEvent
public class Order : Entity
{
    private Order() { }

    public Order(string customer) : base(Guid.NewGuid())
    {
        Customer = customer;
        AddEvent(new OrderPlacedEvent(Id)); // explicit event, queued immediately
    }

    public string Customer { get; private set; } = string.Empty;
    public string Status { get; set; } = "Pending";
}

public record OrderPlacedEvent(Guid OrderId);
```

Notes: `AddEvent` only appends to the entity's own queue; nothing publishes until `DKNet.EfCore.Events`' save hook drains it (`dknet-efcore-save-pipeline`). `[RaisesEvent]`'s string argument is a label, not the emitted type's name — `DKNet.EfCore.DtoGenerator` composes the real name (`EntityName` + label + narrowed properties + `Created`/`Updated`/`Deleted` + `Event`). `AddEvent<TEvent>()` needs a registered `IMapper` at dispatch time; `AddEvent(object)` does not.

### Add audit tracking and a concurrency token together

When: an entity needs first-write-wins `CreatedBy`/`CreatedOn` + `UpdatedBy`/`UpdatedOn`, and optimistic concurrency so a stale update doesn't silently overwrite a newer one.

```csharp
using DKNet.EfCore.Abstractions.Entities;

public class Invoice : AuditedEntity, IConcurrencyEntity<byte[]>
{
    private Invoice() { } // Id stays unset here - GuidV7ValueGenerator fills it in on insert

    public static Invoice Create(string createdBy, decimal amount)
    {
        var invoice = new Invoice { Amount = amount };
        invoice.SetCreatedBy(createdBy); // no-op if CreatedBy is already set
        return invoice;
    }

    public decimal Amount { get; private set; }
    public byte[]? RowVersion { get; private set; }

    public void SetRowVersion(byte[] rowVersion) => RowVersion = rowVersion;
    public void MarkPaid(string updatedBy) => SetUpdatedBy(updatedBy);
}
```

Notes: implementing `IConcurrencyEntity<T>` alone changes nothing in the database — `DefaultEntityTypeConfiguration<TEntity>` must detect it by reflection and configure `RowVersion` as a `[Timestamp]`/`ValueGeneratedOnAddOrUpdate()` token (see the auto-discovery recipe below). `SetUpdatedBy` is monotonic: a call with an `updatedOn` older than the stored value silently no-ops *before* validating `userName`, so even a blank name doesn't throw on that path.

### Soft-delete via a global query filter

When: hide records without physically deleting them, without hand-writing `HasQueryFilter` on every entity configuration.

```csharp
using System.Linq.Expressions;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.Extensions.Configurations;

public class Document : Entity, ISoftDeletableEntity
{
    private Document() { }
    public Document(string name) : base(Guid.NewGuid()) => Name = name;

    public string Name { get; private set; } = string.Empty;
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedOn { get; private set; }
    public string? DeletedBy { get; private set; }

    public IResultBase Delete(string byUser, DateTimeOffset? deletedOn = null)
    {
        IsDeleted = true;
        DeletedBy = byUser;
        DeletedOn = deletedOn ?? DateTimeOffset.UtcNow;
        return Result.Ok();
    }
}

internal sealed class SoftDeleteFilter : GlobalQueryFilter
{
    public override string FilterKey => nameof(SoftDeleteFilter);

    protected override IEnumerable<IMutableEntityType> GetEntityTypes(ModelBuilder modelBuilder) =>
        modelBuilder.Model.GetEntityTypes()
            .Where(t => typeof(ISoftDeletableEntity).IsAssignableFrom(t.ClrType));

    protected override Expression<Func<TEntity, bool>>? HasQueryFilter<TEntity>(DbContext context) =>
        e => !((ISoftDeletableEntity)e).IsDeleted;
}
```

Notes: `SoftDeleteFilter` needs no DI registration — a non-abstract, parameterless-constructible `IGlobalModelBuilder` in a scanned assembly is instantiated automatically. `IsIgnorable` defaults to `true` (callers can bypass it via `ISpecification.IsIgnoreQueryFilters()`); override it to `false` for a filter that must never be bypassable, such as row-level ownership.

### Auto-discover entity configurations across assemblies

When: a modular app's `IEntityTypeConfiguration<T>` classes live in more than one assembly, or you want to confirm the numeric-key path (no GUID generator involved) alongside the GUID one from Quick start.

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DKNet.EfCore.Extensions.Configurations;

public class Warehouse
{
    public int Id { get; set; }
    public string Location { get; set; } = string.Empty;
}

public class WarehouseConfiguration : DefaultEntityTypeConfiguration<Warehouse>
{
    public override void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        base.Configure(builder); // Id is numeric -> ValueGeneratedOnAdd(), no GUID generator involved
        builder.Property(w => w.Location).HasMaxLength(255);
    }
}

public class WarehouseDbContext(DbContextOptions<WarehouseDbContext> options) : DbContext(options)
{
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
}

public static class WarehouseApp
{
    public static async Task RunAsync()
    {
        var optionsBuilder = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseSqlServer("Server=.;Database=App;Trusted_Connection=True;")
            .UseAutoConfigModel<WarehouseDbContext>(typeof(Warehouse).Assembly, typeof(WarehouseConfiguration).Assembly);

        await using var db = new WarehouseDbContext(optionsBuilder.Options);
        db.Warehouses.Add(new Warehouse { Location = "Seattle" });
        await db.SaveChangesAsync();
    }
}
```

Notes: an entity with no `IEntityTypeConfiguration<T>` and no `DbSet<T>` stays invisible to the model regardless of how many assemblies you pass — this call finds configurations, not entities. Passing no assemblies at all defaults to `[typeof(TContext).Assembly]`.

### Seed reference data at startup

When: idempotent lookup/reference data (countries, statuses) should exist after `Migrate`/`EnsureCreated`, without a bespoke seeding script.

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

public class SeedDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<Country> Countries => Set<Country>();
}

public static class SeedApp
{
    public static async Task RunAsync()
    {
        var optionsBuilder = new DbContextOptionsBuilder<SeedDbContext>()
            .UseSqlServer("Server=.;Database=App;Trusted_Connection=True;")
            .UseAutoConfigModel<SeedDbContext>()
            .UseAutoDataSeeding([typeof(CountrySeed).Assembly]);

        await using var db = new SeedDbContext(optionsBuilder.Options);
        await db.Database.MigrateAsync(); // UseSeeding/UseAsyncSeeding run as part of EnsureCreated/Migrate
    }
}
```

Notes: `UseAutoDataSeeding` is a separate opt-in from `UseAutoConfigModel` — neither implies the other. `DataSeedingConfiguration<T>` dedupes candidates against the database **by primary key**, not by `TEntity` equality, so re-running the seeder never re-inserts rows even without an `Equals` override. `Order` exists on the interface but is never read; seeders run in assembly-scan order. `SeedDbContext` takes the non-generic `DbContextOptions`, not `DbContextOptions<SeedDbContext>`: `UseAutoDataSeeding` only has a `DbContextOptionsBuilder` (non-generic) overload, so chaining it after `UseAutoConfigModel<SeedDbContext>()` collapses the expression's static type back to non-generic — `optionsBuilder.Options` below is a plain `DbContextOptions`.

### Generate sequence-backed codes

When: a human-facing code (ticket number, invoice number) must come from a real database sequence, not an application-side counter.

```csharp
using Microsoft.EntityFrameworkCore;
using DKNet.EfCore.Abstractions.Attributes;

[SqlSequence] // schema "seq" (default)
public enum TicketNumberSeq
{
    [Sequence(StartAt = 1000, IncrementsBy = 1)]
    Support
}

public class SequenceDbContext(DbContextOptions<SequenceDbContext> options) : DbContext(options);

public static class TicketApp
{
    public static async Task RunAsync()
    {
        var options = new DbContextOptionsBuilder<SequenceDbContext>()
            .UseSqlServer("Server=.;Database=App;Trusted_Connection=True;")
            .UseAutoConfigModel<SequenceDbContext>()
            .Options;

        await using var db = new SequenceDbContext(options);
        var next = await db.NextSeqValue<TicketNumberSeq, int>(TicketNumberSeq.Support);
    }
}
```

Notes: `[Sequence]` targets the enum *member*, not an entity property. Sequence registration (and `NextSeqValue`) only works on SQL Server or Npgsql — every other provider gets no sequence on the model, and `NextSeqValue` throws `NotSupportedException` at call time. Need a formatted code like `"SUP-2026-001000"` instead of a bare number? Use `NextSeqValueWithFormat<TEnum>` and set `[Sequence(FormatString = "...")]`.

### Withhold a sensitive property from unauthorized callers

When: one property (e.g. a supplier cost) must serialize only for callers holding a given role.

```csharp
using System.Security.Claims;
using DKNet.EfCore.Abstractions.Attributes;
using DKNet.EfCore.Extensions.Serialization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

public class Employee
{
    public string Name { get; set; } = string.Empty;

    [SensitiveData("payroll")]
    public decimal Salary { get; set; }
}

internal sealed class HttpContextSensitiveDataPrincipalAccessor(IHttpContextAccessor httpContextAccessor)
    : ISensitiveDataPrincipalAccessor
{
    public ClaimsPrincipal? Current => httpContextAccessor.HttpContext?.User;
}

public static class SensitiveDataStartup
{
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

Notes: a withheld property is **absent** from the JSON, not `null` or redacted text. `UseRoleAwareSensitiveData` must run before the `JsonSerializerOptions` instance first serializes anything, or `System.Text.Json` itself throws `InvalidOperationException`. `[SensitiveData]` alone does not encrypt anything — that's `DKNet.EfCore.Encryption`'s unrelated `[Encrypted]` attribute (`dknet-efcore-data-security`).

### Provision a table without a migration

When: a dev/test environment, seed routine, or health check needs the schema to exist and migrations aren't in play yet.

```csharp
using Microsoft.EntityFrameworkCore;
using DKNet.EfCore.Relational.Helpers;

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlServer("Server=.;Database=App;Trusted_Connection=True;")
    .Options;
await using var db = new AppDbContext(options);

if (!await db.TableExistsAsync<Product>())
{
    await db.CreateTableAsync<Product>();
}
```

Notes: `CreateTableAsync<TEntity>` creates **every** missing table in the model, not only `TEntity`'s, and does nothing at all once `TEntity`'s own table exists — it will not backfill a table added to the model later. This is not `dbContext.Database.Migrate()`; mixing the two against the same database leaves `__EFMigrationsHistory` missing or inconsistent.

## Runtime behaviour

Model build, once per `DbContext` model, after `UseAutoConfigModel` swaps in the auto-config `IModelCustomizer`:

1. Resolve the registered assembly list (default: `typeof(TContext).Assembly`).
2. `ApplyConfigurationsFromAssembly` per assembly — applies every `IEntityTypeConfiguration<T>`, including `DefaultEntityTypeConfiguration<T>.Configure`'s reflection-based Id/audit/concurrency wiring.
3. Instantiate and apply every discovered `IGlobalModelBuilder` (assembly-scanned, de-duplicated with anything added via `AddGlobalModelBuilder<T>()`).
4. On SQL Server/Npgsql only: turn every `[SqlSequence]`/`[Sequence]` enum into `modelBuilder.HasSequence(...)`.
5. Delegate to the original `IModelCustomizer` so a provider's own customizer still runs.

Domain-event queueing is decoupled from all of the above: `AddEvent`/`[RaisesEvent]` only append to the entity's in-memory queue at any point before `SaveChangesAsync`; nothing drains or publishes that queue without `DKNet.EfCore.Events` registered (`dknet-efcore-save-pipeline`) — a project referencing only the packages this skill owns compiles and runs but never publishes anything. Likewise, `AuditedEntity.SetCreatedBy`/`SetUpdatedBy` are plain setters your own domain code calls; the save-pipeline hooks that stamp those fields from a signed-in user or an ownership key write the tracked property value directly and never call these setters.

## Gotchas

- **`Entity<TKey>.Id` has a `private` setter.** There is no `SetId`/`WithId` — set it only via the `protected Entity(TKey id)` constructor, from a derived type's own constructor.
- **Auto-configuration finds configurations, not entities.** No `DbSet<T>` and no `IEntityTypeConfiguration<T>` means the type is invisible to the model, full stop — `[IgnoreEntity]` does not additionally exclude anything because nothing in this package's discovery code reads it.
- **Audit columns are capped at 255 chars, not the 500 the interface annotates.** `IAuditedProperties`' `[MaxLength(500)]` loses to `DefaultEntityTypeConfiguration`'s `.HasMaxLength(255)` fluent call.
- **`[Sequence]`'s numeric defaults are `-1` ("leave it to the database"), not `0`.** Only values `> 0` on `StartAt`/`IncrementsBy`/`Min`/`Max` are applied; `Cyclic` has no such sentinel and is always applied.
- **Sequences and `NextSeqValue` are SQL Server/Npgsql only, silently.** SQLite/InMemory get no sequence at model build; `NextSeqValue` then throws `NotSupportedException` the first time it's actually called.
- **`AddGlobalModelBuilder<T>()` still can't supply constructor arguments.** It goes through the same `Activator.CreateInstance` as assembly-scanned discovery — it only adds a filter from *outside* the scanned assemblies, and a filter with no public parameterless constructor still crashes model build with `MissingMethodException` either way.
- **`DataSeedingConfiguration<T>.Order` is decorative.** It is declared, never read; seeders run in assembly-scan order, not by `Order`.
- **`TableExistsAsync`/`CreateTableAsync` disagree on how they fail.** `TableExistsAsync<TEntity>` throws for an unmapped entity; `GetTableName<TEntity>` instead returns `(null, null)` and never throws. `CreateTableAsync` is not scoped to `TEntity` in either direction — see Rule 10.
- **`GetDbConnection` opens a closed connection but never closes it.** Close it yourself if you only needed it for one command.
- **A frozen `JsonSerializerOptions` fails `UseRoleAwareSensitiveData` at opt-in time, not at serialize time.**

## Do not

- Write `class Order : AggregateRoot` — `AggregateRoot` does not exist in this codebase:
  ```csharp
  // no-compile
  public class Item : AggregateRoot // AggregateRoot does not exist; derive from Entity<TKey> / Entity instead
  {
  }
  ```
- Chain `UseAutoConfigModel` off `IServiceCollection`:
  ```csharp
  // no-compile
  services.AddDbContext<AppDbContext>().UseAutoConfigModel<AppDbContext>(); // not a member of IServiceCollection
  ```
- Assume `[RaisesEvent("CustomerTouched", EventOperations.Created)]` emits a type literally named `CustomerTouched` — it composes `CustomerTouchedCreatedEvent`; the string is a label, not the event name.
- Assume `[IgnoreEntity]` hides a type from `UseAutoConfigModel` or from any generator — it has no shipped consumer anywhere in the framework today. Remove the `DbSet`/configuration instead.
- Reach for `DKNet.EfCore.Repos` or `DKNet.EfCore.Repos.Abstractions` — both are removed from the solution; use `DKNet.EfCore.Specifications` (`dknet-efcore-specifications`).
- Invent a `GuidValueGenerator` type — the only value generator this skill's packages ship is `GuidV7ValueGenerator` (time-ordered GUID v7, not `Guid.NewGuid()`'s random v4).
- Assume `TableExistsAsync<TEntity>` returns `false` for an entity that isn't in the model — it throws `InvalidOperationException`.
- Assume `EfCoreExceptionHandler.HandlingAsync` string-matches the concurrency exception's message — it decides purely from `exception.Entries.Count`.

## Related skills

- `dknet-packages` — the package router; start there to pick a package before diving into any specific skill.
- `dknet-efcore-specifications` — querying/persisting the entities this skill defines; the repository entry point once modelling is done.
- `dknet-efcore-save-pipeline` — dispatches the domain events `AddEvent`/`[RaisesEvent]` only queue here, and the audit-log trail for `[AuditLog]`/`[SensitiveData]`.
- `dknet-efcore-data-security` — row-level ownership filtering (which also stamps `CreatedBy`/`UpdatedBy`) and column encryption; neither is implemented by this skill's packages.
- `dknet-codegen` — the `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]`/`[GenerateDto]` generators that consume this skill's attributes; apply the attributes there, not by hand-writing the generated members.
- `dknet-slimbus-cqrs` — CQRS handlers that fetch, mutate and persist the aggregates modelled here.
- `dknet-aspcore-api` — minimal-API endpoint conventions once a model built here needs exposing over HTTP.
- `dknet-idempotency` — idempotent endpoints sitting in front of the same `DbContext`.
- `dknet-blob-storage` — a separate persistence concern (files, not rows); don't model a blob as an entity here.
- `dknet-services` — encryption/PDF/template services that live outside the EF Core model.
- `dknet-core-utilities` — the reflection/type-scanning helpers this skill's auto-discovery is built on.
- `dknet-testing` — TestContainers fixtures and the SQLite/InMemory choices for testing entities and configurations defined here.

## References

- [references/DKNet.EfCore.Abstractions.md](references/DKNet.EfCore.Abstractions.md) — entity/event/audit/concurrency/soft-delete contracts and attributes: full member tables, diagnostics IDs, gotchas.
- [references/DKNet.EfCore.Extensions.md](references/DKNet.EfCore.Extensions.md) — `UseAutoConfigModel`/`UseAutoDataSeeding`, `DefaultEntityTypeConfiguration`, `GlobalQueryFilter`, sequences, `SnapshotContext`, concurrency retry, sensitive-data JSON: full member tables, diagnostics, gotchas.
- [references/DKNet.EfCore.Relational.Helpers.md](references/DKNet.EfCore.Relational.Helpers.md) — the four `DbContextHelpers` methods: signatures, runtime behaviour, diagnostics.
- Docs site: https://baoduy.github.io/DKNet/ · package pages: https://github.com/baoduy/DKNet/blob/main/docs/EfCore/DKNet.EfCore.Abstractions.md, https://github.com/baoduy/DKNet/blob/main/docs/EfCore/DKNet.EfCore.Extensions.md, https://github.com/baoduy/DKNet/blob/main/docs/EfCore/DKNet.EfCore.Relational.Helpers.md.
- NuGet: https://www.nuget.org/packages/DKNet.EfCore.Abstractions, https://www.nuget.org/packages/DKNet.EfCore.Extensions, https://www.nuget.org/packages/DKNet.EfCore.Relational.Helpers.
