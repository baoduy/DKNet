namespace SlimBus.Share.Options;

public class FeatureOptions
{
    public static string Name => "FeatureManagement";
    public bool EnableHttps { get; set; }
    public bool EnableSwagger { get; set; }
    public bool EnableAntiforgery { get; set; }

    public bool EnableServiceBus { get; set; }
    public bool RequireAuthorization { get; set; }
    public bool RunDbMigrationWhenAppStart { get; set; }

    public bool EnableHealthCheck { get; set; } = true;

    /// <summary>
    /// Enable Graph token validation
    /// </summary>
    public bool EnableMsGraphJwtTokenValidation { get; set; }

    public bool EnableOpenTelemetry{ get; set; }
}