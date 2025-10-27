namespace SlimBus.Api.Configs.RateLimits;

public interface IRateLimitKeyProvider
{
    #region Methods

    public string GetPartitionKey(HttpContext context);

    #endregion
}

/// <summary>
///     Provides rate limiting policies based on client IP or JWT user identity
/// </summary>
internal sealed class RateLimitKeyProvider : IRateLimitKeyProvider
{
    #region Methods

    /// <summary>
    ///     Gets the partition key for rate limiting based on authorization header or IP address
    /// </summary>
    public string GetPartitionKey(HttpContext context) =>
        context.User.Identity?.Name ??
        context.Connection.RemoteIpAddress?.ToString() ?? context.Request.Host.Host;

    #endregion
}