namespace SlimBus.Api.Configs.AzureAppConfig;

/// <summary>
/// Configuration options for Azure App Configuration integration
/// </summary>
[ExcludeFromCodeCoverage]
public class AzureAppConfigOptions
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public static string Name => "AzureAppConfiguration";

    /// <summary>
    /// Connection string for Azure App Configuration
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Key prefix to filter configuration values (optional)
    /// </summary>
    public string KeyPrefix { get; set; } = string.Empty;

    /// <summary>
    /// Label to filter configuration values (optional)
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Cache expiration time for configuration values in seconds
    /// </summary>
    public int CacheExpirationInSeconds { get; set; } = 300; // 5 minutes default

    /// <summary>
    /// Whether to load feature flags from Azure App Configuration
    /// </summary>
    public bool LoadFeatureFlags { get; set; } = true;

    /// <summary>
    /// Feature flag prefix to filter feature flags (optional)
    /// </summary>
    public string FeatureFlagPrefix { get; set; } = string.Empty;
}