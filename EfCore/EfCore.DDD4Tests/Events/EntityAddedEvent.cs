using System;

namespace EfCore.DDD4Tests.Events;

public record EntityAddedEvent 
{
    public Guid Id { get; set; }
    public string Name { get; set; }
}

public record EntityUpdatedEvent 
{
    public Guid Id { get; set; }

    public string Name { get; set; }
}

public record EntityDeletedEvent 
{
    public Guid Id { get; set; }

    public string Name { get; set; }
}

public record TypeEvent 
{
    public Guid Id { get; set; }

    public string Name { get; set; }
}