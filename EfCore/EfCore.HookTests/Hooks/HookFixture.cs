using Testcontainers.MsSql;

namespace EfCore.HookTests.Hooks;

public sealed class HookFixture : IAsyncLifetime
{
    private MsSqlContainer _sqlContainer;
    public ServiceProvider Provider { get; private set; }


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
            .AddDbContextWithHook<HookContext>(o =>
                o.UseSqlServer(_sqlContainer.GetConnectionString()).UseAutoConfigModel())
            .AddHook<HookContext, Hook>()
            .BuildServiceProvider();

        //Ensure Db Created
        var db = Provider.GetRequiredService<HookContext>();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (_sqlContainer is null) return;
        await _sqlContainer.StopAsync();
        await _sqlContainer.DisposeAsync();
    }
}