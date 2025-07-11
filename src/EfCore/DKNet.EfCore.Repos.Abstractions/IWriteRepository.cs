using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;

namespace DKNet.EfCore.Repos.Abstractions;

public interface IWriteRepository<TEntity> where TEntity : class
{
    EntityEntry<TEntity> Entry(TEntity entity);

    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///    Save Changes to Database
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///   Add Entity
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="cancellationToken"></param>
    ValueTask AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    ///  Add Range of Entities
    /// </summary>
    /// <param name="entities"></param>
    /// <param name="cancellationToken"></param>
    ValueTask AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Update Entity
    /// </summary>
    /// <param name="entity"></param>
    Task<int> UpdateAsync(TEntity entity);

    /// <summary>
    ///  Update Range of Entities
    /// </summary>
    /// <param name="entities"></param>
    Task UpdateRangeAsync(IEnumerable<TEntity> entities);

    /// <summary>
    ///  Delete Entity
    /// </summary>
    /// <param name="entity"></param>
    void Delete(TEntity entity);

    /// <summary>
    /// Delete Range of Entities
    /// </summary>
    /// <param name="entities"></param>
    void DeleteRange(IEnumerable<TEntity> entities);
}