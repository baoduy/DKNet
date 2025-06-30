using Mapster;

namespace SlimBus.App.Tests.Unit.LazyMapper;

public sealed class LazyMapFixture : IAsyncDisposable
{
    public ServiceProvider ServiceProvider { get; } = new ServiceCollection()
        .AddSingleton(TypeAdapterConfig.GlobalSettings)
        .AddScoped<IMapper, ServiceMapper>()
        .BuildServiceProvider();

    public ValueTask DisposeAsync() => ServiceProvider.DisposeAsync();
}