using Microsoft.EntityFrameworkCore;
using X.PagedList;
using X.PagedList.EF;

namespace DKNet.EfCore.Specifications.Extensions;

/// <summary>
///     Provides extension methods for applying <see cref="IModelSpecification{TEntity,TModel}" /> instances to an
///     <see cref="IRepositorySpec" /> in order to execute filtered, ordered, and projected queries against the
///     underlying data source.
///     These helpers encapsulate the common query patterns (first, list, paged list, async enumeration) while preserving
///     projection semantics (entity -> model) defined by the <c>IModelSpecification</c>.
/// </summary>
public static class ModelSpecRepoExtensions
{
    /// <param name="repo">The repository used to materialize the query.</param>
    extension(IRepositorySpec repo)
    {
        /// <summary>
        ///     Asynchronously returns the first projected model produced by entities matching the specification.
        /// </summary>
        /// <typeparam name="TEntity">The EF Core entity type being queried.</typeparam>
        /// <typeparam name="TModel">The destination model / DTO type produced by the mapping layer.</typeparam>
        /// <param name="specification">The model specification defining filter and ordering logic.</param>
        /// <param name="cancellationToken">A token allowing the operation to be cancelled.</param>
        /// <returns>The first projected <typeparamref name="TModel" /> instance.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the sequence is empty (no entity matches).</exception>
        /// <remarks>
        ///     If no ordering is defined in the specification EF Core will emit a warning about using First / FirstOrDefault
        ///     without OrderBy which may result in non-deterministic row selection.
        /// </remarks>
        public Task<TModel> FirstAsync<TEntity, TModel>(IModelSpecification<TEntity, TModel> specification,
            CancellationToken cancellationToken = default)
            where TEntity : class
            where TModel : class =>
            repo.Query<TEntity, TModel>(specification).FirstAsync(cancellationToken);

        /// <summary>
        ///     Asynchronously returns the first projected model produced by entities matching the specification or <c>null</c>
        ///     when the sequence is empty.
        /// </summary>
        /// <typeparam name="TEntity">The EF Core entity type being queried.</typeparam>
        /// <typeparam name="TModel">The destination model / DTO type produced by the mapping layer.</typeparam>
        /// <param name="specification">The model specification defining filter and ordering logic.</param>
        /// <param name="cancellationToken">A token allowing the operation to be cancelled.</param>
        /// <returns>The first projected model or <c>null</c> if no entity matches.</returns>
        public Task<TModel?> FirstOrDefaultAsync<TEntity, TModel>(IModelSpecification<TEntity, TModel> specification,
            CancellationToken cancellationToken = default)
            where TEntity : class
            where TModel : class =>
            repo.Query<TEntity, TModel>(specification).FirstOrDefaultAsync(cancellationToken);

        /// <summary>
        ///     Asynchronously materializes and returns all projected models for entities matching the specification.
        /// </summary>
        /// <typeparam name="TEntity">The EF Core entity type being queried.</typeparam>
        /// <typeparam name="TModel">The destination model / DTO type produced by the mapping layer.</typeparam>
        /// <param name="specification">The model specification defining filter and ordering logic.</param>
        /// <param name="cancellationToken">A token allowing the operation to be cancelled.</param>
        /// <returns>An <see cref="IList{T}" /> containing zero or more projected models.</returns>
        /// <remarks>
        ///     Use <see cref="ToPagedListAsync{TEntity,TModel}(IRepositorySpec,IModelSpecification{TEntity,TModel},int,int)" />
        ///     for large result sets to avoid retrieving the full collection in a single query.
        /// </remarks>
        public async Task<IList<TModel>> ToListAsync<TEntity, TModel>(
            IModelSpecification<TEntity, TModel> specification,
            CancellationToken cancellationToken = default)
            where TEntity : class
            where TModel : class =>
            await repo.Query<TEntity, TModel>(specification).ToListAsync(cancellationToken);

        /// <summary>
        ///     Asynchronously materializes a single page of projected models for entities matching the specification.
        /// </summary>
        /// <typeparam name="TEntity">The EF Core entity type being queried.</typeparam>
        /// <typeparam name="TModel">The destination model / DTO type produced by the mapping layer.</typeparam>
        /// <param name="specification">The model specification defining filter and ordering logic.</param>
        /// <param name="pageNumber">The 1-based page number to retrieve.</param>
        /// <param name="pageSize">The number of items per page.</param>
        /// <returns>An <see cref="IPagedList{TModel}" /> representing the requested page (may be empty).</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     Thrown when <paramref name="pageNumber" /> or
        ///     <paramref name="pageSize" /> are invalid (&lt;= 0).
        /// </exception>
        public Task<IPagedList<TModel>> ToPagedListAsync<TEntity, TModel>(
            IModelSpecification<TEntity, TModel> specification, int pageNumber, int pageSize)
            where TEntity : class
            where TModel : class =>
            repo.Query<TEntity, TModel>(specification).ToPagedListAsync(pageNumber, pageSize);

        /// <summary>
        ///     Returns an async enumerable that streams the projected models for entities matching the specification using
        ///     internal page buffering. Useful for large result sets where full materialization is undesirable.
        /// </summary>
        /// <typeparam name="TEntity">The EF Core entity type being queried.</typeparam>
        /// <typeparam name="TModel">The destination model / DTO type produced by the mapping layer.</typeparam>
        /// <param name="specification">The model specification defining filter and ordering logic.</param>
        /// <returns>An <see cref="IAsyncEnumerable{TModel}" /> that yields projected models.</returns>
        /// <remarks>
        ///     Enumeration occurs lazily; the underlying query executes page by page. Apply ordering in the specification to
        ///     ensure stable pagination across multiple iterations.
        /// </remarks>
        public IAsyncEnumerable<TModel> ToPageEnumerable<TEntity, TModel>(
            IModelSpecification<TEntity, TModel> specification)
            where TEntity : class
            where TModel : class
        {
            specification.EnsureSpecHasOrdering();
            var query = repo.Query<TEntity, TModel>(specification);
            return query.ToPageEnumerable();
        }
    }
}