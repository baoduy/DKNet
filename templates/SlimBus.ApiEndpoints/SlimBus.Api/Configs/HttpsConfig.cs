namespace SlimBus.Api.Configs;

[ExcludeFromCodeCoverage]
internal static class HttpsConfig
{
    private static bool _configAdded;

    public static IServiceCollection AddHttpsConfig(this IServiceCollection services,
        Action<HstsOptions>? configureOptions = null)
    {
        if (configureOptions is not null)
            services.AddHsts(configureOptions);
        else
            services.AddHsts(c =>
            {
                c.Preload = true;
                c.IncludeSubDomains = true;
                c.MaxAge = TimeSpan.FromDays(30);
            });

        _configAdded = true;
        return services;
    }

    public static WebApplication UseHttpsConfig(this WebApplication app)
    {
        if (_configAdded)
        {
            app.UseHsts()
                .UseHttpsRedirection();
            Console.WriteLine("Hsts enabled.");
        }

        return app;
    }
}