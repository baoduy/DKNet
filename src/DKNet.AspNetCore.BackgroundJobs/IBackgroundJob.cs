namespace DKNet.AspNetCore.BackgroundJobs;

public interface IBackgroundJob
{
    Task RunAsync(CancellationToken cancellationToken = default);
}