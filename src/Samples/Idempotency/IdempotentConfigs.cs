namespace Mx.Pgw.Api.Configs.Idempotency;

[ExcludeFromCodeCoverage]
internal static class IdempotentConfigs
{
    private static bool _configAdded;
    public static string IdempotentHeaderKey { get; private set; } = null!;

    public static IServiceCollection AddIdempotency(this IServiceCollection services,
        Action<IdempotencyOptions>? config = null)
    {
        var options = new IdempotencyOptions();
        config?.Invoke(options);

        services
            .AddSingleton<IIdempotencyKeyRepository, IdempotencyDistributedCacheRepository>()
            .AddSingleton(Options.Create(options));
        _configAdded = true;
        IdempotentHeaderKey = options.IdempotencyHeaderKey;

        return services;
    }

    public static RouteHandlerBuilder RequiredIdempotentKey(this RouteHandlerBuilder builder)
    {
        if (_configAdded)
            builder.AddEndpointFilter<IdempotencyEndpointFilter>();
        return builder;
    }
}