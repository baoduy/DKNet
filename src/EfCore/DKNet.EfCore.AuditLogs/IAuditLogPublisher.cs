namespace DKNet.EfCore.AuditLogs;

public interface IAuditLogPublisher
{
    Task PublishAsync(IEnumerable<EfCoreAuditLog> logs, CancellationToken cancellationToken = default);
}