namespace SlimBus.Api.Configs;

internal static class CacheConfigs
{
    public static IServiceCollection CacheConfig(this IServiceCollection services, IConfiguration configuration)
    {
        var conn = configuration.GetConnectionString("Redis");

        // services.AddMemoryCache()
        //     .AddHybridCache();

        if (!string.IsNullOrWhiteSpace(conn))
        {
            services.AddStackExchangeRedisCache(s =>
            {
                s.Configuration = conn;
                s.InstanceName = SharedConsts.ApiName;
            });
        }
        else services.AddDistributedMemoryCache();

        return services;
    }
}