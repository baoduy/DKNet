// <copyright file="DynamicPredicateBuilderExtensions.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using DKNet.EfCore.Specifications.Extensions;
using DKNet.Fw.Extensions;
using DKNet.Fw.Extensions.Reflection;

namespace DKNet.EfCore.Specifications.Dynamics;

/// <summary>
///     Internal extension methods for building dynamic LINQ predicates.
///     Provides reusable logic for type resolution, operation adjustment, and clause building.
/// </summary>
internal static class DynamicPredicateBuilderExtensions
{
    #region Constants

    /// <summary>
    ///     Regex pattern that matches valid property paths: alphanumeric segments joined by dots.
    ///     Prevents injection of arbitrary Dynamic LINQ syntax via property name parameters.
    /// </summary>
    private static readonly Regex ValidPropertyPathPattern = new(
        @"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    ///     Characters and keywords that are disallowed in raw Dynamic LINQ expressions
    ///     to prevent injection of dangerous constructs (method calls on arbitrary types, etc.).
    /// </summary>
    private static readonly string[] DangerousExpressionPatterns =
    [
        "System.", "Microsoft.", "Reflection.", "Process.", "Assembly.",
        "GetType(", "typeof(", "Activator.", "Environment.",
        "File.", "Directory.", "Path.", "Stream.",
        "SqlCommand", "SqlConnection", "DbCommand",
        "Runtime.", "Unsafe.", "Marshal.",
        "AppDomain.", "Thread.", "Task.Run"
    ];

    /// <summary>
    ///     Non-enum target types (besides numeric types, covered by <c>IsNumericType</c>)
    ///     that <c>TryCoerceValue</c> knows how to convert a value into.
    /// </summary>
    private static readonly HashSet<Type> CoercibleNonEnumTypes =
    [
        typeof(bool), typeof(DateTime), typeof(DateTimeOffset), typeof(DateOnly), typeof(TimeOnly), typeof(Guid)
    ];

    #endregion

    #region Methods

    /// <summary>
    ///     Validates that a property name/path contains only safe characters (letters, digits, underscores,
    ///     hyphens, and dots for path separators). Returns false for null/empty or any string containing
    ///     characters that could be used for Dynamic LINQ injection.
    /// </summary>
    /// <param name="propertyName">The raw property name or path to validate.</param>
    /// <returns>True if the property name is safe to use in a dynamic expression; otherwise, false.</returns>
    internal static bool IsValidPropertyName(string? propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
            return false;

        if (propertyName.Length > 256 ||
            propertyName.Any(c => !char.IsLetterOrDigit(c) && c is not '.' and not '_' and not '-'))
            return false;

        var normalizedPath = propertyName.ToPascalCase();
        return !string.IsNullOrWhiteSpace(normalizedPath) &&
               ValidPropertyPathPattern.IsMatch(normalizedPath);
    }

    /// <summary>
    ///     Validates that a raw Dynamic LINQ expression string does not contain dangerous patterns
    ///     that could enable arbitrary code execution or information disclosure.
    /// </summary>
    /// <param name="expression">The Dynamic LINQ expression to validate.</param>
    /// <exception cref="ArgumentException">Thrown when the expression contains dangerous patterns.</exception>
    internal static void ValidateExpression(string expression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);

        foreach (var pattern in DangerousExpressionPatterns)
        {
            if (expression.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(
                    $"Dynamic LINQ expression contains disallowed pattern '{pattern}'. " +
                    "Only property access, comparison operators, and standard LINQ methods are permitted.",
                    nameof(expression));
        }
    }

    /// <summary>
    ///     Adjusts the operation based on the property value type.
    ///     For non-string types, string operations (Contains, NotContains, StartsWith, EndsWith)
    ///     are converted to equality operations.
    /// </summary>
    /// <param name="propValueType">The type of the property value.</param>
    /// <param name="op">The requested operation.</param>
    /// <returns>The adjusted operation appropriate for the property type.</returns>
    internal static Ops AdjustOperationForValueType(this Type? propValueType, Ops op)
    {
        if (propValueType == null || propValueType == typeof(string) ||
            Nullable.GetUnderlyingType(propValueType) == typeof(string)) return op;

        // For all non-string types, switch Contains/NotContains to Equal/NotEqual
        return op switch
        {
            Ops.Contains => Ops.Equal,
            Ops.NotContains => Ops.NotEqual,
            Ops.StartsWith or Ops.EndsWith => Ops.Equal,
            _ => op
        };
    }

    /// <summary>
    ///     Builds a dynamic LINQ clause string for a single condition.
    ///     Handles null values appropriately and generates the correct comparison syntax.
    /// </summary>
    /// <param name="prop">The property name or path (e.g., "Name" or "Address.City").</param>
    /// <param name="op">The operation to perform.</param>
    /// <param name="val">The value to compare against (can be null).</param>
    /// <param name="paramIndex">The parameter index for the @N placeholder.</param>
    /// <returns>A string representing the dynamic LINQ clause.</returns>
    internal static string BuildClause(string prop, Ops op, object? val, int paramIndex)
    {
        return val switch
        {
            null when op is Ops.Equal => $"{prop} == null",
            null when op is Ops.NotEqual => $"{prop} != null",
            _ => op switch
            {
                Ops.Equal => $"{prop} == @{paramIndex}",
                Ops.NotEqual => $"{prop} != @{paramIndex}",
                Ops.GreaterThan => $"{prop} > @{paramIndex}",
                Ops.GreaterThanOrEqual => $"{prop} >= @{paramIndex}",
                Ops.LessThan => $"{prop} < @{paramIndex}",
                Ops.LessThanOrEqual => $"{prop} <= @{paramIndex}",
                // The string operations carry an explicit null check so the predicate means the same thing
                // however it is evaluated. A relational provider never needs it — EF Core rewrites these into
                // null-aware SQL, and the guard folds away in the query plan — but the same expression
                // evaluated as ordinary LINQ (the InMemory provider, client-side evaluation, or a caller
                // compiling the predicate from TryBuildPredicate) would dereference the null and throw.
                // NotContains admits nulls for the same reason: that is what the translated SQL already does,
                // and "does not contain x" is true of a row that holds nothing at all.
                Ops.Contains => $"{prop} != null && {prop}.Contains(@{paramIndex})",
                Ops.NotContains => $"({prop} == null || !{prop}.Contains(@{paramIndex}))",
                Ops.StartsWith => $"{prop} != null && {prop}.StartsWith(@{paramIndex})",
                Ops.EndsWith => $"{prop} != null && {prop}.EndsWith(@{paramIndex})",
                Ops.In => $"@{paramIndex}.Contains({prop})",
                Ops.NotIn => $"!@{paramIndex}.Contains({prop})",
                Ops.IsNull => $"{prop} == null",
                Ops.IsNotNull => $"{prop} != null",
                _ => throw new NotSupportedException($"Operation {op} not supported.")
            }
        };
    }

    /// <summary>
    ///     Resolves the type of property given an entity type and property path.
    ///     Supports nested properties using dot notation (e.g., "Address.City").
    /// </summary>
    /// <param name="entityType">The root entity type.</param>
    /// <param name="propertyPath">The property path (can include dots for nested properties).</param>
    /// <returns>The resolved property type, or null if the property path is invalid.</returns>
    internal static Type? ResolvePropertyType(this Type entityType, string propertyPath)
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
    ///     Validates if a value is a valid array/collection for In/NotIn operations.
    ///     Returns false for null, empty collections, or non-enumerable types (including string).
    /// </summary>
    /// <param name="value">The value to validate</param>
    /// <param name="operation">The operation being performed</param>
    /// <returns>True if value is valid for the operation, false otherwise</returns>
    internal static bool ValidateArrayValue(object? value, Ops operation)
    {
        // Only validate for In/NotIn operations
        if (operation is not (Ops.In or Ops.NotIn))
            return true;

        if (value == null)
            return false;

        // String implements IEnumerable but should not be treated as array
        if (value is string)
            return false;

        // Check if value is enumerable
        if (value is not IEnumerable enumerable)
            return false;

        // Check if collection is non-empty
        var enumerator = enumerable.GetEnumerator();
        try
        {
            return enumerator.MoveNext();
        }
        finally
        {
            (enumerator as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    ///     Converts a value to an enum type, accepting the member name ("Pending") as well as its numeric
    ///     form ("0" or <c>0</c>).
    /// </summary>
    /// <remarks>
    ///     <c>TryConvertToEnum</c> goes through <see cref="Convert.ChangeType(object, Type, IFormatProvider)" />,
    ///     which only understands the numeric form — but a name is what an API surface serializes, and therefore
    ///     what a caller filters by.
    /// </remarks>
    /// <param name="enumType">The target enum type; may be a nullable enum.</param>
    /// <param name="value">The value to convert.</param>
    /// <param name="converted">The converted enum value on success; otherwise <see langword="null" />.</param>
    /// <returns><see langword="true" /> when the value converted; otherwise <see langword="false" />.</returns>
    internal static bool TryConvertEnum(this Type enumType, object? value, out object? converted)
    {
        if (value is string name
            && Enum.TryParse(enumType.GetNonNullableType(), name, ignoreCase: true, out converted)
            && converted is not null)
            return true;

        converted = null;
        return value is not null && enumType.TryConvertToEnum(value, out converted);
    }

    /// <summary>
    ///     Validates if a value can be used with an enum property.
    ///     For non-nullable enums with null values, or invalid enum values, returns false.
    ///     Supports both single enum values and arrays of enum values (for In/NotIn operations).
    /// </summary>
    /// <param name="type">The property type (can be nullable enum or null if property not found).</param>
    /// <param name="value">The value to validate (can be single value or array/collection).</param>
    /// <returns>True if the value is valid for the enum type; otherwise, false.</returns>
    internal static bool ValidateEnumValue(this Type? type, object? value)
    {
        if (type == null || !type.IsEnumType()) return true;

        if (value == null)
            // Null is valid for nullable enum
            return Nullable.GetUnderlyingType(type) != null;

        var enumType = type.GetNonNullableType();

        // Handle single value: TryCoerceValue converts it to the enum type further down the pipeline,
        // so any value convertible to the underlying enum type is acceptable here.
        if (value is not (IEnumerable enumerable and not string)) return enumType.TryConvertEnum(value, out _);

        // Handle array/collection of values (for In/NotIn operations): element-wise coercion of non-string
        // elements is out of scope (DRK-39), so an array is only valid when its elements are already the
        // enum type itself (e.g. OrderStatus[]) - an int[] would reach the Dynamic LINQ parser unconverted
        // and throw on Contains() type-mismatch, so it must be rejected here instead. A string[] is coerced
        // to OrderStatus[] by TryCoerceArray before reaching this check.
        return enumerable.OfType<object>().All(enumType.IsInstanceOfType);
    }

    /// <summary>
    ///     Attempts to coerce a scalar filter value to the resolved property's non-nullable CLR type, so
    ///     that values arriving as strings (query strings, JSON bodies, UI forms) can be used against
    ///     numeric, boolean, date/time, <see cref="Guid" />, and enum properties without throwing at
    ///     Dynamic LINQ parse time.
    /// </summary>
    /// <param name="propertyType">The resolved property type (can be a nullable value type).</param>
    /// <param name="value">The filter value to coerce (can be null).</param>
    /// <param name="coerced">
    ///     When this method returns, the coerced value: <see langword="null" /> when <paramref name="value" />
    ///     is <see langword="null" />, the original <paramref name="value" /> when no coercion was needed or
    ///     applicable, or the converted value on a successful conversion.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> when <paramref name="value" /> is <see langword="null" />, is already
    ///     assignable to <paramref name="propertyType" />, is not a type this method knows how to convert, or
    ///     was successfully converted; <see langword="false" /> when conversion was attempted and failed.
    /// </returns>
    internal static bool TryCoerceValue(this Type propertyType, object? value, out object? coerced)
    {
        if (value == null)
        {
            coerced = null;
            return true;
        }

        var targetType = propertyType.GetNonNullableType();
        if (targetType.IsInstanceOfType(value))
        {
            coerced = value;
            return true;
        }

        if (targetType.IsEnumType())
            return targetType.TryConvertEnum(value, out coerced);

        if (!targetType.IsNumericType() && !CoercibleNonEnumTypes.Contains(targetType))
        {
            // Not a type we know how to coerce - leave the value as-is (unchanged behaviour).
            coerced = value;
            return true;
        }

        var text = value is string raw ? raw.Trim() : null;

        try
        {
            coerced = targetType switch
            {
                _ when targetType == typeof(Guid) =>
                    Guid.Parse(text ?? value.ToString()!, CultureInfo.InvariantCulture),
                // DateTimeOffset has no TypeCode, so Convert.ChangeType cannot reach it — parse explicitly.
                _ when targetType == typeof(DateTimeOffset) =>
                    DateTimeOffset.Parse(text ?? value.ToString()!, CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind),
                _ when targetType == typeof(DateOnly) =>
                    DateOnly.Parse(text ?? value.ToString()!, CultureInfo.InvariantCulture),
                _ when targetType == typeof(TimeOnly) =>
                    TimeOnly.Parse(text ?? value.ToString()!, CultureInfo.InvariantCulture),
                _ => Convert.ChangeType(text ?? value, targetType, CultureInfo.InvariantCulture)
            };
            return true;
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException
                                        or ArgumentException)
        {
            coerced = null;
            return false;
        }
    }

    /// <summary>
    ///     Coerces every element of a string collection supplied for an <c>In</c>/<c>NotIn</c> operation to the
    ///     resolved property's CLR type, producing a strongly typed array the Dynamic LINQ
    ///     <c>@0.Contains(prop)</c> clause can bind against. Without this, the <c>string[]</c> a query string
    ///     yields reaches the parser unconverted and throws on a <c>Contains()</c> type mismatch.
    /// </summary>
    /// <param name="propertyType">The resolved property type; a nullable type yields a nullable element array.</param>
    /// <param name="values">The string values to coerce.</param>
    /// <param name="coerced">The typed array on success; otherwise <see langword="null" />.</param>
    /// <returns>
    ///     <see langword="true" /> when every element coerced successfully; <see langword="false" /> when
    ///     <paramref name="values" /> is empty or any element failed to convert.
    /// </returns>
    internal static bool TryCoerceArray(this Type propertyType, IEnumerable<string> values, out object? coerced)
    {
        coerced = null;

        var elementType = propertyType.GetNonNullableType();
        var converted = new List<object>();
        foreach (var item in values)
        {
            if (!elementType.TryCoerceValue(item, out var element) || element is null) return false;
            converted.Add(element);
        }

        if (converted.Count == 0) return false;

        // Build the array as the property's own type (nullable included) so a Nullable<T> property still
        // binds — Array.SetValue boxes a T into a T?[] slot for us.
        var array = Array.CreateInstance(propertyType, converted.Count);
        for (var i = 0; i < converted.Count; i++) array.SetValue(converted[i], i);

        coerced = array;
        return true;
    }

    #endregion
}
