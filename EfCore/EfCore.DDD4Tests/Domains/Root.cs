using System.ComponentModel.DataAnnotations;
using EfCore.DDD4Tests.Abstracts;
using Microsoft.EntityFrameworkCore;

namespace EfCore.DDD4Tests.Domains;

//[AutoEvents(CreatedEventType = typeof(EntityAddedEvent),UpdatedEventType = typeof(EntityUpdatedEvent),DeletedEventType = typeof(EntityDeletedEvent))]
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
    }
}