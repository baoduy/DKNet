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

public sealed class WidgetDbContext(DbContextOptions<WidgetDbContext> options) : DbContext(options)
{
    #region Properties

    public DbSet<WidgetEntity> Widgets => Set<WidgetEntity>();

    #endregion
}
