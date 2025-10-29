#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: IWriteRepository.cs
// Description: Abstractions for write-capable EF Core repositories (add/update/delete/save/transactions).

using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;

namespace DKNet.EfCore.Repos.Abstractions;

/// <summary>
///     Repository interface that exposes write operations for entity types.
///     Implementations should provide add/update/delete semantics and transaction support for EF Core.
/// </summary>
/// <typeparam name="TEntity">The entity type the repository manages.</typeparam>
public interface IWriteRepository<TEntity>
    where TEntity : class
{
    #region Methods

    /// <summary>
    ///     Adds a new entity to the repository asynchronously.
    ///     The entity will be tracked by the underlying DbContext and persisted when <see cref="SaveChangesAsync" /> is
    ///     called.
    /// </summary>
    /// <param name="entity">The entity instance to add.</param>
    /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask" /> that completes when the add operation has been scheduled.</returns>
    ValueTask AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Adds a range of entities to the repository asynchronously.
    ///     Entities will be tracked by the underlying DbContext and persisted when <see cref="SaveChangesAsync" /> is called.
    /// </summary>
    /// <param name="entities">The collection of entities to add.</param>
    /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask" /> that completes when the add-range operation has been scheduled.</returns>
    ValueTask AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Begins a new database transaction on the underlying DbContext.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
    /// <returns>An <see cref="IDbContextTransaction" /> representing the started transaction.</returns>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes the specified entity from the repository.
    ///     The deletion will be persisted when <see cref="SaveChangesAsync" /> is called.
    /// </summary>
    /// <param name="entity">The entity instance to remove.</param>
    void Delete(TEntity entity);

    /// <summary>
    ///     Deletes the provided range of entities from the repository.
    ///     The deletions will be persisted when <see cref="SaveChangesAsync" /> is called.
    /// </summary>
    /// <param name="entities">The collection of entities to remove.</param>
    void DeleteRange(IEnumerable<TEntity> entities);

    /// <summary>
    ///     Returns the EntityEntry for the supplied entity so callers can access/change tracking information.
    /// </summary>
    /// <param name="entity">The entity to obtain an entry for.</param>
    /// <returns>An <see cref="EntityEntry{TEntity}" /> for the entity.</returns>
    EntityEntry<TEntity> Entry(TEntity entity);

    /// <summary>
    ///     Persists all pending changes on the underlying DbContext to the database asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
    /// <returns>The number of state entries written to the database.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates the supplied entity asynchronously. Implementations may choose how to apply changes to tracked
    ///     or detached entities and should return the number of affected rows where applicable.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
    /// <returns>An integer result (for example number of affected rows) or implementation-specific value.</returns>
    Task<int> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates a range of entities asynchronously. Implementations should apply updates to each entity in the collection.
    /// </summary>
    /// <param name="entities">The collection of entities to update.</param>
    /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
    /// <returns>A task that completes when the update-range operation has finished.</returns>
    Task UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    #endregion
}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member