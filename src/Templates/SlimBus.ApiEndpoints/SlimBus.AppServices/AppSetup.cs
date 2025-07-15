namespace SlimBus.AppServices;

public static class AppSetup
{
    public static IServiceCollection AddAppServices(this IServiceCollection services)
    {
        TypeAdapterConfig.GlobalSettings.Default.MapToConstructor(true);
        TypeAdapterConfig.GlobalSettings.Default.PreserveReference(true);
        TypeAdapterConfig.GlobalSettings.ScanMapsTo();
        TypeAdapterConfig.GlobalSettings.Compile();

        services
            .AddSingleton(TypeAdapterConfig.GlobalSettings)
            .AddScoped<IMapper, ServiceMapper>();


        return services;
    }
}