namespace SlimBus.Api.Configs;

[ExcludeFromCodeCoverage]
internal static class CacheConfigs
{
    #region Methods

    public static IServiceCollection CacheConfig(this IServiceCollection services, IConfiguration configuration)
    {
        var conn = configuration.GetConnectionString("Redis");

        if (string.IsNullOrWhiteSpace(conn))
            services.AddDistributedMemoryCache();
        else
            services.AddStackExchangeRedisCache(s =>
            {
                s.Configuration = conn;
                s.InstanceName = SharedConsts.ApiName;
            });

        services.AddHybridCache();

        return services;
    }

    #endregion
}