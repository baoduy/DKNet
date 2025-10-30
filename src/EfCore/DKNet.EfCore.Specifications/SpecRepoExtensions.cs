using DKNet.EfCore.Extensions.Extensions;
using Microsoft.EntityFrameworkCore;
using X.PagedList;
using X.PagedList.EF;

namespace DKNet.EfCore.Specifications;

/// <summary>
///     Provides extension methods for applying specifications to repositories and queries.
/// </summary>
public static class SpecRepoExtensions
{
    #region Methods

    /// <summary>
    ///     Asynchronously determines whether any elements match the specification.
    /// </summary>
    /// <typeparam name="TEntity">Type of the entity</typeparam>
    /// <param name="repo">The repository</param>
    /// <param name="specification">The specification to apply</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>
    ///     A task representing the asynchronous operation that returns true if any elements match the specification;
    ///     otherwise, false
    /// </returns>
    public static Task<bool> AnyAsync<TEntity>(
        this IRepositorySpec repo,
        ISpecification<TEntity> specification,
        CancellationToken cancellationToken = default)
        where TEntity : class =>
        repo.Query(specification).AnyAsync(cancellationToken);

    /// <summary>
    ///     Asynchronously returns the number of elements matching the specification.
    /// </summary>
    /// <typeparam name="TEntity">Type of the entity</typeparam>
    /// <param name="repo">The repository</param>
    /// <param name="specification">The specification to apply</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>
    ///     A task representing the asynchronous operation that returns the number of elements that match the
    ///     specification
    /// </returns>
    public static Task<int> CountAsync<TEntity>(
        this IRepositorySpec repo,
        ISpecification<TEntity> specification,
        CancellationToken cancellationToken = default)
        where TEntity : class =>
        repo.Query(specification).CountAsync(cancellationToken);

    /// <summary>
    ///     Asynchronously returns the first entity matching the specification.
    /// </summary>
    /// <typeparam name="TEntity">Type of the entity</typeparam>
    /// <param name="repo">The repository</param>
    /// <param name="specification">The specification to apply</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation that returns the first entity</returns>
    /// <exception cref="InvalidOperationException">Thrown when no entity matching the specification is found</exception>
    public static Task<TEntity> FirstAsync<TEntity>(
        this IRepositorySpec repo,
        ISpecification<TEntity> specification,
        CancellationToken cancellationToken = default)
        where TEntity : class =>
        repo.Query(specification).FirstAsync(cancellationToken);

    /// <summary>
    ///     Asynchronously returns the first entity matching the specification, or null if none found.
    /// </summary>
    /// <typeparam name="TEntity">Type of the entity</typeparam>
    /// <param name="repo">The repository</param>
    /// <param name="specification">The specification to apply</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation that returns the first entity or null</returns>
    public static Task<TEntity?> FirstOrDefaultAsync<TEntity>(
        this IRepositorySpec repo,
        ISpecification<TEntity> specification,
        CancellationToken cancellationToken = default)
        where TEntity : class =>
        repo.Query(specification).FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    ///     Asynchronously returns the first projected model matching the specification, or null if none found.
    /// </summary>
    /// <typeparam name="TEntity">Type of the entity</typeparam>
    /// <typeparam name="TModel">Type of the model to project to</typeparam>
    /// <param name="repo">The repository</param>
    /// <param name="specification">The specification to apply</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation that returns the first projected model or null</returns>
    public static Task<TModel?> FirstOrDefaultAsync<TEntity, TModel>(
        this IRepositorySpec repo,
        ISpecification<TEntity> specification,
        CancellationToken cancellationToken = default)
        where TEntity : class
        where TModel : class
        => repo.Query<TEntity, TModel>(specification).FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    ///     Asynchronously returns a list of entities matching the specification.
    /// </summary>
    /// <typeparam name="TEntity">Type of the entity</typeparam>
    /// <param name="repo">The repository</param>
    /// <param name="specification">The specification to apply</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation that returns a list of entities</returns>
    public static async Task<IList<TEntity>> ToListAsync<TEntity>(
        this IRepositorySpec repo,
        ISpecification<TEntity> specification,
        CancellationToken cancellationToken = default)
        where TEntity : class =>
        await repo.Query(specification).ToListAsync(cancellationToken);

    /// <summary>
    ///     Asynchronously returns a list of projected models matching the specification.
    /// </summary>
    /// <typeparam name="TEntity">Type of the entity</typeparam>
    /// <typeparam name="TModel">Type of the model to project to</typeparam>
    /// <param name="repo">The repository</param>
    /// <param name="specification">The specification to apply</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation that returns a list of projected models</returns>
    public static async Task<IList<TModel>> ToListAsync<TEntity, TModel>(
        this IRepositorySpec repo,
        ISpecification<TEntity> specification,
        CancellationToken cancellationToken = default)
        where TEntity : class
        where TModel : class
        => await repo.Query<TEntity, TModel>(specification).ToListAsync(cancellationToken);

    /// <summary>
    ///     Asynchronously returns a paged list of entities matching the specification.
    /// </summary>
    /// <typeparam name="TEntity">Type of the entity</typeparam>
    /// <param name="repo">The repository</param>
    /// <param name="specification">The specification to apply</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <returns>A task representing the asynchronous operation that returns a paged list of entities</returns>
    public static Task<IPagedList<TEntity>> ToPagedListAsync<TEntity>(
        this IRepositorySpec repo,
        ISpecification<TEntity> specification,
        int pageNumber,
        int pageSize) where TEntity : class => repo.Query(specification).ToPagedListAsync(pageNumber, pageSize);

    /// <summary>
    ///     Asynchronously returns a paged list of projected models matching the specification.
    /// </summary>
    /// <typeparam name="TEntity">Type of the entity</typeparam>
    /// <typeparam name="TModel">Type of the model to project to</typeparam>
    /// <param name="repo">The repository</param>
    /// <param name="specification">The specification to apply</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <returns>A task representing the asynchronous operation that returns a paged list of projected models</returns>
    public static Task<IPagedList<TModel>> ToPagedListAsync<TEntity, TModel>(
        this IRepositorySpec repo,
        ISpecification<TEntity> specification,
        int pageNumber,
        int pageSize)
        where TEntity : class
        where TModel : class
        => repo.Query<TEntity, TModel>(specification).ToPagedListAsync(pageNumber, pageSize);

    /// <summary>
    ///     Returns an async enumerable of entities matching the specification, paged.
    /// </summary>
    /// <typeparam name="TEntity">Type of the entity</typeparam>
    /// <param name="repo">The repository</param>
    /// <param name="specification">The specification to apply</param>
    /// <returns>An async enumerable of entities</returns>
    public static IAsyncEnumerable<TEntity> ToPageEnumerable<TEntity>(
        this IRepositorySpec repo,
        ISpecification<TEntity> specification) where TEntity : class =>
        repo.Query(specification).ToPageEnumerable();

    /// <summary>
    ///     Returns an async enumerable of projected models matching the specification, paged.
    /// </summary>
    /// <typeparam name="TEntity">Type of the entity</typeparam>
    /// <typeparam name="TModel">Type of the model to project to</typeparam>
    /// <param name="repo">The repository</param>
    /// <param name="specification">The specification to apply</param>
    /// <returns>An async enumerable of projected models</returns>
    public static IAsyncEnumerable<TModel> ToPageEnumerable<TEntity, TModel>(
        this IRepositorySpec repo,
        ISpecification<TEntity> specification)
        where TEntity : class
        where TModel : class
        => repo.Query<TEntity, TModel>(specification).ToPageEnumerable();

    #endregion
}