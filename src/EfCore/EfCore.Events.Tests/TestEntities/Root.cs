using System.ComponentModel.DataAnnotations;
using DKNet.EfCore.Extensions.Configurations;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EfCore.Events.Tests.TestEntities;

public class Root(string name, string ownedBy)
    : AggregateRoot(Guid.CreateVersion7(), ownedBy, $"Unit Test {Guid.NewGuid()}")
{
    private readonly HashSet<Entity> _entities = [];

    [Required] public string Name { get; private set; } = name;

    [BackingField(nameof(_entities))] public IReadOnlyCollection<Entity> Entities => _entities;

    public void UpdateName(string name)
    {
        Name = name;
    }

    public void AddEntity(string name)
    {
        var entity = new Entity(name, Id);
        _entities.Add(entity);
    }
}

internal sealed class RootEfConfig : DefaultEntityTypeConfiguration<Root>
{
    public override void Configure(EntityTypeBuilder<Root> builder)
    {
        base.Configure(builder);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100);
    }
}

public class Entity(string name, Guid rootId) : EntityBase<Guid>(Guid.Empty, "TestOwner", $"Unit Test {Guid.NewGuid()}")
{
    [Required] public string Name { get; private set; } = name;
    public Guid RootId { get; private set; } = rootId;
}

internal sealed class EntityEfConfig : DefaultEntityTypeConfiguration<Entity>
{
    public override void Configure(EntityTypeBuilder<Entity> builder)
    {
        base.Configure(builder);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100);
    }
}