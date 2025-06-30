using DotNet.Testcontainers.Containers;
using Mapster;
using MapsterMapper;
using Microsoft.Extensions.Hosting;
using Testcontainers.MsSql;

namespace EfCore.Events.Tests.EventPublisherTests;

public sealed class EvenPublisherFixture : IAsyncLifetime
{
    private MsSqlContainer _sqlContainer;

    public string GetConnectionString() =>
        _sqlContainer?.GetConnectionString()
            .Replace("Database=master", "Database=EvenPubDb", StringComparison.OrdinalIgnoreCase) ??
        throw new InvalidOperationException(
            "SQL Server container is not initialized.");

    public async Task InitializeAsync()
    {
        _sqlContainer = new MsSqlBuilder()
            .WithPassword("a1ckZmGjwV8VqNdBUexV")
            //.WithReuse(true)
            .Build();

        await _sqlContainer.StartAsync();
        // Wait for SQL Server to be ready
        await Task.Delay(TimeSpan.FromSeconds(20));

        Provider = new ServiceCollection()
            .AddLogging()
            .AddEventPublisher<DddContext, TestEventPublisher>()
            .AddSingleton(sp =>
            {
                var config = TypeAdapterConfig.GlobalSettings;
                // Configure mapping from Root to TypeEvent
                config.NewConfig<Root, TypeEvent>()
                    .MapWith(src => new TypeEvent());
                return config;
            })
            .AddScoped<IMapper, ServiceMapper>()
            .AddDbContextWithHook<DddContext>(builder =>
                builder.UseSqlServer(GetConnectionString()).UseAutoConfigModel())
            .BuildServiceProvider();

        // Start hosted services manually for test environment
        var hostedServices = Provider.GetServices<IHostedService>();
        foreach (var hostedService in hostedServices)
        {
            await hostedService.StartAsync(CancellationToken.None);
        }

        var db = Provider.GetRequiredService<DddContext>();
        await db.Database.EnsureCreatedAsync();

        db.Set<Root>()
            .AddRange(new Root("Duy", "Steven"), new Root("Steven", "Steven"),
                new Root("Hoang", "Steven"), new Root("DKNet", "Steven"));
        await db.SaveChangesAsync();
    }

    public async Task EnsureSqlReadyAsync()
    {
        if (_sqlContainer is null) return;
        if (_sqlContainer.State == TestcontainersStates.Running) return;
        await _sqlContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        // Stop hosted services
        var hostedServices = Provider?.GetServices<IHostedService>();
        if (hostedServices != null)
        {
            foreach (var hostedService in hostedServices)
            {
                await hostedService.StopAsync(CancellationToken.None);
            }
        }
        
        Provider?.Dispose();
    }

    public ServiceProvider Provider { get; private set; } = null!;
}