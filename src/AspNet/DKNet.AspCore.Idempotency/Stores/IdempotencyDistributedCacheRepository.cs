// <copyright file="IdempotencyDistributedCacheRepository.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DKNet.AspCore.Idempotency.Stores;

/// <summary>
///     Distributed cache-based implementation of <see cref="IIdempotencyKeyRepository" />.
///     Uses <see cref="IDistributedCache" /> for storage (Redis, SQL Server, etc.).
/// </summary>
internal sealed class IdempotencyDistributedCacheRepository : IIdempotencyKeyRepository
{
    #region Fields

    private readonly IDistributedCache _cache;
    private readonly ILogger<IdempotencyDistributedCacheRepository> _logger;
    private readonly IdempotencyOptions _options;

    #endregion

    #region Constructors

    /// <summary>
    ///     Initializes a new instance of the <see cref="IdempotencyDistributedCacheRepository" /> class.
    /// </summary>
    /// <param name="cache">The distributed cache implementation.</param>
    /// <param name="options">The idempotency options.</param>
    /// <param name="logger">The logger.</param>
    public IdempotencyDistributedCacheRepository(
        IDistributedCache cache,
        IOptions<IdempotencyOptions> options,
        ILogger<IdempotencyDistributedCacheRepository> logger)
    {
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    #endregion

    #region Methods

    /// <summary>
    ///     Builds a sanitized cache key from the idempotency key.
    /// </summary>
    private string BuildCacheKey(IdempotencyKey key)
    {
        var sanitized = key.Value
            .Replace("/", "_", StringComparison.OrdinalIgnoreCase)
            .Replace("\n", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("\r", string.Empty, StringComparison.OrdinalIgnoreCase);

        return $"{_options.CachePrefix}:{sanitized}".ToLowerInvariant();
    }

    /// <summary>
    ///     Builds a distributed lock key from the idempotency key.
    /// </summary>
    private string BuildLockKey(IdempotencyKey key)
    {
        var sanitized = key.Value
            .Replace("/", "_", StringComparison.OrdinalIgnoreCase)
            .Replace("\n", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("\r", string.Empty, StringComparison.OrdinalIgnoreCase);

        return $"{IdempotencyConstants.LockKeyPrefix}{_options.CachePrefix}:{sanitized}".ToLowerInvariant();
    }

    /// <inheritdoc />
    public async Task<CachedResponse?> GetAsync(IdempotencyKey key, CancellationToken cancellationToken = default)
    {
        var cacheKey = BuildCacheKey(key);
        _logger.LogDebug("Retrieving cached response for key: {Key}", key.Value);


        var json = await _cache.GetStringAsync(cacheKey, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrEmpty(json))
        {
            _logger.LogDebug("No cached response found for key: {Key}", key.Value);
            return null;
        }

        var response = JsonSerializer.Deserialize<CachedResponse>(json, _options.JsonSerializerOptions);

        if (response?.IsExpired == true)
        {
            _logger.LogDebug("Cached response expired for key: {Key}", key.Value);
            await _cache.RemoveAsync(cacheKey, cancellationToken).ConfigureAwait(false);
            return null;
        }

        _logger.LogDebug("Cached response found for key: {Key}", key.Value);
        return response;
    }

    /// <inheritdoc />
    public async Task ReleaseLockAsync(IdempotencyKey key, CancellationToken cancellationToken = default)
    {
        var lockKey = BuildLockKey(key);
        _logger.LogDebug("Releasing lock for key: {Key}", key.Value);


        await _cache.RemoveAsync(lockKey, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("Lock released for key: {Key}", key.Value);
    }

    /// <inheritdoc />
    public async Task SetAsync(IdempotencyKey key, CachedResponse response,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);

        var cacheKey = BuildCacheKey(key);
        _logger.LogDebug("Caching response for key: {Key}", key.Value);

        try
        {
            var json = JsonSerializer.Serialize(response, _options.JsonSerializerOptions);
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _options.Expiration
            };

            await _cache.SetStringAsync(cacheKey, json, cacheOptions, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation("Response cached for key: {Key}", key.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching response for key: {Key}", key.Value);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> TryAcquireLockAsync(
        IdempotencyKey key,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var lockKey = BuildLockKey(key);
        var lockValue = Guid.NewGuid().ToString();
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)

        {
            // Try to set the lock (this is a simple implementation)
            // Note: IDistributedCache doesn't support NX (only if not exists)
            // For production, consider using StackExchange.Redis directly for atomic operations
            var existing = await _cache.GetStringAsync(lockKey, cancellationToken)
                .ConfigureAwait(false);

            if (string.IsNullOrEmpty(existing))
            {
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = timeout
                };

                await _cache.SetStringAsync(lockKey, lockValue, cacheOptions, cancellationToken)
                    .ConfigureAwait(false);

                _logger.LogDebug("Lock acquired for key: {Key}", key.Value);
                return true;
            }

            // Wait a bit before retrying
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }


        _logger.LogWarning("Failed to acquire lock for key: {Key} within timeout", key.Value);
        return false;
    }

    #endregion
}