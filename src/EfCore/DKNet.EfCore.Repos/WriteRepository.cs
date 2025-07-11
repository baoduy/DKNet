using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DKNet.EfCore.Repos;

public class WriteRepository<TEntity>(DbContext dbContext) : IWriteRepository<TEntity>
    where TEntity : class
{
    private readonly IEntityType _entityType = dbContext.Model.FindEntityType(typeof(TEntity))!;

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



    public virtual async Task<int> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        dbContext.Entry(entity).State = EntityState.Modified;

        //Scan and include all untracked entities from navigation properties
        var navigations = _entityType
            .GetNavigations().Where(n => n.IsCollection && !n.IsShadowProperty());

        var count = 0;
        foreach (var nav in navigations)
        {
            foreach (var item in entity.GetNavigationCollection(nav))
            {
                var newEntry = dbContext.Entry(item);
                if (newEntry.IsNewItem())
                {
                    await dbContext.AddAsync(item, cancellationToken);
                    count += 1;
                }
            }
        }

        return count;
    }

    public async Task UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        foreach (var entity in entities)
            await UpdateAsync(entity, cancellationToken);
    }
}