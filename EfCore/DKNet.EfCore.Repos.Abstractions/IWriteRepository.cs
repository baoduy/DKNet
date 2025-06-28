using Microsoft.EntityFrameworkCore.Storage;

namespace DKNet.EfCore.Repos.Abstractions;

public interface IWriteRepository<in TEntity> where TEntity : class
{
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
    void Add(TEntity entity);

    /// <summary>
    ///  Add Range of Entities
    /// </summary>
    /// <param name="entities"></param>
    void AddRange(IEnumerable<TEntity> entities);

    /// <summary>
    ///   Update Entity
    /// </summary>
    /// <param name="entity"></param>
    void Update(TEntity entity);

    /// <summary>
    ///  Update Range of Entities
    /// </summary>
    /// <param name="entities"></param>
    void UpdateRange(IEnumerable<TEntity> entities);

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

    ///// <summary>
    /////  Bulk Insert Entities
    ///// </summary>
    ///// <param name="entities"></param>
    ///// <param name="cancellationToken"></param>
    ///// <returns></returns>
    //Task<int> BulkInsertAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    ///// <summary>
    ///// Bulk Update Entities
    ///// </summary>
    ///// <param name="setPropertyCalls"></param>
    ///// <param name="cancellationToken"></param>
    ///// <returns></returns>
    //Task<int> BulkUpdateAsync(Expression<Func<SetPropertyCalls<TEntity>, SetPropertyCalls<TEntity>>> setPropertyCalls, CancellationToken cancellationToken = default);

    ///// <summary>
    ///// Bulk Delete Entities
    ///// </summary>
    ///// <param name="predicate"></param>
    ///// <param name="cancellationToken"></param>
    ///// <returns></returns>
    //Task<int> BulkDeleteAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
}