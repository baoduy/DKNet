using System.Text.Json;
using DKNet.EfCore.Abstractions.Events;
using DKNet.Fw.Extensions;
using Microsoft.EntityFrameworkCore;

namespace DKNet.EfCore.Events;

internal static class EventExtensions
{
    #region Methods

    public static IEnumerable<object> GetEventObjects(this SnapshotContext context, IMapper? mapper)
    {
        foreach (var entry in context.Entities.Where(entry => entry.Entity is IEventEntity))
        {
            var entity = (IEventEntity)entry.Entity;
            var finalEvents = new HashSet<object>();
            var (events, eventTypes) = entity.GetEventsAndClear();
            finalEvents.AddRange(events);

            if (mapper != null)
            {
                finalEvents.AddRange(
                    eventTypes.Select(eventType =>
                        mapper.Map(entity, entry.Entry.Metadata.ClrType, eventType)));
            }

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

    #endregion
}