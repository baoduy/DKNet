using Azure.Identity;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using SlimBus.Api.Extensions;

namespace SlimBus.Api.Configs.AzureAppConfig;

/// <summary>
///     Extension methods for configuring Azure App Configuration integration
/// </summary>
[ExcludeFromCodeCoverage]
internal static class AzureAppConfigSetup
{
    /// <summary>
    ///     Adds Azure App Configuration as a configuration source
    /// </summary>
    /// <param name="builder">The configuration builder</param>
    /// <param name="feature">The feature flag management</param>
    /// <returns>The configuration builder</returns>
    public static WebApplicationBuilder AddAzureAppConfig(this WebApplicationBuilder builder, FeatureOptions feature)
    {
        if (!feature.EnableAzureAppConfig) return builder;

        var options = builder.Configuration.Bind<AzureAppConfigOptions>(AzureAppConfigOptions.Name);
        var conn = builder.Configuration.GetConnectionString(options.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(conn)) return builder;

        builder.Configuration.AddAzureAppConfiguration(op =>
        {
            op.Connect(new Uri(conn), new DefaultAzureCredential())
                .UseFeatureFlags()
                .ConfigureRefresh(rs =>
                    rs.RegisterAll()
                        .SetRefreshInterval(TimeSpan.FromMinutes(options.RefreshIntervalInMinutes)));

            var label = options.Label ?? SharedConsts.ApiName;
            op.Select(KeyFilter.Any, label);
        });

        Console.WriteLine("Azure App Configuration is enabled.");


        return builder;
    }
}