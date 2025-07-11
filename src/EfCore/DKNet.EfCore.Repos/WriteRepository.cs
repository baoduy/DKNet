using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace DKNet.EfCore.Repos;

public class WriteRepository<TEntity>(DbContext dbContext) : IWriteRepository<TEntity>
    where TEntity : class
{
    public EntityEntry<TEntity> Entry(TEntity entity)
        => dbContext.Entry(entity);

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

    public virtual Task UpdateAsync(TEntity entity)
    {
        var entry = dbContext.Entry(entity);
        entry.State = EntityState.Modified;
        return Task.CompletedTask;
        //Scan and include all untracked entities from navigation properties
        // foreach (var nav in entry.Navigations)
        // {
        //     if (!nav.Metadata.IsCollection) continue;
        //     if (nav.CurrentValue is not IEnumerable coll) continue;
        //     foreach (var item in coll)
        //     {
        //         var itemEntry = dbContext.Entry(item);
        //         if (itemEntry.State is not EntityState.Modified and EntityState.Deleted)
        //             await dbContext.AddAsync(item);
        //     }
        // }
    }

    public async Task UpdateRangeAsync(IEnumerable<TEntity> entities)
    {
        foreach (var entity in entities)
            await UpdateAsync(entity);
    }
}