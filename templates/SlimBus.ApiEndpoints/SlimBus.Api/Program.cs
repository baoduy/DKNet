using SlimBus.Api.Configs;
using SlimBus.Api.Configs.AzureAppConfiguration;
using SlimBus.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Check if Azure App Configuration should be enabled from initial configuration
var initialFeature = builder.Configuration.Bind<FeatureOptions>(FeatureOptions.Name);
if (initialFeature.EnableAzureAppConfiguration)
{
    var connectionString = builder.Configuration.GetConnectionString("AzureAppConfiguration");
    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        // Configure Azure App Configuration options from appsettings
        var azureAppConfigOptions = new AzureAppConfigurationOptions();
        builder.Configuration.GetSection(AzureAppConfigurationOptions.Name).Bind(azureAppConfigOptions);
        azureAppConfigOptions.ConnectionString = connectionString;

        // Add Azure App Configuration to configuration sources
        builder.Configuration.AddSlimBusAzureAppConfiguration(connectionString, azureAppConfigOptions);
    }
}

// Rebind features after potentially loading from Azure App Configuration
var feature = builder.Configuration.Bind<FeatureOptions>(FeatureOptions.Name);

builder.AddLogConfig(feature)
    .AddFluentValidationConfig();

//Run migration and exit the app if needed.
await builder.RunMigrationAsync(feature, args);

// Add services to the container.
builder.Services
    .AddOptions(builder.Configuration)
    .AddAppConfig(feature, builder.Configuration);

await builder.Build()
    .UseAppConfig(a => a.UseEndpointConfigs());

//This Startup endpoint for Unit Tests
namespace SlimBus.Api
{
    public class Program;
}