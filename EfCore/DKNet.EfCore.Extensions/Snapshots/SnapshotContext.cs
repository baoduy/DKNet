using System.Collections.Immutable;
using Microsoft.EntityFrameworkCore;

namespace DKNet.EfCore.Extensions.Snapshots;

/// <summary>
///     The EntitiesSnapshotContext. The Tracking changes of DbContext will be switch off after snapshot the Entities.
///     Call <see cref="Dispose" /> to enable it back.
/// </summary>
public sealed class SnapshotContext : IDisposable,IAsyncDisposable
{
    internal SnapshotContext(DbContext context)
    {
        _dbContext = context;
    }

    private ImmutableList<SnapshotEntityEntry>? _snapshotEntities;
    private DbContext? _dbContext;

    public DbContext DbContext => _dbContext ?? throw new ObjectDisposedException(nameof(SnapshotContext));

    /// <summary>
    ///     The snapshot of changed entities. Only Entity with status is Modified or Created.
    /// </summary>
    public ImmutableList<SnapshotEntityEntry> SnapshotEntities
    {
        get
        {
            if (_snapshotEntities != null) return _snapshotEntities;
            return _snapshotEntities = [.. DbContext.ChangeTracker.Entries().Select(e => new SnapshotEntityEntry(e))];
        }
    }

    public void Dispose()
    {
        _snapshotEntities = null;
        _dbContext = null;
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}