using DKNet.EfCore.Abstractions.Entities;

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

    #endregion
}
