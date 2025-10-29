namespace DKNet.EfCore.Abstractions.Events;

/// <summary>
///     Centralized event publisher.
///     All events will be route to this publisher.
/// </summary>
public interface IEventPublisher
{
    #region Methods

    /// <summary>
    ///     Publishes an event asynchronously.
    /// </summary>
    /// <param name="eventObj">The event object to publish.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task PublishAsync(object eventObj, CancellationToken cancellationToken = default);

    #endregion
}