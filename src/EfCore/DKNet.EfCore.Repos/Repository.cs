namespace DKNet.EfCore.Repos;

public class Repository<TEntity>(DbContext dbContext, IEnumerable<IMapper>? mappers = null)
    : WriteRepository<TEntity>(dbContext), IRepository<TEntity>
    where TEntity : class
{
    private readonly IMapper? _mapper = mappers?.FirstOrDefault();

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
        if (_mapper is null) throw new InvalidOperationException("IMapper is not registered.");

        var query = Gets();
        if (filter is not null)
            query = query.Where(filter);
        return query.ProjectToType<TModel>(_mapper.Config);
    }

    public virtual IQueryable<TEntity> Gets() => dbContext.Set<TEntity>();
}