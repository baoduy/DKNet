using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace DKNet.EfCore.Extensions.Snapshots;

/// <summary>
///     This snapshot will keep the State of entity before save changes.
/// </summary>
/// <param name="entry"></param>
public sealed class SnapshotEntityEntry(EntityEntry entry)
{
    #region Properties

    public EntityEntry Entry { get; } = entry;

    /// <summary>
    ///     The original stage before saved changes.
    /// </summary>
    public EntityState OriginalState { get; } = entry.State;

    public object Entity => this.Entry.Entity;

    #endregion
}