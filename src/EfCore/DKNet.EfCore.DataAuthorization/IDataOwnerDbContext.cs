namespace DKNet.EfCore.DataAuthorization;

/// <summary>
///     Defines a contract for database contexts that implement data ownership functionality.
/// </summary>
/// <remarks>
///     This interface is part of the data authorization infrastructure and:
///     - Provides access to authorized data keys
///     - Supports global query filtering based on ownership
///     - Integrates with the Entity Framework Core pipeline
/// </remarks>
public interface IDataOwnerDbContext
{
    /// <summary>
    ///     Gets the collection of data keys accessible to the current context.
    /// </summary>
    /// <value>
    ///     An enumerable collection of string keys representing authorized data access.
    /// </value>
    /// <remarks>
    ///     These keys are used by the global query filters to:
    ///     - Restrict data access based on ownership
    ///     - Filter query results automatically
    ///     - Enforce data isolation
    /// </remarks>
    IEnumerable<string> AccessibleKeys { get; }
}