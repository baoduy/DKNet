using System.Collections.Concurrent;

namespace DKNet.EfCore.AuditLogs.Tests;

public sealed class TestPublisher : IAuditLogPublisher
{
    #region Fields

    private static readonly ConcurrentBag<AuditLogEntry> _received = [];

    #endregion

    #region Properties

    public static IReadOnlyCollection<AuditLogEntry> Received => _received;

    #endregion

    #region Methods

    public static void Clear()
    {
        while (_received.TryTake(out _))
        {
        }
    }

    public Task PublishAsync(IEnumerable<AuditLogEntry> logs, CancellationToken cancellationToken = default)
    {
        foreach (var l in logs) _received.Add(l);
        return Task.CompletedTask;
    }

    #endregion
}