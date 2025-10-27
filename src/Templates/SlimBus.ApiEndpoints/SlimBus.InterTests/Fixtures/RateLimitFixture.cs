namespace SlimBus.InterTests.Fixtures;

public sealed class RateLimitFixture : HostFixture
{
    #region Methods

    protected override IDictionary<string, string> SetupEnvironments()
    {
        var envs = base.SetupEnvironments();
        envs.Add("FeatureManagement__EnableRateLimit", "true");
        envs.Add("FeatureManagement__DefaultRequestLimit", "1");
        envs.Add("FeatureManagement__DefaultConcurrentLimit", "1");
        envs.Add("FeatureManagement__TimeWindowInSeconds", "10");
        return envs;
    }

    #endregion
}