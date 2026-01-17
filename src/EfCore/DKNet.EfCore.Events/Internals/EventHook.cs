using DKNet.EfCore.Abstractions.Events;
using Microsoft.Extensions.Logging;

namespace DKNet.EfCore.Events.Internals;

internal sealed class EventHook(
    IEnumerable<IEventPublisher> eventPublishers,
    IEnumerable<IMapper> mappers,
    ILogger<EventHook>? logger = null)
    : HookAsync
{
    #region Fields

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
            logger.LogInformation("{Name} Publishing events for context {ContextId}", nameof(EventHook),
                context.DbContext.ContextId);

        var events = context.GetEventObjects(_mapper);
        foreach (var @event in events.Distinct())
        foreach (var publisher in eventPublishers)
            await publisher.PublishAsync(@event, cancellationToken);
    }

    #endregion
}