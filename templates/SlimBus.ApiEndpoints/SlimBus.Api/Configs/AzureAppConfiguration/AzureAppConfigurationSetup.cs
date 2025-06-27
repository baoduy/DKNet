namespace SlimBus.Api.Configs.AzureAppConfiguration;

/// <summary>
/// Extension methods for configuring Azure App Configuration integration
/// </summary>
[ExcludeFromCodeCoverage]
internal static class AzureAppConfigurationSetup
{
    /// <summary>
    /// Adds Azure App Configuration as a configuration source
    /// </summary>
    /// <param name="builder">The configuration builder</param>
    /// <param name="connectionString">The Azure App Configuration connection string</param>
    /// <param name="options">Configuration options</param>
    /// <returns>The configuration builder</returns>
    public static IConfigurationBuilder AddSlimBusAzureAppConfiguration(
        this IConfigurationBuilder builder,
        string connectionString,
        AzureAppConfigurationOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.WriteLine("Azure App Configuration connection string is not provided. Skipping Azure App Configuration integration.");
            return builder;
        }

        options ??= new AzureAppConfigurationOptions();

        try
        {
            builder.AddAzureAppConfiguration(config =>
                ConfigureAzureAppConfiguration(config, connectionString, options));

            Console.WriteLine("Azure App Configuration integration enabled successfully.");
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine($"Invalid Azure App Configuration connection string: {ex.Message}");
            Console.WriteLine("Continuing without Azure App Configuration...");
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"Failed to configure Azure App Configuration: {ex.Message}");
            Console.WriteLine("Continuing without Azure App Configuration...");
        }

        return builder;
    }

    /// <summary>
    /// Configures Azure App Configuration options
    /// </summary>
    /// <param name="config">The configuration builder</param>
    /// <param name="connectionString">Connection string</param>
    /// <param name="options">Configuration options</param>
    private static void ConfigureAzureAppConfiguration(
        Microsoft.Extensions.Configuration.AzureAppConfiguration.AzureAppConfigurationOptions config,
        string connectionString,
        AzureAppConfigurationOptions options)
    {
        config.Connect(connectionString);

        // Configure key-value retrieval
        ConfigureKeyValueRetrieval(config, options);

        // Configure caching
        ConfigureRefresh(config, options);

        // Configure feature flags if enabled
        if (options.LoadFeatureFlags)
        {
            ConfigureFeatureFlags(config, options);
        }
    }

    /// <summary>
    /// Configures key-value retrieval from Azure App Configuration
    /// </summary>
    private static void ConfigureKeyValueRetrieval(Microsoft.Extensions.Configuration.AzureAppConfiguration.AzureAppConfigurationOptions config, AzureAppConfigurationOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.KeyPrefix))
        {
            config.Select($"{options.KeyPrefix}*", options.Label);
        }
        else
        {
            config.Select("*", options.Label);
        }
    }

    /// <summary>
    /// Configures refresh settings for Azure App Configuration
    /// </summary>
    private static void ConfigureRefresh(Microsoft.Extensions.Configuration.AzureAppConfiguration.AzureAppConfigurationOptions config, AzureAppConfigurationOptions options)
    {
        config.ConfigureRefresh(refresh =>
        {
            refresh.SetRefreshInterval(TimeSpan.FromSeconds(options.CacheExpirationInSeconds));
        });
    }

    /// <summary>
    /// Configures feature flags for Azure App Configuration
    /// </summary>
    private static void ConfigureFeatureFlags(Microsoft.Extensions.Configuration.AzureAppConfiguration.AzureAppConfigurationOptions config, AzureAppConfigurationOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.FeatureFlagPrefix))
        {
            config.UseFeatureFlags(featureFlags =>
            {
                featureFlags.Select($"{options.FeatureFlagPrefix}*", options.Label);
            });
        }
        else
        {
            config.UseFeatureFlags();
        }
    }

    /// <summary>
    /// Adds Azure App Configuration services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration</param>
    /// <returns>The service collection</returns>
    public static IServiceCollection AddAzureAppConfigurationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure Azure App Configuration options
        services.Configure<AzureAppConfigurationOptions>(
            configuration.GetSection(AzureAppConfigurationOptions.Name));

        // Add Azure App Configuration services for refresh and feature management
        services.AddAzureAppConfiguration();

        return services;
    }
}