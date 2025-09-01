namespace DKNet.EfCore.Repos;

public class ReadRepository<TEntity>(DbContext dbContext)
    : IReadRepository<TEntity>
    where TEntity : class
{

    /// <summary>
    ///     Get ReadOnly (No Tracking) Query for Entity
    /// </summary>
    /// <returns></returns>
    public virtual IQueryable<TEntity> Gets() => dbContext.Set<TEntity>().AsNoTrackingWithIdentityResolution();

    public ValueTask<TEntity?> FindAsync(object keyValue, CancellationToken cancellationToken = default)
        => dbContext.Set<TEntity>().FindAsync([keyValue], cancellationToken);

    public async ValueTask<TEntity?> FindAsync(object[] keyValues, CancellationToken cancellationToken = default) =>
        await dbContext.FindAsync<TEntity>(keyValues, cancellationToken);

    public async Task<TEntity?> FindAsync(Expression<Func<TEntity, bool>> filter,
        CancellationToken cancellationToken = default) =>
        await Gets().Where(filter).FirstOrDefaultAsync(cancellationToken);
}