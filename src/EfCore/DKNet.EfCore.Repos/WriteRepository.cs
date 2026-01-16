// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: WriteRepository.cs
// Description: Write-capable repository implementation providing add/update/delete and transaction helpers.

using DKNet.EfCore.Extensions.Extensions;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;

namespace DKNet.EfCore.Repos;

/// <summary>
///     A write-capable repository that provides common EF Core write operations for <typeparamref name="TEntity" />.
///     This implementation uses a provided <see cref="DbContext" /> instance to perform operations.
/// </summary>
/// <typeparam name="TEntity">Entity CLR type.</typeparam>
public class WriteRepository<TEntity>(DbContext dbContext, IServiceProvider? provider = null)
    : IWriteRepository<TEntity>
    where TEntity : class
{
    #region Methods

    /// <inheritdoc />
    public virtual async ValueTask AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        => await dbContext.AddAsync(entity, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public virtual async ValueTask AddRangeAsync(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default) =>
        await dbContext.AddRangeAsync(entities, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => dbContext.Database.BeginTransactionAsync(cancellationToken);

    /// <inheritdoc />
    public virtual void Delete(TEntity entity) => dbContext.Set<TEntity>().Remove(entity);

    /// <inheritdoc />
    public virtual void DeleteRange(IEnumerable<TEntity> entities) => dbContext.Set<TEntity>().RemoveRange(entities);

    /// <inheritdoc />
    public EntityEntry<TEntity> Entry(TEntity entity) => dbContext.Entry(entity);

    /// <inheritdoc />
    public virtual async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.AddNewEntitiesFromNavigations(cancellationToken).ConfigureAwait(false);
        var handler = provider?.GetKeyedService<IEfCoreExceptionHandler>(dbContext.GetType().FullName);
        return await dbContext.SaveChangesWithConcurrencyHandlingAsync(handler, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task<int> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        dbContext.Entry(entity).State = EntityState.Modified;

        var newEntities = dbContext.GetNewEntitiesFromNavigations(dbContext.Entry(entity)).ToList();
        await dbContext.AddRangeAsync(newEntities, cancellationToken).ConfigureAwait(false);
        return newEntities.Count;
    }

    /// <inheritdoc />
    public async Task UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        foreach (var entity in entities) await UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    #endregion
}