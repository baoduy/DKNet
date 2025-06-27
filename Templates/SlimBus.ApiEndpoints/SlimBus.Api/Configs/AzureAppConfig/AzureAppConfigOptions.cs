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
    public static string Name => "AzureAppConfig";

    /// <summary>
    /// Connection string for Azure App Configuration
    /// </summary>
    public string ConnectionStringName { get; set; } = Name;

    public string Endpoint { get; set; } = Name;

    /// <summary>
    /// Label to filter configuration values (optional)
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// Cache expiration time for configuration values in Minutes
    /// </summary>
    public int RefreshIntervalInMinutes { get; set; } = 300;

    /// <summary>
    /// Whether to load feature flags from Azure App Configuration
    /// </summary>
    public bool LoadFeatureFlags { get; set; } = true;

    /// <summary>
    /// Feature flag prefix to filter feature flags (optional)
    /// </summary>
    public string FeatureFlagPrefix { get; set; } = string.Empty;
}