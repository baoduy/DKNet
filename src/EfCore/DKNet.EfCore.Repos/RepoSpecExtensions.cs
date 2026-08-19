using System.Runtime.CompilerServices;
using DKNet.EfCore.Extensions.Configurations;
using DKNet.EfCore.Specifications;
using X.PagedList;
using X.PagedList.EF;

namespace DKNet.EfCore.Repos;

/// <summary>
///     Provides extension methods for applying specifications to repositories and queries.
/// </summary>
[Obsolete("DKNet.EfCore.Repos is retired. Use DKNet.EfCore.Specifications (IRepositorySpec + SpecSetup) instead. See docs/EfCore/Migrating-Repos-To-Specifications.md.")]
#pragma warning disable CS0618 // IReadRepository<TEntity> is the obsolete member being flagged here
public static class RepoExtensions
{
    #region Methods

    /// <summary>
    ///     Applies a specification's filter, includes and ordering to a queryable.
    /// </summary>
    /// <remarks>
    ///     Local copy of <c>DKNet.EfCore.Specifications.Extensions.SpecificationExtensions.ApplySpecs</c>, which is
    ///     internal to that package. Kept in sync only for as long as this retired library ships.
    /// </remarks>
    private static IQueryable<TEntity> ApplySpecs<TEntity>(
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

        var hasOrderBy = specification.OrderByQueries.Count > 0;
        var hasOrderByDesc = specification.OrderByDescendingQueries.Count > 0;
        IOrderedQueryable<TEntity>? ordered = null;

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

        return queryable;
    }

    /// <summary>
    ///     Local copy of <c>DKNet.EfCore.Specifications.Extensions.SpecificationExtensions.EnsureSpecHasOrdering</c>,
    ///     which is internal to that package.
    /// </summary>
    private static void EnsureSpecHasOrdering<TEntity>(this ISpecification<TEntity> specification)
        where TEntity : class
    {
        if (specification.OrderByQueries.Count == 0 && specification.OrderByDescendingQueries.Count == 0)
            throw new NotSupportedException("The specification must include at least one ordering.");
    }

    /// <summary>
    ///     Local copy of the paging behaviour of
    ///     <c>DKNet.EfCore.Specifications.PageAsyncEnumeratorExtensions.ToPageEnumerable</c>, which is internal to
    ///     that package: streams the query in fixed-size pages instead of materializing the whole result set.
    /// </summary>
    private static async IAsyncEnumerable<TEntity> ToPageEnumerable<TEntity>(
        this IQueryable<TEntity> query,
        int pageSize = 100,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) where TEntity : class
    {
        var currentPage = 0;
        var hasMorePages = true;

        while (hasMorePages)
        {
            var page = await query
                .Skip(currentPage * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            currentPage++;
            hasMorePages = page.Count == pageSize;

            foreach (var item in page)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return item;
            }
        }
    }

    #endregion

    /// <param name="repo">The repository</param>
    /// <typeparam name="TEntity">Type of the entity</typeparam>
    extension<TEntity>(IReadRepository<TEntity> repo) where TEntity : class
    {
        /// <summary>
        ///     Applies a specification to a repository and returns a queryable result.
        /// </summary>
        /// <param name="specification">The specification to apply</param>
        /// <returns>An <see cref="IQueryable{TEntity}" /> with the specification applied</returns>
        public IQueryable<TEntity> QuerySpecs(ISpecification<TEntity> specification) =>
            repo.Query().ApplySpecs(specification);

        /// <summary>
        ///     Asynchronously determines whether a sequence contains any elements.
        /// </summary>
        /// <param name="specification">The specification to apply</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public Task<bool> SpecsAnyAsync(ISpecification<TEntity> specification,
            CancellationToken cancellationToken = default) =>
            repo.QuerySpecs(specification).AnyAsync(cancellationToken);

        /// <summary>
        ///     Asynchronously returns the number of elements in a sequence.
        /// </summary>
        /// <param name="specification">The specification to apply</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public Task<int> SpecsCountAsync(ISpecification<TEntity> specification,
            CancellationToken cancellationToken = default) =>
            repo.QuerySpecs(specification).CountAsync(cancellationToken);

        /// <summary>
        ///     Asynchronously returns the first entity matching the specification.
        /// </summary>
        /// <param name="specification">The specification to apply</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public Task<TEntity> SpecsFirstAsync(ISpecification<TEntity> specification,
            CancellationToken cancellationToken = default) =>
            repo.QuerySpecs(specification).FirstAsync(cancellationToken);

        /// <summary>
        ///     Asynchronously returns the first entity matching the specification, or null if none found.
        /// </summary>
        /// <param name="specification">The specification to apply</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public Task<TEntity?> SpecsFirstOrDefaultAsync(ISpecification<TEntity> specification,
            CancellationToken cancellationToken = default) =>
            repo.QuerySpecs(specification).FirstOrDefaultAsync(cancellationToken);

        /// <summary>
        ///     Asynchronously returns a list of entities matching the specification.
        /// </summary>
        /// <param name="specification">The specification to apply</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task<IList<TEntity>> SpecsListAsync(ISpecification<TEntity> specification,
            CancellationToken cancellationToken = default) =>
            await repo.QuerySpecs(specification).ToListAsync(cancellationToken);

        /// <summary>
        ///     Returns an async enumerable of entities matching the specification, paged.
        /// </summary>
        /// <param name="specification">The specification to apply</param>
        /// <returns>An async enumerable of entities</returns>
        public IAsyncEnumerable<TEntity> SpecsToPageEnumerable(ISpecification<TEntity> specification)
        {
            specification.EnsureSpecHasOrdering();
            var query = (IOrderedQueryable<TEntity>)repo.Query().ApplySpecs(specification);
            return query.ToPageEnumerable();
        }

        /// <summary>
        ///     Asynchronously returns a paged list of entities matching the specification.
        /// </summary>
        /// <param name="specification">The specification to apply</param>
        /// <param name="pageNumber">Page number</param>
        /// <param name="pageSize">Page size</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A paged list of entities</returns>
        public Task<IPagedList<TEntity>> SpecsToPageListAsync(ISpecification<TEntity> specification,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            repo.QuerySpecs(specification)
                .ToPagedListAsync(pageNumber, pageSize, totalSetCount: null, cancellationToken: cancellationToken);
    }
}
#pragma warning restore CS0618