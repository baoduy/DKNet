using DKNet.EfCore.Abstractions.Events;
using SlimMessageBus;

namespace SlimBus.Infra.Services;

/// <summary>
///     The event publisher, IMessageBus for both internal and external events.
/// </summary>
/// <param name="bus"></param>
internal sealed class EventPublisher(IMessageBus bus) : IEventPublisher
{
    public async Task PublishAsync(object eventObj, CancellationToken cancellationToken = default)
    {
        await bus.Publish(eventObj, cancellationToken: cancellationToken);
    }
}