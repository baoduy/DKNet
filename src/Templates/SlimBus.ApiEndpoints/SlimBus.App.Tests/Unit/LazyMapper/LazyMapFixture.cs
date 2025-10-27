using Mapster;

namespace SlimBus.App.Tests.Unit.LazyMapper;

public sealed class LazyMapFixture : IAsyncDisposable
{
    #region Properties

    public ServiceProvider ServiceProvider { get; } = new ServiceCollection()
        .AddSingleton(TypeAdapterConfig.GlobalSettings)
        .AddScoped<IMapper, ServiceMapper>()
        .BuildServiceProvider();

    #endregion

    #region Methods

    public ValueTask DisposeAsync() => ServiceProvider.DisposeAsync();

    #endregion
}