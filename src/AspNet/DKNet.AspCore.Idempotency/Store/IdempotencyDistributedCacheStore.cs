using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DKNet.AspCore.Idempotency.Store;

/// <summary>
///     Implementation of <see cref="IIdempotencyKeyStore" /> using a distributed cache.
///     This store provides idempotency support by caching processed keys and their responses.
/// </summary>
/// <remarks>
///     <see cref="IDistributedCache" /> has no atomic compare-and-set primitive, so this store narrows the
///     check-then-act race window to the gap between <c>GetStringAsync</c> and <c>SetStringAsync</c> in
///     <see cref="IsKeyProcessedAsync" /> rather than eliminating it the way the SQL store's unique index does.
///     Do not treat this store as fully atomic under concurrency.
/// </remarks>
internal sealed class IdempotencyDistributedCacheStore(
    IDistributedCache cache,
    IOptions<IdempotencyOptions> options,
    ILogger<IdempotencyEndpointFilter> logger) : IIdempotencyKeyStore
{
    #region Fields

    /// <summary>
    ///     HTTP 102 (Processing) is used as the sentinel status code for an in-flight reservation entry.
    /// </summary>
    private const int ReservationStatusCode = 102;

    /// <summary>
    ///     Gets the idempotency options used for cache configuration and JSON serialization.
    /// </summary>
    private readonly IdempotencyOptions _options = options.Value;

    #endregion

    #region Methods

    /// <summary>
    ///     Checks if the idempotency key has been processed and retrieves its cached response if available.
    ///     The cached response includes the HTTP status code, response body, and content type for accurate replay.
    /// </summary>
    /// <param name="keyInfo">The idempotency key to check in the distributed cache.</param>
    /// <returns>
    ///     A tuple containing:
    ///     - A boolean indicating whether the key exists in the cache (has been processed)
    ///     - The CachedResponse if available, or null if no response was cached
    /// </returns>
    public async ValueTask<(bool processed, CachedResponse? response)> IsKeyProcessedAsync(IdempotentKeyInfo keyInfo)
    {
        var cacheKey = SanitizeKey(keyInfo.CompositeKey);
        logger.LogDebug("Trying to get existing response for cache key: {CacheKey}", cacheKey);

        var cachedJson = await cache.GetStringAsync(cacheKey).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(cachedJson))
        {
            var cachedResponse = JsonSerializer.Deserialize<CachedResponse>(cachedJson, _options.JsonSerializerOptions);

            if (cachedResponse?.IsExpired == true)
            {
                logger.LogDebug("Cached response has expired for key: {CacheKey}", cacheKey);
                await cache.RemoveAsync(cacheKey).ConfigureAwait(false);
            }
            else if (cachedResponse?.StatusCode == ReservationStatusCode)
            {
                logger.LogDebug("Reservation still in-flight for key: {CacheKey}", cacheKey);
                return (true, null);
            }
            else
            {
                logger.LogDebug("Cached response found for key: {CacheKey} with status code: {StatusCode}",
                    cacheKey, cachedResponse?.StatusCode);
                return (true, cachedResponse);
            }
        }

        // No live entry found — reserve this key so a concurrent request for the identical key sees the
        // in-flight placeholder instead of also observing a miss (see class remarks for the residual race).
        logger.LogDebug("No cached response found for key: {CacheKey}. Reserving.", cacheKey);

        var reservation = new CachedResponse
        {
            StatusCode = ReservationStatusCode,
            Body = null,
            ContentType = string.Empty,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.Add(_options.InFlightReservationTimeout)
        };

        await cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(reservation, _options.JsonSerializerOptions),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _options.InFlightReservationTimeout
            }).ConfigureAwait(false);

        return (false, null);
    }

    /// <summary>
    ///     Marks an idempotency key as processed and caches the complete HTTP response.
    ///     Serializes the response (including status code, body, and content type) to JSON for distributed caching.
    /// </summary>
    /// <param name="keyInfo">The idempotency key to mark as processed.</param>
    /// <param name="cachedResponse">
    ///     The complete cached response containing status code, body, content type, and expiration info.
    /// </param>
    /// <returns>A task representing the asynchronous cache operation.</returns>
    public async ValueTask MarkKeyAsProcessedAsync(IdempotentKeyInfo keyInfo, CachedResponse cachedResponse)
    {
        var cacheKey = SanitizeKey(keyInfo.CompositeKey);
        logger.LogDebug("Setting cached response for cache key: {CacheKey} with status code: {StatusCode}",
            cacheKey, cachedResponse.StatusCode);

        var json = JsonSerializer.Serialize(cachedResponse, _options.JsonSerializerOptions);

        await cache.SetStringAsync(cacheKey, json,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _options.Expiration
            }).ConfigureAwait(false);

        logger.LogInformation("Response cached for key: {CacheKey} with status code: {StatusCode}",
            cacheKey, cachedResponse.StatusCode);
    }

    /// <summary>
    ///     Sanitizes an idempotency key for use as a cache key by hashing it.
    ///     Hashing (rather than replacing characters) guarantees structurally distinct
    ///     composite keys never collapse onto the same cache key. The configured cache
    ///     prefix is prepended unchanged.
    /// </summary>
    /// <param name="key">The idempotency key to sanitize.</param>
    /// <returns>
    ///     The configured cache prefix followed by a deterministic, fixed-length (64-character)
    ///     lowercase hex SHA-256 hash of the key.
    /// </returns>
    private string SanitizeKey(string key)
    {
        return $"{_options.CachePrefix}{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant()}";
    }

    #endregion
}