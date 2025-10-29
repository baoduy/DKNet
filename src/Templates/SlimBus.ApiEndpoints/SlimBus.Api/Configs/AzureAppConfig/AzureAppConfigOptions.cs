namespace SlimBus.Api.Configs.AzureAppConfig;

/// <summary>
///     Configuration options for Azure App Configuration integration
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class AzureAppConfigOptions
{
    #region Properties

    /// <summary>
    ///     Whether to load feature flags from Azure App Configuration
    /// </summary>
    public bool LoadFeatureFlags { get; set; } = true;

    /// <summary>
    ///     Cache expiration time for configuration values in Minutes
    /// </summary>
    public int RefreshIntervalInMinutes { get; set; } = 300;

    /// <summary>
    ///     Connection string for Azure App Configuration
    /// </summary>
    public string ConnectionStringName { get; set; } = Name;

    /// <summary>
    ///     Feature flag prefix to filter feature flags (optional)
    /// </summary>
    public string FeatureFlagPrefix { get; set; } = string.Empty;

    /// <summary>
    ///     Configuration section name
    /// </summary>
    public static string Name => "AzureAppConfig";

    /// <summary>
    ///     Label to filter configuration values (optional)
    /// </summary>
    public string? Label { get; set; }

    #endregion
}