using DKNet.EfCore.Abstractions.Events;
using SlimMessageBus;

// ReSharper disable ClassWithVirtualMembersNeverInherited.Global

namespace DKNet.SlimBus.Extensions.Handlers;

public class SlimBusEventPublisher(IMessageBus bus) : IEventPublisher
{
    public virtual Task PublishAsync(object eventObj, CancellationToken cancellationToken = default)
    {
        if (eventObj is not IEventItem item) return bus.Publish(eventObj, cancellationToken: cancellationToken);

        var headers =
            item.AdditionalData.ToDictionary(i => i.Key, object (v) => v.Value, StringComparer.OrdinalIgnoreCase);
        return bus.Publish(item, headers: headers, cancellationToken: cancellationToken);
    }
}