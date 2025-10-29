// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: SnapshotEntityEntry.cs
// Description: Lightweight snapshot wrapper that captures an EntityEntry and its original state at snapshot time.

using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace DKNet.EfCore.Extensions.Snapshots;

/// <summary>
///     A snapshot of an EF Core <see cref="EntityEntry" /> capturing the entry and its state at the time of the snapshot.
/// </summary>
public sealed class SnapshotEntityEntry
{
    #region Constructors

    /// <summary>
    ///     Initializes a new instance of <see cref="SnapshotEntityEntry" /> from the provided <paramref name="entry" />.
    /// </summary>
    /// <param name="entry">The <see cref="EntityEntry" /> to snapshot (must not be null).</param>
    public SnapshotEntityEntry(EntityEntry entry)
    {
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        OriginalState = entry.State;
    }

    #endregion

    #region Properties

    /// <summary>
    ///     The underlying entity instance associated with the entry.
    /// </summary>
    public object Entity => Entry.Entity;

    /// <summary>
    ///     The captured <see cref="EntityEntry" /> instance.
    /// </summary>
    public EntityEntry Entry { get; }

    /// <summary>
    ///     The entity state at the time the snapshot was created.
    /// </summary>
    public EntityState OriginalState { get; }

    #endregion
}