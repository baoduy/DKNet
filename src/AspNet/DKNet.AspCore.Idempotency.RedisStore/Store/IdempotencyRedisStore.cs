// <copyright file="IdempotencyRedisStore.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DKNet.AspCore.Idempotency.Filtering;
using DKNet.AspCore.Idempotency.Store;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace DKNet.AspCore.Idempotency.RedisStore.Store;

/// <summary>
///     Redis implementation of <see cref="IIdempotencyKeyStore" /> using StackExchange.Redis.
///     Provides atomic key reservation via SET NX and response replay.
/// </summary>
internal sealed class IdempotencyRedisStore : IIdempotencyKeyStore
{
    #region Fields

    /// <summary>
    ///     HTTP 102 (Processing) is used as the sentinel status code for an in-flight reservation entry.
    /// </summary>
    private const int ReservationStatusCode = 102;

    private readonly IdempotencyOptions _options;
    private readonly IDatabase _database;
    private readonly ILogger<IdempotencyRedisStore> _logger;

    #endregion

    #region Constructors

    /// <summary>
    ///     Initializes a new instance of the <see cref="IdempotencyRedisStore" /> class.
    /// </summary>
    public IdempotencyRedisStore(
        IConnectionMultiplexer multiplexer,
        IOptions<IdempotencyOptions> options,
        ILogger<IdempotencyRedisStore> logger)
    {
        ArgumentNullException.ThrowIfNull(multiplexer);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _database = multiplexer.GetDatabase();
        _options = options.Value;
        _logger = logger;
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    public async ValueTask<(bool processed, CachedResponse? response)> IsKeyProcessedAsync(IdempotentKeyInfo keyInfo)
    {
        var cacheKey = SanitizeKey(keyInfo.CompositeKey);
        _logger.LogDebug("Checking if idempotency key has been processed: {Key}", cacheKey);

        var cachedJson = (string?)await _database.StringGetAsync(cacheKey).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(cachedJson))
        {
            var cachedResponse = JsonSerializer.Deserialize<CachedResponse>(cachedJson, _options.JsonSerializerOptions);

            if (cachedResponse?.IsExpired == true)
            {
                _logger.LogDebug("Cached response has expired for key: {Key}", cacheKey);
                await _database.KeyDeleteAsync(cacheKey).ConfigureAwait(false);
            }
            else if (cachedResponse?.StatusCode == ReservationStatusCode)
            {
                _logger.LogDebug("Idempotency key reservation still in-flight: {Key}", cacheKey);
                return (true, null);
            }
            else
            {
                _logger.LogInformation(
                    "Idempotency key found with status code {StatusCode}: {Key}",
                    cachedResponse?.StatusCode,
                    cacheKey);

                return (true, cachedResponse);
            }
        }

        _logger.LogDebug("Idempotency key not found or expired, reserving: {Key}", cacheKey);

        var reservation = new CachedResponse
        {
            StatusCode = ReservationStatusCode,
            Body = null,
            ContentType = string.Empty,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.Add(_options.InFlightReservationTimeout)
        };

        var reservationJson = JsonSerializer.Serialize(reservation, _options.JsonSerializerOptions);

        var reserved = await _database.StringSetAsync(
                cacheKey,
                reservationJson,
                _options.InFlightReservationTimeout,
                When.NotExists)
            .ConfigureAwait(false);

        if (reserved)
        {
            return (false, null);
        }

        // Another caller reserved the key first; re-read its value.
        _logger.LogInformation(
            "Idempotency key reservation collided with a concurrent request: {Key}. Re-checking status.",
            cacheKey);

        var concurrentJson = (string?)await _database.StringGetAsync(cacheKey).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(concurrentJson))
        {
            // The competing entry expired or was removed between the failed SET and the re-read.
            return (false, null);
        }

        var concurrentResponse = JsonSerializer.Deserialize<CachedResponse>(concurrentJson, _options.JsonSerializerOptions);

        if (concurrentResponse?.IsExpired == true)
        {
            _logger.LogDebug("Concurrent cached response has expired for key: {Key}", cacheKey);
            await _database.KeyDeleteAsync(cacheKey).ConfigureAwait(false);
            return (false, null);
        }

        if (concurrentResponse?.StatusCode == ReservationStatusCode)
        {
            return (true, null);
        }

        return (true, concurrentResponse);
    }

    /// <inheritdoc />
    public async ValueTask MarkKeyAsProcessedAsync(IdempotentKeyInfo keyInfo, CachedResponse cachedResponse)
    {
        var cacheKey = SanitizeKey(keyInfo.CompositeKey);

        _logger.LogDebug(
            "Marking idempotency key as processed with status code {StatusCode}: {Key}",
            cachedResponse.StatusCode,
            cacheKey);

        var json = JsonSerializer.Serialize(cachedResponse, _options.JsonSerializerOptions);

        await _database.StringSetAsync(
                cacheKey,
                json,
                _options.Expiration)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Successfully stored idempotency key with status code {StatusCode}: {Key}",
            cachedResponse.StatusCode,
            cacheKey);
    }

    /// <summary>
    ///     Sanitizes an idempotency key for use as a Redis key by hashing it.
    ///     Hashing guarantees structurally distinct composite keys never collapse onto the same Redis key.
    ///     The configured cache prefix is prepended unchanged.
    /// </summary>
    /// <param name="key">The idempotency key to sanitize.</param>
    /// <returns>
    ///     The configured cache prefix followed by a deterministic, fixed-length (64-character)
    ///     lowercase hex SHA-256 hash of the key.
    /// </returns>
    private string SanitizeKey(string key)
    {
        return $"{_options.CachePrefix}{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)))}";
    }

    #endregion
}
