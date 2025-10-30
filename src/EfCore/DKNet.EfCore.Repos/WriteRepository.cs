// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: WriteRepository.cs
// Description: Write-capable repository implementation providing add/update/delete and transaction helpers.

using DKNet.EfCore.Extensions.Extensions;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace DKNet.EfCore.Repos;

/// <summary>
///     A write-capable repository that provides common EF Core write operations for <typeparamref name="TEntity" />.
///     This implementation uses a provided <see cref="DbContext" /> instance to perform operations.
/// </summary>
/// <typeparam name="TEntity">Entity CLR type.</typeparam>
public class WriteRepository<TEntity> : IWriteRepository<TEntity>
    where TEntity : class
{
    #region Fields

    private readonly DbContext _dbContext;

    #endregion

    #region Constructors

    /// <summary>
    ///     Initializes a new instance of the <see cref="WriteRepository{TEntity}" /> class.
    /// </summary>
    /// <param name="dbContext">The <see cref="DbContext" /> used to run write operations. Must not be null.</param>
    public WriteRepository(DbContext dbContext) =>
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    #endregion

    #region Methods

    /// <inheritdoc />
    public virtual async ValueTask AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        => await _dbContext.AddAsync(entity, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public virtual async ValueTask AddRangeAsync(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default) =>
        await _dbContext.AddRangeAsync(entities, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => _dbContext.Database.BeginTransactionAsync(cancellationToken);

    /// <inheritdoc />
    public virtual void Delete(TEntity entity) => _dbContext.Set<TEntity>().Remove(entity);

    /// <inheritdoc />
    public virtual void DeleteRange(IEnumerable<TEntity> entities) => _dbContext.Set<TEntity>().RemoveRange(entities);

    /// <inheritdoc />
    public EntityEntry<TEntity> Entry(TEntity entity) => _dbContext.Entry(entity);

    /// <inheritdoc />
    public virtual async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.AddNewEntitiesFromNavigations(cancellationToken).ConfigureAwait(false);
        return await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task<int> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        _dbContext.Entry(entity).State = EntityState.Modified;

        var newEntities = _dbContext.GetNewEntitiesFromNavigations(_dbContext.Entry(entity)).ToList();
        await _dbContext.AddRangeAsync(newEntities, cancellationToken).ConfigureAwait(false);
        return newEntities.Count;
    }

    /// <inheritdoc />
    public async Task UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        foreach (var entity in entities) await UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    #endregion
}