using DKNet.EfCore.Extensions.Snapshots;
using DKNet.EfCore.Hooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DKNet.EfCore.AuditLogs.Internals;

internal sealed class EfCoreAuditHook(
    IServiceProvider serviceProvider,
    IOptions<AuditLogOptions> option,
    ILogger<EfCoreAuditHook> logger) : HookAsync
{
    #region Fields

    private readonly Dictionary<Guid, List<AuditLogEntry>> _cache = [];

    #endregion

    #region Methods

    public override async Task AfterSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        await base.AfterSaveAsync(context, cancellationToken);

        var logs = _cache.GetValueOrDefault(context.DbContext.ContextId.InstanceId);
        if (logs is not { Count: > 0 }) return;
        _cache.Remove(context.DbContext.ContextId.InstanceId);
        PublishLogsAsync(context.DbContext, logs);
    }

    public override Task BeforeSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        var logs = context.Entities
            .Where(e => e.OriginalState is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => e.Entry.BuildAuditLog(e.OriginalState, option.Value.Behaviour))
            .Where(l => l is not null)
            .OfType<AuditLogEntry>()
            .ToList();

        logger.LogInformation("Found {Count} audit log entries in current save operation of DbContext {DbContextId}",
            logs.Count, context.DbContext.ContextId.InstanceId);

        if (logs is { Count: > 0 })
            _cache[context.DbContext.ContextId.InstanceId] = logs;
        return base.BeforeSaveAsync(context, cancellationToken);
    }

    private void PublishLogsAsync(DbContext context, IEnumerable<AuditLogEntry> logs)
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
                catch (DbUpdateException ex)
                {
                    logger.LogError(ex, "Audit log publishing failed for {Publisher}", publisher.GetType().Name);
                }
            });
    }

    #endregion
}