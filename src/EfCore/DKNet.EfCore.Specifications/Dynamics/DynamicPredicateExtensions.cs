// <copyright file="DynamicPredicateExtensions.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using DKNet.EfCore.Specifications.Dynamics;
using DKNet.EfCore.Specifications.Extensions;

// ReSharper disable once CheckNamespace
namespace LinqKit;

/// <summary>
///     Extension methods for combining dynamic LINQ expressions with LinqKit's ExpressionStarter.
///     Provides fluent API for building complex predicates using string-based dynamic expressions.
/// </summary>
public static class DynamicPredicateExtensions
{
    #region Methods

    /// <summary>
    ///     Converts an array of values to a properly typed enum array for In/NotIn operations.
    ///     This ensures EF Core can properly translate the Contains expression.
    /// </summary>
    /// <param name="enumerable">The source enumerable containing values to convert</param>
    /// <param name="enumType">The target enum type</param>
    /// <returns>A properly typed array of enum values</returns>
    // private static object ConvertToEnumArray(IEnumerable enumerable, Type enumType)
    // {
    //     var convertedList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(enumType))!;
    //
    //     foreach (var item in enumerable)
    //     {
    //         if (item != null && enumType.TryConvertToEnum(item, out var enumValue))
    //         {
    //             convertedList.Add(enumValue!); // TryConvertToEnum guarantees non-null on success
    //         }
    //     }
    //
    //     // Convert List<TEnum> to TEnum[] using reflection to maintain generic type info
    //     var toArrayMethod = typeof(Enumerable).GetMethod("ToArray")!.MakeGenericMethod(enumType);
    //     return toArrayMethod.Invoke(null, [convertedList])!;
    // }

    /// <summary>
    ///     Builds a dynamic predicate expression for the given property, operation, and value using System.Linq.Dynamic.Core.
    ///     Returns null if the property is not found or the value is invalid for the property type.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="propertyName">Property name or path (dot notation supported)</param>
    /// <param name="operation">Operation to perform</param>
    /// <param name="value">Value to compare</param>
    /// <returns>Expression or null if not valid</returns>
    private static Expression<Func<T, bool>>? BuildDynamicExpression<T>(string propertyName,
        Ops operation, object? value)
    {
        // Normalize property path using PropertyNameExtensions (PascalCase each segment)
        var normalizedPath = propertyName.ToPascalCase();

        var propType = typeof(T).ResolvePropertyType(normalizedPath);
        if (propType == null)
            return null;

        // Validate array value for In/NotIn operations
        if (!DynamicPredicateBuilderExtensions.ValidateArrayValue(value, operation))
            return null;

        // Adjust operation for type
        var op = propType.AdjustOperationForValueType(operation);

        // Validate enum value if needed
        if (!propType.ValidateEnumValue(value))
            return null;

        // Build the dynamic LINQ predicate string using shared BuildClause method
        var predicateString = DynamicPredicateBuilderExtensions.BuildClause(normalizedPath, op, value, 0);


        // Use System.Linq.Dynamic.Core to parse the predicate string
        // For In/NotIn, value is the array passed as @0 parameter
        var lambda = value == null
            ? DynamicExpressionParser.ParseLambda<T, bool>(ParsingConfig.Default, false, predicateString)
            : DynamicExpressionParser.ParseLambda<T, bool>(ParsingConfig.Default, false, predicateString, value);

        return lambda;
    }

    /// <summary>
    ///     Combines the existing predicate with a new dynamic condition using AND logic.
    ///     If the dynamic expression is null or empty, the original predicate is returned unchanged.
    ///     Property name/path may be provided in camelCase, snake_case, kebab-case or mixed; each dotted segment is
    ///     normalized to PascalCase.
    /// </summary>
    /// <typeparam name="T">The type of the entity being queried</typeparam>
    /// <param name="predicate">The existing expression starter to extend</param>
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
    ///     The extended <see cref="ExpressionStarter{T}" /> with the AND condition applied, or the original predicate if the
    ///     dynamic expression is null
    /// </returns>
    public static Expression<Func<T, bool>> DynamicAnd<T>(this ExpressionStarter<T> predicate,
        string propertyName, Ops operation, object? value)
    {
        var dynamicExpression = BuildDynamicExpression<T>(propertyName, operation, value);
        return dynamicExpression == null ? predicate : predicate.And(dynamicExpression);
    }

    /// <summary>
    ///     Combines the existing predicate with a new dynamic condition using AND logic.
    ///     If the dynamic expression is null or empty, the original predicate is returned unchanged.
    ///     Property name/path may be provided in camelCase, snake_case, kebab-case or mixed; each dotted segment is
    ///     normalized to PascalCase.
    /// </summary>
    /// <typeparam name="T">The type of the entity being queried</typeparam>
    /// <param name="predicate">The existing expression starter to extend</param>
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
    ///     The extended <see cref="ExpressionStarter{T}" /> with the AND condition applied, or the original predicate if the
    ///     dynamic expression is null
    /// </returns>
    public static Expression<Func<T, bool>> DynamicAnd<T>(this Expression<Func<T, bool>> predicate,
        string propertyName, Ops operation, object? value)
    {
        var dynamicExpression = BuildDynamicExpression<T>(propertyName, operation, value);
        return dynamicExpression == null ? predicate : predicate.And(dynamicExpression);
    }

    /// <summary>
    ///     Combines the existing predicate with a new dynamic condition using OR logic.
    ///     If the dynamic expression is null or empty, the original predicate is returned unchanged.
    ///     Property name/path may be provided in camelCase, snake_case, kebab-case or mixed; each dotted segment is
    ///     normalized to PascalCase.
    /// </summary>
    /// <typeparam name="T">The type of the entity being queried</typeparam>
    /// <param name="predicate">The existing expression starter to extend</param>
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
    ///     The extended <see cref="ExpressionStarter{T}" /> with the OR condition applied, or the original predicate if the
    ///     dynamic expression is null
    /// </returns>
    public static Expression<Func<T, bool>> DynamicOr<T>(
        this ExpressionStarter<T> predicate,
        string propertyName, Ops operation, object? value)
    {
        var dynamicExpression = BuildDynamicExpression<T>(propertyName, operation, value);
        return dynamicExpression == null ? predicate : predicate.Or(dynamicExpression);
    }

    /// <summary>
    ///     Combines the existing predicate with a new dynamic condition using OR logic.
    ///     If the dynamic expression is null or empty, the original predicate is returned unchanged.
    ///     Property name/path may be provided in camelCase, snake_case, kebab-case or mixed; each dotted segment is
    ///     normalized to PascalCase.
    /// </summary>
    /// <typeparam name="T">The type of the entity being queried</typeparam>
    /// <param name="predicate">The existing expression starter to extend</param>
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
    ///     The extended <see cref="ExpressionStarter{T}" /> with the OR condition applied, or the original predicate if the
    ///     dynamic expression is null
    /// </returns>
    public static Expression<Func<T, bool>> DynamicOr<T>(
        this Expression<Func<T, bool>> predicate,
        string propertyName, Ops operation, object? value)
    {
        var dynamicExpression = BuildDynamicExpression<T>(propertyName, operation, value);
        return dynamicExpression == null ? predicate : predicate.Or(dynamicExpression);
    }

    #endregion
}