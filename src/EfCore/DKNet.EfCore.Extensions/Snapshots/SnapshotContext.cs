using System.Collections.Immutable;

namespace DKNet.EfCore.Extensions.Snapshots;

/// <summary>
///     The EntitiesSnapshotContext. The Tracking changes of DbContext will be switched off after snapshot the Entities.
///     Call <see /> to enable it back.
/// </summary>
public sealed class SnapshotContext : IAsyncDisposable
{
    public SnapshotContext(DbContext context)
    {
        _dbContext = context ?? throw new ArgumentNullException(nameof(context));
        DbContext.ChangeTracker.DetectChanges();
        _snapshotEntities = [.. DbContext.ChangeTracker.Entries().Select(e => new SnapshotEntityEntry(e))];
    }

    private DbContext? _dbContext;
    private readonly List<SnapshotEntityEntry> _snapshotEntities;

    public DbContext DbContext => _dbContext ?? throw new ObjectDisposedException(nameof(SnapshotContext));

    /// <summary>
    ///     The snapshot of changed entities. Only Entity with status is Modified or Created.
    /// </summary>
    public IReadOnlyCollection<SnapshotEntityEntry> Entities => _snapshotEntities;

    public ValueTask DisposeAsync()
    {
        _snapshotEntities.Clear();
        _dbContext = null;
        return ValueTask.CompletedTask;
    }
}