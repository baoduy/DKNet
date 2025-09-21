using System.Linq.Expressions;

namespace DKNet.EfCore.Specifications;

/// <summary>
///     The search specification definition
/// </summary>
/// <typeparam name="TEntity">Type of the entity</typeparam>
public interface ISpecification<TEntity>
    where TEntity : class
{
    /// <summary>
    ///     A filtering function to test each element for condition
    /// </summary>
    Expression<Func<TEntity, bool>>? FilterQuery { get; }

    /// <summary>
    ///     A collection of functions that describes included entities
    /// </summary>
    IReadOnlyCollection<Expression<Func<TEntity, object?>>> IncludeQueries { get; }

    /// <summary>
    ///     A function that describes how to order entities by ascending
    /// </summary>
    IReadOnlyCollection<Expression<Func<TEntity, object>>> OrderByQueries { get; }

    /// <summary>
    ///     A function that describes how to order entities by descending
    /// </summary>
    IReadOnlyCollection<Expression<Func<TEntity, object>>> OrderByDescendingQueries { get; }

    /// <summary>
    /// Ignore the global query filters (e.g., for soft delete or multi-tenancy)
    /// </summary>
    bool IgnoreQueryFilters { get; }
}

/// <summary>
///     Base class for search specifications, providing filtering, includes, and ordering.
/// </summary>
/// <typeparam name="TEntity">Type of the entity</typeparam>
public abstract class Specification<TEntity> : ISpecification<TEntity>
    where TEntity : class
{
    private readonly List<Expression<Func<TEntity, object?>>> _includeQueries = [];
    private readonly List<Expression<Func<TEntity, object>>> _orderByDescendingQueries = [];
    private readonly List<Expression<Func<TEntity, object>>> _orderByQueries = [];

    /// <summary>
    ///     Initializes a new instance of the <see cref="Specification{TEntity}"/> class.
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
    ///     Initializes a new instance of the class
    /// </summary>
    /// <param name="specification">A specification to be built</param>
    protected Specification(ISpecification<TEntity> specification)
    {
        FilterQuery = specification.FilterQuery;

        _includeQueries = [.. specification.IncludeQueries];
        _orderByQueries = [.. specification.OrderByQueries];
        _orderByDescendingQueries = [.. specification.OrderByDescendingQueries];
    }

    public Expression<Func<TEntity, bool>>? FilterQuery { get; private set; }

    public IReadOnlyCollection<Expression<Func<TEntity, object?>>> IncludeQueries => _includeQueries;

    public IReadOnlyCollection<Expression<Func<TEntity, object>>> OrderByQueries => _orderByQueries;

    public IReadOnlyCollection<Expression<Func<TEntity, object>>> OrderByDescendingQueries =>
        _orderByDescendingQueries;

    public bool IgnoreQueryFilters { get; private set; }

    protected void IgnoreQueryFiltersEnabled()
    {
        IgnoreQueryFilters = true;
    }

    /// <summary>
    ///     Adds a filtering function to test each element for condition
    /// </summary>
    /// <param name="query">A filtering function that describes how to test each element for condition</param>
    protected void WithFilter(Expression<Func<TEntity, bool>> query)
    {
        FilterQuery = query;
    }

    /// <summary>
    ///     Adds an query that describes included entities
    /// </summary>
    /// <param name="query">Expression that describes included entities</param>
    protected void AddInclude(Expression<Func<TEntity, object?>> query)
    {
        _includeQueries.Add(query);
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
    ///     Returns an indication whether an entity matches the current specification
    /// </summary>
    /// <param name="entity">Entity</param>
    /// <returns>
    ///     <see langword="true" /> if an entity matches the current specification; otherwise, <see langword="false" />
    /// </returns>
    public bool Match(TEntity entity)
    {
        if (FilterQuery is null) return false;

        var predicate = FilterQuery.Compile();
        return predicate(entity);
    }

    /// <summary>
    ///     Returns an expression that combines two specifications with a logical "and"
    /// </summary>
    /// <param name="specification">Specification to combine with</param>
    /// <returns>
    ///     <see cref="AndSpecification{T}" />
    /// </returns>
    public Specification<TEntity> And(Specification<TEntity> specification) =>
        new AndSpecification<TEntity>(this, specification);

    /// <summary>
    ///     Returns an expression that combines two specifications with a logical "or"
    /// </summary>
    /// <param name="specification">Specification to combine with</param>
    /// <returns>
    ///     <see cref="OrSpecification{T}" />
    /// </returns>
    public Specification<TEntity> Or(Specification<TEntity> specification) =>
        new OrSpecification<TEntity>(this, specification);

    public static Specification<TEntity> operator &(Specification<TEntity> left, Specification<TEntity> right) =>
        left.And(right);

    public static Specification<TEntity> operator |(Specification<TEntity> left, Specification<TEntity> right) =>
        left.Or(right);
}