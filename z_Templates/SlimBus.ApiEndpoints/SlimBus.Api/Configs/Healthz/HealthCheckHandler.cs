namespace SlimBus.Api.Configs.Healthz;

internal sealed class HealthCheckHandler(ILogger<HealthCheckHandler> logger) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        //TODO: Do the health check and return the result here
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
}