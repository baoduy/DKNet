using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.Extensions.Snapshots;
using DKNet.EfCore.Hooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DKNet.EfCore.AuditLogs.Internals;

internal sealed class EfCoreAuditHook(IServiceProvider serviceProvider, ILogger<EfCoreAuditHook> logger) : HookAsync
{
    private readonly Dictionary<Guid, List<EfCoreAuditLog>> _cache = [];

    public override Task BeforeSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        var logs = context.Entities.Where(e => e is
                { Entity: IAuditedProperties, OriginalState: EntityState.Modified or EntityState.Deleted })
            .Select(e => new { e.Entry, e.OriginalState })
            .Select(e => e.Entry.BuildAuditLog(e.OriginalState))
            .Where(l => l is not null && l.Changes.Count > 0)
            .OfType<EfCoreAuditLog>()
            .ToList();

        logger.LogInformation("Found {Count} audit log entries in current save operation of DbContext {DbContextId}",
            logs.Count, context.DbContext.ContextId.InstanceId);

        if (logs is { Count: > 0 })
            _cache[context.DbContext.ContextId.InstanceId] = logs;
        return base.BeforeSaveAsync(context, cancellationToken);
    }

    private void PublishLogsAsync(DbContext context, IEnumerable<EfCoreAuditLog> logs)
    {
        // Fire & forget: do not await publishers. Each publisher runs independently.
        var publishers = serviceProvider.GetKeyedServices<IAuditLogPublisher>(context.GetType().FullName).ToList();
        foreach (var publisher in publishers)
            Task.Run(async () =>
            {
                try
                {
                    // Ignore cancellation for fire-and-forget to ensure attempt
                    await publisher.PublishAsync(logs, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Audit log publishing failed for {Publisher}", publisher.GetType().Name);
                }
            });
    }

    public override async Task AfterSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        await base.AfterSaveAsync(context, cancellationToken);

        var logs = _cache.GetValueOrDefault(context.DbContext.ContextId.InstanceId);
        if (logs is not { Count: > 0 }) return;
        PublishLogsAsync(context.DbContext, logs);
    }
}

internal static class AuditLogEntryExtensions
{
    public static EfCoreAuditLog? BuildAuditLog(this EntityEntry entry, EntityState originalState)
    {
        if (entry.Entity is not IAuditedProperties audited) return null;
        var changes = new List<EfCoreAuditFieldChange>();
        foreach (var prop in entry.Properties)
        {
            var name = prop.Metadata.Name;
            var oldVal = prop.OriginalValue;
            var newVal = prop.CurrentValue;
            if (originalState == EntityState.Deleted)
            {
                changes.Add(new EfCoreAuditFieldChange { FieldName = name, OldValue = oldVal, NewValue = null });
                continue;
            }

            if (prop.IsModified || !Equals(oldVal, newVal))
                changes.Add(new EfCoreAuditFieldChange { FieldName = name, OldValue = oldVal, NewValue = newVal });
        }

        return new EfCoreAuditLog
        {
            CreatedBy = audited.CreatedBy,
            CreatedOn = audited.CreatedOn,
            UpdatedBy = audited.UpdatedBy,
            UpdatedOn = audited.UpdatedOn,
            EntityName = entry.Entity.GetType().Name,
            Changes = changes
        };
    }
}