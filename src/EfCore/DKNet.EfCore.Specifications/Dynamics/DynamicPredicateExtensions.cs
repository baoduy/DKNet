// <copyright file="DynamicPredicateExtensions.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Linq.Dynamic.Core;
using System.Linq.Dynamic.Core.Exceptions;
using System.Linq.Expressions;
using DKNet.EfCore.Specifications.Dynamics;
using DKNet.EfCore.Specifications.Extensions;

// ReSharper disable once CheckNamespace
namespace LinqKit;

/// <summary>
///     Provides dynamic predicate helpers for LinqKit predicates.
/// </summary>
public static class DynamicPredicateExtensions
{
    #region Methods

    /// <summary>
    ///     Builds a dynamic predicate for a property filter.
    /// </summary>
    /// <typeparam name="T">Entity type.</typeparam>
    /// <param name="propertyName">Property name or dotted path.</param>
    /// <param name="operation">Filter operation.</param>
    /// <param name="value">Filter value.</param>
    /// <returns>A parsed predicate, or <see langword="null"/> when invalid.</returns>
    private static Expression<Func<T, bool>>? BuildDynamicExpression<T>(string propertyName,
        Ops operation, object? value)
    {
        // Validate property name contains only safe characters before any processing
        if (!DynamicPredicateBuilderExtensions.IsValidPropertyName(propertyName))
            return null;

        // Normalize property path using PropertyNameExtensions (PascalCase each segment)
        var normalizedPath = propertyName.ToPascalCase();

        var propType = typeof(T).ResolvePropertyType(normalizedPath);
        if (propType == null)
            return null;

        // IsNull/IsNotNull take no value, so none of the value validations below can apply — an enum column
        // would otherwise be rejected because no ignored value converts to the enum. Build and parse the
        // value-less clause directly.
        if (operation is Ops.IsNull or Ops.IsNotNull)
        {
            var nullClause = DynamicPredicateBuilderExtensions.BuildClause(normalizedPath, operation, null, 0);
            try
            {
                return DynamicExpressionParser.ParseLambda<T, bool>(ParsingConfig.Default, false, nullClause);
            }
            catch (ParseException)
            {
                // A non-nullable value type cannot be compared to null; that field simply does not support
                // the operation, reported the same way as every other unusable condition.
                return null;
            }
        }

        // Validate array value for In/NotIn operations
        if (!DynamicPredicateBuilderExtensions.ValidateArrayValue(value, operation))
            return null;

        // Coerce a string collection for In/NotIn element-wise up front, so the string[] a query string yields
        // becomes the typed array both the enum validation below and the Dynamic LINQ Contains() clause need.
        // A collection of any other element type is left untouched for the checks below to accept (an already
        // typed OrderStatus[]) or skip (an int[] for an enum property - element-wise coercion of non-string
        // elements remains out of scope, DRK-39).
        var coercedValue = value;
        if (operation is Ops.In or Ops.NotIn && value is IEnumerable<string> textValues
            && !propType.TryCoerceArray(textValues, out coercedValue))
            return null;

        // Adjust operation for type
        var op = propType.AdjustOperationForValueType(operation);

        // Validate enum value if needed
        if (!propType.ValidateEnumValue(coercedValue))
            return null;

        // Coerce scalar values (e.g. strings) to the property's CLR type; In/NotIn arrays are already typed
        if (operation is not (Ops.In or Ops.NotIn) && !propType.TryCoerceValue(value, out coercedValue))
            return null;

        // Build the dynamic LINQ predicate string using shared BuildClause method
        var predicateString = DynamicPredicateBuilderExtensions.BuildClause(normalizedPath, op, coercedValue, 0);

        try
        {
            // Use System.Linq.Dynamic.Core to parse the predicate string
            // For In/NotIn, value is the array passed as @0 parameter
            return coercedValue == null
                ? DynamicExpressionParser.ParseLambda<T, bool>(ParsingConfig.Default, false, predicateString)
                : DynamicExpressionParser.ParseLambda<T, bool>(ParsingConfig.Default, false, predicateString,
                    coercedValue);
        }
        catch (ParseException)
        {
            // The clause resolved a property but the parser rejected the comparison — a navigation property
            // matched against a scalar value ("Category == \"x\"") being the reachable case. This is one more
            // kind of unusable condition, and it gets the same answer as every other one: null, so DynamicAnd
            // skips it and TryBuildPredicate reports it, instead of the exception surfacing as a 500 out of an
            // endpoint that was handed nothing but a query string.
            return null;
        }
    }

    /// <summary>
    ///     Builds a standalone predicate for a single property filter, reporting failure explicitly instead of
    ///     silently skipping it the way <c>DynamicAnd</c>/<c>DynamicOr</c> do.
    /// </summary>
    /// <remarks>
    ///     Silently dropping an unusable condition is the right behaviour when composing internally — a caller
    ///     that passes an absent optional filter wants the predicate unchanged. It is the wrong behaviour at a
    ///     trust boundary such as an HTTP endpoint, where a dropped filter hands the caller unfiltered data
    ///     under a query that looked like it applied. Validate with this first, then compose.
    /// </remarks>
    /// <typeparam name="T">Entity type.</typeparam>
    /// <param name="propertyName">Property name or dotted path.</param>
    /// <param name="operation">Filter operation.</param>
    /// <param name="value">Filter value; a collection for <see cref="Ops.In" />/<see cref="Ops.NotIn" />.</param>
    /// <param name="predicate">The parsed predicate on success; otherwise <see langword="null" />.</param>
    /// <returns>
    ///     <see langword="true" /> when the property resolved on <typeparamref name="T" /> and
    ///     <paramref name="value" /> was usable with <paramref name="operation" />; otherwise
    ///     <see langword="false" />.
    /// </returns>
    public static bool TryBuildPredicate<T>(string propertyName, Ops operation, object? value,
        out Expression<Func<T, bool>>? predicate)
    {
        predicate = BuildDynamicExpression<T>(propertyName, operation, value);
        return predicate is not null;
    }

    /// <summary>
    ///     Validates and parses a raw dynamic LINQ expression string into a typed lambda.
    /// </summary>
    /// <typeparam name="T">Entity type.</typeparam>
    /// <param name="expression">Dynamic LINQ expression.</param>
    /// <param name="values">Expression parameter values.</param>
    /// <returns>The parsed predicate expression.</returns>
    private static Expression<Func<T, bool>> ParseDynamicExpression<T>(string expression, object?[] values)
    {
        DynamicPredicateBuilderExtensions.ValidateExpression(expression);
        return DynamicExpressionParser.ParseLambda<T, bool>(ParsingConfig.Default, false, expression, values);
    }

    #endregion

    extension<T>(ExpressionStarter<T> predicate)
    {
        /// <summary>
        ///     Adds a dynamic condition using AND.
        /// </summary>
        /// <remarks>
        ///     If <paramref name="value" /> cannot be converted to the target property's type, the condition
        ///     is silently skipped and the predicate is returned unchanged, rather than throwing.
        /// </remarks>
        /// <param name="propertyName">Property name or dotted path.</param>
        /// <param name="operation">Filter operation.</param>
        /// <param name="value">Filter value.</param>
        /// <returns>The combined predicate.</returns>
        public Expression<Func<T, bool>> DynamicAnd(string propertyName, Ops operation, object? value)
        {
            var dynamicExpression = BuildDynamicExpression<T>(propertyName, operation, value);
            return dynamicExpression == null ? predicate : predicate.And(dynamicExpression);
        }

        /// <summary>
        ///     Adds a dynamic condition using OR.
        /// </summary>
        /// <remarks>
        ///     If <paramref name="value" /> cannot be converted to the target property's type, the condition
        ///     is silently skipped and the predicate is returned unchanged, rather than throwing.
        /// </remarks>
        /// <param name="propertyName">Property name or dotted path.</param>
        /// <param name="operation">Filter operation.</param>
        /// <param name="value">Filter value.</param>
        /// <returns>The combined predicate.</returns>
        public Expression<Func<T, bool>> DynamicOr(string propertyName, Ops operation, object? value)
        {
            var dynamicExpression = BuildDynamicExpression<T>(propertyName, operation, value);
            return dynamicExpression == null ? predicate : predicate.Or(dynamicExpression);
        }

        /// <summary>
        ///     Parses a dynamic LINQ expression and combines it using AND.
        /// </summary>
        /// <param name="expression">Dynamic LINQ expression.</param>
        /// <param name="values">Expression parameter values.</param>
        /// <returns>The combined predicate.</returns>
        public ExpressionStarter<T> DynamicAnd(string expression,
            params object?[] values) =>
            predicate.And(ParseDynamicExpression<T>(expression, values));

        /// <summary>
        ///     Parses a dynamic LINQ expression and combines it using OR.
        /// </summary>
        /// <param name="expression">Dynamic LINQ expression.</param>
        /// <param name="values">Expression parameter values.</param>
        /// <returns>The combined predicate.</returns>
        public ExpressionStarter<T> DynamicOr(string expression,
            params object?[] values) =>
            predicate.Or(ParseDynamicExpression<T>(expression, values));
    }

    extension<T>(Expression<Func<T, bool>> predicate)
    {
        /// <summary>
        ///     Adds a dynamic condition using AND.
        /// </summary>
        /// <remarks>
        ///     If <paramref name="value" /> cannot be converted to the target property's type, the condition
        ///     is silently skipped and the predicate is returned unchanged, rather than throwing.
        /// </remarks>
        /// <param name="propertyName">Property name or dotted path.</param>
        /// <param name="operation">Filter operation.</param>
        /// <param name="value">Filter value.</param>
        /// <returns>The combined predicate.</returns>
        public Expression<Func<T, bool>> DynamicAnd(string propertyName, Ops operation, object? value)
        {
            var dynamicExpression = BuildDynamicExpression<T>(propertyName, operation, value);
            return dynamicExpression == null ? predicate : predicate.And(dynamicExpression);
        }

        /// <summary>
        ///     Adds a dynamic condition using OR.
        /// </summary>
        /// <remarks>
        ///     If <paramref name="value" /> cannot be converted to the target property's type, the condition
        ///     is silently skipped and the predicate is returned unchanged, rather than throwing.
        /// </remarks>
        /// <param name="propertyName">Property name or dotted path.</param>
        /// <param name="operation">Filter operation.</param>
        /// <param name="value">Filter value.</param>
        /// <returns>The combined predicate.</returns>
        public Expression<Func<T, bool>> DynamicOr(string propertyName, Ops operation, object? value)
        {
            var dynamicExpression = BuildDynamicExpression<T>(propertyName, operation, value);
            return dynamicExpression == null ? predicate : predicate.Or(dynamicExpression);
        }

        /// <summary>
        ///     Parses a dynamic LINQ expression and combines it using AND.
        /// </summary>
        /// <param name="expression">Dynamic LINQ expression.</param>
        /// <param name="values">Expression parameter values.</param>
        /// <returns>The combined predicate.</returns>
        public ExpressionStarter<T> DynamicAnd(string expression,
            params object?[] values) =>
            predicate.And(ParseDynamicExpression<T>(expression, values));

        /// <summary>
        ///     Parses a dynamic LINQ expression and combines it using OR.
        /// </summary>
        /// <param name="expression">Dynamic LINQ expression.</param>
        /// <param name="values">Expression parameter values.</param>
        /// <returns>The combined predicate.</returns>
        public ExpressionStarter<T> DynamicOr(string expression,
            params object?[] values) =>
            predicate.Or(ParseDynamicExpression<T>(expression, values));
    }
}