using Microsoft.EntityFrameworkCore;

namespace DKNet.EfCore.Specifications;

/// <summary>
///     The Ef Core specification implementation
/// </summary>
/// <typeparam name="TEntity">Type of the entity</typeparam>
/// <remarks>
///     Initializes a new instance of the class
/// </remarks>
public sealed class EfCoreSpecification<TEntity>(ISpecification<TEntity> specification)
    : Specification<TEntity>(specification)
    where TEntity : class
{
    /// <summary>
    ///     Returns a specification expression.<br />
    ///     IMPORTANT: EfCore Specification supports IncludeQueries
    /// </summary>
    /// <returns>Expression</returns>
    public IQueryable<TEntity> Apply(IQueryable<TEntity> queryable)
    {
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