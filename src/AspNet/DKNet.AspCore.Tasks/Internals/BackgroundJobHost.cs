using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DKNet.AspCore.Tasks.Internals;

internal sealed class BackgroundJobHost(
    ILogger<BackgroundJobHost> logger,
    IServiceProvider provider,
    IEnumerable<BackgroundJobRegistration> registrations)
    : BackgroundService
{
    #region Methods

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Background job host started");
        await Task.WhenAll(registrations.Select(r => ExecuteJobAsync(r.JobType, stoppingToken)));
        logger.LogInformation("Background job host finished");
    }

    // Each job gets its own scope. Resolution sits outside the try, so a job that cannot be constructed
    // faults ExecuteAsync instead of being logged as a job failure.
    [SuppressMessage("Design", "CA1031:Do not catch general exception types")]
    private async Task ExecuteJobAsync(Type jobType, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var task = (IBackgroundTask)scope.ServiceProvider.GetRequiredService(jobType);
        try
        {
            await task.RunAsync(cancellationToken);
        }
        catch (Exception ex) when (!(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            logger.LogError(ex, "{JobType} job failed", jobType.FullName);
        }
    }

    #endregion
}
