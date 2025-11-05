using System.Reflection;
using System.Text;
using DKNet.Fw.Extensions;

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
public sealed class DynamicPredicateBuilder<TEntity>
{
    #region Fields

    private readonly List<(string Property, FilterOperations Operation, object? Value)> _conditions = [];

    #endregion

    #region Methods

    private static FilterOperations AdjustOperationForValueType(Type? propValueType, FilterOperations op)
    {
        if (propValueType == null || propValueType == typeof(string) ||
            Nullable.GetUnderlyingType(propValueType) == typeof(string)) return op;
        // For all non-string types, switch Contains/NotContains to Equal/NotEqual, and throw for StartsWith/EndsWith
        return op switch
        {
            FilterOperations.Contains => FilterOperations.Equal,
            FilterOperations.NotContains => FilterOperations.NotEqual,
            FilterOperations.StartsWith or FilterOperations.EndsWith => FilterOperations.Equal,
            _ => op
        };
    }

    /// <summary>
    ///     Builds the dynamic LINQ predicate string and returns the corresponding parameter values.
    ///     All conditions are combined using AND logic.
    ///     If a property is an enum, only Equal and NotEqual operations are allowed. If
    ///     Contains/NotContains/StartsWith/EndsWith are used on enums, they are auto-switched to Equal/NotEqual.
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
        var entityType = typeof(TEntity);

        for (var i = 0; i < _conditions.Count; i++)
        {
            var (prop, op, val) = _conditions[i];
            if (i > 0)
                sb.Append(" and ");

            var type = ResolvePropertyType(entityType, prop);
            op = AdjustOperationForValueType(type, op);

            // Validate enum operations and value types
            if (type.IsEnumType())
            {
                if (val == null)
                {
                    // Null is valid for nullable enum
                    if (Nullable.GetUnderlyingType(type!) == null)
                        continue; // Skip non-nullable enum with null value
                }
                else
                {
                    // Try to convert value to enum
                    var enumType = type!.GetNonNullableType();
                    if (!enumType.TryConvertToEnum(val, out _))
                        continue;
                }
            }

            var clause = BuildClause(prop, op, val, parameters.Count);
            if (op is not FilterOperations.Equal and not FilterOperations.NotEqual || val != null)
                parameters.Add(val!);
            sb.Append(clause);
        }

        return (sb.ToString(), parameters.ToArray());
    }

    private static string BuildClause(string prop, FilterOperations op, object? val, int paramIndex)
    {
        return val switch
        {
            null when op is FilterOperations.Equal => $"{prop} == null",
            null when op is FilterOperations.NotEqual => $"{prop} != null",
            _ => op switch
            {
                FilterOperations.Equal => $"{prop} == @{paramIndex}",
                FilterOperations.NotEqual => $"{prop} != @{paramIndex}",
                FilterOperations.GreaterThan => $"{prop} > @{paramIndex}",
                FilterOperations.GreaterThanOrEqual => $"{prop} >= @{paramIndex}",
                FilterOperations.LessThan => $"{prop} < @{paramIndex}",
                FilterOperations.LessThanOrEqual => $"{prop} <= @{paramIndex}",
                FilterOperations.Contains => $"{prop}.Contains(@{paramIndex})",
                FilterOperations.NotContains => $"!{prop}.Contains(@{paramIndex})",
                FilterOperations.StartsWith => $"{prop}.StartsWith(@{paramIndex})",
                FilterOperations.EndsWith => $"{prop}.EndsWith(@{paramIndex})",
                _ => throw new NotSupportedException($"Operation {op} not supported.")
            }
        };
    }

    private static Type? ResolvePropertyType(Type entityType, string propertyPath)
    {
        var segments = propertyPath.Split('.');
        var currentType = entityType;
        foreach (var segment in segments)
        {
            var pi = currentType.GetProperty(segment,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (pi == null) return null;
            currentType = pi.PropertyType;
        }

        return currentType;
    }

    /// <summary>
    ///     Adds a filter condition to the dynamic predicate builder.
    ///     Multiple conditions are combined using AND logic when <see cref="Build" /> is called.
    ///     Property name/path may be provided in camelCase, snake_case, kebab-case or mixed; each dotted segment is
    ///     normalized to PascalCase (e.g. "user_name" -> "UserName", "address.city_name" -> "Address.CityName").
    ///     Property type is resolved from <typeparamref name="TEntity" /> at build time.
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
    ///     The current <see cref="DynamicPredicateBuilder{TEntity}" /> instance for method chaining.
    /// </returns>
    public DynamicPredicateBuilder<TEntity> With(string propertyName, FilterOperations operation, object? value)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
            throw new ArgumentException("Property name cannot be null or empty.", nameof(propertyName));
        var normalized = propertyName.ToPascalCase();
        _conditions.Add((normalized, operation, value));
        return this;
    }

    #endregion
}