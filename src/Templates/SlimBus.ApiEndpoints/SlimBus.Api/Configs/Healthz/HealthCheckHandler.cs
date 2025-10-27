namespace SlimBus.Api.Configs.Healthz;

[ExcludeFromCodeCoverage]
internal sealed class HealthCheckHandler(ILogger<HealthCheckHandler> logger) : IHealthCheck
{
    #region Methods

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // Perform basic health check - this is a template implementation
        var healthCheckResultHealthy = true;

        if (healthCheckResultHealthy)
        {
            var goodms = "TEMP Services is in GOOD health";
            logger.LogInformation(goodms);

            return Task.FromResult(
                HealthCheckResult.Healthy(goodms));
        }

        var ms = "TEMP Services is in BAD health";
        logger.LogInformation(ms);

        return Task.FromResult(
            HealthCheckResult.Unhealthy(ms));
    }

    #endregion
}