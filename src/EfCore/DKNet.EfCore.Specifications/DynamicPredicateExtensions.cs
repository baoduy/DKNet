using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using DKNet.EfCore.Specifications;

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
    ///     Builds a dynamic predicate expression from the provided <paramref name="builder" /> action.
    ///     The builder accumulates a string-based expression and associated parameter values which are then
    ///     parsed into a strongly-typed <see cref="Expression" /> using <see cref="DynamicExpressionParser" />.
    /// </summary>
    /// <typeparam name="T">Entity type for which the predicate is being constructed.</typeparam>
    /// <param name="builder">Callback that configures the dynamic predicate (expression text and parameter values).</param>
    /// <param name="condition">The condition operator</param>
    /// <returns>A compiled <see cref="Expression{TDelegate}" /> representing the dynamic predicate.</returns>
    private static Expression<Func<T, bool>>? CreateDynamicExpression<T>(Action<DynamicPredicateBuilder<T>> builder,
        Conditions condition)
    {
        var dynamic = new DynamicPredicateBuilder<T>();
        builder(dynamic);

        var (expression, values) = dynamic.Build(condition);
        if (string.IsNullOrEmpty(expression)) return null;

        return DynamicExpressionParser.ParseLambda<T, bool>(
            new ParsingConfig { ResolveTypesBySimpleName = true },
            false,
            expression,
            values
        );
    }

    /// <summary>
    ///     Combines the existing predicate with a new dynamic condition using AND logic.
    ///     If the dynamic expression is null or empty, the original predicate is returned unchanged.
    /// </summary>
    /// <typeparam name="T">The type of the entity being queried</typeparam>
    /// <param name="predicate">The existing expression starter to extend</param>
    /// <param name="builder">
    ///     Parameter values for the dynamic expression, corresponding to @0, @1, @2, etc.
    /// </param>
    /// <returns>
    ///     The extended <see cref="ExpressionStarter{T}" /> with the AND condition applied, or the original predicate if the
    ///     dynamic expression is null
    /// </returns>
    public static Expression<Func<T, bool>> DynamicAnd<T>(this Expression<Func<T, bool>> predicate,
        Action<DynamicPredicateBuilder<T>> builder)
    {
        var dynamicExpression = CreateDynamicExpression(builder, Conditions.And);
        return dynamicExpression == null ? predicate : predicate.And(dynamicExpression);
    }

    /// <summary>
    ///     Combines the existing predicate with a new dynamic condition using AND logic.
    ///     If the dynamic expression is null or empty, the original predicate is returned unchanged.
    /// </summary>
    /// <typeparam name="T">The type of the entity being queried</typeparam>
    /// <param name="predicate">The existing expression starter to extend</param>
    /// <param name="builder">
    ///     Parameter values for the dynamic expression, corresponding to @0, @1, @2, etc.
    /// </param>
    /// <returns>
    ///     The extended <see cref="ExpressionStarter{T}" /> with the AND condition applied, or the original predicate if the
    ///     dynamic expression is null
    /// </returns>
    public static Expression<Func<T, bool>> DynamicAnd<T>(this ExpressionStarter<T> predicate,
        Action<DynamicPredicateBuilder<T>> builder)
    {
        var dynamicExpression = CreateDynamicExpression(builder, Conditions.And);
        return dynamicExpression == null ? predicate : predicate.And(dynamicExpression);
    }

    /// <summary>
    ///     Combines the existing predicate with a new dynamic condition using OR logic.
    ///     If the dynamic expression is null or empty, the original predicate is returned unchanged.
    /// </summary>
    /// <typeparam name="T">The type of the entity being queried</typeparam>
    /// <param name="predicate">The existing expression starter to extend</param>
    /// <param name="builder">
    ///     A string-based LINQ expression (e.g., "Age > 18 and Name.Contains(@0)")
    /// </param>
    /// <returns>
    ///     The extended <see cref="ExpressionStarter{T}" /> with the OR condition applied, or the original predicate if the
    ///     dynamic expression is null
    /// </returns>
    public static Expression<Func<T, bool>> DynamicOr<T>(
        this Expression<Func<T, bool>> predicate,
        Action<DynamicPredicateBuilder<T>> builder)
    {
        var dynamicExpression = CreateDynamicExpression(builder, Conditions.Or);
        return dynamicExpression == null ? predicate : predicate.Or(dynamicExpression);
    }

    /// <summary>
    ///     Combines the existing predicate with a new dynamic condition using OR logic.
    ///     If the dynamic expression is null or empty, the original predicate is returned unchanged.
    /// </summary>
    /// <typeparam name="T">The type of the entity being queried</typeparam>
    /// <param name="predicate">The existing expression starter to extend</param>
    /// <param name="builder">
    ///     A string-based LINQ expression (e.g., "Age > 18 and Name.Contains(@0)")
    /// </param>
    /// <returns>
    ///     The extended <see cref="ExpressionStarter{T}" /> with the OR condition applied, or the original predicate if the
    ///     dynamic expression is null
    /// </returns>
    public static Expression<Func<T, bool>> DynamicOr<T>(
        this ExpressionStarter<T> predicate,
        Action<DynamicPredicateBuilder<T>> builder)
    {
        var dynamicExpression = CreateDynamicExpression(builder, Conditions.Or);
        return dynamicExpression == null ? predicate : predicate.Or(dynamicExpression);
    }

    #endregion
}