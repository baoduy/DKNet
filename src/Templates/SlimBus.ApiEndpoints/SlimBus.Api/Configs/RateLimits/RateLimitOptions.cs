namespace SlimBus.Api.Configs.RateLimits;

/// <summary>
///     Configuration options for rate limiting
/// </summary>
internal sealed class RateLimitOptions
{
    public static string Name => "RateLimit";

    /// <summary>
    ///     Default number of requests allowed per time window
    /// </summary>
    public int DefaultRequestLimit { get; set; } = 2;

    /// <summary>
    ///    Default number of concurrent requests allowed
    /// </summary>
    public int DefaultConcurrentLimit { get; set; } = 2;

    /// <summary>
    ///     Time window for rate limiting in seconds
    /// </summary>
    public int TimeWindowInSeconds { get; set; } = 1;
}