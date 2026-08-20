// <copyright file="KeysetQueryExtensions.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MR.EntityFrameworkCore.KeysetPagination;

namespace DKNet.EfCore.Specifications.Extensions;

/// <summary>
///     Provides extension methods for keyset (cursor-based) pagination on <see cref="IQueryable{T}" /> queries.
/// </summary>
/// <remarks>
///     <para>
///         Keyset pagination uses a cursor position instead of SQL OFFSET/FETCH, which makes it significantly
///         more efficient for large datasets. Instead of scanning all rows up to the offset, the database can
///         use an index seek directly to the cursor position.
///     </para>
///     <para>
///         <see cref="AfterKeyset{TEntity,TKey}" /> and <see cref="BeforeKeyset{TEntity,TKey}" /> (and their
///         two-key overloads) only add a <c>WHERE</c> predicate — the caller owns ordering via its own
///         <c>OrderBy</c>. <see cref="ToKeysetPageAsync{TEntity}" /> is the arbitrary-arity surface that
///         delegates to <c>MR.EntityFrameworkCore.KeysetPagination</c> for ordering, filtering, and the
///         has-previous/has-next existence checks.
///     </para>
///     <para>
///         For a composite keyset with two columns ordered ascending the generated SQL is:
///         <c>WHERE key1 &gt; cursor1 OR (key1 = cursor1 AND key2 &gt; cursor2)</c>
///         which is equivalent to the row-value comparison <c>(key1, key2) &gt; (cursor1, cursor2)</c>.
///     </para>
///     <para>
///         <b>Usage example (single key):</b>
///         <code>
///             // Get the next page of products after the last seen Id
///             var page = await context.Products
///                 .OrderBy(p => p.Id)
///                 .AfterKeyset(p => p.Id, lastSeenId)
///                 .Take(pageSize)
///                 .ToListAsync();
///         </code>
///     </para>
///     <para>
///         <b>Usage example (composite key):</b>
///         <code>
///             // Get the next page of orders after the last seen (OrderDate, Id) pair
///             var page = await context.Orders
///                 .OrderBy(o => o.OrderDate).ThenBy(o => o.Id)
///                 .AfterKeyset(o => o.OrderDate, o => o.Id, lastDate, lastId)
///                 .Take(pageSize)
///                 .ToListAsync();
///         </code>
///     </para>
///     <para>
///         <b>Usage example (arbitrary arity, forward/backward, has-previous/has-next):</b>
///         <code>
///             // Page merchants by country asc, revenue desc, identifier asc
///             var page = await context.Merchants.ToKeysetPageAsync(
///                 b => b.Ascending(m => m.Country).Descending(m => m.Revenue).Ascending(m => m.Id),
///                 pageSize,
///                 KeysetPaginationDirection.Forward,
///                 reference: lastSeenMerchant);
///         </code>
///     </para>
/// </remarks>
public static class KeysetQueryExtensions
{
    #region Methods

    /// <summary>
    ///     Applies a forward keyset cursor filter on a single key column (ascending order).
    ///     Generates: <c>WHERE key &gt; cursorValue</c>
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TKey">The key type; must be comparable and a value type for EF Core translation.</typeparam>
    /// <param name="query">The query to apply the cursor filter to.</param>
    /// <param name="keySelector">An expression that selects the key column from the entity.</param>
    /// <param name="cursor">The last seen cursor value; the query will return rows after this value.</param>
    /// <returns>The filtered queryable that returns rows strictly after the cursor position.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="query" /> or <paramref name="keySelector" /> is null.</exception>
    public static IQueryable<TEntity> AfterKeyset<TEntity, TKey>(
        this IQueryable<TEntity> query,
        Expression<Func<TEntity, TKey>> keySelector,
        TKey cursor)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(keySelector);

        var predicate = BuildSingleKeyPredicate(keySelector, cursor, greaterThan: true);
        return query.Where(predicate);
    }

    /// <summary>
    ///     Applies a backward keyset cursor filter on a single key column (ascending order).
    ///     Generates: <c>WHERE key &lt; cursorValue</c>
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TKey">The key type; must be comparable and a value type for EF Core translation.</typeparam>
    /// <param name="query">The query to apply the cursor filter to.</param>
    /// <param name="keySelector">An expression that selects the key column from the entity.</param>
    /// <param name="cursor">The first seen cursor value; the query will return rows before this value.</param>
    /// <returns>The filtered queryable that returns rows strictly before the cursor position.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="query" /> or <paramref name="keySelector" /> is null.</exception>
    public static IQueryable<TEntity> BeforeKeyset<TEntity, TKey>(
        this IQueryable<TEntity> query,
        Expression<Func<TEntity, TKey>> keySelector,
        TKey cursor)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(keySelector);

        var predicate = BuildSingleKeyPredicate(keySelector, cursor, greaterThan: false);
        return query.Where(predicate);
    }

    /// <summary>
    ///     Applies a forward keyset cursor filter on two key columns (composite key, both ascending).
    ///     Generates: <c>WHERE key1 &gt; cursor1 OR (key1 = cursor1 AND key2 &gt; cursor2)</c>
    ///     which is semantically equivalent to the tuple comparison
    ///     <c>(key1, key2) &gt; (cursor1, cursor2)</c>.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TKey1">The type of the primary (first) key column.</typeparam>
    /// <typeparam name="TKey2">The type of the secondary (second) key column used for tie-breaking.</typeparam>
    /// <param name="query">The query to apply the cursor filter to.</param>
    /// <param name="key1Selector">An expression selecting the primary key column.</param>
    /// <param name="key2Selector">An expression selecting the secondary (tie-break) key column.</param>
    /// <param name="cursor1">The primary cursor value from the last seen row.</param>
    /// <param name="cursor2">The secondary cursor value from the last seen row.</param>
    /// <returns>The filtered queryable that returns rows strictly after the composite cursor position.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    public static IQueryable<TEntity> AfterKeyset<TEntity, TKey1, TKey2>(
        this IQueryable<TEntity> query,
        Expression<Func<TEntity, TKey1>> key1Selector,
        Expression<Func<TEntity, TKey2>> key2Selector,
        TKey1 cursor1,
        TKey2 cursor2)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(key1Selector);
        ArgumentNullException.ThrowIfNull(key2Selector);

        var predicate = BuildCompositeKeyPredicate(key1Selector, key2Selector, cursor1, cursor2, greaterThan: true);
        return query.Where(predicate);
    }

    /// <summary>
    ///     Applies a backward keyset cursor filter on two key columns (composite key, both ascending).
    ///     Generates: <c>WHERE key1 &lt; cursor1 OR (key1 = cursor1 AND key2 &lt; cursor2)</c>
    ///     which is semantically equivalent to the tuple comparison
    ///     <c>(key1, key2) &lt; (cursor1, cursor2)</c>.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TKey1">The type of the primary (first) key column.</typeparam>
    /// <typeparam name="TKey2">The type of the secondary (second) key column used for tie-breaking.</typeparam>
    /// <param name="query">The query to apply the cursor filter to.</param>
    /// <param name="key1Selector">An expression selecting the primary key column.</param>
    /// <param name="key2Selector">An expression selecting the secondary (tie-break) key column.</param>
    /// <param name="cursor1">The primary cursor value from the first seen row of the current page.</param>
    /// <param name="cursor2">The secondary cursor value from the first seen row of the current page.</param>
    /// <returns>The filtered queryable that returns rows strictly before the composite cursor position.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    public static IQueryable<TEntity> BeforeKeyset<TEntity, TKey1, TKey2>(
        this IQueryable<TEntity> query,
        Expression<Func<TEntity, TKey1>> key1Selector,
        Expression<Func<TEntity, TKey2>> key2Selector,
        TKey1 cursor1,
        TKey2 cursor2)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(key1Selector);
        ArgumentNullException.ThrowIfNull(key2Selector);

        var predicate = BuildCompositeKeyPredicate(key1Selector, key2Selector, cursor1, cursor2, greaterThan: false);
        return query.Where(predicate);
    }

    /// <summary>
    ///     Pages a keyset over an arbitrary number of columns, each with its own direction (ascending or
    ///     descending), in either the forward or backward direction, and reports whether a further page
    ///     exists ahead of and behind the returned rows.
    /// </summary>
    /// <remarks>
    ///     Costs three round trips to the database: one for the page itself, and one each for
    ///     <c>HasPreviousAsync</c>/<c>HasNextAsync</c>. Those two existence checks come from
    ///     MR.EntityFrameworkCore.KeysetPagination 1.6.0, which does not accept a
    ///     <see cref="CancellationToken" /> on either call — only the page query itself observes
    ///     <paramref name="cancellationToken" />.
    /// </remarks>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="query">The query to page.</param>
    /// <param name="configureKeyset">
    ///     Configures the ordered columns that make up the keyset, e.g.
    ///     <c>b =&gt; b.Ascending(x =&gt; x.Country).Descending(x =&gt; x.Revenue).Ascending(x =&gt; x.Id)</c>.
    /// </param>
    /// <param name="pageSize">The maximum number of rows to return.</param>
    /// <param name="direction">Whether to page forward or backward. Default is forward.</param>
    /// <param name="reference">
    ///     The last seen row (or an object whose properties match the configured column names) that the
    ///     returned page is positioned relative to. Pass <see langword="null" /> to fetch the first (or last,
    ///     when <paramref name="direction" /> is backward) page.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    ///     A task that returns a <see cref="KeysetPage{TEntity}" /> containing up to <paramref name="pageSize" />
    ///     rows in declared keyset order, along with whether further pages exist ahead and behind them.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="query" /> or <paramref name="configureKeyset" /> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pageSize" /> is less than or equal to zero.</exception>
    public static async Task<KeysetPage<TEntity>> ToKeysetPageAsync<TEntity>(
        this IQueryable<TEntity> query,
        Action<KeysetPaginationBuilder<TEntity>> configureKeyset,
        int pageSize,
        KeysetPaginationDirection direction = KeysetPaginationDirection.Forward,
        object? reference = null,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(configureKeyset);
        if (pageSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(pageSize), "pageSize must be greater than zero.");

        var keysetContext = query.KeysetPaginate(configureKeyset, direction, reference);

        var items = await keysetContext.Query.Take(pageSize).ToListAsync(cancellationToken);
        keysetContext.EnsureCorrectOrder(items);

        var hasPrevious = await keysetContext.HasPreviousAsync(items);
        var hasNext = await keysetContext.HasNextAsync(items);

        return new KeysetPage<TEntity>(items, hasPrevious, hasNext);
    }

    /// <summary>
    ///     Builds a single-key comparison predicate: <c>key &gt; cursor</c> or <c>key &lt; cursor</c>.
    /// </summary>
    private static Expression<Func<TEntity, bool>> BuildSingleKeyPredicate<TEntity, TKey>(
        Expression<Func<TEntity, TKey>> keySelector,
        TKey cursor,
        bool greaterThan)
    {
        var parameter = Expression.Parameter(typeof(TEntity), "x");
        var keyBody = new ParameterReplacer(keySelector.Parameters[0], parameter).Visit(keySelector.Body);
        var cursorConst = Expression.Constant(cursor, typeof(TKey));

        var comparison = greaterThan
            ? Expression.GreaterThan(keyBody, cursorConst)
            : Expression.LessThan(keyBody, cursorConst);

        return Expression.Lambda<Func<TEntity, bool>>(comparison, parameter);
    }

    /// <summary>
    ///     Builds a composite two-key comparison predicate.
    ///     For <c>greaterThan = true</c>: <c>key1 &gt; c1 OR (key1 = c1 AND key2 &gt; c2)</c>
    ///     For <c>greaterThan = false</c>: <c>key1 &lt; c1 OR (key1 = c1 AND key2 &lt; c2)</c>
    /// </summary>
    private static Expression<Func<TEntity, bool>> BuildCompositeKeyPredicate<TEntity, TKey1, TKey2>(
        Expression<Func<TEntity, TKey1>> key1Selector,
        Expression<Func<TEntity, TKey2>> key2Selector,
        TKey1 cursor1,
        TKey2 cursor2,
        bool greaterThan)
    {
        var parameter = Expression.Parameter(typeof(TEntity), "x");

        var key1Body = new ParameterReplacer(key1Selector.Parameters[0], parameter).Visit(key1Selector.Body);
        var key2Body = new ParameterReplacer(key2Selector.Parameters[0], parameter).Visit(key2Selector.Body);

        var cursor1Const = Expression.Constant(cursor1, typeof(TKey1));
        var cursor2Const = Expression.Constant(cursor2, typeof(TKey2));

        // key1 > cursor1 (or key1 < cursor1 for backward)
        var key1Comparison = greaterThan
            ? Expression.GreaterThan(key1Body, cursor1Const)
            : Expression.LessThan(key1Body, cursor1Const);

        // key1 = cursor1
        var key1Equal = Expression.Equal(key1Body, cursor1Const);

        // key2 > cursor2 (or key2 < cursor2 for backward)
        var key2Comparison = greaterThan
            ? Expression.GreaterThan(key2Body, cursor2Const)
            : Expression.LessThan(key2Body, cursor2Const);

        // (key1 = cursor1 AND key2 > cursor2)
        var tieBreak = Expression.AndAlso(key1Equal, key2Comparison);

        // key1 > cursor1 OR (key1 = cursor1 AND key2 > cursor2)
        var combined = Expression.OrElse(key1Comparison, tieBreak);

        return Expression.Lambda<Func<TEntity, bool>>(combined, parameter);
    }

    #endregion

    #region Nested Types

    /// <summary>
    ///     Replaces a specific <see cref="ParameterExpression" /> within an expression tree with a new parameter.
    ///     Used internally to merge key selector expressions into a shared lambda parameter.
    /// </summary>
    private sealed class ParameterReplacer(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        /// <inheritdoc />
        protected override Expression VisitParameter(ParameterExpression node)
            => node == from ? to : base.VisitParameter(node);
    }

    #endregion
}
