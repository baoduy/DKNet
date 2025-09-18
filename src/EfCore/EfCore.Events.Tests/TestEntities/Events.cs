using System.Collections.Generic;
using System.Threading;
using DKNet.EfCore.Events.Handlers;

namespace EfCore.Events.Tests.TestEntities;

public class TestEventPublisher : IEventPublisher
{
    public static List<object> Events { get; } = [];

    public Task PublishAsync(IEventObject eventObj, CancellationToken cancellationToken = default)
    {
        Events.AddRange(eventObj.Events);
        return Task.CompletedTask;
    }
}

public class EntityAddedEvent
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}