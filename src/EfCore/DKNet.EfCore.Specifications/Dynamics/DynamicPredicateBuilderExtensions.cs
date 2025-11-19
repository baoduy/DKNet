// <copyright file="DynamicPredicateBuilderExtensions.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Reflection;
using DKNet.Fw.Extensions;

namespace DKNet.EfCore.Specifications.Dynamics;

/// <summary>
///     Internal extension methods for building dynamic LINQ predicates.
///     Provides reusable logic for type resolution, operation adjustment, and clause building.
/// </summary>
internal static class DynamicPredicateBuilderExtensions
{
    #region Methods

    /// <summary>
    ///     Adjusts the operation based on the property value type.
    ///     For non-string types, string operations (Contains, NotContains, StartsWith, EndsWith)
    ///     are converted to equality operations.
    /// </summary>
    /// <param name="propValueType">The type of the property value.</param>
    /// <param name="op">The requested operation.</param>
    /// <returns>The adjusted operation appropriate for the property type.</returns>
    internal static DynamicOperations AdjustOperationForValueType(this Type? propValueType, DynamicOperations op)
    {
        if (propValueType == null || propValueType == typeof(string) ||
            Nullable.GetUnderlyingType(propValueType) == typeof(string)) return op;

        // For all non-string types, switch Contains/NotContains to Equal/NotEqual
        return op switch
        {
            DynamicOperations.Contains => DynamicOperations.Equal,
            DynamicOperations.NotContains => DynamicOperations.NotEqual,
            DynamicOperations.StartsWith or DynamicOperations.EndsWith => DynamicOperations.Equal,
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
    internal static string BuildClause(string prop, DynamicOperations op, object? val, int paramIndex)
    {
        return val switch
        {
            null when op is DynamicOperations.Equal => $"{prop} == null",
            null when op is DynamicOperations.NotEqual => $"{prop} != null",
            _ => op switch
            {
                DynamicOperations.Equal => $"{prop} == @{paramIndex}",
                DynamicOperations.NotEqual => $"{prop} != @{paramIndex}",
                DynamicOperations.GreaterThan => $"{prop} > @{paramIndex}",
                DynamicOperations.GreaterThanOrEqual => $"{prop} >= @{paramIndex}",
                DynamicOperations.LessThan => $"{prop} < @{paramIndex}",
                DynamicOperations.LessThanOrEqual => $"{prop} <= @{paramIndex}",
                DynamicOperations.Contains => $"{prop}.Contains(@{paramIndex})",
                DynamicOperations.NotContains => $"!{prop}.Contains(@{paramIndex})",
                DynamicOperations.StartsWith => $"{prop}.StartsWith(@{paramIndex})",
                DynamicOperations.EndsWith => $"{prop}.EndsWith(@{paramIndex})",
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
    ///     Validates if a value can be used with an enum property.
    ///     For non-nullable enums with null values, or invalid enum values, returns false.
    /// </summary>
    /// <param name="type">The property type (can be nullable enum or null if property not found).</param>
    /// <param name="value">The value to validate.</param>
    /// <returns>True if the value is valid for the enum type; otherwise, false.</returns>
    internal static bool ValidateEnumValue(this Type? type, object? value)
    {
        if (type == null || !type.IsEnumType()) return true;

        if (value == null)
            // Null is valid for nullable enum
            return Nullable.GetUnderlyingType(type) != null;

        // Try to convert value to enum
        var enumType = type.GetNonNullableType();
        return enumType.TryConvertToEnum(value, out _);
    }

    #endregion
}