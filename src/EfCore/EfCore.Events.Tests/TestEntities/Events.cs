using DKNet.EfCore.Abstractions.Events;

namespace EfCore.Events.Tests.TestEntities;

public class TestEventPublisher : IEventPublisher
{
    public static IList<object> Events { get; } = [];

    public Task PublishAsync(object eventObj, CancellationToken cancellationToken = default)
    {
        Events.Add(eventObj);
        return Task.CompletedTask;
    }
}

public sealed record EntityAddedEvent
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
}