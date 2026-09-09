// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: ActivityWindow.cs
// Description: Builds the recent-activity-window predicate the generic list endpoint ANDs onto its filter.

using System.Collections.Concurrent;
using System.Linq.Expressions;
using DKNet.EfCore.Abstractions.Entities;

namespace DKNet.AspCore.Extensions.Endpoints;

/// <summary>
///     Builds the <c>fromDate</c>/<c>toDate</c> activity-window predicate for the generic list endpoints
///     (DRK-1166 §3 row 5).
/// </summary>
/// <remarks>
///     Follows <see cref="ListQuery" />'s free-text search shape: a predicate template built once per entity
///     type — here via the <see cref="Expression" /> API directly rather than
///     Dynamic LINQ, since <see cref="IAuditedProperties.CreatedOn" />/<see cref="IAuditedProperties.UpdatedOn" />
///     are fixed members rather than a caller-named field — with its bound values read through a
///     <see cref="ActivityWindowBox" /> placeholder instance so <see cref="ActivityWindowBoxSwap" /> can swap in
///     a fresh box per request instead of rebuilding the expression tree, and so EF Core binds both bounds as
///     query parameters rather than inlining them as literals (R11).
/// </remarks>
internal static class ActivityWindow
{
    #region Fields

    /// <summary>
    ///     Templates cached per entity type, so the expression tree is built once ever instead of once per
    ///     request. <see langword="null" /> caches a type that carries no audit timestamps
    ///     (R6 — checked via <see cref="IAuditedProperties" />, deliberately wider than <see cref="IAuditedEntity{TKey}" />).
    /// </summary>
    private static readonly ConcurrentDictionary<Type, ActivityWindowTemplate?> Templates = new();

    #endregion

    #region Methods

    /// <summary>
    ///     Builds the activity-window predicate for a resolved <c>(from, to)</c> pair.
    /// </summary>
    /// <typeparam name="TEntity">Entity type the predicate applies to.</typeparam>
    /// <param name="from">Inclusive lower bound, or <see langword="null" /> for open.</param>
    /// <param name="to">Inclusive upper bound, or <see langword="null" /> for open.</param>
    /// <returns>
    ///     The predicate, or <see langword="null" /> when both bounds are absent or <typeparamref name="TEntity" />
    ///     carries no audit timestamps (R7 — the bounds are ignored, not refused).
    /// </returns>
    internal static Expression<Func<TEntity, bool>>? Build<TEntity>(DateTimeOffset? from, DateTimeOffset? to)
        where TEntity : class
    {
        if (from is null && to is null) return null;

        var template = Templates.GetOrAdd(typeof(TEntity), static _ => BuildTemplate<TEntity>());
        if (template is null) return null;

        var box = new ActivityWindowBox { From = from, To = to };
        var swapped = new ActivityWindowBoxSwap(template.Placeholder, box).Visit(template.Expression);
        return (Expression<Func<TEntity, bool>>)swapped;
    }

    /// <summary>
    ///     Builds the <c>(CreatedOn in range) || (UpdatedOn is not null and UpdatedOn in range)</c> predicate
    ///     (R4) for <typeparamref name="TEntity" />, reading its bounds through a placeholder
    ///     <see cref="ActivityWindowBox" /> embedded as a direct <see cref="ConstantExpression" /> — not a
    ///     compiler-captured closure field — so <see cref="ActivityWindowBoxSwap" /> can find and replace it by
    ///     reference.
    /// </summary>
    /// <remarks>
    ///     Reads <c>CreatedOn</c>/<c>UpdatedOn</c> straight off <typeparamref name="TEntity" /> itself via
    ///     reflection rather than casting the parameter to <see cref="IAuditedProperties" />: EF Core's query
    ///     translator does not reliably see through an interface cast on the query root, even though the
    ///     concrete property access it resolves to is identical.
    /// </remarks>
    /// <typeparam name="TEntity">Entity type to build the predicate over.</typeparam>
    /// <returns>The template, or <see langword="null" /> when <typeparamref name="TEntity" /> is not audited.</returns>
    private static ActivityWindowTemplate? BuildTemplate<TEntity>()
        where TEntity : class
    {
        if (!typeof(IAuditedProperties).IsAssignableFrom(typeof(TEntity))) return null;

        var entityParam = Expression.Parameter(typeof(TEntity), "x");
        var createdOnProperty = typeof(TEntity).GetProperty(nameof(IAuditedProperties.CreatedOn))!;
        var updatedOnProperty = typeof(TEntity).GetProperty(nameof(IAuditedProperties.UpdatedOn))!;
        var createdOn = Expression.Property(entityParam, createdOnProperty);
        var updatedOn = Expression.Property(entityParam, updatedOnProperty);

        var placeholder = new ActivityWindowBox();
        var box = Expression.Constant(placeholder);
        var from = Expression.Property(box, nameof(ActivityWindowBox.From));
        var to = Expression.Property(box, nameof(ActivityWindowBox.To));
        var noBound = Expression.Constant(null, typeof(DateTimeOffset?));

        var createdInWindow = InWindow(createdOn, from, to, noBound);
        var updatedInWindow = Expression.AndAlso(
            Expression.NotEqual(updatedOn, noBound),
            InWindow(updatedOn, from, to, noBound));

        var body = Expression.OrElse(createdInWindow, updatedInWindow);
        var lambda = Expression.Lambda<Func<TEntity, bool>>(body, entityParam);
        return new ActivityWindowTemplate(lambda, placeholder);
    }

    /// <summary>
    ///     <c>(from == null || value &gt;= from) &amp;&amp; (to == null || value &lt;= to)</c> — an omitted side
    ///     imposes no comparison on that side.
    /// </summary>
    /// <param name="value">The instant to test, widened to <see cref="Nullable{DateTimeOffset}" /> if needed.</param>
    /// <param name="from">The lower-bound member access.</param>
    /// <param name="to">The upper-bound member access.</param>
    /// <param name="noBound">A typed <see langword="null" /> constant to compare bounds against.</param>
    /// <returns>The in-window comparison expression.</returns>
    private static Expression InWindow(Expression value, Expression from, Expression to, Expression noBound)
    {
        var nullableValue = value.Type == typeof(DateTimeOffset?)
            ? value
            : Expression.Convert(value, typeof(DateTimeOffset?));

        var afterFrom = Expression.OrElse(
            Expression.Equal(from, noBound),
            Expression.GreaterThanOrEqual(nullableValue, from));
        var beforeTo = Expression.OrElse(
            Expression.Equal(to, noBound),
            Expression.LessThanOrEqual(nullableValue, to));

        return Expression.AndAlso(afterFrom, beforeTo);
    }

    #endregion
}

/// <summary>An activity-window predicate, built once, plus the placeholder its bounds are read through.</summary>
/// <param name="Expression">The built predicate; its bound comparisons read through <paramref name="Placeholder" />.</param>
/// <param name="Placeholder">The <see cref="ActivityWindowBox" /> instance embedded as the predicate's constant.</param>
internal sealed record ActivityWindowTemplate(LambdaExpression Expression, ActivityWindowBox Placeholder);

/// <summary>
///     Mutable holder for an activity window's bounds. A cached predicate template reads the bounds through a
///     member access on an instance of this class rather than a literal, so the bounds can be swapped per
///     request without rebuilding the expression tree, and so EF Core binds them as query parameters instead of
///     inlining them (R11).
/// </summary>
internal sealed class ActivityWindowBox
{
    /// <summary>Inclusive lower bound, or <see langword="null" /> for open.</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>Inclusive upper bound, or <see langword="null" /> for open.</summary>
    public DateTimeOffset? To { get; init; }
}

/// <summary>Replaces every reference to one <see cref="ActivityWindowBox" /> in an expression tree with another.</summary>
/// <param name="from">The placeholder instance to replace.</param>
/// <param name="to">The instance to replace it with.</param>
internal sealed class ActivityWindowBoxSwap(ActivityWindowBox from, ActivityWindowBox to) : ExpressionVisitor
{
    /// <inheritdoc />
    protected override Expression VisitConstant(ConstantExpression node) =>
        ReferenceEquals(node.Value, from) ? Expression.Constant(to) : node;
}
