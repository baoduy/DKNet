namespace EfCore.Events.Tests;

public sealed class EventFinderHandlerFixture : IAsyncDisposable
{
    #region Properties

    public ServiceProvider Provider { get; } = new ServiceCollection()
        .AddLogging()
        .BuildServiceProvider();

    #endregion

    #region Methods

    public async ValueTask DisposeAsync()
    {
        if (Provider != null) await Provider.DisposeAsync();
    }

    #endregion
}