using DKNet.EfCore.Abstractions.Events;

namespace EfCore.Events.Tests.TestEntities;

public class TestEventPublisher : DefaultEventPublisher
{
    #region Properties

    public static IList<object> Events { get; } = [];

    #endregion

    #region Methods

    public override Task PublishAsync(object eventObj, CancellationToken cancellationToken = default)
    {
        Events.Add(eventObj);
        return Task.CompletedTask;
    }

    #endregion
}

public sealed record EntityAddedEvent
{
    #region Properties

    public required Guid Id { get; init; }

    public required string Name { get; init; }

    #endregion
}