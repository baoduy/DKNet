// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: SnapshotContext.cs
// Description: Captures a snapshot of tracked entity entries (Added/Modified) and temporarily disables
//              automatic change detection on the provided DbContext until the snapshot is disposed.

namespace DKNet.EfCore.Extensions.Snapshots;

/// <summary>
///     Captures a snapshot of the current tracked entities (Added and Modified) from a <see cref="DbContext" />
///     and temporarily disables automatic change detection on the context to improve performance while the
///     snapshot is held. Call <see cref="RestoreTracking" /> or dispose the instance to restore the previous
///     AutoDetectChangesEnabled setting.
/// </summary>
public sealed class SnapshotContext : IAsyncDisposable, IDisposable
{
    #region Fields

    private readonly bool _previousAutoDetectChangesEnabled;
    private readonly List<SnapshotEntityEntry> _snapshotEntities;

    private DbContext? _dbContext;

    #endregion

    #region Constructors

    /// <summary>
    ///     Creates a new snapshot context for the provided <paramref name="context" />.
    ///     The snapshot will include only entries in the Added or Modified state.
    /// </summary>
    /// <param name="context">The DbContext to snapshot; this instance is not owned by the snapshot and will not be disposed.</param>
    public SnapshotContext(DbContext context)
    {
        _ = context ?? throw new ArgumentNullException(nameof(context));

        _dbContext = context;

        // Ensure the change tracker is up to date before capturing state
        DbContext.ChangeTracker.DetectChanges();

        // Capture whether auto-detect-changes was enabled so we can restore it later
        _previousAutoDetectChangesEnabled = DbContext.ChangeTracker.AutoDetectChangesEnabled;

        // Capture only entities that are Added or Modified
        _snapshotEntities =
        [
            .. DbContext.ChangeTracker
                .Entries()
                .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .Select(e => new SnapshotEntityEntry(e))
        ];

        // Turn off automatic DetectChanges while snapshot is active to reduce overhead
        DbContext.ChangeTracker.AutoDetectChangesEnabled = false;
    }

    #endregion

    #region Properties

    /// <summary>
    ///     The underlying <see cref="DbContext" /> used for the snapshot. Throws if the snapshot has been disposed.
    /// </summary>
    public DbContext DbContext => _dbContext ?? throw new ObjectDisposedException(nameof(SnapshotContext));

    /// <summary>
    ///     The snapshot of changed entities captured at construction time. Only entities that were Added or Modified
    ///     at the time of snapshot are included.
    /// </summary>
    public IReadOnlyCollection<SnapshotEntityEntry> Entities => _snapshotEntities;

    #endregion

    #region Methods

    /// <summary>
    ///     Disposes the snapshot context, clears the in-memory snapshot and restores the DbContext's automatic
    ///     change-detection behavior. The underlying DbContext is NOT disposed.
    /// </summary>
    public void Dispose()
    {
        // Restore tracking before clearing snapshot
        RestoreTracking();

        _snapshotEntities.Clear();

        // DO NOT dispose DbContext; it is not owned by this class.
        _dbContext = null;
    }

    /// <summary>
    ///     Asynchronously disposes the snapshot context. This simply calls the synchronous Dispose and
    ///     returns a completed ValueTask.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    ///     Restores the DbContext's AutoDetectChangesEnabled setting to its previous value.
    ///     This method is idempotent: calling it multiple times has no additional effect.
    /// </summary>
    public void RestoreTracking()
    {
        if (_dbContext is not null)
            _dbContext.ChangeTracker.AutoDetectChangesEnabled = _previousAutoDetectChangesEnabled;
    }

    #endregion
}