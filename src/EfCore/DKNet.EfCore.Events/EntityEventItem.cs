namespace DKNet.EfCore.Events;

/// <summary>
///     The Entity and Events information
/// </summary>
public sealed class EntityEventItem(IEventEntity entity, object[] events)
{
    /// <summary>
    ///    The Owner Entity of the events.
    /// </summary>
    public IEventEntity Entity { get; } = entity;

    /// <summary>
    ///   The Events of the entity.
    /// </summary>
    public ICollection<object> Events { get; } = [.. events];
}