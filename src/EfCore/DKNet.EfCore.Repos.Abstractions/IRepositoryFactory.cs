namespace DKNet.EfCore.Repos.Abstractions;

public interface IRepositoryFactory : IDisposable, IAsyncDisposable
{
    IRepository<TEntity> Create<TEntity>() where TEntity : class;
    IReadRepository<TEntity> CreateRead<TEntity>() where TEntity : class;
    IWriteRepository<TEntity> CreateWrite<TEntity>() where TEntity : class;
}