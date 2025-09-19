namespace DKNet.EfCore.Abstractions.Events;

/// <summary>
///     Centralized event publisher.
///     All events will be route to this publisher. Use <see />
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync(object eventObj, CancellationToken cancellationToken = default);
}