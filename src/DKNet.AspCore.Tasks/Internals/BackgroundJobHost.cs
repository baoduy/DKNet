using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DKNet.AspCore.Tasks.Internals;

internal sealed class BackgroundJobHost(ILogger<BackgroundJobHost> logger, IServiceProvider provider)
    : BackgroundService
{
    #region Methods

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Background job host started");
        await using var scope = provider.CreateAsyncScope();
        var jobs = scope.ServiceProvider.GetServices<IBackgroundTask>();
        await Task.WhenAll(jobs.Select(j => this.ExecuteJobAsync(j, stoppingToken)));
        logger.LogInformation("Background job host finished");
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types")]
    private async Task ExecuteJobAsync(IBackgroundTask task, CancellationToken cancellationToken = default)
    {
        try
        {
            await task.RunAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while executing job `{JobType}`", task.GetType().FullName);
        }
    }

    #endregion
}