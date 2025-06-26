using DotNet.Testcontainers.Containers;

namespace EfCore.Extensions.Tests;

/// <summary>
/// Base class for tests that need SQL Server TestContainer setup.
/// Follow this pattern for isolated test methods that need fresh database instances.
/// </summary>
public abstract class SqlServerTestBase : IAsyncDisposable
{
    private MsSqlContainer _container;

    private MsSqlContainer CreateSqlContainer() =>
        new MsSqlBuilder()
            .WithPassword("a1ckZmGjwV8VqNdBUexV")
            //.WithReuse(true)
            .Build();

    protected async Task<MsSqlContainer> StartSqlContainerAsync()
    {
        _container = CreateSqlContainer();
        await _container.StartAsync();
        // Wait for SQL Server to be ready
        await Task.Delay(TimeSpan.FromSeconds(10));
        return _container;
    }

    protected Task EnsureSqlStartedAsync()
    {
        if (_container.State != TestcontainersStates.Running)
            return _container.StartAsync();
        return Task.CompletedTask;
    }

    protected static MyDbContext CreateDbContext(string connectionString) =>
        new(new DbContextOptionsBuilder()
            .UseSqlServer(connectionString)
            .UseAutoConfigModel(op => op.ScanFrom(typeof(MyDbContext).Assembly))
            .UseAutoDataSeeding()
            .Options);

    public ValueTask DisposeAsync()
    {
        // if (_container is null) return;
        // await _container.StopAsync();
        // await _container.DisposeAsync();
        return ValueTask.CompletedTask;
    }
}