using DKNet.EfCore.Abstractions.Events;
using DKNet.Fw.Extensions;
using Microsoft.Extensions.Logging;

namespace DKNet.EfCore.Events.Internals;

internal sealed class EventHook(
    IEnumerable<IEventPublisher> eventPublishers,
    IEnumerable<IMapper> mappers,
    ILogger<EventHook>? logger = null)
    : HookAsync
{
    #region Fields

    private static readonly HashSet<object> EventList = [];
    private static readonly SemaphoreSlim EventListLock = new(1, 1);
    private readonly IMapper? _mapper = mappers.FirstOrDefault();

    #endregion

    #region Methods

    /// <summary>
    ///     Run RunAfterSaveAsync Events and ignore the result even failed.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="cancellationToken"></param>
    public override async Task AfterSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        if (logger is not null && logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("{Name}:AfterSaveAsync for {ContextId}", nameof(EventHook),
                context.DbContext.ContextId);

        await EventListLock.WaitAsync(cancellationToken);
        try
        {
            if (EventList.Count > 0)
                foreach (var publisher in eventPublishers)
                    await publisher.PublishAsync(EventList, cancellationToken);
            EventList.Clear();
        }
        finally
        {
            EventListLock.Release();
        }
    }

    public override async Task BeforeSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        if (logger is not null && logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("{Name}:BeforeSaveAsync for DbContextId {ContextId}", nameof(EventHook),
                context.DbContext.ContextId.InstanceId);

        await EventListLock.WaitAsync(cancellationToken);
        try
        {
            var events = context.GetEventObjects(_mapper);
            EventList.AddRange(events);
        }
        finally
        {
            EventListLock.Release();
        }

        if (logger is not null && logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("{Name}:BeforeSaveAsync there are {Count} events found DbContextId {ContextId}",
                nameof(EventHook), EventList.Count,
                context.DbContext.ContextId);

        await base.BeforeSaveAsync(context, cancellationToken);
    }

    #endregion
}