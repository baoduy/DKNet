using System.Linq.Expressions;
using LinqKit;

// ReSharper disable UnusedMember.Global

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
    protected Specification(Expression<Func<TEntity, bool>> query) => this.FilterQuery = query;

    /// <summary>
    ///     Initializes a new instance of the class
    /// </summary>
    /// <param name="specification">A specification to be built</param>
    protected Specification(ISpecification<TEntity> specification)
    {
        this.FilterQuery = specification.FilterQuery;
        this.IsIgnoreQueryFilters = specification.IsIgnoreQueryFilters;

        this._includeQueries = [.. specification.IncludeQueries];
        this._orderByQueries = [.. specification.OrderByQueries];
        this._orderByDescendingQueries = [.. specification.OrderByDescendingQueries];
    }

    #endregion

    #region Properties

    public bool IsIgnoreQueryFilters { get; private set; }

    public Expression<Func<TEntity, bool>>? FilterQuery { get; private set; }

    public IReadOnlyCollection<Expression<Func<TEntity, object?>>> IncludeQueries => this._includeQueries;

    public IReadOnlyCollection<Expression<Func<TEntity, object>>> OrderByDescendingQueries =>
        this._orderByDescendingQueries;

    public IReadOnlyCollection<Expression<Func<TEntity, object>>> OrderByQueries => this._orderByQueries;

    #endregion

    #region Methods

    /// <summary>
    ///     Adds an query that describes included entities
    /// </summary>
    /// <param name="query">Expression that describes included entities</param>
    protected void AddInclude(Expression<Func<TEntity, object?>> query)
    {
        this._includeQueries.Add(query);
    }

    /// <summary>
    ///     Adds a query that orders entities by ascending
    /// </summary>
    /// <param name="query">A function that describes how to order entities by ascending</param>
    protected void AddOrderBy(Expression<Func<TEntity, object>> query)
    {
        this._orderByQueries.Add(query);
    }

    /// <summary>
    ///     Adds a query that orders entities by descending
    /// </summary>
    /// <param name="query">A function that describes how to order entities by descending</param>
    protected void AddOrderByDescending(Expression<Func<TEntity, object>> query)
    {
        this._orderByDescendingQueries.Add(query);
    }

    protected ExpressionStarter<TEntity> CreatePredicate(Expression<Func<TEntity, bool>>? expression = null) =>
        expression == null ? PredicateBuilder.New<TEntity>() : PredicateBuilder.New(expression);

    protected void IgnoreQueryFilters()
    {
        this.IsIgnoreQueryFilters = true;
    }

    /// <summary>
    ///     Returns an indication whether an entity matches the current specification
    /// </summary>
    /// <param name="entity">Entity</param>
    /// <returns>
    ///     <see langword="true" /> if an entity matches the current specification; otherwise, <see langword="false" />
    /// </returns>
    public bool Match(TEntity entity)
    {
        if (this.FilterQuery is null)
        {
            return false;
        }

        var predicate = this.FilterQuery.Compile();
        return predicate(entity);
    }

    /// <summary>
    ///     Adds a filtering function to test each element for condition
    /// </summary>
    /// <param name="query">A filtering function that describes how to test each element for condition</param>
    protected void WithFilter(Expression<Func<TEntity, bool>> query)
    {
        this.FilterQuery = query;
    }

    #endregion
}