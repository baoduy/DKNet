// <copyright file="IIdempotencyKeyRepository.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace DKNet.AspCore.Idempotency.Stores;

/// <summary>
///     Defines the contract for idempotency response storage and retrieval.
/// </summary>
public interface IIdempotencyKeyRepository
{
    /// <summary>
    ///     Retrieves a cached response for the given idempotency key.
    /// </summary>
    /// <param name="key">The validated idempotency key.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the cached response if found
    ///     and not expired; otherwise, <c>null</c>.
    /// </returns>
    Task<CachedResponse?> GetAsync(IdempotencyKey key, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Stores a response for the given idempotency key.
    /// </summary>
    /// <param name="key">The validated idempotency key.</param>
    /// <param name="response">The response to cache.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task SetAsync(IdempotencyKey key, CachedResponse response, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Attempts to acquire a distributed lock for the given idempotency key.
    /// </summary>
    /// <param name="key">The validated idempotency key.</param>
    /// <param name="timeout">The maximum time to wait for the lock.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result is <c>true</c> if the lock was
    ///     successfully acquired; otherwise, <c>false</c>.
    /// </returns>
    Task<bool> TryAcquireLockAsync(IdempotencyKey key, TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Releases a previously acquired lock.
    /// </summary>
    /// <param name="key">The validated idempotency key.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ReleaseLockAsync(IdempotencyKey key, CancellationToken cancellationToken = default);
}