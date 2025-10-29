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
        this._dbContext = context ?? throw new ArgumentNullException(nameof(context));
        this.DbContext.ChangeTracker.DetectChanges();
        this._snapshotEntities = [.. this.DbContext.ChangeTracker.Entries().Select(e => new SnapshotEntityEntry(e))];
    }

    #endregion

    #region Properties

    public DbContext DbContext => this._dbContext ?? throw new ObjectDisposedException(nameof(SnapshotContext));

    /// <summary>
    ///     The snapshot of changed entities. Only Entity with status is Modified or Created.
    /// </summary>
    public IReadOnlyCollection<SnapshotEntityEntry> Entities => this._snapshotEntities;

    #endregion

    #region Methods

    public void Dispose()
    {
        this._snapshotEntities.Clear();

        //DO NOT dispose DbContext, it is not owned by this class.
        this._dbContext = null;
    }

    public ValueTask DisposeAsync()
    {
        this.Dispose();
        return ValueTask.CompletedTask;
    }

    #endregion
}