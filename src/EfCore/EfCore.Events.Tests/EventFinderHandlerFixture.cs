namespace EfCore.Events.Tests;

public sealed class EventFinderHandlerFixture : IAsyncDisposable
{
    public ServiceProvider Provider { get; } = new ServiceCollection()
        .AddLogging()
        .BuildServiceProvider();


    public async ValueTask DisposeAsync()
    {
        if (Provider != null) await Provider.DisposeAsync();
    }
}