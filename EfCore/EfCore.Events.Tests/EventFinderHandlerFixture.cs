namespace EfCore.Events.Tests;

public sealed class EventFinderHandlerFixture : IDisposable
{
    public ServiceProvider Provider { get; } = new ServiceCollection()
        .AddLogging()
        .BuildServiceProvider();

    public void Dispose() => Provider?.Dispose();
}