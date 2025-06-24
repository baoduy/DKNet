namespace SlimBus.Api.Configs.RateLimits;

/// <summary>
/// Configuration options for rate limiting
/// </summary>
public class RateLimitOptions
{
    /// <summary>
    /// Default number of requests allowed per time window
    /// </summary>
    public int DefaultRequestLimit { get; set; } = 2;

    /// <summary>
    /// Time window for rate limiting in seconds
    /// </summary>
    public int TimeWindowInSeconds { get; set; } = 1;

    /// <summary>
    /// Maximum number of queued requests when rate limit is reached
    /// </summary>
    public int QueueLimit { get; set; } = 0;

    /// <summary>
    /// Queue processing order
    /// </summary>
    public RateLimitQueueProcessingOrder QueueProcessingOrder { get; set; } = RateLimitQueueProcessingOrder.OldestFirst;
}

/// <summary>
/// Defines the order in which queued requests are processed
/// </summary>
public enum RateLimitQueueProcessingOrder
{
    OldestFirst,
    NewestFirst
}