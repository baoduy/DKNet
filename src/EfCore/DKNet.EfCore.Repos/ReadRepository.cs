namespace DKNet.EfCore.Repos;

public class ReadRepository<TEntity>(DbContext dbContext, IEnumerable<IMapper>? mappers = null)
    : IReadRepository<TEntity>
    where TEntity : class
{
    private readonly IMapper? _mapper = mappers?.FirstOrDefault();

    public virtual IQueryable<TEntity> Query() => dbContext.Set<TEntity>().AsNoTracking();
    public IQueryable<TEntity> Query(Expression<Func<TEntity, bool>> filter) => Query().Where(filter);

    public ValueTask<TEntity?> FindAsync(object keyValue, CancellationToken cancellationToken = default)
        => FindAsync([keyValue], cancellationToken);

    public async ValueTask<TEntity?> FindAsync(object[] keyValues, CancellationToken cancellationToken = default) =>
        await dbContext.FindAsync<TEntity>(keyValues, cancellationToken);

    public Task<TEntity?> FindAsync(Expression<Func<TEntity, bool>> filter,
        CancellationToken cancellationToken = default) =>
        Query(filter).FirstOrDefaultAsync(cancellationToken);

    public Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> filter,
        CancellationToken cancellationToken = default)
        => Query(filter).AnyAsync(cancellationToken);

    public Task<int> CountAsync(Expression<Func<TEntity, bool>> filter,
        CancellationToken cancellationToken = default) =>
        Query(filter).CountAsync(cancellationToken);

    public IQueryable<TModel> Query<TModel>(Expression<Func<TEntity, bool>> filter)
        where TModel : class
    {
        if (_mapper is null) throw new InvalidOperationException("IMapper is not registered.");

        var query = Query(filter);
        return query.ProjectToType<TModel>(_mapper.Config);
    }
}