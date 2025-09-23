using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DKNet.AspNetCore.BackgroundJobs.Internals;

internal sealed class BackgroundJobHost(ILogger<BackgroundJobHost> logger, IServiceProvider provider)
    : BackgroundService
{
    [SuppressMessage("Design", "CA1031:Do not catch general exception types")]
    private async Task ExecuteJobAsync(IBackgroundJob job, CancellationToken cancellationToken = default)
    {
        try
        {
            await job.RunAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while executing job `{JobType}`", job.GetType().FullName);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Background job host started");
        await using var scope = provider.CreateAsyncScope();
        var jobs = scope.ServiceProvider.GetServices<IBackgroundJob>();
        await Task.WhenAll(jobs.Select(j => ExecuteJobAsync(j, stoppingToken)));
        logger.LogInformation("Background job host finished");
    }
}