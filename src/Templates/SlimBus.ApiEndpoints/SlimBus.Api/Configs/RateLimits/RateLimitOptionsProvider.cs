using System.Threading.RateLimiting;

namespace SlimBus.Api.Configs.RateLimits;

public interface IRateLimitOptionsProvider
{
    #region Methods

    public ConcurrencyLimiterOptions GetConcurrencyLimiterOptions();
    public FixedWindowRateLimiterOptions GetRateLimiterOptions();

    #endregion
}

internal sealed class RateLimitOptionsProvider(IOptions<RateLimitOptions> options)
    : IRateLimitOptionsProvider
{
    #region Fields

    private readonly RateLimitOptions _option = options.Value;

    #endregion

    #region Methods

    public ConcurrencyLimiterOptions GetConcurrencyLimiterOptions() =>
        new()
        {
            PermitLimit = _option.DefaultConcurrentLimit,
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        };

    public FixedWindowRateLimiterOptions GetRateLimiterOptions() =>
        new()
        {
            AutoReplenishment = true,
            PermitLimit = _option.DefaultRequestLimit,
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            Window = TimeSpan.FromSeconds(_option.TimeWindowInSeconds)
        };

    #endregion
}