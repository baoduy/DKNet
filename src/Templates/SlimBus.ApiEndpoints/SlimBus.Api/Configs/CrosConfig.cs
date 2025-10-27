namespace SlimBus.Api.Configs;

[ExcludeFromCodeCoverage]
internal static class CrosConfig
{
    #region Fields

    private static bool _configAdded;

    #endregion

    #region Methods

    public static IServiceCollection AddCrosConfig(this IServiceCollection services)
    {
        services.AddCors(c => c.AddDefaultPolicy(o => o.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
        _configAdded = true;
        return services;
    }

    public static WebApplication UseCrosConfig(this WebApplication app)
    {
        if (_configAdded)
        {
            app.UseCors();
            Console.WriteLine("CROS enabled.");
        }

        return app;
    }

    #endregion
}