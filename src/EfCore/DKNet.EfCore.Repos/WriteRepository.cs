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

    public virtual void Update(TEntity entity)
        => dbContext.Entry(entity).State = EntityState.Modified;

    public virtual void UpdateRange(IEnumerable<TEntity> entities)
    {
        foreach (var entity in entities)
        {
            dbContext.Entry(entity).State = EntityState.Modified;
        }
    }
}