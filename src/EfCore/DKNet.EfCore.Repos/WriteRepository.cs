using System.Collections;

namespace DKNet.EfCore.Repos;

public class WriteRepository<TEntity>(DbContext dbContext) : IWriteRepository<TEntity>
    where TEntity : class
{
    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => dbContext.Database.BeginTransactionAsync(cancellationToken);

    public virtual async ValueTask AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        => await dbContext.AddAsync(entity, cancellationToken);

    public virtual async ValueTask AddRangeAsync(IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default)
        => await dbContext.AddRangeAsync(entities, cancellationToken);

    public virtual void Delete(TEntity entity)
        => dbContext.Set<TEntity>().Remove(entity);

    public virtual void DeleteRange(IEnumerable<TEntity> entities)
        => dbContext.Set<TEntity>().RemoveRange(entities);

    public virtual Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);

    public virtual async Task UpdateAsync(TEntity entity)
    {
        var entry = dbContext.Entry(entity);
        entry.State = EntityState.Modified;
        //Scan and include all untracked entities from navigation properties
        foreach (var property in entry.Navigations)
        {
            if (property.CurrentValue is not ICollection coll) continue;
            foreach (var item in coll)
            {
                var itemEntry = dbContext.Entry(item);
                if (itemEntry.State == EntityState.Detached)
                    await dbContext.AddAsync(item);
            }
        }

    }

    public async Task UpdateRangeAsync(IEnumerable<TEntity> entities)
    {
        foreach (var entity in entities)
            await UpdateAsync(entity);
    }
}