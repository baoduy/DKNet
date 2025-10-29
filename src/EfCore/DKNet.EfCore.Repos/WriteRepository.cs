using DKNet.EfCore.Extensions.Extensions;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace DKNet.EfCore.Repos;

public class WriteRepository<TEntity>(DbContext dbContext) : IWriteRepository<TEntity>
    where TEntity : class
{
    #region Methods

    public virtual async ValueTask AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        => await dbContext.AddAsync(entity, cancellationToken);

    public virtual async ValueTask AddRangeAsync(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default) =>
        await dbContext.AddRangeAsync(entities, cancellationToken);

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => dbContext.Database.BeginTransactionAsync(cancellationToken);

    public virtual void Delete(TEntity entity) => dbContext.Set<TEntity>().Remove(entity);

    public virtual void DeleteRange(IEnumerable<TEntity> entities) => dbContext.Set<TEntity>().RemoveRange(entities);

    public EntityEntry<TEntity> Entry(TEntity entity) => dbContext.Entry(entity);

    public virtual async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.AddNewEntitiesFromNavigations(cancellationToken);
        return await dbContext.SaveChangesAsync(cancellationToken);
    }

    public virtual async Task<int> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        dbContext.Entry(entity).State = EntityState.Modified;

        var newEntities = dbContext.GetNewEntitiesFromNavigations(dbContext.Entry(entity)).ToList();
        await dbContext.AddRangeAsync(newEntities, cancellationToken);
        return newEntities.Count;
    }

    public async Task UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        foreach (var entity in entities)
        {
            await this.UpdateAsync(entity, cancellationToken);
        }
    }

    #endregion
}