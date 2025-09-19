using System.Text.Json;
using DKNet.EfCore.Abstractions.Events;
using DKNet.Fw.Extensions;
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

    public static IEnumerable<object> GetEventObjects(this SnapshotContext context, IMapper? mapper)
    {
        foreach (var entry in context.SnapshotEntities.Where(entry => entry.Entity is IEventEntity))
        {
            var entity = (IEventEntity)entry.Entity;
            var finalEvents = new HashSet<object>();
            var (events, eventTypes) = entity.GetEventsAndClear();
            finalEvents.AddRange(events);

            if (mapper != null)
                finalEvents.AddRange(eventTypes.Select(eventType =>
                    mapper.Map(entity, entry.Entry.Metadata.ClrType, eventType)));

            var sourceType = entry.Entry.Metadata.ClrType.FullName!;
            var sourceKeys = entry.Entry.GetEntityKeyValues();

            foreach (var e in finalEvents)
            {
                if (e is IEventItem item)
                {
                    item.AdditionalData.Add(nameof(sourceType), sourceType);
                    item.AdditionalData.Add(nameof(sourceKeys), JsonSerializer.Serialize(sourceKeys));
                }

                yield return e;
            }
        }
    }
}