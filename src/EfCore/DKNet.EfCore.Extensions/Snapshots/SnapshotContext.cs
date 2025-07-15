using System.Collections.Immutable;

namespace DKNet.EfCore.Extensions.Snapshots;

/// <summary>
///     The EntitiesSnapshotContext. The Tracking changes of DbContext will be switch off after snapshot the Entities.
///     Call <see cref="Dispose" /> to enable it back.
/// </summary>
public sealed class SnapshotContext : IDisposable, IAsyncDisposable
{
    private DbContext? _dbContext;

    private ImmutableList<SnapshotEntityEntry> _snapshotEntities = [];

    internal SnapshotContext(DbContext context) => _dbContext = context;

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
            DbContext.ChangeTracker.DetectChanges();
            _snapshotEntities = [.. DbContext.ChangeTracker.Entries().Select(e => new SnapshotEntityEntry(e))];
            return _snapshotEntities;
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        _ = _snapshotEntities.Clear();
        _dbContext = null;
    }
}