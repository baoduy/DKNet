using Mapster;
using SlimBus.AppServices.Profiles.V1.Actions;
using SlimBus.Domains.Features.Profiles.Entities;

namespace SlimBus.AppServices;

public static class AppSetup
{
    public static IServiceCollection AddAppServices(this IServiceCollection services)
    {
        TypeAdapterConfig.GlobalSettings.Default.MapToConstructor(true);
        //Manual config for those classes that does not have a default constructor.
        TypeAdapterConfig<CreateProfileCommand, CustomerProfile>.NewConfig().ConstructUsing(s =>
            new CustomerProfile(s.Name, s.MembershipNo, s.Email, s.Phone, s.UserId!));

        services
            .AddSingleton(TypeAdapterConfig.GlobalSettings)
            .AddScoped<IMapper, ServiceMapper>();

        return services;
    }
}