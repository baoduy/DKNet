using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.Abstractions.Events;

namespace AspCore.Extensions.Tests.TestEntities;

/// <summary>
///     DbContext for the <c>MapDeleteById</c> relational scenarios (DRK-703 §5) — backed by SQLite rather than
///     the InMemory provider the rest of this test assembly uses, because InMemory does not enforce foreign
///     keys and would let the conflict scenario's referencing-row delete silently succeed.
/// </summary>
public sealed class DeleteByIdDbContext(DbContextOptions<DeleteByIdDbContext> options) : DbContext(options)
{
    #region Properties

    public DbSet<WidgetEntity> Widgets => Set<WidgetEntity>();

    public DbSet<StockRecordEntity> StockRecords => Set<StockRecordEntity>();

    public DbSet<AuditedWidgetEntity> AuditedWidgets => Set<AuditedWidgetEntity>();

    #endregion

    #region Methods

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Restrict, not the required-relationship default of Cascade — a referencing stock record must block
        // the widget's removal (DRK-703 §5 conflict scenario), not silently disappear with it.
        modelBuilder.Entity<StockRecordEntity>()
            .HasOne<WidgetEntity>()
            .WithMany()
            .HasForeignKey(s => s.WidgetId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }

    #endregion
}

/// <summary>
///     A stock record with a required FK to <see cref="WidgetEntity" /> — the referencing row the DRK-703 §5
///     conflict scenario needs to make a widget delete raise <see cref="DbUpdateException" /> for real.
/// </summary>
public sealed class StockRecordEntity : Entity
{
    #region Constructors

    public StockRecordEntity()
    {
    }

    public StockRecordEntity(Guid id, Guid widgetId, string warehouse) : base(id)
    {
        WidgetId = widgetId;
        Warehouse = warehouse;
    }

    #endregion

    #region Properties

    public Guid WidgetId { get; set; }

    public string Warehouse { get; set; } = string.Empty;

    #endregion
}

/// <summary>
///     Hand-written payload for <see cref="AuditedWidgetEntity" />'s <c>[RaisesEvent]</c> delete rule below.
///     No <c>[GenerateDto]</c> needed: the type-naming form only requires the named type to exist, mapped by
///     convention (matching property names) via the registered <c>IMapper</c>.
/// </summary>
public sealed record WidgetRemovedEvent
{
    #region Properties

    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    #endregion
}

/// <summary>
///     Audited, event-raising widget entity — proves a <c>MapDeleteById</c> deletion reaches both the audit
///     trail (audited, via <c>DKNet.EfCore.AuditLogs</c>) and the save-time domain-event
///     dispatcher (<c>[RaisesEvent]</c>, via <c>DKNet.EfCore.Events</c>) — DRK-703 §5's two invariant scenarios.
/// </summary>
[RaisesEvent(typeof(WidgetRemovedEvent), EventOperations.Deleted)]
public sealed class AuditedWidgetEntity : AuditedEntity
{
    #region Constructors

    public AuditedWidgetEntity()
    {
    }

    public AuditedWidgetEntity(Guid id, string name, string createdBy) : base(id)
    {
        Name = name;
        SetCreatedOn(createdBy);
    }

    #endregion

    #region Properties

    public string Name { get; set; } = string.Empty;

    #endregion

    #region Methods

    public void SetCreatedOn(string byUser, DateTimeOffset? on = null) => SetCreatedBy(byUser, on);

    #endregion
}
