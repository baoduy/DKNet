namespace DKNet.EfCore.Abstractions.Events;

/// <summary>
///     Identifies which entity lifecycle operation(s) a generated domain event corresponds to.
/// </summary>
/// <remarks>
///     Combine flags (e.g. <c>Created | Updated</c>) to declare that a single generated event type
///     is raised for more than one lifecycle operation.
/// </remarks>
[Flags]
public enum EventOperations
{
    /// <summary>
    ///     The entity was added and the save succeeded.
    /// </summary>
    Created = 1,

    /// <summary>
    ///     The entity was modified and the save succeeded.
    /// </summary>
    Updated = 2,

    /// <summary>
    ///     The entity was removed and the save succeeded.
    /// </summary>
    Deleted = 4,
}
