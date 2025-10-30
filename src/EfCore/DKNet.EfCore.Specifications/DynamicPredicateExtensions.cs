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
    ///     Combines the existing predicate with a new dynamic condition using AND logic.
    /// </summary>
    /// <typeparam name="T">The type of the entity being queried</typeparam>
    /// <param name="predicate">The existing expression starter to extend</param>
    /// <param name="builder">
    ///     Parameter values for the dynamic expression, corresponding to @0, @1, @2, etc.
    /// </param>
    /// <returns>
    ///     The extended <see cref="ExpressionStarter{T}" /> with the AND condition applied
    /// </returns>
    public static Expression<Func<T, bool>> DynamicAnd<T>(this Expression<Func<T, bool>> predicate,
        Action<DynamicPredicateBuilder> builder)
    {
        var dynamic = new DynamicPredicateBuilder();
        builder(dynamic);

        var (expression, values) = dynamic.Build();
        var newExpr = DynamicExpressionParser.ParseLambda<T, bool>(
            new ParsingConfig { ResolveTypesBySimpleName = true },
            false,
            expression,
            values
        );

        return predicate.And(newExpr);
    }

    /// <summary>
    ///     Combines the existing predicate with a new dynamic condition using AND logic.
    /// </summary>
    /// <typeparam name="T">The type of the entity being queried</typeparam>
    /// <param name="predicate">The existing expression starter to extend</param>
    /// <param name="builder">
    ///     Parameter values for the dynamic expression, corresponding to @0, @1, @2, etc.
    /// </param>
    /// <returns>
    ///     The extended <see cref="ExpressionStarter{T}" /> with the AND condition applied
    /// </returns>
    public static Expression<Func<T, bool>> DynamicAnd<T>(this ExpressionStarter<T> predicate,
        Action<DynamicPredicateBuilder> builder)
    {
        var dynamic = new DynamicPredicateBuilder();
        builder(dynamic);

        var (expression, values) = dynamic.Build();
        var newExpr = DynamicExpressionParser.ParseLambda<T, bool>(
            new ParsingConfig { ResolveTypesBySimpleName = true },
            false,
            expression,
            values
        );

        return predicate.And(newExpr);
    }

    /// <summary>
    ///     Combines the existing predicate with a new dynamic condition using OR logic.
    /// </summary>
    /// <typeparam name="T">The type of the entity being queried</typeparam>
    /// <param name="predicate">The existing expression starter to extend</param>
    /// <param name="builder">
    ///     A string-based LINQ expression (e.g., "Age > 18 and Name.Contains(@0)")
    /// </param>
    /// <returns>
    ///     The extended <see cref="ExpressionStarter{T}" /> with the OR condition applied
    /// </returns>
    public static Expression<Func<T, bool>> DynamicOr<T>(
        this Expression<Func<T, bool>> predicate,
        Action<DynamicPredicateBuilder> builder)
    {
        var dynamic = new DynamicPredicateBuilder();
        builder(dynamic);

        var (expression, values) = dynamic.Build();

        var newExpr = DynamicExpressionParser.ParseLambda<T, bool>(
            new ParsingConfig { ResolveTypesBySimpleName = true },
            false,
            expression,
            values
        );

        return predicate.Or(newExpr);
    }

    /// <summary>
    ///     Combines the existing predicate with a new dynamic condition using OR logic.
    /// </summary>
    /// <typeparam name="T">The type of the entity being queried</typeparam>
    /// <param name="predicate">The existing expression starter to extend</param>
    /// <param name="builder">
    ///     A string-based LINQ expression (e.g., "Age > 18 and Name.Contains(@0)")
    /// </param>
    /// <returns>
    ///     The extended <see cref="ExpressionStarter{T}" /> with the OR condition applied
    /// </returns>
    public static Expression<Func<T, bool>> DynamicOr<T>(
        this ExpressionStarter<T> predicate,
        Action<DynamicPredicateBuilder> builder)
    {
        var dynamic = new DynamicPredicateBuilder();
        builder(dynamic);

        var (expression, values) = dynamic.Build();

        var newExpr = DynamicExpressionParser.ParseLambda<T, bool>(
            new ParsingConfig { ResolveTypesBySimpleName = true },
            false,
            expression,
            values
        );

        return predicate.Or(newExpr);
    }

    #endregion
}