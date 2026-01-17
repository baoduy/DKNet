namespace DKNet.EfCore.Abstractions.Entities;

/// <summary>
///     Defines a contract for domain entities that support event raising capabilities.
/// </summary>
/// <remarks>
///     This interface enables domain entities to maintain and manage their domain events.
///     It provides functionality for queuing events that can be later processed by the domain event handlers.
/// </remarks>
public interface IEventEntity
{
    #region Methods

    /// <summary>
    ///     Adds a domain event object to the event queue for later processing.
    /// </summary>
    /// <param name="eventObj">The event objects to be queued.</param>
    /// <exception cref="ArgumentNullException">Thrown when eventObj is null.</exception>
    protected void AddEvent(object eventObj);

    /// <summary>
    ///     Adds an event type to the queue for later instantiation and processing.
    /// </summary>
    /// <typeparam name="TEvent">The type of event to be queued.</typeparam>
    /// <remarks>
    ///     This method is useful when the event instance will be created from the entity state
    ///     at the time of event processing.
    /// </remarks>
    protected void AddEvent<TEvent>()
        where TEvent : class;

    /// <summary>
    ///     Retrieves all queued events and event types, then clears the queue.
    /// </summary>
    /// <returns>A tuple containing arrays of event objects and event types.</returns>
    /// <remarks>
    ///     The first element of the tuple contains instantiated event objects.
    ///     The second element contains event types that need to be instantiated.
    /// </remarks>
    (object[] Events, Type[] EventTypes) GetEventsAndClear();

    #endregion
}