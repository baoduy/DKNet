using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DKNet.AspCore.Idempotency.Store;

internal class IdempotencyDistributedCacheStore(
    IDistributedCache cache,
    IOptions<IdempotencyOptions> options,
    ILogger<IdempotencyEndpointFilter> logger) : IIdempotencyKeyStore
{
    #region Fields

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
    /// <param name="idempotencyKey">The idempotency key to check in the distributed cache.</param>
    /// <returns>
    ///     A tuple containing:
    ///     - A boolean indicating whether the key exists in the cache (has been processed)
    ///     - The CachedResponse if available, or null if no response was cached
    /// </returns>
    public async ValueTask<(bool processed, CachedResponse? response)> IsKeyProcessedAsync(string idempotencyKey)
    {
        var cacheKey = SanitizeKey(idempotencyKey);
        logger.LogDebug("Trying to get existing response for cache key: {CacheKey}", cacheKey);

        var cachedJson = await cache.GetStringAsync(cacheKey).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(cachedJson))
        {
            logger.LogDebug("No cached response found for key: {CacheKey}", cacheKey);
            return (false, null);
        }

        try
        {
            var cachedResponse = JsonSerializer.Deserialize<CachedResponse>(cachedJson, _options.JsonSerializerOptions);

            // Check if the cached response has expired
            if (cachedResponse?.IsExpired == true)
            {
                logger.LogDebug("Cached response has expired for key: {CacheKey}", cacheKey);
                await cache.RemoveAsync(cacheKey).ConfigureAwait(false);
                return (false, null);
            }

            logger.LogDebug("Cached response found for key: {CacheKey} with status code: {StatusCode}",
                cacheKey, cachedResponse?.StatusCode);
            return (true, cachedResponse);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to deserialize cached response for key: {CacheKey}", cacheKey);
            return (false, null);
        }
    }

    /// <summary>
    ///     Marks an idempotency key as processed and caches the complete HTTP response.
    ///     Serializes the response (including status code, body, and content type) to JSON for distributed caching.
    /// </summary>
    /// <param name="idempotencyKey">The idempotency key to mark as processed.</param>
    /// <param name="cachedResponse">
    ///     The complete cached response containing status code, body, content type, and expiration info.
    /// </param>
    /// <returns>A task representing the asynchronous cache operation.</returns>
    public async ValueTask MarkKeyAsProcessedAsync(string idempotencyKey, CachedResponse cachedResponse)
    {
        var cacheKey = SanitizeKey(idempotencyKey);
        logger.LogDebug("Setting cached response for cache key: {CacheKey} with status code: {StatusCode}",
            cacheKey, cachedResponse.StatusCode);

        try
        {
            var json = JsonSerializer.Serialize(cachedResponse, _options.JsonSerializerOptions);

            await cache.SetStringAsync(cacheKey, json,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = _options.Expiration
                }).ConfigureAwait(false);

            logger.LogInformation("Response cached for key: {CacheKey} with status code: {StatusCode}",
                cacheKey, cachedResponse.StatusCode);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to serialize cached response for key: {CacheKey}", cacheKey);
            throw;
        }
    }

    /// <summary>
    ///     Sanitizes an idempotency key for use as a cache key by removing or replacing invalid characters.
    ///     This prevents cache key injection attacks by normalizing the input.
    ///     The sanitized key is prefixed with the configured cache prefix and converted to uppercase.
    /// </summary>
    /// <param name="key">The idempotency key to sanitize.</param>
    /// <returns>
    ///     A sanitized cache key with invalid characters removed and the configured prefix prepended,
    ///     converted to uppercase for consistency.
    /// </returns>
    private string SanitizeKey(string key)
    {
        var k = key
            .Replace("/", "_", StringComparison.OrdinalIgnoreCase)
            .Replace("\n", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("\r", string.Empty, StringComparison.OrdinalIgnoreCase); // Sanitize user input

        return $"{_options.CachePrefix}{k}".ToUpperInvariant();
    }

    #endregion
}