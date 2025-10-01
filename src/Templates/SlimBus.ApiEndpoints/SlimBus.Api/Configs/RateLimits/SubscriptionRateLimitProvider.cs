using System.Threading.RateLimiting;

namespace SlimBus.Api.Configs.RateLimits;

public interface ISubscriptionRateLimitProvider
{
    public FixedWindowRateLimiterOptions GetRateLimiterOptions();
    public ConcurrencyLimiterOptions GetConcurrencyLimiterOptions();
}

internal sealed class SubscriptionRateLimitProvider(IOptions<RateLimitOptions> options)
    : ISubscriptionRateLimitProvider
{
    private readonly RateLimitOptions _option = options.Value;

    public FixedWindowRateLimiterOptions GetRateLimiterOptions() =>
        new()
        {
            AutoReplenishment = true,
            PermitLimit = _option.DefaultRequestLimit,
            QueueLimit = _option.QueueLimit,
            QueueProcessingOrder = _option.QueueProcessingOrder,
            Window = TimeSpan.FromSeconds(_option.TimeWindowInSeconds)
        };

    public ConcurrencyLimiterOptions GetConcurrencyLimiterOptions() =>
        new()
        {
            PermitLimit = _option.DefaultConcurrentLimit,
            QueueLimit = 0,
            QueueProcessingOrder = _option.QueueProcessingOrder
        };
}