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
    /// <remarks>
    ///     Reserves with a single <c>SET key value NX GET</c> command (<c>StringSetAndGetAsync</c> with
    ///     <see cref="When.NotExists" />) instead of a <c>GET</c> followed by a conditional <c>SET</c>: Redis
    ///     sets the key only when it is absent and, in the very same round trip, returns whatever value was already
    ///     there when it is not — atomically, with no window between the two previously separate commands.
    /// </remarks>
    public async ValueTask<(bool processed, CachedResponse? response)> IsKeyProcessedAsync(IdempotentKeyInfo keyInfo)
    {
        var cacheKey = SanitizeKey(keyInfo.CompositeKey);
        _logger.LogDebug("Checking if idempotency key has been processed: {Key}", cacheKey);

        var reservation = new CachedResponse
        {
            StatusCode = ReservationStatusCode,
            Body = null,
            ContentType = string.Empty,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.Add(_options.InFlightReservationTimeout)
        };

        var reservationJson = JsonSerializer.Serialize(reservation, _options.JsonSerializerOptions);

        var previous = await _database.StringSetAndGetAsync(
                cacheKey,
                reservationJson,
                _options.InFlightReservationTimeout,
                when: When.NotExists)
            .ConfigureAwait(false);

        var previousJson = (string?)previous;

        if (string.IsNullOrWhiteSpace(previousJson))
        {
            // Nothing was there before this call - this call just reserved the key.
            return (false, null);
        }

        var existing = JsonSerializer.Deserialize<CachedResponse>(previousJson, _options.JsonSerializerOptions);

        if (existing?.IsExpired == true)
        {
            // Redis has not evicted the key by TTL yet, but the payload itself considers itself stale
            // (e.g. clock skew). Clear it and retry once so the retry's own SET NX GET reserves fresh -
            // recursing back through the same atomic path rather than reserving here without re-checking.
            _logger.LogDebug("Cached response has expired for key: {Key}", cacheKey);
            await _database.KeyDeleteAsync(cacheKey).ConfigureAwait(false);
            return await IsKeyProcessedAsync(keyInfo).ConfigureAwait(false);
        }

        if (existing?.StatusCode == ReservationStatusCode)
        {
            _logger.LogDebug("Idempotency key reservation still in-flight: {Key}", cacheKey);
            return (true, null);
        }

        _logger.LogInformation(
            "Idempotency key found with status code {StatusCode}: {Key}",
            existing?.StatusCode,
            cacheKey);

        return (true, existing);
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
