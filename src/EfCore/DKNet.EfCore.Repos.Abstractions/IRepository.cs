namespace DKNet.EfCore.Repos.Abstractions;

/// <summary>
///     Combines read and write operations for a domain entity
/// </summary>
/// <typeparam name="TEntity">The entity type this repository manages</typeparam>
[Obsolete("DKNet.EfCore.Repos.Abstractions is retired. Use DKNet.EfCore.Specifications (IRepositorySpec + SpecSetup) instead. See docs/EfCore/Migrating-Repos-To-Specifications.md.")]
#pragma warning disable CS0618 // base interfaces are the obsolete members being flagged here
public interface IRepository<TEntity> : IReadRepository<TEntity>, IWriteRepository<TEntity>
    where TEntity : class;
#pragma warning restore CS0618