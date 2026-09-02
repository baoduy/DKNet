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

    // Convention-form rules resolve their payload record by reflection (entity's assembly + namespace); cache
    // the result per (entity type, composed name) since that lookup is not free and the mapping never changes
    // at runtime.
    private static readonly ConcurrentDictionary<(Type EntityType, string ComposedName), Type?> ResolvedEventTypeCache = new();

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
                _declaredEvents.Add((entry.Entity, ResolveEventType(entry.Entity.GetType(), rule)));
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
    ///     Resolves the <see cref="Type" /> a <see cref="RaisesEventAttribute" /> rule raises: the declared
    ///     <see cref="RaisesEventAttribute.EventType" /> for the type-naming form, or the generated payload
    ///     record resolved by name (entity's own assembly + namespace) for the convention forms — composed by
    ///     <see cref="EventNameComposer" />, the same source the build-time generator uses, so the two can
    ///     never disagree.
    /// </summary>
    /// <exception cref="EventException">
    ///     The rule is a convention form and no generated payload record with the composed name exists in the
    ///     entity's namespace — never silently dropped.
    /// </exception>
    private static Type ResolveEventType(Type entityType, RaisesEventAttribute rule)
    {
        if (rule.EventType is not null)
            return rule.EventType;

        var composedName = EventNameComposer.Compose(entityType.Name, rule.Label, rule.Properties, (int)rule.Operations);
        var resolved = ResolvedEventTypeCache.GetOrAdd((entityType, composedName), static key =>
        {
            var (entity, name) = key;
            var qualifiedName = entity.Namespace is null ? name : $"{entity.Namespace}.{name}";
            return entity.Assembly.GetType(qualifiedName);
        });

        return resolved ?? throw new EventException(Result.Fail(
            $"Entity '{entityType.Name}' declares a [RaisesEvent] convention-form rule composing to '{composedName}', but no generated payload record named '{composedName}' was found in its namespace. Ensure DKNet.EfCore.DtoGenerator is referenced and the entity builds cleanly."));
    }

    /// <summary>
    ///     Run RunAfterSaveAsync Events and ignore the result even failed.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="cancellationToken"></param>
    /// <remarks>
    ///     This runs after the save has already committed, so a publisher failure cannot roll the write
    ///     back — the transaction is done. By design, each <see cref="IEventPublisher" /> is invoked inside
    ///     its own try/catch: a failure is logged and swallowed, and that publisher's events are lost — the
    ///     write succeeds, the event never lands, and nothing downstream is notified. This is an accepted
    ///     trade-off, not a bug: an event publish failure must not undo a committed write, and once the
    ///     transaction is closed it cannot. Consumers that need at-least-once delivery cannot rely on this
    ///     hook; use a transactional outbox instead — write the events to an outbox table from
    ///     <see cref="BeforeSaveAsync" /> inside the same save transaction, and drain that table with a
    ///     separate dispatcher.
    /// </remarks>
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