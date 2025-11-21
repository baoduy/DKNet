using Mapster;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.Serialization.SystemTextJson;

namespace SlimBus.Extensions.Tests.Fixtures;

public sealed class Fixture : IAsyncDisposable
{
    #region Constructors

    public Fixture()
    {
        ServiceProvider = new ServiceCollection()
            .AddLogging()
            .AddSingleton(TypeAdapterConfig.GlobalSettings)
            .AddScoped<IMapper, ServiceMapper>()
            .AddDbContext<TestDbContext>(b => b.UseInMemoryDatabase(nameof(TestDbContext)))
            .AddScoped<DbContext>(p => p.GetRequiredService<TestDbContext>())
            .AddSlimBusEfCoreInterceptor<TestDbContext>()
            .AddSlimMessageBus(mmb =>
            {
                //This is a global config for all the child busses
                mmb.AddJsonSerializer()
                    .AddServicesFromAssembly(typeof(Fixture).Assembly)
                    .AddChildBus(
                        "ImMemory",
                        me =>
                        {
                            me.WithProviderMemory()
                                .AutoDeclareFrom(typeof(Fixture).Assembly);
                        });
            })
            .BuildServiceProvider();

        var db = ServiceProvider.GetRequiredService<TestDbContext>();
        db.Database.EnsureCreated();
    }

    #endregion

    #region Properties

    public ServiceProvider ServiceProvider { get; }

    #endregion

    #region Methods

    public ValueTask DisposeAsync() => ServiceProvider.DisposeAsync();

    #endregion
}