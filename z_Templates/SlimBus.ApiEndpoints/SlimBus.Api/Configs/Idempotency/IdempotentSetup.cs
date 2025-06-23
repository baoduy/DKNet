namespace SlimBus.Api.Configs.Idempotency;

internal static class IdempotentSetup
{
    private static bool _configAdded;

    public static IServiceCollection AddIdempotency(this IServiceCollection services, Action<IdempotencyOptions>? config = null)
    {
        var options = new IdempotencyOptions();
        config?.Invoke(options);

        services
            .AddSingleton<IIdempotencyKeyRepository, IdempotencyKeyRepository>()
            .AddSingleton(Options.Create(options));
        _configAdded = true;

        return services;
    }

    public static RouteHandlerBuilder AddIdempotencyFilter(this RouteHandlerBuilder builder)
    {
        if (_configAdded)
            builder.AddEndpointFilter<IdempotencyEndpointFilter>();
        return builder;
    }
}
