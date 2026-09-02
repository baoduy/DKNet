// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: SnapshotContext.cs
// Description: Captures a snapshot of tracked entity entries (Added/Modified/Deleted) from a DbContext's
//              change tracker, to be read later after the change tracker's own state has moved on.

namespace DKNet.EfCore.Extensions.Snapshots;

/// <summary>
///     Captures a snapshot of the current tracked entities (Added, Modified and Deleted) from a
///     <see cref="DbContext" />'s change tracker via <see cref="Initialize" />, so they can still be read
///     via <see cref="Entities" /> after the change tracker's own state has moved on. This type does not
///     itself change <c>ChangeTracker.AutoDetectChangesEnabled</c> or any other tracker setting -
///     callers that need that (e.g. to suppress detection while the snapshot is held) must manage it themselves.
/// </summary>
public sealed class SnapshotContext(DbContext context) : IAsyncDisposable, IDisposable
{
    #region Fields

    private readonly List<SnapshotEntityEntry> _snapshotEntities = [];
    private bool _disposed;
    private bool _isInitialized;

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
    ///     Disposes the snapshot context and clears the in-memory snapshot. The underlying DbContext,
    ///     including its <c>ChangeTracker</c> settings, is left untouched and is NOT disposed.
    /// </summary>
    public void Dispose()
    {
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

        // Capture only entities that are Added or Modified
        var entities = DbContext.ChangeTracker
            .Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => new SnapshotEntityEntry(e));

        _snapshotEntities.AddRange(entities);
        _isInitialized = true;
    }

    #endregion
}