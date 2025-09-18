using DKNet.EfCore.Events.Handlers;
using SlimMessageBus;

namespace SlimBus.Infra.Services;

/// <summary>
///     The event publisher, IMessageBus for both internal and external events.
/// </summary>
/// <param name="bus"></param>
internal sealed class EventPublisher(IMessageBus bus) : IEventPublisher
{
    public async Task PublishAsync(IEventObject eventObj, CancellationToken cancellationToken = default)
    {
        foreach (var obj in eventObj.Events)
            await bus.Publish(obj, cancellationToken: cancellationToken);
    }
}