namespace DKNet.EfCore.Repos;

public class Repository<TEntity>(DbContext dbContext)
    : WriteRepository<TEntity>(dbContext), IRepository<TEntity>
    where TEntity : class
{
    public ValueTask<TEntity?> FindAsync(object keyValue, CancellationToken cancellationToken = default)
        => dbContext.Set<TEntity>().FindAsync([keyValue], cancellationToken);

    public ValueTask<TEntity?> FindAsync(object[] keyValues, CancellationToken cancellationToken = default)
        => dbContext.Set<TEntity>().FindAsync(keyValues, cancellationToken);

    public Task<TEntity?> FindAsync(Expression<Func<TEntity, bool>> filter,
        CancellationToken cancellationToken = default) =>
        dbContext.Set<TEntity>().Where(filter).FirstOrDefaultAsync(cancellationToken);

    public IQueryable<TModel> GetDto<TModel>(Expression<Func<TEntity, bool>>? filter = null)
        where TModel : class
    {
        var query = Gets();
        if (filter is not null)
            query = query.Where(filter);
        return query.SelectFacet<TEntity,TModel>();
    }

    public virtual IQueryable<TEntity> Gets() => dbContext.Set<TEntity>();
}