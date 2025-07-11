

namespace DKNet.EfCore.Repos;

public class ReadRepository<TEntity>(DbContext dbContext, IEnumerable<IMapper>? mappers = null) : IReadRepository<TEntity>
    where TEntity : class
{
    private readonly IMapper? _mapper = mappers?.FirstOrDefault();
    /// <summary>
    ///     Get ReadOnly (No Tracking) Query for Entity
    /// </summary>
    /// <returns></returns>
    public virtual IQueryable<TEntity> Gets()
        => dbContext.Set<TEntity>().AsNoTrackingWithIdentityResolution();


    public virtual async ValueTask<TEntity?> FindAsync(params object[] id)
    {
        var entity = await dbContext.FindAsync<TEntity>(id);
        if (entity != null)
            dbContext.Entry(entity).State = EntityState.Detached;
        return entity;
    }

    public virtual async Task<TEntity?> FindAsync(Expression<Func<TEntity, bool>> filter, CancellationToken cancellationToken = default)
    {
        var entity = await Gets().Where(filter).FirstOrDefaultAsync(cancellationToken);
        if (entity != null)
            dbContext.Entry(entity).State = EntityState.Detached;
        return entity;
    }

    public virtual IQueryable<TModel> GetProjection<TModel>() where TModel : class
        => _mapper == null
            ? throw new InvalidOperationException("Mapper is not registered")
            : Gets().ProjectToType<TModel>(_mapper.Config);
}