#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: IRepositoryFactory.cs
// Description: Factory interface for creating repository instances (read/write) for EF Core entities.

namespace DKNet.EfCore.Repos.Abstractions;

/// <summary>
///     Factory responsible for creating repository instances for entity types.
///     The factory provides methods to obtain read-only and read-write repository interfaces
///     and supports synchronous disposal and asynchronous disposal patterns.
/// </summary>
public interface IRepositoryFactory : IDisposable, IAsyncDisposable
{
    #region Methods

    /// <summary>
    ///     Creates a repository instance for the specified entity type that supports both read and write operations.
    /// </summary>
    /// <typeparam name="TEntity">The entity type for which to create the repository.</typeparam>
    /// <returns>An <see cref="IRepository{TEntity}" /> instance for the specified entity type.</returns>
    IRepository<TEntity> Create<TEntity>() where TEntity : class;

    /// <summary>
    ///     Creates a read-only repository instance for the specified entity type.
    /// </summary>
    /// <typeparam name="TEntity">The entity type for which to create the read-only repository.</typeparam>
    /// <returns>An <see cref="IReadRepository{TEntity}" /> instance for the specified entity type.</returns>
    IReadRepository<TEntity> CreateRead<TEntity>() where TEntity : class;

    /// <summary>
    ///     Creates a write-capable repository instance for the specified entity type.
    /// </summary>
    /// <typeparam name="TEntity">The entity type for which to create the write repository.</typeparam>
    /// <returns>An <see cref="IWriteRepository{TEntity}" /> instance for the specified entity type.</returns>
    IWriteRepository<TEntity> CreateWrite<TEntity>() where TEntity : class;

    #endregion
}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member