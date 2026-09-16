using System.Text.Json;
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
    ILogger<EfCoreAuditHook> logger,
    ICurrentUserProvider? currentUserProvider = null) : HookAsync
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
        await PublishLogsAsync(context.DbContext, logs, cancellationToken);
    }

    public override Task BeforeSaveAsync(SnapshotContext context, CancellationToken cancellationToken = default)
    {
        StampCurrentUser(context);

        var logs = context.Entities
            .Where(e => e.OriginalState is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => e.Entry.BuildAuditLog(e.OriginalState, option.Value.Behaviour, option.Value.PropertyPolicy))
            .Where(l => l is not null)
            .OfType<AuditLogEntry>()
            .ToList();

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation(
                "Found {Count} audit log entries in current save operation of DbContext {DbContextId}",
                logs.Count,
                context.DbContext.ContextId.InstanceId);

        if (logs is { Count: > 0 }) _cache[context.DbContext.ContextId.InstanceId] = logs;

        return base.BeforeSaveAsync(context, cancellationToken);
    }

    /// <summary>
    ///     Stamps <c>CreatedBy</c>/<c>CreatedOn</c> on every added entry and <c>UpdatedBy</c>/<c>UpdatedOn</c>
    ///     on every modified entry from the registered <see cref="ICurrentUserProvider" />, before the audit
    ///     log entries are captured below — so a published entry carries the same values the row was saved
    ///     with. No-op when no provider is registered or it returns null/empty.
    /// </summary>
    /// <param name="context">The snapshot context containing entity changes.</param>
    private void StampCurrentUser(SnapshotContext context)
    {
        var currentUser = currentUserProvider?.GetCurrentUser();
        if (string.IsNullOrEmpty(currentUser)) return;

        foreach (var entry in context.Entities)
            switch (entry.OriginalState)
            {
                case EntityState.Added:
                    AuditPropertyStamper.StampCreatedBy(entry, currentUser);
                    break;

                case EntityState.Modified:
                    AuditPropertyStamper.StampUpdatedBy(entry, currentUser);
                    break;
            }
    }

    /// <summary>
    ///     Publishes the audit log entries captured for this save to every registered
    ///     <see cref="IAuditLogPublisher" /> keyed to the given <see cref="DbContext" /> type.
    /// </summary>
    /// <param name="context">The <see cref="DbContext" /> the entries were captured from.</param>
    /// <param name="logs">The audit log entries to publish.</param>
    /// <param name="cancellationToken">Cancellation token for the publish operation.</param>
    /// <remarks>
    ///     Called from <see cref="AfterSaveAsync" />, i.e. after the save has already committed — a
    ///     publisher failure here cannot and must not roll the write back. Each
    ///     <see cref="IAuditLogPublisher" /> is invoked inside its own try/catch: a failure is logged and
    ///     swallowed, and that publisher's audit entries are lost. This is an accepted trade-off (the write
    ///     already succeeded and cannot be undone), not a bug, but it means there is no recovery path for a
    ///     dropped audit entry. Consumers that need at-least-once delivery of audit logs cannot rely on this
    ///     hook; use a transactional outbox instead — persist the entries to an outbox table from
    ///     <see cref="BeforeSaveAsync" /> inside the same save transaction, and drain that table with a
    ///     separate dispatcher.
    /// </remarks>
    private async Task PublishLogsAsync(DbContext context, IEnumerable<AuditLogEntry> logs, CancellationToken cancellationToken)
    {
        var publishers = serviceProvider.GetKeyedServices<IAuditLogPublisher>(context.GetType().FullName).ToList();
        foreach (var publisher in publishers)
        {
            try
            {
                await publisher.PublishAsync(logs, cancellationToken);
            }
            catch (Exception ex)
            {
                if (!logger.IsEnabled(LogLevel.Error)) continue;

                string? payload = null;
                try
                {
                    payload = JsonSerializer.Serialize(logs);
                }
                catch
                {
                    // Serialization failure must not escape the catch block.
                }

                if (payload is not null)
                    logger.LogError(ex, "Audit log publishing failed for {Publisher}. Entries: {AuditLogEntries}", publisher.GetType().Name, payload);
                else
                    logger.LogError(ex, "Audit log publishing failed for {Publisher}. Entries count: {AuditLogEntriesCount}", publisher.GetType().Name, logs.Count());
            }
        }
    }

    #endregion
}