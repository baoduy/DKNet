using System.ComponentModel.DataAnnotations;

namespace EfCore.Events.Tests.TestEntities;

public class Root(string name, string ownedBy) : AggregateRoot(Guid.Empty, ownedBy, $"Unit Test {Guid.NewGuid()}")
{
    private readonly HashSet<Entity> _entities = [];

    [Required] public string Name { get; private set; } = name;

    [BackingField(nameof(_entities))] public IReadOnlyCollection<Entity> Entities => _entities;

    public void UpdateName(string name) => Name = name;

    public void AddEntity(string name)
    {
        var entity = new Entity(name, Id);
        _entities.Add(entity);
        
        // Add an event when an entity is added
        AddEvent(new EntityAddedEvent { Id = entity.Id, Name = entity.Name });
    }
}

public class Entity(string name, Guid rootId) : EntityBase<Guid>(Guid.Empty, "TestOwner", $"Unit Test {Guid.NewGuid()}")
{
    [Required] public string Name { get; private set; } = name;
    public Guid RootId { get; private set; } = rootId;
}