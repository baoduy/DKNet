using System.Collections.Concurrent;
using System.Reflection;
using DKNet.EfCore.Abstractions.Events;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DKNet.EfCore.Events.Internals;

internal sealed class EventHook(
    IEnumerable<IEventPublisher> eventPublishers,
    IEnumerable<IMapper> mappers,
    ILogger<EventHook>? logger = null)
    : HookAsync
{
    #region Fields

    private static readonly ConcurrentDictionary<Type, RaisesEventAttribute[]> DeclaredEventCache = new();

    private readonly IMapper? _mapper = mappers.FirstOrDefault();
    private readonly HashSet<(object Entity, Type EventType)> _declaredEvents = [];

    #endregion

    #region Methods

    /// <summary>
    ///     Captures which declared (<c>[RaisesEvent]</c>) events qualify for this save, before the save
    ///     happens. Update narrowing (<see cref="RaisesEventAttribute.Properties" />) can only be evaluated
    ///     here: <c>EntityEntry.Property(...).IsModified</c> is meaningless once the save completes.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="cancellationToken"></param>
    public override Task BeforeSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        _declaredEvents.Clear();

        foreach (var entry in context.Entities)
        {
            EventOperations? operation = entry.OriginalState switch
            {
                EntityState.Added => EventOperations.Created,
                EntityState.Modified => EventOperations.Updated,
                EntityState.Deleted => EventOperations.Deleted,
                _ => null,
            };

            if (operation is null) continue;

            foreach (var rule in GetRaisesEventAttributes(entry.Entity.GetType()))
            {
                if (!rule.Operations.HasFlag(operation.Value)) continue;

                if (operation == EventOperations.Updated && rule.Properties.Count > 0 &&
                    !rule.Properties.Any(p => entry.Entry.Property(p).IsModified))
                    continue;

                // R5: two rules naming the same payload for the same operation raise it once
                // (HashSet dedups on the entity instance + event type).
                _declaredEvents.Add((entry.Entity, rule.EventType));
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Gets the <see cref="RaisesEventAttribute" /> declarations carried by the given entity type,
    ///     cached per <see cref="Type" /> so reflection only runs once for the lifetime of the process.
    /// </summary>
    private static RaisesEventAttribute[] GetRaisesEventAttributes(Type entityType) =>
        DeclaredEventCache.GetOrAdd(entityType,
            static t => t.GetCustomAttributes<RaisesEventAttribute>().ToArray());

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

        var eventContext = new EventContext(context, _mapper);
        var events = eventContext.GetEvents().ToList();

        if (_declaredEvents.Count > 0)
        {
            if (_mapper is null)
                throw new EventException(Result.Fail(
                    $"Entity raised {_declaredEvents.Count} declared event(s) via [RaisesEvent], which map the entity onto the event type and therefore require an IMapper registration. Register one to use declared domain events."));

            events.AddRange(_declaredEvents.Select(d => _mapper.Map(d.Entity, d.Entity.GetType(), d.EventType)));
        }

        foreach (var publisher in eventPublishers)
        {
            try
            {
                await publisher.PublishAsync(events, cancellationToken);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "{Name}: publisher {Publisher} failed for {ContextId}",
                    nameof(EventHook), publisher.GetType().Name, context.DbContext.ContextId);
            }
        }

        eventContext.ClearEvents();
        _declaredEvents.Clear();
    }

    #endregion
}