using DKNet.EfCore.Abstractions.Events;
using DKNet.Fw.Extensions;

namespace DKNet.EfCore.Events.Internals;

internal sealed class EventContext(SnapshotContext snapshotContext, IMapper mapper)
{
    #region Fields

    private readonly ICollection<IEventEntity> _cachedEntities = (List<IEventEntity>)[];

    #endregion

    #region Methods

    public void ClearEvents()
    {
        foreach (var entity in _cachedEntities) entity.ClearEvents();
        _cachedEntities.Clear();
    }

    private ICollection<IEventEntity> GetEventEntities()
    {
        if (_cachedEntities.Count > 0) return _cachedEntities;

        _cachedEntities.AddRange(snapshotContext.Entities.Where(entry => entry.Entity is IEventEntity)
            .Select(entry => (IEventEntity)entry.Entity));

        return _cachedEntities;
    }

    public IEnumerable<object> GetEvents()
    {
        foreach (var entity in GetEventEntities())
        {
            var finalEvents = new HashSet<object>();
            var (events, eventTypes) = entity.GetEvents();
            finalEvents.AddRange(events);

            finalEvents.AddRange(
                eventTypes.Select(eventType =>
                    mapper.Map(entity, entity.GetType(), eventType)));

            var sourceType = entity.GetType().FullName!;

            foreach (var e in finalEvents)
            {
                if (e is IEventItem item) item.AdditionalData.Add(nameof(sourceType), sourceType);
                yield return e;
            }
        }
    }

    #endregion
}