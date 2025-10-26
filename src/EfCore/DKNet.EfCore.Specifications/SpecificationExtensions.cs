using Microsoft.EntityFrameworkCore;

namespace DKNet.EfCore.Specifications;

/// <summary>
///     Provides extension methods for applying specifications to repositories and queries.
/// </summary>
internal static class SpecificationExtensions
{
    /// <summary>
    ///     Applies a specification to an IQueryable and returns the modified queryable.
    /// </summary>
    /// <typeparam name="TEntity">Type of the entity</typeparam>
    /// <param name="queryable">The queryable to apply the specification to</param>
    /// <param name="specification">The specification to apply</param>
    /// <returns>An <see cref="IQueryable{TEntity}"/> with the specification applied</returns>
    public static IQueryable<TEntity> ApplySpecs<TEntity>(
        this IQueryable<TEntity> queryable,
        ISpecification<TEntity> specification) where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(specification);

        if (specification.IgnoreQueryFilters)
            queryable = queryable.IgnoreQueryFilters();

        if (specification.FilterQuery is not null) queryable = queryable.Where(specification.FilterQuery);

        if (specification.IncludeQueries.Count > 0)
            queryable = specification.IncludeQueries.Aggregate(queryable,
                (current, includeQuery) => current.Include(includeQuery));

        // Apply ordering using OrderByQueries and OrderByDescendingQueries in the order they were added
        var hasOrderBy = specification.OrderByQueries.Count > 0;
        var hasOrderByDesc = specification.OrderByDescendingQueries.Count > 0;
        IOrderedQueryable<TEntity>? ordered = null;
        // Apply OrderBy queries first
        if (hasOrderBy)
        {
            var isFirst = true;
            foreach (var expr in specification.OrderByQueries)
                if (isFirst)
                {
                    ordered = queryable.OrderBy(expr);
                    isFirst = false;
                }
                else
                {
                    ordered = ordered!.ThenBy(expr);
                }
        }

        // Then apply OrderByDescending queries
        if (hasOrderByDesc)
        {
            if (ordered == null)
            {
                var isFirst = true;
                foreach (var expr in specification.OrderByDescendingQueries)
                    if (isFirst)
                    {
                        ordered = queryable.OrderByDescending(expr);
                        isFirst = false;
                    }
                    else
                    {
                        ordered = ordered!.ThenByDescending(expr);
                    }
            }
            else
            {
                ordered = specification.OrderByDescendingQueries.Aggregate(ordered,
                    (current, expr) => current.ThenByDescending(expr));
            }
        }

        if (ordered != null) queryable = ordered;

        return queryable;
    }
}