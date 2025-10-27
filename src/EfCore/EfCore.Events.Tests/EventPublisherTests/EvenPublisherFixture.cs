using DotNet.Testcontainers.Containers;
using Mapster;
using MapsterMapper;
using Testcontainers.MsSql;

namespace EfCore.Events.Tests.EventPublisherTests;

public sealed class EvenPublisherFixture : IAsyncLifetime
{
    #region Fields

    private MsSqlContainer? _sqlContainer;

    #endregion

    #region Properties

    public ServiceProvider Provider { get; private set; } = null!;

    #endregion

    #region Methods

    public Task DisposeAsync() => Task.CompletedTask;

    public async Task EnsureSqlReadyAsync()
    {
        if (_sqlContainer is null) return;
        if (_sqlContainer.State == TestcontainersStates.Running) return;
        await _sqlContainer.StartAsync();
    }

    private string GetConnectionString() =>
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

        TypeAdapterConfig.GlobalSettings.Default.MapToConstructor(true);

        Provider = new ServiceCollection()
            .AddLogging()
            .AddEventPublisher<DddContext, TestEventPublisher>()
            .AddSingleton(TypeAdapterConfig.GlobalSettings)
            .AddScoped<IMapper, ServiceMapper>()
            .AddDbContextWithHook<DddContext>(builder =>
                builder.UseAutoConfigModel(typeof(DddContext).Assembly)
                    .UseSqlServer(GetConnectionString()))
            .BuildServiceProvider();

        var db = Provider.GetRequiredService<DddContext>();
        await db.Database.EnsureCreatedAsync();

        db.Set<Root>()
            .AddRange(new Root("Duy", "Steven"), new Root("Steven", "Steven"),
                new Root("Hoang", "Steven"), new Root("DKNet", "Steven"));
        await db.SaveChangesAsync();
    }

    #endregion
}