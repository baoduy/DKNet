using System.Text.Json;

namespace DKNet.AspCore.Idempotency;

/// <summary>
///     Defines how the idempotency filter handles conflicts when a request with the same idempotency key is received.
/// </summary>
public enum IdempotentConflictHandling
{
    /// <summary>
    ///     Returns the cached result from the previous request to the client.
    ///     Use this when the client expects the same response as the original request.
    /// </summary>
    CachedResult,

    /// <summary>
    ///     Returns an HTTP 409 Conflict response to the client.
    ///     Use this when the client should be explicitly notified that the request has already been processed.
    /// </summary>
    ConflictResponse
}

/// <summary>
///     Options for configuring idempotency behavior in ASP.NET Core endpoints.
///     These options control how idempotency keys are validated, cached, and how conflicts are handled.
/// </summary>
public sealed class IdempotencyOptions
{
    /// <summary>
    ///     Gets or sets the HTTP header name used to identify idempotency keys.
    ///     Default is "X-Idempotency-Key", following the idempotency specification.
    /// </summary>
    public string IdempotencyHeaderKey { get; set; } = "X-Idempotency-Key";

    /// <summary>
    ///     Gets or sets the prefix to prepend to all idempotency keys when storing them in the distributed cache.
    ///     This helps namespace idempotency keys to avoid conflicts with other cached data.
    ///     Default is "idem".
    /// </summary>
    public string CachePrefix { get; set; } = "idem";

    /// <summary>
    ///     Gets or sets the absolute expiration time for cached idempotency results.
    ///     Once this timespan has elapsed since the result was cached, the idempotency key is considered expired
    ///     and subsequent requests with the same key will be processed as new requests.
    ///     Default is 4 hours.
    /// </summary>
    public TimeSpan Expiration { get; set; } = TimeSpan.FromHours(4);

    /// <summary>
    ///     Gets or sets the JSON serializer options used to serialize response bodies before caching them.
    ///     This is used when the conflict handling strategy is set to return cached results.
    ///     Default uses camel case naming policy for consistency with typical JSON APIs.
    /// </summary>
    public JsonSerializerOptions JsonSerializerOptions { get; set; } =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>
    ///     Gets or sets how the idempotency filter handles requests with duplicate idempotency keys.
    ///     Default is <see cref="IdempotentConflictHandling.ConflictResponse"/> to explicitly notify clients
    ///     that the request has already been processed.
    /// </summary>
    public IdempotentConflictHandling ConflictHandling { get; set; } = IdempotentConflictHandling.ConflictResponse;
}