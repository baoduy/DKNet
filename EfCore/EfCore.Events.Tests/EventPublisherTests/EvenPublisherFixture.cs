using Mapster;
using MapsterMapper;
using Testcontainers.MsSql;

namespace EfCore.Events.Tests.EventPublisherTests;

public sealed class EvenPublisherFixture : IAsyncLifetime
{
    private MsSqlContainer _sqlContainer;


    public async Task InitializeAsync()
    {
        _sqlContainer = new MsSqlBuilder()
            .WithPassword("a1ckZmGjwV8VqNdBUexV")
            .WithReuse(true)
            .Build();

        await _sqlContainer.StartAsync();
        // Wait for SQL Server to be ready
        await Task.Delay(TimeSpan.FromSeconds(20));

        Provider = new ServiceCollection()
            .AddLogging()
            .AddEventPublisher<DddContext, TestEventPublisher>()
            .AddSingleton(TypeAdapterConfig.GlobalSettings)
            .AddScoped<IMapper, ServiceMapper>()
            .AddDbContextWithHook<DddContext>(builder =>
                builder.UseSqlServer(_sqlContainer.GetConnectionString()).UseAutoConfigModel())
            .BuildServiceProvider();

        Context = Provider.GetRequiredService<DddContext>();
        await Context.Database.EnsureCreatedAsync();

        Context.Set<Root>()
            .AddRange(new Root("Duy"), new Root("Steven"), new Root("Hoang"), new Root("DKNet"));

        //BeforeAddedEventTestHandler.ReturnFailureResult = false;
        Context.SaveChangesAsync().GetAwaiter().GetResult();
    }

    public async Task DisposeAsync()
    {
        if (_sqlContainer is null) return;
        await _sqlContainer.StopAsync();
        await _sqlContainer.DisposeAsync();
    }

    public ServiceProvider Provider { get; private set; }
    public DddContext Context { get; private set; }
}