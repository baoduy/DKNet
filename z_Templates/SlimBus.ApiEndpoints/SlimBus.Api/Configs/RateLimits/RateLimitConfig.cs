using System.Threading.RateLimiting;

namespace SlimBus.Api.Configs.RateLimits;

/// <summary>
/// Rate limiting configuration for SlimBus API
/// </summary>
[ExcludeFromCodeCoverage]
internal static class RateLimitConfig
{
    public static bool ConfigAdded;
    private const string DefaultPolicyName = "DefaultRateLimit";

    /// <summary>
    /// Adds rate limiting services to the service collection
    /// </summary>
    /// <param name="services">The service collection to configure</param>
    /// <param name="configAction">Optional configuration action for rate limit options</param>
    /// <returns>The service collection with rate limiting configured</returns>
    public static IServiceCollection AddRateLimitConfig(this IServiceCollection services, Action<RateLimitOptions>? configAction = null)
    {
        var options = new RateLimitOptions();
        configAction?.Invoke(options);

        services.AddSingleton(Options.Create(options));
        services.AddSingleton<RateLimitPolicyProvider>();

        services.AddRateLimiter(rateLimiterOptions =>
        {
            rateLimiterOptions.AddPolicy(DefaultPolicyName, httpContext =>
            {
                var policyProvider = httpContext.RequestServices.GetRequiredService<RateLimitPolicyProvider>();
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: policyProvider.GetPartitionKey(httpContext),
                    factory: _ => 
                    {
                        var rateLimitOptions = httpContext.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value;
                        return new FixedWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = rateLimitOptions.DefaultRequestLimit,
                            Window = TimeSpan.FromSeconds(rateLimitOptions.TimeWindowInSeconds),
                            QueueLimit = rateLimitOptions.QueueLimit,
                            QueueProcessingOrder = (QueueProcessingOrder)rateLimitOptions.QueueProcessingOrder
                        };
                    });
            });
            
            // Global settings
            rateLimiterOptions.GlobalLimiter = null; // We use partitioned limiter instead
            rateLimiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        });

        ConfigAdded = true;
        Console.WriteLine("Rate Limiting enabled.");
        
        return services;
    }

    /// <summary>
    /// Applies rate limiting middleware to the application
    /// </summary>
    /// <param name="app">The web application to configure</param>
    /// <returns>The web application with rate limiting applied</returns>
    public static WebApplication UseRateLimitConfig(this WebApplication app)
    {
        if (!ConfigAdded) return app;

        app.UseRateLimiter();
        
        Console.WriteLine("Rate Limiting middleware enabled.");
        return app;
    }

    /// <summary>
    /// Applies rate limiting to a route handler
    /// </summary>
    /// <param name="builder">The route handler builder</param>
    /// <returns>The route handler builder with rate limiting applied</returns>
    public static RouteHandlerBuilder RequireRateLimit(this RouteHandlerBuilder builder)
    {
        if (ConfigAdded)
        {
            builder.RequireRateLimiting(DefaultPolicyName);
        }
        return builder;
    }

    /// <summary>
    /// Applies rate limiting to a route group
    /// </summary>
    /// <param name="group">The route group builder</param>
    /// <returns>The route group builder with rate limiting applied</returns>
    public static RouteGroupBuilder RequireRateLimit(this RouteGroupBuilder group)
    {
        if (ConfigAdded)
        {
            group.RequireRateLimiting(DefaultPolicyName);
        }
        return group;
    }
}