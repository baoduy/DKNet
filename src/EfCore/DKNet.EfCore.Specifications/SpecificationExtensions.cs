using DKNet.EfCore.Extensions.Extensions;
using DKNet.EfCore.Repos.Abstractions;
using Microsoft.EntityFrameworkCore;
using X.PagedList;
using X.PagedList.EF;

namespace DKNet.EfCore.Specifications;

/// <summary>
///     Provides extension methods for applying specifications to repositories and queries.
/// </summary>
public static class SpecificationExtensions
{
    /// <summary>
    ///     Applies a specification to an IQueryable and returns the modified queryable.
    /// </summary>
    /// <typeparam name="TEntity">Type of the entity</typeparam>
    /// <param name="queryable">The queryable to apply the specification to</param>
    /// <param name="specification">The specification to apply</param>
    /// <returns>An <see cref="IQueryable{TEntity}"/> with the specification applied</returns>
    public static IQueryable<TEntity> QuerySpecs<TEntity>(
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

    /// <summary>
    ///     Applies a specification to a repository and returns a queryable result.
    /// </summary>
    /// <typeparam name="TEntity">Type of the entity</typeparam>
    /// <param name="repo">The repository</param>
    /// <param name="specification">The specification to apply</param>
    /// <returns>An <see cref="IQueryable{TEntity}"/> with the specification applied</returns>
    public static IQueryable<TEntity> QuerySpecs<TEntity>(
        this IReadRepository<TEntity> repo,
        ISpecification<TEntity> specification) where TEntity : class =>
        repo.Query().QuerySpecs(specification);

    /// <summary>
    ///     Asynchronously returns a list of entities matching the specification.
    /// </summary>
    /// <typeparam name="TEntity">Type of the entity</typeparam>
    /// <param name="repo">The repository</param>
    /// <param name="specification">The specification to apply</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of entities</returns>
    public static async Task<IList<TEntity>> SpecsListAsync<TEntity>(
        this IReadRepository<TEntity> repo,
        ISpecification<TEntity> specification, CancellationToken cancellationToken = default) where TEntity : class =>
        await repo.QuerySpecs(specification).ToListAsync(cancellationToken);

    /// <summary>
    ///     Asynchronously returns the first entity matching the specification, or null if none found.
    /// </summary>
    /// <typeparam name="TEntity">Type of the entity</typeparam>
    /// <param name="repo">The repository</param>
    /// <param name="specification">The specification to apply</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The first entity or null</returns>
    public static Task<TEntity?> SpecsFirstOrDefaultAsync<TEntity>(
        this IReadRepository<TEntity> repo,
        ISpecification<TEntity> specification, CancellationToken cancellationToken = default) where TEntity : class =>
        repo.QuerySpecs(specification).FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    ///     Asynchronously returns the first entity matching the specification.
    /// </summary>
    /// <typeparam name="TEntity">Type of the entity</typeparam>
    /// <param name="repo">The repository</param>
    /// <param name="specification">The specification to apply</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The first entity or null</returns>
    public static Task<TEntity> SpecsFirstAsync<TEntity>(
        this IReadRepository<TEntity> repo,
        ISpecification<TEntity> specification, CancellationToken cancellationToken = default) where TEntity : class =>
        repo.QuerySpecs(specification).FirstAsync(cancellationToken);

    /// <summary>
    ///     Returns an async enumerable of entities matching the specification, paged.
    /// </summary>
    /// <typeparam name="TEntity">Type of the entity</typeparam>
    /// <param name="repo">The repository</param>
    /// <param name="specification">The specification to apply</param>
    /// <returns>An async enumerable of entities</returns>
    public static IAsyncEnumerable<TEntity> SpecsToPageEnumerable<TEntity>(
        this IReadRepository<TEntity> repo,
        ISpecification<TEntity> specification) where TEntity : class =>
        repo.QuerySpecs(specification).ToPageEnumerable();

    /// <summary>
    ///     Asynchronously returns a paged list of entities matching the specification.
    /// </summary>
    /// <typeparam name="TEntity">Type of the entity</typeparam>
    /// <param name="repo">The repository</param>
    /// <param name="specification">The specification to apply</param>
    /// <param name="pageNumber">Page number</param>
    /// <param name="pageSize">Page size</param>
    /// <returns>A paged list of entities</returns>
    public static Task<IPagedList<TEntity>> SpecsToPageListAsync<TEntity>(
        this IReadRepository<TEntity> repo,
        ISpecification<TEntity> specification, int pageNumber,
        int pageSize) where TEntity : class =>
        repo.QuerySpecs(specification)
            .ToPagedListAsync(pageNumber, pageSize);
}