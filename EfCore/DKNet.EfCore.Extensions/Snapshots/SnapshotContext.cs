using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace DKNet.EfCore.Extensions.Snapshots;

/// <summary>
///     The EntitiesSnapshotContext. The Tracking changes of DbContext will be switch off after snapshot the Entities.
///     Call <see cref="Dispose" /> to enable it back.
/// </summary>
public sealed class SnapshotContext : IDisposable, IAsyncDisposable
{
    internal SnapshotContext(DbContext context)
    {
        _dbContext = context;
    }

    private ImmutableList<SnapshotEntityEntry> _snapshotEntities = [];
    private DbContext? _dbContext;

    public DbContext DbContext => _dbContext ?? throw new ObjectDisposedException(nameof(SnapshotContext));

    /// <summary>
    ///     The snapshot of changed entities. Only Entity with status is Modified or Created.
    /// </summary>
    public ImmutableList<SnapshotEntityEntry> SnapshotEntities
    {
        get
        {
            // NOTE: Potential circular event handling in domain event handlers needs review
            if (_snapshotEntities.Count > 0) return _snapshotEntities;
            _snapshotEntities = [.. DbContext.ChangeTracker.Entries().Select(e => new SnapshotEntityEntry(e))];
            Trace.WriteLine($"SnapshotContext: Created SnapshotEntities with {_snapshotEntities.Count} ");
            return _snapshotEntities;
        }
    }

    public void Dispose()
    {
        _ = _snapshotEntities.Clear();
        _dbContext = null;
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}