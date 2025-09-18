using DKNet.EfCore.DataAuthorization.Internals;

namespace DKNet.EfCore.DataAuthorization;

/// <summary>
///     Defines a contract for providing data ownership and access control functionality.
/// </summary>
/// <remarks>
///     This interface is responsible for:
///     - Managing data access permissions through key-based authorization
///     - Providing ownership information for new entities
///     - Supporting the data authorization infrastructure
/// </remarks>
public interface IDataOwnerProvider
{
    /// <summary>
    ///     Gets the collection of data keys that the current context can access.
    /// </summary>
    /// <returns>A collection of accessible data keys used in global query filters.</returns>
    /// <remarks>
    ///     These keys are used to:
    ///     - Filter query results based on data ownership
    ///     - Restrict access to authorized data only
    ///     - Support multi-tenancy scenarios
    /// </remarks>
    ICollection<string> GetAccessibleKeys();

    /// <summary>
    ///     Gets the ownership key for newly created entities.
    /// </summary>
    /// <returns>The ownership key to be assigned, or an empty string if not available.</returns>
    /// <remarks>
    ///     This key is automatically assigned to new entities during creation
    ///     through the <see cref="DataOwnerHook" />.
    /// </remarks>
    string GetOwnershipKey();
}