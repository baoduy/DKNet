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
///     snapshot is held. Call <see /> or dispose the instance to restore the previous
///     AutoDetectChangesEnabled setting.
/// </summary>
public sealed class SnapshotContext(DbContext context) : IAsyncDisposable, IDisposable
{
    #region Fields

    private readonly List<SnapshotEntityEntry> _snapshotEntities = [];
    private bool _disposed;
    private bool _isInitialized;
    private bool _previousAutoDetectChangesEnabled;

    #endregion

    #region Properties

    /// <summary>
    ///     The underlying <see cref="DbContext" /> used for the snapshot. Throws if the snapshot has been disposed.
    /// </summary>
    public DbContext DbContext
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(SnapshotContext));
            return context;
        }
    }

    /// <summary>
    ///     The snapshot of changed entities captured at construction time. Only entities that were Added or Modified
    ///     at the time of snapshot are included.
    /// </summary>
    public IReadOnlyCollection<SnapshotEntityEntry> Entities
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(SnapshotContext));

            if (!_isInitialized)
                throw new InvalidOperationException(
                    "SnapshotContext is not initialized. Call Initialize() before accessing Entities.");
            return _snapshotEntities;
        }
    }

    #endregion

    #region Methods

    /// <summary>
    ///     Disposes the snapshot context, clears the in-memory snapshot and restores the DbContext's automatic
    ///     change-detection behavior. The underlying DbContext is NOT disposed.
    /// </summary>
    public void Dispose()
    {
        // Restore tracking before clearing snapshot
        try
        {
            context.ChangeTracker.AutoDetectChangesEnabled = _previousAutoDetectChangesEnabled;
        }
        catch (ObjectDisposedException)
        {
        }

        _snapshotEntities.Clear();
        _disposed = true;
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
    ///     Ensure the snapshot is initialized. This method is called automatically during construction,
    /// </summary>
    public void Initialize()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(SnapshotContext));

        // Ensure the change tracker is up to date before capturing state
        DbContext.ChangeTracker.DetectChanges();
        // Capture whether auto-detect-changes was enabled so we can restore it later
        _previousAutoDetectChangesEnabled = DbContext.ChangeTracker.AutoDetectChangesEnabled;

        // Capture only entities that are Added or Modified
        var entities = DbContext.ChangeTracker
            .Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => new SnapshotEntityEntry(e));

        _snapshotEntities.AddRange(entities);
        // Turn off automatic DetectChanges while snapshot is active to reduce overhead
        DbContext.ChangeTracker.AutoDetectChangesEnabled = false;
        _isInitialized = true;
    }

    #endregion
}