using System.Text.Json;

namespace SlimBus.Api.Configs.Idempotency;

/// <summary>
///     Options for idempotency handling.
/// </summary>
internal enum IdempotentConflictHandling
{
    /// <summary>
    ///     Returns the existing result to the client.
    /// </summary>
    CachedResult,

    /// <summary>
    ///     Returns a conflict response to the client.
    /// </summary>
    ConflictResponse
}

internal sealed class IdempotencyOptions
{
    #region Properties

    public IdempotentConflictHandling ConflictHandling { get; set; } = IdempotentConflictHandling.ConflictResponse;

    public JsonSerializerOptions JsonSerializerOptions { get; set; } =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public string CachePrefix { get; set; } = "idem-";

    public string IdempotencyHeaderKey { get; set; } = "X-Idempotency-Key";

    public TimeSpan Expiration { get; set; } = TimeSpan.FromHours(4);

    #endregion
}