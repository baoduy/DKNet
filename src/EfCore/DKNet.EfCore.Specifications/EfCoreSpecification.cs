using Microsoft.EntityFrameworkCore;

namespace DKNet.EfCore.Specifications;

/// <summary>
///     The Ef Core specification implementation.
/// </summary>
/// <typeparam name="TEntity">Type of the entity</typeparam>
public sealed class EfCoreSpecification<TEntity>(ISpecification<TEntity> specification)
    : Specification<TEntity>(specification)
    where TEntity : class
{
    /// <summary>
    ///     Applies the specification to the given queryable source.
    ///     Supports filtering, includes, and ordering.
    /// </summary>
    /// <param name="queryable">The queryable source</param>
    /// <returns>An <see cref="IQueryable{TEntity}"/> with the specification applied</returns>
    public IQueryable<TEntity> Apply(IQueryable<TEntity> queryable)
    {
        if (IgnoreQueryFilters)
            queryable = queryable.IgnoreQueryFilters();

        if (FilterQuery is not null) queryable = queryable.Where(FilterQuery);

        if (IncludeQueries.Count > 0)
            queryable = IncludeQueries.Aggregate(queryable, (current, includeQuery) => current.Include(includeQuery));

        if (OrderByQueries.Count > 0)
        {
            var orderedQueryable = queryable.OrderBy(OrderByQueries.First());
            orderedQueryable = OrderByQueries.Skip(1)
                .Aggregate(orderedQueryable, (current, orderQuery) => current.ThenBy(orderQuery));
            queryable = orderedQueryable;
        }

        if (OrderByDescendingQueries.Count > 0)
        {
            var orderedQueryable = queryable.OrderByDescending(OrderByDescendingQueries.First());
            orderedQueryable = OrderByDescendingQueries.Skip(1).Aggregate(orderedQueryable,
                (current, orderQuery) => current.ThenByDescending(orderQuery));
            queryable = orderedQueryable;
        }

        return queryable;
    }
}