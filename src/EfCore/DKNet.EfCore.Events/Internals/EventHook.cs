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

    private readonly HashSet<object> _eventList = [];

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

        if (_eventList.Count > 0)
            foreach (var publisher in eventPublishers)
                await publisher.PublishAsync(_eventList, cancellationToken);
        _eventList.Clear();
    }

    public override Task BeforeSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        if (logger is not null && logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("{Name}:BeforeSaveAsync for {ContextId}", nameof(EventHook),
                context.DbContext.ContextId);

        var events = context.GetEventObjects(_mapper);
        _eventList.AddRange(events);
        return base.BeforeSaveAsync(context, cancellationToken);
    }

    #endregion
}