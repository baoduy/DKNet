namespace DKNet.AspCore.Idempotency.Store;

/// <summary>
///     The cache store interface for managing idempotency keys and their cached responses.
///     Handles serialization/deserialization of cached HTTP responses for idempotent request replay.
/// </summary>
public interface IIdempotencyKeyStore
{
    /// <summary>
    ///     Checks if the key has been processed and retrieves the cached response if available.
    ///     Returns the cached response including HTTP status code, body, and content type.
    /// </summary>
    /// <param name="idempotencyKey">The idempotency key to check for prior processing.</param>
    /// <returns>
    ///     A tuple containing:
    ///     - A boolean indicating whether the key has been processed
    ///     - The CachedResponse if available, or null if no cached response exists
    /// </returns>
    ValueTask<(bool processed, CachedResponse? response)> IsKeyProcessedAsync(string idempotencyKey);

    /// <summary>
    ///     Marks the key as processed and caches the complete HTTP response.
    ///     The response includes status code, body, content type, and expiration metadata.
    /// </summary>
    /// <param name="idempotencyKey">The idempotency key to mark as processed.</param>
    /// <param name="cachedResponse">The complete cached response to store, including status code and body.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    ValueTask MarkKeyAsProcessedAsync(string idempotencyKey, CachedResponse cachedResponse);
}