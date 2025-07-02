


namespace DKNet.EfCore.Repos;

public class Repository<TEntity>(DbContext dbContext, IEnumerable<IMapper>? mappers = null) : WriteRepository<TEntity>(dbContext), IRepository<TEntity>
    where TEntity : class
{
    private readonly IMapper? _mapper = mappers?.FirstOrDefault();

    public virtual ValueTask<TEntity?> FindAsync(params object[] id)
    => dbContext.Set<TEntity>().FindAsync(id);

    public virtual Task<TEntity?> FindAsync(Expression<Func<TEntity, bool>> filter, CancellationToken cancellationToken = default)
    => dbContext.Set<TEntity>().Where(filter).FirstOrDefaultAsync(cancellationToken);

    public virtual IQueryable<TModel> GetProjection<TModel>() where TModel : class
    => _mapper is null
            ? throw new InvalidOperationException("IMapper is not registered.")
            : dbContext.Set<TEntity>().ProjectToType<TModel>(_mapper.Config);

    public virtual IQueryable<TEntity> Gets()
    => dbContext.Set<TEntity>();
}