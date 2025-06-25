namespace SlimBus.App.Tests.Fixtures;

[Collection(nameof(ShareInfraCollection))]
public sealed class ApiFixture(ShareInfraFixture infra) : ApiFixtureBase(infra)
{
    protected override void SetEnvironmentVariables()
    {
        Environment.SetEnvironmentVariable($"FeatureManagement__{nameof(FeatureOptions.EnableRateLimit)}", "false");
        Environment.SetEnvironmentVariable("FeatureManagement__EnableServiceBus", "false");

        var cacheConn = infra.CacheConn;
        var dbConn = infra.DbConn;
        Environment.SetEnvironmentVariable($"ConnectionStrings__{SharedConsts.RedisConnectionString}", cacheConn);
        Environment.SetEnvironmentVariable($"ConnectionStrings__{SharedConsts.DbConnectionString}", dbConn);
    }
}