using System.Threading.RateLimiting;

namespace SlimBus.Api.Configs.RateLimits;

public interface IRateLimitOptionsProvider
{
    public FixedWindowRateLimiterOptions GetRateLimiterOptions();
    public ConcurrencyLimiterOptions GetConcurrencyLimiterOptions();
}

internal sealed class RateLimitOptionsProvider(IOptions<RateLimitOptions> options)
    : IRateLimitOptionsProvider
{
    private readonly RateLimitOptions _option = options.Value;

    public FixedWindowRateLimiterOptions GetRateLimiterOptions() =>
        new()
        {
            AutoReplenishment = true,
            PermitLimit = _option.DefaultRequestLimit,
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            Window = TimeSpan.FromSeconds(_option.TimeWindowInSeconds)
        };

    public ConcurrencyLimiterOptions GetConcurrencyLimiterOptions() =>
        new()
        {
            PermitLimit = _option.DefaultConcurrentLimit,
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        };
}