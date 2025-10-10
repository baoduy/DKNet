using System.Collections.Concurrent;

namespace DKNet.EfCore.AuditLogs.Tests;

public sealed class TestPublisher : IAuditLogPublisher
{
    private static readonly ConcurrentBag<AuditLogEntry> _received = [];
    public static IReadOnlyCollection<AuditLogEntry> Received => _received;

    public Task PublishAsync(IEnumerable<AuditLogEntry> logs, CancellationToken cancellationToken = default)
    {
        foreach (var l in logs) _received.Add(l);
        return Task.CompletedTask;
    }

    public static void Clear()
    {
        while (_received.TryTake(out _))
        {
        }
    }
}