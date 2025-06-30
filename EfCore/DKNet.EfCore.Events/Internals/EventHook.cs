using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace DKNet.EfCore.Events.Internals;

/// <summary>
/// EventRunnerHook - Modified to use Channels for asynchronous event processing
/// </summary>
/// <param name="eventChannel">Channel for queuing events</param>
/// <param name="autoMappers">Auto mappers for event transformation</param>
/// <param name="logger">Logger instance</param>
internal sealed class EventHook(
    Channel<DKNet.EfCore.Events.Services.QueuedEventBatch> eventChannel,
    IEnumerable<IMapper> autoMappers,
    ILogger<EventHook> logger) : IHookAsync
{
    private readonly IMapper? _autoMapper = autoMappers.FirstOrDefault();
    private readonly Channel<DKNet.EfCore.Events.Services.QueuedEventBatch> _eventChannel = eventChannel;
    private readonly ILogger<EventHook> _logger = logger;
    private ImmutableList<EntityEventItem> _eventEntities = [];

    /// <summary>
    /// RunBeforeSaveAsync
    /// </summary>
    /// <param name="context"></param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="EventException"></exception>
    public Task RunBeforeSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        _eventEntities = ProcessAndFilterDomainEvents(context);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Run RunAfterSaveAsync - Queue events to channel for asynchronous processing
    /// </summary>
    /// <param name="context"></param>
    /// <param name="cancellationToken"></param>
    public async Task RunAfterSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        if (_eventEntities.Count == 0)
        {
            _logger.LogDebug("No events to process");
            return;
        }
        
        try
        {
            var eventBatch = new DKNet.EfCore.Events.Services.QueuedEventBatch(_eventEntities);
            
            // Queue the events for background processing
            await _eventChannel.Writer.WriteAsync(eventBatch, cancellationToken);
            
            _logger.LogDebug("Queued {EventCount} events from {EntityCount} entities for background processing",
                _eventEntities.Sum(e => e.Events.Count),
                _eventEntities.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue events for background processing");
            // In case of channel failure, we continue without blocking the save operation
        }
        finally
        {
            // Clear the events
            _eventEntities = [];
        }
    }

    /// <summary>
    /// ProcessAndFilterDomainEvents
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    /// <exception cref="NoNullAllowedException"></exception>
    private ImmutableList<EntityEventItem> ProcessAndFilterDomainEvents(SnapshotContext context)
    {
        var found = context.SnapshotEntities.Where(e => e.Entity is IEventEntity)
            .Select(e =>
            {
                var events = new List<object?>();
                var finallyEventTypes = new List<Type>();
                var entity = (IEventEntity)e.Entity;
                var eventsAndTypes = entity.GetEventsAndClear();

                if (eventsAndTypes.events != null)
                {
                    //Collect events
                    events.AddRange(eventsAndTypes.events);
                }

                if (eventsAndTypes.eventTypes != null)
                    finallyEventTypes.AddRange(eventsAndTypes.eventTypes);

                if (finallyEventTypes.Count > 0)
                {
                    if (_autoMapper == null)
                        throw new NoNullAllowedException($"The {nameof(IMapper)} is not provided.");

                    events.AddRange(finallyEventTypes.Distinct()
                        .Select(d => _autoMapper.Map(entity, e.Entry.Metadata.ClrType, d)));
                }

                return new EntityEventItem(entity, events.Where(i => i is not null).Distinct().ToArray()!);
            })
            .Where(e => e.Events.Count > 0)
            .ToImmutableList();

        return found;
    }
}