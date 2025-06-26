namespace EfCore.Extensions.Tests;

/// <summary>
/// Base class for tests that need SQL Server TestContainer setup.
/// Follow this pattern for isolated test methods that need fresh database instances.
/// </summary>
public abstract class SqlServerTestBase : IAsyncDisposable
{
    private static MsSqlContainer _container;

    private static MsSqlContainer CreateSqlContainer() =>
        new MsSqlBuilder()
            .WithPassword("a1ckZmGjwV8VqNdBUexV")
            .WithReuse(true)
            .Build();

    protected static async Task<MsSqlContainer> StartSqlContainerAsync()
    {
        _container = CreateSqlContainer();
        await _container.StartAsync();
        // Wait for SQL Server to be ready
        await Task.Delay(TimeSpan.FromSeconds(10));
        return _container;
    }

    protected static MyDbContext CreateDbContext(string connectionString) =>
        new(new DbContextOptionsBuilder()
            .UseSqlServer(connectionString)
            .UseAutoConfigModel(op => op.ScanFrom(typeof(MyDbContext).Assembly))
            .UseAutoDataSeeding()
            .Options);

    public async ValueTask DisposeAsync()
    {
        if (_container is null) return;
        await Task.Delay(TimeSpan.FromSeconds(10));
        await _container.StopAsync();
        await _container.DisposeAsync();
    }
}