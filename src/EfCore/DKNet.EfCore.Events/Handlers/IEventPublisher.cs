namespace DKNet.EfCore.Events.Handlers;

/// <summary>
///     Centralized event publisher.
///     All events will be route to this publisher. Use <see />
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync(IEventObject eventObj, CancellationToken cancellationToken = default);
}