namespace SlimBus.App.Tests.Fixtures;

[Collection(nameof(ShareInfraCollectionFixture))]
public sealed class RateLimitFixture(ShareInfraFixture infra) : ApiFixtureBase(infra)
{
    protected override void SetEnvironmentVariables()
    {
        Environment.SetEnvironmentVariable($"FeatureManagement__{nameof(FeatureOptions.EnableRateLimit)}", "true");
        Environment.SetEnvironmentVariable("RateLimit__DefaultRequestLimit", "1");
        Environment.SetEnvironmentVariable("FeatureManagement__TimeWindowInSeconds", "1");
        Environment.SetEnvironmentVariable("FeatureManagement__QueueLimit", "1");

        Environment.SetEnvironmentVariable($"FeatureManagement__{nameof(FeatureOptions.EnableServiceBus)}", "false");
        Environment.SetEnvironmentVariable($"FeatureManagement__{nameof(FeatureOptions.RequireAuthorization)}",
            "false");

        var cacheConn = infra.CacheConn;
        var dbConn = infra.DbConn;

        Environment.SetEnvironmentVariable($"ConnectionStrings__{SharedConsts.RedisConnectionString}", cacheConn);
        Environment.SetEnvironmentVariable($"ConnectionStrings__{SharedConsts.DbConnectionString}", dbConn);
    }
}