using DKNet.EfCore.Abstractions.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AspCore.Extensions.Tests.TestEntities;

/// <summary>Guid-keyed entity used to exercise <c>MapGetById</c> / <c>MapGetList</c> against a real EF Core store.</summary>
public sealed class WidgetEntity : Entity
{
    #region Constructors

    public WidgetEntity()
    {
    }

    public WidgetEntity(Guid id, string name) : base(id) => Name = name;

    #endregion

    #region Properties

    public string Name { get; set; } = string.Empty;

    #endregion
}

/// <summary>Projection model <see cref="WidgetEntity" /> maps to by convention (matching property names).</summary>
public sealed class WidgetModel
{
    #region Properties

    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    #endregion
}

/// <summary>
///     Audited, Guid-keyed entity used to exercise <c>MapGetList</c>'s newest-first (<c>CreatedOn</c> desc,
///     <c>Id</c> tie-break) ordering fallback against a real EF Core store.
/// </summary>
public sealed class GadgetEntity : AuditedEntity
{
    #region Constructors

    public GadgetEntity()
    {
    }

    public GadgetEntity(Guid id, string name, string createdBy, DateTimeOffset createdOn) : base(id)
    {
        Name = name;
        SetCreatedBy(createdBy, createdOn);
    }

    #endregion

    #region Properties

    public string Name { get; set; } = string.Empty;

    #endregion
}

/// <summary>Projection model <see cref="GadgetEntity" /> maps to by convention (matching property names).</summary>
public sealed class GadgetModel
{
    #region Properties

    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public DateTimeOffset CreatedOn { get; init; }

    #endregion
}

public sealed class WidgetDbContext(DbContextOptions<WidgetDbContext> options) : DbContext(options)
{
    #region Properties

    public DbSet<WidgetEntity> Widgets => Set<WidgetEntity>();

    public DbSet<GadgetEntity> Gadgets => Set<GadgetEntity>();

    public DbSet<SprocketEntity> Sprockets => Set<SprocketEntity>();

    public DbSet<CouponEntity> Coupons => Set<CouponEntity>();

    public DbSet<OrderEntity> Orders => Set<OrderEntity>();

    public DbSet<InvoiceEntity> Invoices => Set<InvoiceEntity>();

    #endregion

    #region Methods

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Sqlite's default DateTimeOffset mapping supports equality only, not ordering comparisons — the
        // provider throws "could not be translated" on '>=' / '<='. Converting to UTC ticks for storage keeps
        // ordering translatable for the Sqlite-backed parameterization tests; the InMemory-backed HTTP hosts
        // are unaffected either way (LINQ-to-objects needs no such conversion).
        DateTimeOffsetToUtcTicks(modelBuilder.Entity<OrderEntity>().Property(o => o.CreatedOn));
        DateTimeOffsetToUtcTicks(modelBuilder.Entity<OrderEntity>().Property(o => o.UpdatedOn));
        DateTimeOffsetToUtcTicks(modelBuilder.Entity<InvoiceEntity>().Property(i => i.CreatedOn));
        DateTimeOffsetToUtcTicks(modelBuilder.Entity<InvoiceEntity>().Property(i => i.UpdatedOn));
    }

    private static void DateTimeOffsetToUtcTicks(PropertyBuilder<DateTimeOffset> property) =>
        property.HasConversion(v => v.UtcTicks, v => new DateTimeOffset(v, TimeSpan.Zero));

    private static void DateTimeOffsetToUtcTicks(PropertyBuilder<DateTimeOffset?> property) =>
        property.HasConversion(
            v => v.HasValue ? v.Value.UtcTicks : (long?)null,
            v => v.HasValue ? new DateTimeOffset(v.Value, TimeSpan.Zero) : (DateTimeOffset?)null);

    #endregion
}
