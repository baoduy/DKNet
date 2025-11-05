// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: Specification.cs
// Description: Base specification interfaces and implementation for building query specifications used by repositories.

using System.ComponentModel;
using System.Linq.Expressions;
using LinqKit;

namespace DKNet.EfCore.Specifications;

/// <summary>
///     The search specification definition
/// </summary>
/// <typeparam name="TEntity">Type of the entity</typeparam>
public interface ISpecification<TEntity>
    where TEntity : class
{
    #region Properties

    /// <summary>
    ///     Ignore the global query filters (e.g., for soft delete or multi-tenancy)
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

    /// <summary>
    ///     A function that describes how to order entities by descending
    /// </summary>
    IReadOnlyCollection<Expression<Func<TEntity, object>>> OrderByDescendingQueries { get; }

    /// <summary>
    ///     A function that describes how to order entities by ascending
    /// </summary>
    IReadOnlyCollection<Expression<Func<TEntity, object>>> OrderByQueries { get; }

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
    private readonly List<Expression<Func<TEntity, object>>> _orderByDescendingQueries = [];
    private readonly List<Expression<Func<TEntity, object>>> _orderByQueries = [];

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
        _orderByQueries.AddRange(specification.OrderByQueries);
        _orderByDescendingQueries.AddRange(specification.OrderByDescendingQueries);
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

    /// <summary>
    ///     Gets a value indicating whether global query filters should be ignored for this specification.
    ///     Call <see cref="IgnoreQueryFilters" /> to enable this behavior.
    /// </summary>
    public bool IsIgnoreQueryFilters { get; private set; }

    /// <summary>
    ///     Gets the collection of expressions that describe descending ordering for query results.
    /// </summary>
    public IReadOnlyCollection<Expression<Func<TEntity, object>>> OrderByDescendingQueries =>
        _orderByDescendingQueries;

    /// <summary>
    ///     Gets the collection of expressions that describe ascending ordering for query results.
    /// </summary>
    public IReadOnlyCollection<Expression<Func<TEntity, object>>> OrderByQueries => _orderByQueries;

    #endregion

    #region Methods

    /// <summary>
    ///     Adds an query that describes included entities
    /// </summary>
    /// <param name="query">Expression that describes included entities</param>
    protected void AddInclude(Expression<Func<TEntity, object?>> query)
    {
        _includeQueries.Add(query);
    }

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
        _orderByQueries.Add(query);
    }

    /// <summary>
    ///     Adds a query that orders entities by descending
    /// </summary>
    /// <param name="query">A function that describes how to order entities by descending</param>
    protected void AddOrderByDescending(Expression<Func<TEntity, object>> query)
    {
        _orderByDescendingQueries.Add(query);
    }

    /// <summary>
    ///     Creates a predicate builder initialized with an optional starting expression.
    /// </summary>
    /// <param name="expression">Optional starting expression for the predicate.</param>
    /// <returns>An <see cref="ExpressionStarter{T}" /> used to build a composable predicate.</returns>
    protected ExpressionStarter<TEntity> CreatePredicate(Expression<Func<TEntity, bool>>? expression = null) =>
        expression == null ? PredicateBuilder.New<TEntity>() : PredicateBuilder.New(expression);

    /// <summary>
    ///     Instructs the specification to ignore global query filters (for example soft-delete filters).
    /// </summary>
    protected void IgnoreQueryFilters()
    {
        IsIgnoreQueryFilters = true;
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