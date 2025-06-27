using SlimBus.Api.Configs;
using SlimBus.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);
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