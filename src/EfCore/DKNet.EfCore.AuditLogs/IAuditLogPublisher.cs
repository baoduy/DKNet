namespace DKNet.EfCore.AuditLogs;

public interface IAuditLogPublisher
{
    Task PublishAsync(IEnumerable<AuditLogEntry> logs, CancellationToken cancellationToken = default);
}