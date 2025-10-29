namespace DKNet.EfCore.Repos;

public class ReadRepository<TEntity>(DbContext dbContext, IEnumerable<IMapper>? mappers = null)
    : IReadRepository<TEntity>
    where TEntity : class
{
    #region Fields

    private readonly IMapper? _mapper = mappers?.FirstOrDefault();

    #endregion

    #region Methods

    public Task<int> CountAsync(
        Expression<Func<TEntity, bool>> filter,
        CancellationToken cancellationToken = default) =>
        this.Query(filter).CountAsync(cancellationToken);

    public Task<bool> ExistsAsync(
        Expression<Func<TEntity, bool>> filter,
        CancellationToken cancellationToken = default)
        =>
            this.Query(filter).AnyAsync(cancellationToken);

    public ValueTask<TEntity?> FindAsync(object keyValue, CancellationToken cancellationToken = default)
        =>
            this.FindAsync([keyValue], cancellationToken);

    public async ValueTask<TEntity?> FindAsync(object[] keyValues, CancellationToken cancellationToken = default) =>
        await dbContext.FindAsync<TEntity>(keyValues, cancellationToken);

    public Task<TEntity?> FindAsync(
        Expression<Func<TEntity, bool>> filter,
        CancellationToken cancellationToken = default) =>
        this.Query(filter).FirstOrDefaultAsync(cancellationToken);

    public virtual IQueryable<TEntity> Query() => dbContext.Set<TEntity>().AsNoTracking();

    public IQueryable<TEntity> Query(Expression<Func<TEntity, bool>> filter) => this.Query().Where(filter);

    public IQueryable<TModel> Query<TModel>(Expression<Func<TEntity, bool>> filter)
        where TModel : class
    {
        if (this._mapper is null)
        {
            throw new InvalidOperationException("IMapper is not registered.");
        }

        var query = this.Query(filter);
        return query.ProjectToType<TModel>(this._mapper.Config);
    }

    #endregion
}