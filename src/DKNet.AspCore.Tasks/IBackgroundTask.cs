namespace DKNet.AspCore.Tasks;

public interface IBackgroundTask
{
    #region Methods

    Task RunAsync(CancellationToken cancellationToken = default);

    #endregion
}