namespace DKNet.EfCore.Extensions.Snapshots;

/// <summary>
///     The EntitiesSnapshotContext. The Tracking changes of DbContext will be switched off after snapshot the Entities.
///     Call <see /> to enable it back.
/// </summary>
public sealed class SnapshotContext : IAsyncDisposable, IDisposable
{
    #region Fields

    private DbContext? _dbContext;
    private readonly List<SnapshotEntityEntry> _snapshotEntities;

    #endregion

    #region Constructors

    public SnapshotContext(DbContext context)
    {
        _dbContext = context ?? throw new ArgumentNullException(nameof(context));
        DbContext.ChangeTracker.DetectChanges();
        _snapshotEntities = [.. DbContext.ChangeTracker.Entries().Select(e => new SnapshotEntityEntry(e))];
    }

    #endregion

    #region Properties

    public DbContext DbContext => _dbContext ?? throw new ObjectDisposedException(nameof(SnapshotContext));

    /// <summary>
    ///     The snapshot of changed entities. Only Entity with status is Modified or Created.
    /// </summary>
    public IReadOnlyCollection<SnapshotEntityEntry> Entities => _snapshotEntities;

    #endregion

    #region Methods

    public void Dispose()
    {
        _snapshotEntities.Clear();
        //DO NOT dispose DbContext, it is not owned by this class.
        _dbContext = null;
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    #endregion
}