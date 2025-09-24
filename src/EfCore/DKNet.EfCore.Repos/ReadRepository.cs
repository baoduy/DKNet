namespace DKNet.EfCore.Repos;

public class ReadRepository<TEntity>(DbContext dbContext, IEnumerable<IMapper>? mappers = null)
    : IReadRepository<TEntity>
    where TEntity : class
{
    private readonly IMapper? _mapper = mappers?.FirstOrDefault();

    /// <summary>
    ///     Get ReadOnly (No Tracking) Query for Entity
    /// </summary>
    /// <returns></returns>
    public virtual IQueryable<TEntity> Gets() => dbContext.Set<TEntity>().AsNoTracking();

    public ValueTask<TEntity?> FindAsync(object keyValue, CancellationToken cancellationToken = default)
        => dbContext.Set<TEntity>().FindAsync([keyValue], cancellationToken);

    public async ValueTask<TEntity?> FindAsync(object[] keyValues, CancellationToken cancellationToken = default) =>
        await dbContext.FindAsync<TEntity>(keyValues, cancellationToken);

    public async Task<TEntity?> FindAsync(Expression<Func<TEntity, bool>> filter,
        CancellationToken cancellationToken = default) =>
        await Gets().Where(filter).FirstOrDefaultAsync(cancellationToken);

    public IQueryable<TModel> GetDto<TModel>(Expression<Func<TEntity, bool>>? filter = null)
        where TModel : class
    {
        if (_mapper is null) throw new InvalidOperationException("IMapper is not registered.");

        var query = Gets();
        if (filter is not null)
            query = query.Where(filter);
        return query.ProjectToType<TModel>(_mapper.Config);
    }
}