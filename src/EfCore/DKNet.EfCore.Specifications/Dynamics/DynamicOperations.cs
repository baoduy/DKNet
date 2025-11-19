namespace DKNet.EfCore.Specifications.Dynamics;

/// <summary>
///     Defines the supported operations for building dynamic predicates.
/// </summary>
public enum DynamicOperations
{
    /// <summary>
    ///     Equality comparison (==)
    /// </summary>
    Equal,

    /// <summary>
    ///     Inequality comparison (!=)
    /// </summary>
    NotEqual,

    /// <summary>
    ///     Greater than comparison (&gt;)
    /// </summary>
    GreaterThan,

    /// <summary>
    ///     Greater than or equal comparison (&gt;=)
    /// </summary>
    GreaterThanOrEqual,

    /// <summary>
    ///     Less than comparison (&lt;)
    /// </summary>
    LessThan,

    /// <summary>
    ///     Less than or equal comparison (&lt;=)
    /// </summary>
    LessThanOrEqual,

    /// <summary>
    ///     String contains operation
    /// </summary>
    Contains,

    /// <summary>
    ///     Negated string contains operation (does not contain)
    /// </summary>
    NotContains,

    /// <summary>
    ///     String starts with operation
    /// </summary>
    StartsWith,

    /// <summary>
    ///     String ends with operation
    /// </summary>
    EndsWith
}