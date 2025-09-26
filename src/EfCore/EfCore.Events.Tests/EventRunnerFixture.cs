using DotNet.Testcontainers.Containers;
using Mapster;
using MapsterMapper;
using Testcontainers.MsSql;

namespace EfCore.Events.Tests;

public sealed class EventRunnerFixture : IAsyncLifetime
{
    private MsSqlContainer _sqlContainer;
    public ServiceProvider Provider { get; private set; }

    public async Task InitializeAsync()
    {
        _sqlContainer = new MsSqlBuilder()
            .WithPassword("a1ckZmGjwV8VqNdBUexV")
            //.WithReuse(true)
            .Build();

        await _sqlContainer.StartAsync();
        // Wait for SQL Server to be ready
        await Task.Delay(TimeSpan.FromSeconds(20));

        TypeAdapterConfig.GlobalSettings.Default.MapToConstructor(true);
        TypeAdapterConfig.GlobalSettings.NewConfig<Root, EntityAddedEvent>()
            .Map(dest => dest.Id, src => src.Id)
            .Map(dest => dest.Name, src => src.Name);

        Provider = new ServiceCollection()
            .AddLogging()
            .AddSingleton(TypeAdapterConfig.GlobalSettings)
            .AddScoped<IMapper, ServiceMapper>()
            .AddDbContextWithHook<DddContext>(o =>
                o.UseSqlServer(_sqlContainer.GetConnectionString()).UseAutoConfigModel())
            .AddEventPublisher<DddContext, TestEventPublisher>()
            .BuildServiceProvider();

        //Ensure Db Created
        var db = Provider.GetRequiredService<DddContext>();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (_sqlContainer is null) return;
        await _sqlContainer.StopAsync();
        await _sqlContainer.DisposeAsync();
    }

    public async Task EnsureSqlReadyAsync()
    {
        if (_sqlContainer is null) return;
        if (_sqlContainer.State == TestcontainersStates.Running) return;
        await _sqlContainer.StartAsync();
    }
}