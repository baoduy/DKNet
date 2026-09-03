using DKNet.EfCore.Abstractions.Events;
using DKNet.Fw.Extensions;
using DKNet.Fw.Extensions.Collections;
using FluentResults;

namespace DKNet.EfCore.Events.Internals;

internal sealed class EventContext(SnapshotContext snapshotContext, IMapper? mapper)
{
    #region Fields

    private readonly List<IEventEntity> _cachedEntities = [];
    private bool _entitiesLoaded;

    #endregion

    #region Methods

    public void ClearEvents()
    {
        foreach (var entity in _cachedEntities) entity.ClearEvents();
        _cachedEntities.Clear();
        _entitiesLoaded = false;
    }

    private ICollection<IEventEntity> GetEventEntities()
    {
        if (_entitiesLoaded) return _cachedEntities;

        _cachedEntities.AddRange(snapshotContext.Entities.Where(entry => entry.Entity is IEventEntity)
            .Select(entry => (IEventEntity)entry.Entity));
        _entitiesLoaded = true;

        return _cachedEntities;
    }

    public IEnumerable<object> GetEvents()
    {
        // Reused across entities instead of allocating a fresh HashSet per entity; Clear() between
        // iterations keeps each entity's de-duplication independent of the others.
        var finalEvents = new HashSet<object>();

        foreach (var entity in GetEventEntities())
        {
            finalEvents.Clear();
            var (events, eventTypes) = entity.GetEvents();
            finalEvents.AddRange(events);

            if (eventTypes.Length > 0 && mapper is null)
                throw new EventException(Result.Fail(
                    $"Entity '{entity.GetType().Name}' raised a type-based event via AddEvent<TEvent>(), which maps the entity onto the event type and therefore requires an IMapper registration. Register one, or use AddEvent(object) with a pre-built event instance."));

            // mapper is guaranteed non-null here: the guard above throws when eventTypes is non-empty and mapper is null.
            finalEvents.AddRange(
                eventTypes.Select(eventType =>
                    mapper!.Map(entity, entity.GetType(), eventType)));

            var sourceType = entity.GetType().FullName!;

            foreach (var e in finalEvents)
            {
                if (e is IEventItem item) item.AdditionalData[nameof(sourceType)] = sourceType;
                yield return e;
            }
        }
    }

    #endregion
}