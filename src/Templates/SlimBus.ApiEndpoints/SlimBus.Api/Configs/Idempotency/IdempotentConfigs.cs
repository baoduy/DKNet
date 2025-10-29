namespace SlimBus.Api.Configs.Idempotency;

[ExcludeFromCodeCoverage]
internal static class IdempotentConfigs
{
    #region Fields

    private static bool _configAdded;

    #endregion

    #region Methods

    public static IServiceCollection AddIdempotency(
        this IServiceCollection services,
        Action<IdempotencyOptions>? config = null)
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
        {
            builder.AddEndpointFilter<IdempotencyEndpointFilter>();
        }

        return builder;
    }

    #endregion
}