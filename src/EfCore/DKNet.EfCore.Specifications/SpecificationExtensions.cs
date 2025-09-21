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
    public static IQueryable<TEntity> WithSpecs<TEntity>(
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

        // Apply ordering in the exact order and direction they were added to the specification
        if (specification.OrderedQueries != null && specification.OrderedQueries.Count > 0)
        {
            var orderings = specification.OrderedQueries; // List<(Expression<Func<TEntity, object>> Expr, bool Desc)>

            IOrderedQueryable<TEntity>? ordered = null;
            for (var i = 0; i < orderings.Count; i++)
            {
                var (expr, desc) = orderings[i];
                ordered = i == 0
                    ? desc ? queryable.OrderByDescending(expr) : queryable.OrderBy(expr)
                    : desc ? ordered!.ThenByDescending(expr) : ordered!.ThenBy(expr);
            }
            queryable = ordered!;
        }

        return queryable;
    }

    /// <summary>
    ///     Applies a specification to a repository and returns a queryable result.
    /// </summary>
    /// <typeparam name="TEntity">Type of the entity</typeparam>
    /// <param name="repo">The repository</param>
    /// <param name="specification">The specification to apply</param>
    /// <returns>An <see cref="IQueryable{TEntity}"/> with the specification applied</returns>
    public static IQueryable<TEntity> WithSpecs<TEntity>(
        this IReadRepository<TEntity> repo,
        ISpecification<TEntity> specification) where TEntity : class =>
        repo.Gets().WithSpecs(specification);

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
        await repo.WithSpecs(specification).ToListAsync(cancellationToken);

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
        repo.WithSpecs(specification).FirstOrDefaultAsync(cancellationToken);

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
        repo.WithSpecs(specification).ToPageEnumerable();

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
        repo.WithSpecs(specification)
            .ToPagedListAsync(pageNumber, pageSize);
}