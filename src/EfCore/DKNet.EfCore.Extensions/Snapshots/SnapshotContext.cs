using System.Collections.Immutable;

namespace DKNet.EfCore.Extensions.Snapshots;

/// <summary>
///     The EntitiesSnapshotContext. The Tracking changes of DbContext will be switched off after snapshot the Entities.
///     Call <see /> to enable it back.
/// </summary>
public sealed class SnapshotContext(DbContext context) : IAsyncDisposable
{
    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed")]
    private DbContext? _dbContext = context;

    private ImmutableList<SnapshotEntityEntry> _snapshotEntities = [];

    public DbContext DbContext => _dbContext ?? throw new ObjectDisposedException(nameof(SnapshotContext));

    /// <summary>
    ///     The snapshot of changed entities. Only Entity with status is Modified or Created.
    /// </summary>
    public ImmutableList<SnapshotEntityEntry> Entities
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
        _ = _snapshotEntities.Clear();
        _dbContext = null;
        return ValueTask.CompletedTask;
    }
}