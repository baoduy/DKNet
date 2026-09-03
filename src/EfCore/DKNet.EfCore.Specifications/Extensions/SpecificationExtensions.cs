using System.ComponentModel;
using DKNet.EfCore.Extensions.Configurations;
using DKNet.EfCore.Specifications.Definitions;
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

        // Foreign ISpecification implementations (not deriving from Specification<TEntity>) are not
        // supported: only the declared-sequence ordering, window (Skip/Take) and read-only state carried
        // by the abstract base class are recognised here.
        if (specification is Specification<TEntity> s)
        {
            if (s.OrderByClauses.Count > 0)
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

            if (s.IsReadOnly) queryable = queryable.AsNoTracking();
            if (s.SkipCount is { } skip) queryable = queryable.Skip(skip);
            if (s.TakeCount is { } take) queryable = queryable.Take(take);
        }

        return queryable;
    }

    public static void EnsureSpecHasOrdering<TEntity>(this ISpecification<TEntity> specification)
        where TEntity : class
    {
        var hasOrdering = specification is Specification<TEntity> { OrderByClauses.Count: > 0 };
        if (!hasOrdering)
            throw new NotSupportedException("The specification must include at least one ordering.");
    }

    #endregion
}