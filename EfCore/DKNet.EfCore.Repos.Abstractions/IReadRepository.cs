using System.Linq.Expressions;

namespace DKNet.EfCore.Repos.Abstractions;

/// <summary>
///    Read Repository Interface
/// </summary>
/// <typeparam name="TEntity"></typeparam>
public interface IReadRepository<TEntity> where TEntity : class
{
    /// <summary>
    /// Get IQueryable of entity.
    /// </summary>
    /// <returns></returns>
    IQueryable<TEntity> Gets();

    /// <summary>
    ///     Get Projection of Entity
    /// </summary>
    /// <typeparam name="TModel"></typeparam>
    /// <returns></returns>
    IQueryable<TModel> GetProjection<TModel>() where TModel : class;

    /// <summary>
    ///    Find Entity by Id
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    ValueTask<TEntity?> FindAsync(params object[] id);

    /// <summary>
    ///   Find Entity by Filter
    /// </summary>
    /// <param name="filter"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<TEntity?> FindAsync(
        Expression<Func<TEntity, bool>> filter,
        CancellationToken cancellationToken = default);
}