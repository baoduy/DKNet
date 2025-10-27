namespace DKNet.EfCore.Repos;

public class Repository<TEntity>(DbContext dbContext, IEnumerable<IMapper>? mappers = null)
    : WriteRepository<TEntity>(dbContext), IRepository<TEntity>
    where TEntity : class
{
    #region Fields

    private readonly IMapper? _mapper = mappers?.FirstOrDefault();

    #endregion

    #region Methods

    public Task<int> CountAsync(Expression<Func<TEntity, bool>> filter,
        CancellationToken cancellationToken = default) =>
        Query(filter).CountAsync(cancellationToken);

    public Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> filter, CancellationToken cancellationToken = default)
        => Query(filter).AnyAsync(cancellationToken);


    public ValueTask<TEntity?> FindAsync(object keyValue, CancellationToken cancellationToken = default)
        => FindAsync([keyValue], cancellationToken);

    public ValueTask<TEntity?> FindAsync(object[] keyValues, CancellationToken cancellationToken = default)
        => dbContext.Set<TEntity>().FindAsync(keyValues, cancellationToken);

    public Task<TEntity?> FindAsync(Expression<Func<TEntity, bool>> filter,
        CancellationToken cancellationToken = default) =>
        Query(filter).FirstOrDefaultAsync(cancellationToken);

    public IQueryable<TModel> Query<TModel>(Expression<Func<TEntity, bool>> filter)
        where TModel : class
    {
        if (_mapper is null) throw new InvalidOperationException("IMapper is not registered.");

        var query = Query(filter);
        return query.ProjectToType<TModel>(_mapper.Config);
    }

    public virtual IQueryable<TEntity> Query() => dbContext.Set<TEntity>();
    public virtual IQueryable<TEntity> Query(Expression<Func<TEntity, bool>> filter) => Query().Where(filter);

    #endregion
}