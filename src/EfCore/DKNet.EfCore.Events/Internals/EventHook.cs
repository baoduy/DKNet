using Microsoft.Extensions.Logging;

namespace DKNet.EfCore.Events.Internals;

/// <summary>
///     EventRunnerHook
/// </summary>
/// <param name="eventPublishers"></param>
/// <param name="autoMappers"></param>
internal sealed class EventHook(
    IEnumerable<IEventPublisher> eventPublishers,
    IEnumerable<IMapper> autoMappers,
    ILogger<EventHook> logger)
    : IHookAsync
{
    private readonly IMapper? _autoMapper = autoMappers.FirstOrDefault();
    private ImmutableList<EntityEventItem> _eventEntities = [];

    /// <summary>
    ///     RunBeforeSaveAsync
    /// </summary>
    /// <param name="context"></param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="EventException"></exception>
    public Task RunBeforeSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        _eventEntities = context.SnapshotEntities.Where(e => e.Entity is IEventEntity)
            .Select(e =>
            {
                var events = new List<object?>();
                var finallyEventTypes = new List<Type>();
                var entity = (IEventEntity)e.Entity;
                var eventsAndTypes = entity.GetEventsAndClear();

                if (eventsAndTypes.events != null)
                    //Collect events
                    events.AddRange(eventsAndTypes.events);

                if (eventsAndTypes.eventTypes != null)
                    finallyEventTypes.AddRange(eventsAndTypes.eventTypes);

                if (finallyEventTypes.Count > 0)
                {
                    if (_autoMapper == null)
                        throw new NoNullAllowedException($"The {nameof(IMapper)} is not provided for the event types");

                    events.AddRange(finallyEventTypes.Distinct()
                        .Select(d => _autoMapper.Map(entity, e.Entry.Metadata.ClrType, d)));
                }

                return new EntityEventItem(entity,
                    [.. events.Where(i => i is not null).Select(i => i!).Distinct()]);
            })
            .Where(e => e.Events.Count > 0)
            .ToImmutableList();

        logger.LogInformation("EventHook: There are {Count} Entity Events Found.", _eventEntities.Count);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Run RunAfterSaveAsync Events and ignore the result even failed.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="cancellationToken"></param>
    public async Task RunAfterSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        var publishers = from entityEventItem in _eventEntities
            from eventPublisher in eventPublishers
            select eventPublisher.PublishAllAsync(entityEventItem.Events, cancellationToken);

        await Task.WhenAll(publishers);
        //Reset the event entities after processing
        _eventEntities = _eventEntities.Clear();
    }
}