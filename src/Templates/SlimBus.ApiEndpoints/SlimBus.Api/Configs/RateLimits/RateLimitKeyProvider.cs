namespace SlimBus.Api.Configs.RateLimits;

public interface IRateLimitKeyProvider
{
    public string GetPartitionKey(HttpContext context);
}

/// <summary>
///     Provides rate limiting policies based on client IP or JWT user identity
/// </summary>
internal sealed class RateLimitKeyProvider : IRateLimitKeyProvider
{
    /// <summary>
    ///     Gets the partition key for rate limiting based on authorization header or IP address
    /// </summary>
    public string GetPartitionKey(HttpContext context) =>
        context.User.Identity?.Name ??
        context.Connection.RemoteIpAddress?.ToString() ?? context.Request.Host.Host;
}