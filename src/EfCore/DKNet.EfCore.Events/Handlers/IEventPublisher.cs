namespace DKNet.EfCore.Events.Handlers;

/// <summary>
///     Centralized event publisher.
///     All events will be route to this publisher. Use <see />
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync(object eventObj, CancellationToken cancellationToken = default);

    Task PublishAllAsync(IEnumerable<object> events, CancellationToken cancellationToken = default)
    {
        return Task.WhenAll(events.Select(e => PublishAsync(e, cancellationToken)));
    }
}