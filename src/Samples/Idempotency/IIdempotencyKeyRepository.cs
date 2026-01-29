using Microsoft.Extensions.Caching.Distributed;

namespace Mx.Pgw.Api.Configs.Idempotency;

public interface IIdempotencyKeyRepository
{
    /// <summary>
    ///     Checks if the key has been processed. If it has, returns true and the result if provided when marking the key as
    ///     processed.
    /// </summary>
    /// <param name="idempotencyKey"></param>
    /// <returns></returns>
    ValueTask<(bool processed, string? result)> IsKeyProcessedAsync(string idempotencyKey);

    /// <summary>
    ///     Marks the key as processed and caches the result.
    ///     The result can be null. If the result is null, the key will be marked as processed but no result will be cached.
    ///     This is useful for idempotent commands that do not return a result.
    /// </summary>
    /// <param name="idempotencyKey"></param>
    /// <param name="result">The result can be null.</param>
    /// <returns></returns>
    ValueTask MarkKeyAsProcessedAsync(string idempotencyKey, string? result = null);
}

internal class IdempotencyDistributedCacheRepository(
    IDistributedCache cache,
    IOptions<IdempotencyOptions> options,
    ILogger<IdempotencyEndpointFilter> logger) : IIdempotencyKeyRepository
{
    private readonly IdempotencyOptions _options = options.Value;

    public async ValueTask<(bool processed, string? result)> IsKeyProcessedAsync(string idempotencyKey)
    {
        var cacheKey = SanitizeKey(idempotencyKey);
        logger.LogDebug("Trying to get existing result for cache key: {CacheKey}", cacheKey);

        var result = await cache.GetStringAsync(cacheKey).ConfigureAwait(false);

        logger.LogDebug("Existing result found: {Result}", result);
        return (!string.IsNullOrWhiteSpace(result), result);
    }

    public async ValueTask MarkKeyAsProcessedAsync(string idempotencyKey, string? result = null)
    {
        var cacheKey = SanitizeKey(idempotencyKey);
        logger.LogDebug("Setting cache result for cache key: {CacheKey}", cacheKey);

        await cache.SetStringAsync(cacheKey, string.IsNullOrWhiteSpace(result) ? bool.TrueString : result,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _options.Expiration
            }).ConfigureAwait(false);
    }

    private string SanitizeKey(string key)
    {
        var k = key
            .Replace("/", "_", StringComparison.OrdinalIgnoreCase)
            .Replace("\n", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("\r", string.Empty, StringComparison.OrdinalIgnoreCase); // Sanitize user input

        return $"{_options.CachePrefix}{k}".ToUpperInvariant();
    }
}