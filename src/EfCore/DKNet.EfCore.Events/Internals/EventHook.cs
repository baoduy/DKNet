using DKNet.EfCore.Abstractions.Events;

namespace DKNet.EfCore.Events.Internals;

internal sealed class EventHook(
    IEnumerable<IEventPublisher> eventPublishers,
    IEnumerable<IMapper> mappers)
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
        var events = context.GetEventObjects(_mapper);
        var publishers = from entityEventItem in events
            from eventPublisher in eventPublishers
            select eventPublisher.PublishAsync(entityEventItem, cancellationToken);

        await Task.WhenAll(publishers);
    }

    #endregion
}