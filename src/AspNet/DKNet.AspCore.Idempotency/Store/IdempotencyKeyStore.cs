namespace DKNet.AspCore.Idempotency.Store;

/// <summary>
///     The cache store interface for managing idempotency keys and their cached responses.
///     Handles serialization/deserialization of cached HTTP responses for idempotent request replay.
/// </summary>
public interface IIdempotencyKeyStore
{
    /// <summary>
    ///     Atomically checks whether the key has been processed and, if not, reserves it for the caller.
    ///     A call that returns <c>(false, null)</c> MUST have already durably recorded that this composite key
    ///     is now in-flight, such that a concurrent call for the identical key can never also observe
    ///     <c>(false, null)</c> — exactly one caller per key is granted the right to proceed.
    /// </summary>
    /// <param name="keyInfo">The idempotency key to check for prior processing.</param>
    /// <returns>
    ///     A tuple containing:
    ///     - A boolean indicating whether the key has been processed or is currently reserved by another caller
    ///     - The CachedResponse if the key was already completed, or null if no cached response exists yet
    ///       (either the key is new and now reserved by this call, or another caller's reservation is still in-flight)
    /// </returns>
    ValueTask<(bool processed, CachedResponse? response)> IsKeyProcessedAsync(IdempotentKeyInfo keyInfo);

    /// <summary>
    ///     Marks the key as processed and caches the complete HTTP response.
    ///     The response includes status code, body, content type, and expiration metadata.
    /// </summary>
    /// <param name="keyInfo">The idempotency key to mark as processed.</param>
    /// <param name="cachedResponse">The complete cached response to store, including status code and body.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    ValueTask MarkKeyAsProcessedAsync(IdempotentKeyInfo keyInfo, CachedResponse cachedResponse);
}