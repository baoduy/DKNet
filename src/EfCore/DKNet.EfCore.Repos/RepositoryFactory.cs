namespace DKNet.EfCore.Repos;

public sealed class RepositoryFactory<TDbContext>(
    IDbContextFactory<TDbContext> dbFactory,
    IEnumerable<IMapper>? mappers = null)
    : IRepositoryFactory where TDbContext : DbContext
{
    #region Fields

    private readonly TDbContext _db = dbFactory.CreateDbContext();

    #endregion

    #region Methods

    public IRepository<TEntity> Create<TEntity>() where TEntity : class => new Repository<TEntity>(this._db, mappers);

    public IReadRepository<TEntity> CreateRead<TEntity>() where TEntity : class =>
        new ReadRepository<TEntity>(this._db, mappers);

    public IWriteRepository<TEntity> CreateWrite<TEntity>() where TEntity : class =>
        new WriteRepository<TEntity>(this._db);

    public void Dispose() => this._db.Dispose();

    public ValueTask DisposeAsync() => this._db.DisposeAsync();

    #endregion
}