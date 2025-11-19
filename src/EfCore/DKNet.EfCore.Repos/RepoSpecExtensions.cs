using DKNet.EfCore.Specifications;
using X.PagedList;
using X.PagedList.EF;

namespace DKNet.EfCore.Repos;

/// <summary>
///     Provides extension methods for applying specifications to repositories and queries.
/// </summary>
public static class RepoExtensions
{
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
        /// <returns>A paged list of entities</returns>
        public Task<IPagedList<TEntity>> SpecsToPageListAsync(ISpecification<TEntity> specification,
            int pageNumber,
            int pageSize) =>
            repo.QuerySpecs(specification)
                .ToPagedListAsync(pageNumber, pageSize);
    }
}