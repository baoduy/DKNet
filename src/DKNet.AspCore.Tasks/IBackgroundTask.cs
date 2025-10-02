namespace DKNet.AspCore.Tasks;

public interface IBackgroundTask
{
    Task RunAsync(CancellationToken cancellationToken = default);
}