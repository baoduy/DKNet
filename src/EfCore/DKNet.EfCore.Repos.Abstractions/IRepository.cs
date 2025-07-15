namespace DKNet.EfCore.Repos.Abstractions;

/// <summary>
///     Combines read and write operations for a domain entity
/// </summary>
/// <typeparam name="TEntity">The entity type this repository manages</typeparam>
public interface IRepository<TEntity> : IReadRepository<TEntity>, IWriteRepository<TEntity>
    where TEntity : class;