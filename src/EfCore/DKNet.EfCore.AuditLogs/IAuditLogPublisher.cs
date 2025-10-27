namespace DKNet.EfCore.AuditLogs;

public interface IAuditLogPublisher
{
    #region Methods

    Task PublishAsync(IEnumerable<AuditLogEntry> logs, CancellationToken cancellationToken = default);

    #endregion
}