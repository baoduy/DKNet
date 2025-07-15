using System.Threading.RateLimiting;

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
    ///     Time window for rate limiting in seconds
    /// </summary>
    public int TimeWindowInSeconds { get; set; } = 1;

    /// <summary>
    ///     Maximum number of queued requests when the rate limit is reached
    /// </summary>
    public int QueueLimit { get; set; }

    /// <summary>
    ///     Queue processing order
    /// </summary>
    public QueueProcessingOrder QueueProcessingOrder { get; set; } = QueueProcessingOrder.OldestFirst;
}