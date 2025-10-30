using System.ComponentModel.DataAnnotations;
using DKNet.EfCore.Extensions.Configurations;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EfCore.Events.Tests.TestEntities;

public class Root(string name, string ownedBy)
    : AggregateRoot(Guid.CreateVersion7(), ownedBy, $"Unit Test {Guid.NewGuid()}")
{
    #region Fields

    private readonly HashSet<Entity> _entities = [];

    #endregion

    #region Properties

    [BackingField(nameof(_entities))] public IReadOnlyCollection<Entity> Entities => this._entities;

    [Required] public string Name { get; private set; } = name;

    #endregion

    #region Methods

    public void AddEntity(string name)
    {
        var entity = new Entity(name, this.Id);
        this._entities.Add(entity);
    }

    public void UpdateName(string name)
    {
        this.Name = name;
    }

    #endregion
}

internal sealed class RootEfConfig : DefaultEntityTypeConfiguration<Root>
{
    #region Methods

    public override void Configure(EntityTypeBuilder<Root> builder)
    {
        base.Configure(builder);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100);
    }

    #endregion
}

public class Entity(string name, Guid rootId) : EntityBase<Guid>(Guid.Empty, "TestOwner", $"Unit Test {Guid.NewGuid()}")
{
    #region Properties

    public Guid RootId { get; private set; } = rootId;

    [Required] public string Name { get; private set; } = name;

    #endregion
}

internal sealed class EntityEfConfig : DefaultEntityTypeConfiguration<Entity>
{
    #region Methods

    public override void Configure(EntityTypeBuilder<Entity> builder)
    {
        base.Configure(builder);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100);
    }

    #endregion
}