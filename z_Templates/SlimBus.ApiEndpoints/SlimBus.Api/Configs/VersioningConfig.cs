using System.Diagnostics.CodeAnalysis;

namespace SlimBus.Api.Configs;

[ExcludeFromCodeCoverage]
internal static class VersioningConfig
{
    public static IServiceCollection AddAppVersioning(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer()
            .AddApiVersioning(op =>
            {
                op.DefaultApiVersion = new ApiVersion(1,0);
                op.ReportApiVersions = true;
                op.AssumeDefaultVersionWhenUnspecified = true;
                op.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .EnableApiVersionBinding();
        return services;
    }
}