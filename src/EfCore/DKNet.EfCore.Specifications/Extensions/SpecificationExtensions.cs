using System.ComponentModel;
using DKNet.EfCore.Extensions.Configurations;
using Microsoft.EntityFrameworkCore;

namespace DKNet.EfCore.Specifications.Extensions;

/// <summary>
///     Provides extension methods for applying specifications to repositories and queries.
/// </summary>
internal static class SpecificationExtensions
{
    #region Methods

    /// <summary>
    ///     Applies a specification to an IQueryable and returns the modified queryable.
    /// </summary>
    /// <typeparam name="TEntity">Type of the entity</typeparam>
    /// <param name="queryable">The queryable to apply the specification to</param>
    /// <param name="specification">The specification to apply</param>
    /// <returns>An <see cref="IQueryable{TEntity}" /> with the specification applied</returns>
    public static IQueryable<TEntity> ApplySpecs<TEntity>(
        this IQueryable<TEntity> queryable,
        ISpecification<TEntity> specification) where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(specification);

        if (specification.IsIgnoreQueryFilters)
        {
            var ignorableKeys = GlobalQueryFilter.IgnorableFilterKeys;
            if (ignorableKeys.Count > 0) queryable = queryable.IgnoreQueryFilters(ignorableKeys);
        }

        if (specification.FilterQuery is not null) queryable = queryable.Where(specification.FilterQuery);

        if (specification.IncludeQueries.Count > 0)
            queryable = specification.IncludeQueries.Aggregate(
                queryable,
                (current, includeQuery) => current.Include(includeQuery));

        if (specification.IncludeBuilders.Count > 0)
            queryable = specification.IncludeBuilders.Aggregate(
                queryable,
                (current, builder) => builder(current));

        if (specification is Specification<TEntity> s && s.OrderByClauses.Count > 0)
        {
            // Declared-sequence ordering: apply mixed-direction clauses in the order they were added.
            IOrderedQueryable<TEntity>? ordered = null;
            foreach (var clause in s.OrderByClauses)
                ordered = ordered is null
                    ? clause.Direction == ListSortDirection.Ascending
                        ? queryable.OrderBy(clause.KeySelector)
                        : queryable.OrderByDescending(clause.KeySelector)
                    : clause.Direction == ListSortDirection.Ascending
                        ? ordered.ThenBy(clause.KeySelector)
                        : ordered.ThenByDescending(clause.KeySelector);

            queryable = ordered!;
        }
        else
        {
            // Legacy two-phase ordering for foreign ISpecification implementations: all ascending queries
            // first, then all descending queries.
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
                    ordered = specification.OrderByDescendingQueries.Aggregate(
                        ordered,
                        (current, expr) => current.ThenByDescending(expr));
                }
            }

            if (ordered != null) queryable = ordered;
        }

        if (specification is Specification<TEntity> ws)
        {
            if (ws.IsReadOnly) queryable = queryable.AsNoTracking();
            if (ws.SkipCount is { } skip) queryable = queryable.Skip(skip);
            if (ws.TakeCount is { } take) queryable = queryable.Take(take);
        }

        return queryable;
    }

    public static void EnsureSpecHasOrdering<TEntity>(this ISpecification<TEntity> specification)
        where TEntity : class
    {
        if (specification.OrderByQueries.Count == 0 && specification.OrderByDescendingQueries.Count == 0)
            throw new NotSupportedException("The specification must include at least one ordering.");
    }

    #endregion
}