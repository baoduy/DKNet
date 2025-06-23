using SlimMessageBus;
using DKNet.EfCore.Events.Handlers;

namespace SlimBus.Infra.Core;

/// <summary>
/// The event publisher, IMessageBus for both internal and external events.
/// </summary>
/// <param name="bus"></param>
internal sealed class EventPublisher(IMessageBus bus) : IEventPublisher
{
    public Task PublishAsync(object eventObj, CancellationToken cancellationToken = default)
        => bus.Publish(eventObj, cancellationToken: cancellationToken);
}