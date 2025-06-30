using DKNet.EfCore.Events.Handlers;

namespace EfCore.Events.Tests.TestEntities;

public class TestEventPublisher : IEventPublisher
{
    public static ICollection<object> Events { get; } = [];

    public Task PublishAsync(object eventObj, CancellationToken cancellationToken = default)
    {
        Events.Add(eventObj);
        return Task.CompletedTask;
    }
}

public class EntityAddedEvent
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public record TypeEvent;