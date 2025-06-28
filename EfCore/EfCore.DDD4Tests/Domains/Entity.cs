using EfCore.DDD4Tests.Abstracts;
using EfCore.DDD4Tests.Events;

namespace EfCore.DDD4Tests.Domains;

public class Entity : DomainEntity
{
    public Entity() : base(Guid.Empty, "Steven", "Unit Test")
    {
    }

    public Entity(string name, Guid rootId) : base(Guid.Empty, "Steven", "Unit Test")
    {
        Name = name;
        RootId = rootId;

        AddEvent(new EntityAddedEvent
        {
            Id = Id,
            Name = Name,
        });
    }

    public string Name { get; private set; }

    public Guid RootId { get; }

    public Root Parent { get; private set; }

    public void Rename(string name)
    {
        Name = name;

        AddEvent(new EntityUpdatedEvent
        {
            Id = Id,
            Name = Name,
        });
    }

    public void DoLoop()
    {
        AddEvent(new EntityLoopEvent());
    }
}