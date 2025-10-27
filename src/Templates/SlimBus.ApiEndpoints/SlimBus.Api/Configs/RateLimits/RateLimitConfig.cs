using System.Threading.RateLimiting;

namespace SlimBus.Api.Configs.RateLimits;

/// <summary>
///     Rate limiting configuration for SlimBus API
/// </summary>
[ExcludeFromCodeCoverage]
internal static class RateLimitConfig
{
    #region Fields

    private static bool _configAdded;

    #endregion

    #region Methods

    /// <summary>
    ///     Adds rate limiting services to the service collection
    /// </summary>
    /// <param name="services">The service collection to configure</param>
    /// <param name="configuration">configuration action for rate limit options</param>
    /// <returns>The service collection with rate limiting configured</returns>
    public static IServiceCollection AddRateLimitConfig(this IServiceCollection services, IConfiguration configuration)
    {
        _configAdded = false;

        services.Configure<RateLimitOptions>(configuration.GetSection(RateLimitOptions.Name));
        services.AddSingleton<IRateLimitKeyProvider, RateLimitKeyProvider>();

        // You will implement ISubscriptionRateLimitResolver and register it
        services.AddScoped<IRateLimitOptionsProvider, RateLimitOptionsProvider>();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                {
                    var keyProvider = httpContext.RequestServices.GetRequiredService<IRateLimitKeyProvider>();
                    var resolver = httpContext.RequestServices.GetRequiredService<IRateLimitOptionsProvider>();

                    return RateLimitPartition.GetFixedWindowLimiter(keyProvider.GetPartitionKey(httpContext),
                        _ => resolver.GetRateLimiterOptions());
                }), PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                {
                    var keyProvider = httpContext.RequestServices.GetRequiredService<IRateLimitKeyProvider>();
                    var resolver = httpContext.RequestServices.GetRequiredService<IRateLimitOptionsProvider>();
                    return RateLimitPartition.GetConcurrencyLimiter(keyProvider.GetPartitionKey(httpContext),
                        _ => resolver.GetConcurrencyLimiterOptions());
                })
            );
        });

        _configAdded = true;
        Console.WriteLine("Rate Limiting enabled.");
        return services;
    }

    /// <summary>
    ///     Applies rate limiting middleware to the application
    /// </summary>
    /// <param name="app">The web application to configure</param>
    /// <returns>The web application with rate limiting applied</returns>
    public static WebApplication UseRateLimitConfig(this WebApplication app)
    {
        if (!_configAdded) return app;
        app.UseRateLimiter();
        return app;
    }

    #endregion
}