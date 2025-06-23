using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DKNet.EfCore.Events.Handlers;

namespace EfCore.DDD4Tests.Events;

public class TestEventPublisher : IEventPublisher
{
    public static ICollection<object> Events { get; } = [];

    public Task PublishAsync(object eventObj, CancellationToken cancellationToken = default)
    {
        Events.Add(eventObj);
        return Task.CompletedTask;
    }
}