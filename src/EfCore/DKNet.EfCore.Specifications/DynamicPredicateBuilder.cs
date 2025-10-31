using System.Text;

namespace DKNet.EfCore.Specifications;

/// <summary>
///     Defines the supported operations for building dynamic predicates.
/// </summary>
public enum FilterOperations
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

/// <summary>
///     A builder for constructing dynamic LINQ predicates using fluent syntax.
///     Combines multiple filter conditions with AND logic to create a dynamic query expression.
/// </summary>
/// <example>
///     <code>
///     var builder = new DynamicPredicateBuilder()
///         .With("Age", Operation.GreaterThan, 18)
///         .With("Name", Operation.Contains, "John");
///
///     var predicate = builder.Build(out var values);
///     // Returns: "Age &gt; @0 and Name.Contains(@1)"
///     // values: [18, "John"]
///     </code>
/// </example>
public sealed class DynamicPredicateBuilder
{
    #region Fields

    private readonly List<(string Property, FilterOperations Operation, object? Value)> _conditions = [];

    #endregion

    #region Methods

    /// <summary>
    ///     Builds the dynamic LINQ predicate string and returns the corresponding parameter values.
    ///     All conditions are combined using AND logic.
    /// </summary>
    /// <returns>
    ///     A string representation of the dynamic LINQ predicate that can be used with
    ///     System.Linq.Dynamic.Core's Where method.
    /// </returns>
    /// <example>
    ///     <code>
    ///     var builder = new DynamicPredicateBuilder()
    ///         .With("Age", Operation.GreaterThan, 18)
    ///         .With("IsActive", Operation.Equal, true);
    ///
    ///     var predicate = builder.Build(out var values);
    ///     // predicate: "Age &gt; @0 and IsActive == @1"
    ///     // values: [18, true]
    ///
    ///     var query = dbContext.Users.Where(predicate, values);
    ///     </code>
    /// </example>
    public (string Expression, object[] Parameters) Build()
    {
        var sb = new StringBuilder();
        var parameters = new List<object>();

        for (var i = 0; i < _conditions.Count; i++)
        {
            var (prop, op, val) = _conditions[i];
            if (i > 0)
                sb.Append(" and ");

            // Handle null values specially for Equal and NotEqual operations
            string clause;
            if (val is null && op is FilterOperations.Equal)
            {
                clause = $"{prop} == null";
            }
            else if (val is null && op is FilterOperations.NotEqual)
            {
                clause = $"{prop} != null";
            }
            else
            {
                clause = op switch
                {
                    FilterOperations.Equal => $"{prop} == @{parameters.Count}",
                    FilterOperations.NotEqual => $"{prop} != @{parameters.Count}",
                    FilterOperations.GreaterThan => $"{prop} > @{parameters.Count}",
                    FilterOperations.GreaterThanOrEqual => $"{prop} >= @{parameters.Count}",
                    FilterOperations.LessThan => $"{prop} < @{parameters.Count}",
                    FilterOperations.LessThanOrEqual => $"{prop} <= @{parameters.Count}",
                    FilterOperations.Contains => $"{prop}.Contains(@{parameters.Count})",
                    FilterOperations.NotContains => $"!{prop}.Contains(@{parameters.Count})",
                    FilterOperations.StartsWith => $"{prop}.StartsWith(@{parameters.Count})",
                    FilterOperations.EndsWith => $"{prop}.EndsWith(@{parameters.Count})",

                    // FilterOperations.Any => $"{prop}.Any(x => {string.Join(" || ", (string[])val)})",
                    // FilterOperations.All => $"{prop}.All(x => {string.Join(" && ", (string[])val)})",
                    _ => throw new NotSupportedException($"Operation {op} not supported.")
                };

                parameters.Add(val!);
            }

            sb.Append(clause);
        }

        return (sb.ToString(), parameters.ToArray());
    }

    /// <summary>
    ///     Adds a filter condition to the dynamic predicate builder.
    ///     Multiple conditions are combined using AND logic when <see cref="Build" /> is called.
    ///     Property name/path may be provided in camelCase, snake_case, kebab-case or mixed; each dotted segment is
    ///     normalized to PascalCase (e.g. "user_name" -> "UserName", "address.city_name" -> "Address.CityName").
    /// </summary>
    /// <param name="propertyName">
    ///     The name of the property to filter on. Supports nested properties using dot notation (e.g., "Address.City").
    /// </param>
    /// <param name="operation">
    ///     The comparison operation to perform (e.g., Equal, GreaterThan, Contains).
    /// </param>
    /// <param name="value">
    ///     The value to compare against. The type should match the property type.
    /// </param>
    /// <returns>
    ///     The current <see cref="DynamicPredicateBuilder" /> instance for method chaining.
    /// </returns>
    public DynamicPredicateBuilder With(string propertyName, FilterOperations operation, object? value)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
            throw new ArgumentException("Property name cannot be null or empty.", nameof(propertyName));

        var normalized = propertyName.ToPascalCase();
        _conditions.Add((normalized, operation, value));
        return this;
    }

    #endregion
}