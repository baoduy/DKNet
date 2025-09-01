using System.Linq.Expressions;

namespace DKNet.EfCore.Repos.Abstractions;

/// <summary>
///     Read Repository Interface
/// </summary>
/// <typeparam name="TEntity"></typeparam>
public interface IReadRepository<TEntity> where TEntity : class
{
    /// <summary>
    ///     Get IQueryable of entity.
    /// </summary>
    /// <returns></returns>
    IQueryable<TEntity> Gets();

    /// <summary>
    ///     Find Entity by Id
    /// </summary>
    /// <param name="keyValue"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    ValueTask<TEntity?> FindAsync(object keyValue, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Find Entity by Id
    /// </summary>
    /// <param name="keyValues"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    ValueTask<TEntity?> FindAsync(object[] keyValues, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Find Entity by Filter
    /// </summary>
    /// <param name="filter"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<TEntity?> FindAsync(
        Expression<Func<TEntity, bool>> filter,
        CancellationToken cancellationToken = default);
}