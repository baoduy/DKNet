namespace DKNet.EfCore.Specifications.Dynamics;

/// <summary>
///     Defines the supported operations for building dynamic predicates.
/// </summary>
public enum Ops
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
    EndsWith,

    /// <summary>
    ///     Checks if the property value is contained in a collection of values.
    ///     Requires value to be an array or IEnumerable (excluding string).
    ///     Translates to SQL IN clause.
    ///     Example: CategoryId IN (1, 2, 3)
    /// </summary>
    In,

    /// <summary>
    ///     Checks if the property value is NOT contained in a collection of values.
    ///     Requires value to be an array or IEnumerable (excluding string).
    ///     Translates to SQL NOT IN clause.
    ///     Example: CategoryId NOT IN (1, 2, 3)
    /// </summary>
    NotIn
}