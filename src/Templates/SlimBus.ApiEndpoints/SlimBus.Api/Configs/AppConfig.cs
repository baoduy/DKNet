using SlimBus.Api.Configs.Auth;
using SlimBus.Api.Configs.GlobalExceptions;
using SlimBus.Api.Configs.Idempotency;
using SlimBus.Api.Configs.RateLimits;
using SlimBus.Api.Configs.Swagger;

namespace SlimBus.Api.Configs;

[ExcludeFromCodeCoverage]
internal static class AppConfig
{
    #region Methods

    public static IServiceCollection AddAppConfig(this IServiceCollection services, FeatureOptions features,
        IConfiguration configuration)
    {
        if (features.EnableAntiforgery)
            services.AddAntiforgeryConfig();

        if (features.RequireAuthorization)
            services.AddAuthConfig();

        if (features.EnableSwagger)
            services.AddOpenApiDoc(features);

        if (features.EnableHttps)
            services.AddHttpsConfig();

        if (features.EnableRateLimit)
            services.AddRateLimitConfig(configuration);

        services.AddHttpContextAccessor()
            .AddFeatureManagement();

        services.CacheConfig(configuration)
            .AddIdempotency();

        return services
            .AddCrosConfig()
            .AddAppVersioning()
            .AddGlobalException()
            .AddAllAppServices(configuration, features)
            .AddHealthzConfig(features);
    }

    public static Task UseAppConfig(this WebApplication app, Action<WebApplication>? extra = null)
    {
        app.UseAntiforgeryConfig()
            .UseCrosConfig()
            .UseHttpsConfig()
            .UseHealthzConfig();

        app.UseRouting();
        app.UseRateLimitConfig();
        //This must be after UseRouting
        app.UseAuthConfig();
        //This is UseEndpoints
        extra?.Invoke(app);

        //These have to be after UseEndpoints.
        app.UseOpenApiDoc()
            .UseGlobalException();

        return app.RunAsync();
    }

    #endregion
}