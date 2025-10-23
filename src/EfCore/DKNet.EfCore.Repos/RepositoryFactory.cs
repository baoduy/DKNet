namespace DKNet.EfCore.Repos;

public sealed class RepositoryFactory<TDbContext>(
    IDbContextFactory<TDbContext> dbFactory,
    IEnumerable<IMapper>? mappers = null)
    : IRepositoryFactory where TDbContext : DbContext
{
    private readonly TDbContext _db = dbFactory.CreateDbContext();

    public void Dispose() => _db.Dispose();

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    public IRepository<TEntity> Create<TEntity>() where TEntity : class => new Repository<TEntity>(_db, mappers);

    public IReadRepository<TEntity> CreateRead<TEntity>() where TEntity : class =>
        new ReadRepository<TEntity>(_db, mappers);

    public IWriteRepository<TEntity> CreateWrite<TEntity>() where TEntity : class =>
        new WriteRepository<TEntity>(_db);
}