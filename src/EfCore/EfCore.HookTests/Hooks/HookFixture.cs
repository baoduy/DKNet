using Testcontainers.MsSql;

namespace EfCore.HookTests.Hooks;

public sealed class HookFixture : IAsyncLifetime
{
    #region Fields

    private MsSqlContainer _sqlContainer = null!;

    #endregion

    #region Properties

    public ServiceProvider Provider { get; private set; } = null!;

    #endregion

    #region Methods

    public Task DisposeAsync() => Task.CompletedTask;

    public string GetConnectionString() =>
        _sqlContainer?.GetConnectionString()
            .Replace("Database=master", "Database=HookDb", StringComparison.OrdinalIgnoreCase) ??
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
            .AddDbContextWithHook<HookContext>(o =>
                o.UseSqlServer(GetConnectionString()).UseAutoConfigModel())
            .AddHook<HookContext, HookTest>()
            .BuildServiceProvider();

        //Ensure Db Created
        var db = Provider.GetRequiredService<HookContext>();
        await db.Database.EnsureCreatedAsync();
    }

    #endregion
}