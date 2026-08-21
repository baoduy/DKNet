// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: RepositoryFactory.cs
// Description: Factory that creates repository instances (read/write) for a given DbContext type.

namespace DKNet.EfCore.Repos.Repositories;

/// <summary>
///     Factory responsible for creating repository instances for a specific <typeparamref name="TDbContext" />.
///     The factory owns a DbContext instance created from the provided <see cref="IDbContextFactory{TDbContext}" />
///     and will dispose it when the factory is disposed.
/// </summary>
/// <typeparam name="TDbContext">The DbContext type used by repositories created by this factory.</typeparam>
[Obsolete("DKNet.EfCore.Repos is retired. Use DKNet.EfCore.Specifications (IRepositorySpec + SpecSetup) instead. See docs/EfCore/Migrating-Repos-To-Specifications.md.")]
#pragma warning disable CS0618 // implemented interface and returned repository types are the obsolete members being flagged here
public sealed class RepositoryFactory<TDbContext>(
    IDbContextFactory<TDbContext> dbFactory,
    IServiceProvider provider) : IRepositoryFactory
    where TDbContext : DbContext
{
    #region Fields

    private readonly TDbContext _db = dbFactory.CreateDbContext();
    private readonly IMapper? _mapper = provider.GetService(typeof(IMapper)) as IMapper;

    #endregion

    #region Methods

    /// <summary>
    ///     Creates a read-write repository for <typeparamref name="TEntity" />.
    /// </summary>
    /// <typeparam name="TEntity">The entity type for the repository.</typeparam>
    /// <returns>A new <see cref="IRepository{TEntity}" /> instance.</returns>
    public IRepository<TEntity> Create<TEntity>() where TEntity : class =>
        new Repository<TEntity>(_db, provider);

    /// <summary>
    ///     Creates a read-only repository for <typeparamref name="TEntity" />.
    /// </summary>
    /// <typeparam name="TEntity">The entity type for the repository.</typeparam>
    /// <returns>A new <see cref="IReadRepository{TEntity}" /> instance.</returns>
    public IReadRepository<TEntity> CreateRead<TEntity>() where TEntity : class =>
        new ReadRepository<TEntity>(_db, _mapper is not null ? [_mapper] : []);

    /// <summary>
    ///     Creates a write-capable repository for <typeparamref name="TEntity" />.
    /// </summary>
    /// <typeparam name="TEntity">The entity type for the repository.</typeparam>
    /// <returns>A new <see cref="IWriteRepository{TEntity}" /> instance.</returns>
    public IWriteRepository<TEntity> CreateWrite<TEntity>() where TEntity : class =>
        new WriteRepository<TEntity>(_db, provider);

    /// <summary>
    ///     Disposes the created DbContext instance held by the factory.
    /// </summary>
    public void Dispose() => _db.Dispose();

    /// <summary>
    ///     Asynchronously disposes the created DbContext instance held by the factory.
    /// </summary>
    public ValueTask DisposeAsync() => _db.DisposeAsync();

    #endregion
}
#pragma warning restore CS0618