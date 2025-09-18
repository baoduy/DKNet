namespace DKNet.EfCore.Events.Internals;

internal sealed class EventHook(
    IEnumerable<IEventPublisher> eventPublishers,
    IEnumerable<IMapper> mappers)
    : IHookAsync
{
    private readonly IMapper? _mapper = mappers.FirstOrDefault();
    //private List<IEventObject> _eventEntities = [];

    /// <summary>
    ///     RunBeforeSaveAsync
    /// </summary>
    /// <param name="context"></param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="EventException"></exception>
    public Task RunBeforeSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default) =>
        // _eventEntities =
        // [
        //     .. context.SnapshotEntities.Where(e => e.Entity is IEventEntity)
        //         .Select(e =>
        //         {
        //             var events = new List<object?>();
        //             var finallyEventTypes = new List<Type>();
        //             var entity = (IEventEntity)e.Entity;
        //             var eventsAndTypes = entity.GetEventsAndClear();
        //
        //             if (eventsAndTypes.events != null)
        //                 //Collect events
        //                 events.AddRange(eventsAndTypes.events);
        //
        //             if (eventsAndTypes.eventTypes != null)
        //                 finallyEventTypes.AddRange(eventsAndTypes.eventTypes);
        //
        //             if (finallyEventTypes.Count > 0)
        //             {
        //                 if (_autoMapper == null)
        //                     throw new NoNullAllowedException(
        //                         $"The {nameof(IMapper)} is not provided for the event types");
        //
        //                 events.AddRange(finallyEventTypes.Distinct()
        //                     .Select(d => _autoMapper.Map(entity, e.Entry.Metadata.ClrType, d)));
        //             }
        //
        //             return new EventObject(e.Entry.Metadata.ClrType.FullName!, e.Entry.GetEntityKeyValues(),
        //                 [.. events.Where(i => i is not null).Select(i => i!).Distinct()]);
        //         })
        //         .Where(e => e.Events.Length > 0)
        // ];
        //
        // logger.LogInformation("EventHook: There are {Count} Entity Events Found.", _eventEntities.Count);
        Task.CompletedTask;

    /// <summary>
    ///     Run RunAfterSaveAsync Events and ignore the result even failed.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="cancellationToken"></param>
    public async Task RunAfterSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        var events = context.GetEventObjects(_mapper);
        var publishers = from entityEventItem in events
            from eventPublisher in eventPublishers
            select eventPublisher.PublishAsync(entityEventItem, cancellationToken);

        await Task.WhenAll(publishers);
    }
}