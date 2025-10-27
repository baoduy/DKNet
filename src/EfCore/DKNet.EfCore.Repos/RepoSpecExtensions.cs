using DKNet.EfCore.Extensions.Extensions;
using DKNet.EfCore.Specifications;
using X.PagedList;
using X.PagedList.EF;

namespace DKNet.EfCore.Repos;

/// <summary>
///     Provides extension methods for applying specifications to repositories and queries.
/// </summary>
public static class RepoExtensions
{
    #region Methods

    /// <summary>
    ///     Applies a specification to a repository and returns a queryable result.
    /// </summary>
    /// <typeparam name="TEntity">Type of the entity</typeparam>
    /// <param name="repo">The repository</param>
    /// <param name="specification">The specification to apply</param>
    /// <returns>An <see cref="IQueryable{TEntity}" /> with the specification applied</returns>
    public static IQueryable<TEntity> QuerySpecs<TEntity>(
        this IReadRepository<TEntity> repo,
        ISpecification<TEntity> specification) where TEntity : class =>
        repo.Query().ApplySpecs(specification);

    /// <summary>
    ///     Asynchronously determines whether a sequence contains any elements.
    /// </summary>
    /// <typeparam name="TEntity">Type of the entity</typeparam>
    /// <param name="repo">The repository</param>
    /// <param name="specification">The specification to apply</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public static Task<bool> SpecsAnyAsync<TEntity>(
        this IReadRepository<TEntity> repo,
        ISpecification<TEntity> specification, CancellationToken cancellationToken = default) where TEntity : class =>
        repo.QuerySpecs(specification).AnyAsync(cancellationToken);

    /// <summary>
    ///     Asynchronously returns the number of elements in a sequence.
    /// </summary>
    /// <typeparam name="TEntity">Type of the entity</typeparam>
    /// <param name="repo">The repository</param>
    /// <param name="specification">The specification to apply</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public static Task<int> SpecsCountAsync<TEntity>(
        this IReadRepository<TEntity> repo,
        ISpecification<TEntity> specification, CancellationToken cancellationToken = default) where TEntity : class =>
        repo.QuerySpecs(specification).CountAsync(cancellationToken);

    /// <summary>
    ///     Asynchronously returns the first entity matching the specification.
    /// </summary>
    /// <typeparam name="TEntity">Type of the entity</typeparam>
    /// <param name="repo">The repository</param>
    /// <param name="specification">The specification to apply</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public static Task<TEntity> SpecsFirstAsync<TEntity>(
        this IReadRepository<TEntity> repo,
        ISpecification<TEntity> specification, CancellationToken cancellationToken = default) where TEntity : class =>
        repo.QuerySpecs(specification).FirstAsync(cancellationToken);

    /// <summary>
    ///     Asynchronously returns the first entity matching the specification, or null if none found.
    /// </summary>
    /// <typeparam name="TEntity">Type of the entity</typeparam>
    /// <param name="repo">The repository</param>
    /// <param name="specification">The specification to apply</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public static Task<TEntity?> SpecsFirstOrDefaultAsync<TEntity>(
        this IReadRepository<TEntity> repo,
        ISpecification<TEntity> specification, CancellationToken cancellationToken = default) where TEntity : class =>
        repo.QuerySpecs(specification).FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    ///     Asynchronously returns a list of entities matching the specification.
    /// </summary>
    /// <typeparam name="TEntity">Type of the entity</typeparam>
    /// <param name="repo">The repository</param>
    /// <param name="specification">The specification to apply</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public static async Task<IList<TEntity>> SpecsListAsync<TEntity>(
        this IReadRepository<TEntity> repo,
        ISpecification<TEntity> specification, CancellationToken cancellationToken = default) where TEntity : class =>
        await repo.QuerySpecs(specification).ToListAsync(cancellationToken);

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

    #endregion
}