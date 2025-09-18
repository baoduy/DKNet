using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace DKNet.EfCore.Events;

internal static class EventExtensions
{
    public static Dictionary<string, object?> GetEntityKeyValues(this EntityEntry entityEntry)
    {
        var primaryKey = entityEntry.Metadata.FindPrimaryKey();

        if (primaryKey == null)
            // Entity does not have a primary key defined
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        return primaryKey.Properties.ToDictionary(
            p => p.Name,
            p => p.PropertyInfo!.GetValue(entityEntry.Entity)
            , StringComparer.OrdinalIgnoreCase);
    }

    public static IEnumerable<IEventObject> GetEventObjects(this SnapshotContext context, IMapper? mapper)
    {
        foreach (var entry in context.SnapshotEntities.Where(entry => entry.Entity is IEventEntity))
        {
            var entity = (IEventEntity)entry.Entity;
            var finalEvents = new List<object>();
            var (events, eventTypes) = entity.GetEventsAndClear();
            finalEvents.AddRange(events);

            if (mapper != null)
                finalEvents.AddRange(eventTypes.Select(eventType =>
                    mapper.Map(entity, entry.Entry.Metadata.ClrType, eventType)));

            var entityType = entry.Entry.Metadata.ClrType.FullName!;
            var primaryKey = entry.Entry.GetEntityKeyValues();

            foreach (var e in finalEvents)
                yield return new EventObject(entityType, primaryKey, e);
        }
    }
}