using Mapster;

namespace SlimBus.App.Tests.LazyMapper;

public sealed class LazyMapFixture : IDisposable, IAsyncDisposable
{
    public ServiceProvider ServiceProvider { get; } = new ServiceCollection()
        .AddSingleton(TypeAdapterConfig.GlobalSettings)
        .AddScoped<IMapper, ServiceMapper>()
        .BuildServiceProvider();

    public void Dispose() => ServiceProvider.Dispose();
    public ValueTask DisposeAsync() => ServiceProvider.DisposeAsync();
}