// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: Specification.cs
// Description: Base specification interfaces and implementation for building query specifications used by repositories.

using System.ComponentModel;
using System.Linq.Expressions;
using DKNet.EfCore.Specifications.Extensions;
using LinqKit;

namespace DKNet.EfCore.Specifications.Definitions;

/// <summary>
///     The search specification definition
/// </summary>
/// <typeparam name="TEntity">Type of the entity</typeparam>
public interface ISpecification<TEntity>
    where TEntity : class
{
    #region Properties

    /// <summary>
    ///     Ignore global query filters that opt in to being bypassed (e.g., soft delete). Filters marked
    ///     non-ignorable (e.g. row-level tenant/ownership isolation) are never bypassed by this flag, regardless
    ///     of consuming application.
    /// </summary>
    bool IsIgnoreQueryFilters { get; }

    /// <summary>
    ///     A filtering function to test each element for condition
    /// </summary>
    Expression<Func<TEntity, bool>>? FilterQuery { get; }

    /// <summary>
    ///     A collection of functions that describes included entities
    /// </summary>
    IReadOnlyCollection<Expression<Func<TEntity, object?>>> IncludeQueries { get; }

    /// <summary>Include chains supporting ThenInclude and per-navigation filtering.</summary>
    IReadOnlyCollection<Func<IQueryable<TEntity>, IQueryable<TEntity>>> IncludeBuilders { get; }

    #endregion
}

/// <summary>
///     Base class for search specifications, providing filtering, includes, and ordering.
/// </summary>
/// <typeparam name="TEntity">Type of the entity</typeparam>
public abstract class Specification<TEntity> : ISpecification<TEntity>
    where TEntity : class
{
    #region Fields

    private readonly List<Expression<Func<TEntity, object?>>> _includeQueries = [];
    private readonly List<Func<IQueryable<TEntity>, IQueryable<TEntity>>> _includeBuilders = [];
    private readonly List<OrderClause<TEntity>> _orderByClauses = [];
    private int? _skip;
    private int? _take;
    private bool _isReadOnly;

    #endregion

    #region Constructors

    /// <summary>
    ///     Initializes a new instance of the <see cref="Specification{TEntity}" /> class.
    /// </summary>
    protected Specification()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the class
    /// </summary>
    /// <param name="query">A filtering function to test each element for condition</param>
    protected Specification(Expression<Func<TEntity, bool>> query) => FilterQuery = query;

    /// <summary>
    ///     Initializes a new instance of the class by copying an existing specification.
    /// </summary>
    /// <param name="specification">A specification to be built</param>
    protected Specification(ISpecification<TEntity> specification)
    {
        ArgumentNullException.ThrowIfNull(specification);

        FilterQuery = specification.FilterQuery;
        IsIgnoreQueryFilters = specification.IsIgnoreQueryFilters;
        // Copy collections into the mutable backing lists (interface properties are non-null by contract)
        _includeQueries.AddRange(specification.IncludeQueries);
        _includeBuilders.AddRange(specification.IncludeBuilders);

        if (specification is Specification<TEntity> source)
        {
            // Declared ordering sequence and window/tracking state carry over as-is.
            _orderByClauses.AddRange(source._orderByClauses);
            _skip = source._skip;
            _take = source._take;
            _isReadOnly = source._isReadOnly;
        }
    }

    #endregion

    #region Properties

    /// <summary>
    ///     Gets the filter expression used by this specification or <c>null</c> when no filter is defined.
    /// </summary>
    public Expression<Func<TEntity, bool>>? FilterQuery { get; private set; }

    /// <summary>
    ///     Gets the collection of include expressions that describe related entities to include when querying.
    /// </summary>
    public IReadOnlyCollection<Expression<Func<TEntity, object?>>> IncludeQueries => _includeQueries;

    /// <summary>Include chains supporting ThenInclude and per-navigation filtering.</summary>
    public IReadOnlyCollection<Func<IQueryable<TEntity>, IQueryable<TEntity>>> IncludeBuilders => _includeBuilders;

    /// <summary>
    ///     Gets a value indicating whether ignorable global query filters should be bypassed for this
    ///     specification. Non-ignorable filters (e.g. row-level tenant/ownership isolation) are never bypassed by
    ///     this flag. Call <see cref="IgnoreQueryFilters" /> to enable this behavior.
    /// </summary>
    public bool IsIgnoreQueryFilters { get; private set; }

    /// <summary>
    ///     Gets the ordering clauses in the sequence they were declared, so mixed-direction ordering can be
    ///     applied as declared instead of segregated by direction.
    /// </summary>
    internal IReadOnlyList<OrderClause<TEntity>> OrderByClauses => _orderByClauses;

    /// <summary>
    ///     Gets the number of leading results to skip, or <c>null</c> when no skip was declared.
    /// </summary>
    internal int? SkipCount => _skip;

    /// <summary>
    ///     Gets the maximum number of results to return, or <c>null</c> when no take was declared.
    /// </summary>
    internal int? TakeCount => _take;

    /// <summary>
    ///     Gets a value indicating whether this specification declared its query as read-only (non-tracking).
    /// </summary>
    internal bool IsReadOnly => _isReadOnly;

    #endregion

    #region Methods

    /// <summary>
    ///     Adds an query that describes included entities. Single-level filtered includes are supported,
    ///     e.g. <c>AddInclude(p =&gt; p.OrderItems.Where(i =&gt; i.Quantity &gt; 0))</c>.
    ///     On tracking queries, filtered includes can surface already-tracked children that don't match
    ///     the filter due to EF Core navigation-fixup; for filter-accurate results consume via
    ///     <c>Query&lt;TEntity,TModel&gt;</c> (projection) or an <c>AsNoTracking()</c> read. See
    ///     https://learn.microsoft.com/ef/core/querying/related-data/eager#filtered-include.
    /// </summary>
    /// <param name="query">Expression that describes included entities</param>
    protected void AddInclude(Expression<Func<TEntity, object?>> query)
    {
        _includeQueries.Add(query);
    }

    /// <summary>Adds an Include/ThenInclude chain (may filter with Where/OrderBy/Skip/Take at any level).</summary>
    /// <param name="includeBuilder">Applies the include chain to the query.</param>
    protected void AddInclude(Func<IQueryable<TEntity>, IQueryable<TEntity>> includeBuilder)
        => _includeBuilders.Add(includeBuilder);

    /// <summary>
    ///     Adds an order by clause based on a property name and sort direction
    /// </summary>
    /// <param name="orderBy">Property Name</param>
    /// <param name="direction">Order descending or ascending</param>
    protected void AddOrderBy(string orderBy, ListSortDirection direction)
    {
        if (string.IsNullOrWhiteSpace(orderBy)) return;

        orderBy = orderBy.ToPascalCase();
        var parameter = Expression.Parameter(typeof(TEntity), "x");
        var member = Expression.PropertyOrField(parameter, orderBy);

        Expression body = member.Type.IsValueType
            ? Expression.Convert(member, typeof(object))
            : member;

        var keySelector = Expression.Lambda<Func<TEntity, object>>(body, parameter);

        if (direction == ListSortDirection.Ascending)
            AddOrderBy(keySelector);
        else AddOrderByDescending(keySelector);
    }

    /// <summary>
    ///     Adds a query that orders entities by ascending
    /// </summary>
    /// <param name="query">A function that describes how to order entities by ascending</param>
    protected void AddOrderBy(Expression<Func<TEntity, object>> query)
    {
        _orderByClauses.Add(new OrderClause<TEntity>(query, ListSortDirection.Ascending));
    }

    /// <summary>
    ///     Adds a query that orders entities by descending
    /// </summary>
    /// <param name="query">A function that describes how to order entities by descending</param>
    protected void AddOrderByDescending(Expression<Func<TEntity, object>> query)
    {
        _orderByClauses.Add(new OrderClause<TEntity>(query, ListSortDirection.Descending));
    }

    /// <summary>
    ///     Instructs the specification to run its query as read-only, suppressing EF Core change tracking
    ///     (<c>AsNoTracking</c>). This only affects tracking behavior; filtering, ordering, and includes are
    ///     unaffected.
    /// </summary>
    protected void AsNoTracking()
    {
        _isReadOnly = true;
    }

    /// <summary>
    ///     Creates a predicate builder initialized with an optional starting expression.
    /// </summary>
    /// <param name="expression">Optional starting expression for the predicate.</param>
    /// <returns>An <see cref="ExpressionStarter{T}" /> used to build a composable predicate.</returns>
    protected ExpressionStarter<TEntity> CreatePredicate(Expression<Func<TEntity, bool>>? expression = null) =>
        expression == null ? PredicateBuilder.New<TEntity>() : PredicateBuilder.New(expression);

    /// <summary>
    ///     Instructs the specification to bypass global query filters that opt in to being ignorable (for
    ///     example soft-delete filters). Non-ignorable filters (e.g. row-level tenant/ownership isolation) are
    ///     never bypassed by this flag, regardless of consuming application.
    /// </summary>
    protected void IgnoreQueryFilters()
    {
        IsIgnoreQueryFilters = true;
    }

    /// <summary>
    ///     Declares the number of leading results this specification's query should skip.
    /// </summary>
    /// <param name="count">The number of results to skip; must be greater than zero.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count" /> is less than or equal to zero.</exception>
    protected void Skip(int count)
    {
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count), "count must be greater than zero.");

        _skip = count;
    }

    /// <summary>
    ///     Declares the maximum number of results this specification's query should return.
    /// </summary>
    /// <param name="count">The maximum number of results to return; must be greater than zero.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count" /> is less than or equal to zero.</exception>
    protected void Take(int count)
    {
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count), "count must be greater than zero.");

        _take = count;
    }

    /// <summary>
    ///     Adds a filtering function to test each element for condition
    /// </summary>
    /// <param name="query">A filtering function that describes how to test each element for condition</param>
    protected void WithFilter(Expression<Func<TEntity, bool>> query)
    {
        FilterQuery = query;
    }

    #endregion
}