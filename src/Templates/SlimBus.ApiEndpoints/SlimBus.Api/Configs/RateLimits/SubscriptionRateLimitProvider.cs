using System.Threading.RateLimiting;
using Microsoft.Extensions.Caching.Hybrid;

namespace SlimBus.Api.Configs.RateLimits;

public interface ISubscriptionRateLimitProvider
{
    public ValueTask<FixedWindowRateLimiterOptions> GetRateLimiterOptionsAsync(string key);
}

internal sealed class SubscriptionRateLimitProvider(HybridCache cache, IOptions<RateLimitOptions> options)
    : ISubscriptionRateLimitProvider
{
    private readonly RateLimitOptions _option = options.Value;

    public ValueTask<FixedWindowRateLimiterOptions> GetRateLimiterOptionsAsync(string key)
    {
        return cache.GetOrCreateAsync(key, _ =>
        {
            var option = new FixedWindowRateLimiterOptions();
            // Configure rate limit options from defaults (database loading can be implemented here)
            option.PermitLimit = _option.DefaultRequestLimit;
            option.QueueLimit = _option.QueueLimit;
            option.QueueProcessingOrder = _option.QueueProcessingOrder;
            option.Window = TimeSpan.FromSeconds(_option.TimeWindowInSeconds);
            return ValueTask.FromResult(option);
        });
    }
}