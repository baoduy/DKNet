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

    #endregion

    extension<T>(ExpressionStarter<T> predicate)
    {
        /// <summary>
        ///     Adds a dynamic condition using AND.
        /// </summary>
        /// <typeparam name="T">Entity type.</typeparam>
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
        /// <typeparam name="T">Entity type.</typeparam>
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
            params object?[] values)
        {
            var dynamicExpr =
                DynamicExpressionParser.ParseLambda<T, bool>(ParsingConfig.Default, false, expression, values);
            return predicate.And(dynamicExpr);
        }

        /// <summary>
        ///     Parses a dynamic LINQ expression and combines it using OR.
        /// </summary>
        /// <param name="expression">Dynamic LINQ expression.</param>
        /// <param name="values">Expression parameter values.</param>
        /// <returns>The combined predicate.</returns>
        public ExpressionStarter<T> DynamicOr(string expression,
            params object?[] values)
        {
            var dynamicExpr =
                DynamicExpressionParser.ParseLambda<T, bool>(ParsingConfig.Default, false, expression, values);
            return predicate.Or(dynamicExpr);
        }
    }

    extension<T>(Expression<Func<T, bool>> predicate)
    {
        /// <summary>
        ///     Adds a dynamic condition using AND.
        /// </summary>
        /// <typeparam name="T">Entity type.</typeparam>
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
        /// <typeparam name="T">Entity type.</typeparam>
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
            params object?[] values)
        {
            var dynamicExpr =
                DynamicExpressionParser.ParseLambda<T, bool>(ParsingConfig.Default, false, expression, values);
            return predicate.And(dynamicExpr);
        }

        /// <summary>
        ///     Parses a dynamic LINQ expression and combines it using OR.
        /// </summary>
        /// <param name="expression">Dynamic LINQ expression.</param>
        /// <param name="values">Expression parameter values.</param>
        /// <returns>The combined predicate.</returns>
        public ExpressionStarter<T> DynamicOr(string expression,
            params object?[] values)
        {
            var dynamicExpr =
                DynamicExpressionParser.ParseLambda<T, bool>(ParsingConfig.Default, false, expression, values);
            return predicate.Or(dynamicExpr);
        }
    }
}