using DotNet.Testcontainers.Containers;

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
            //.WithReuse(true)
            .Build();

    protected static async Task StartSqlContainerAsync()
    {
        _container = CreateSqlContainer();
        await _container.StartAsync();
        // Wait for SQL Server to be ready
        await Task.Delay(TimeSpan.FromSeconds(10));
    }

    protected Task EnsureSqlStartedAsync()
    {
        if (_container.State != TestcontainersStates.Running)
            return _container.StartAsync();
        return Task.CompletedTask;
    }

    protected static string GetConnectionString(string DbName) =>
        _container.GetConnectionString()
            .Replace("Database=master;", $"Database={DbName};", StringComparison.OrdinalIgnoreCase);

    protected static MyDbContext CreateDbContext(string DbName) =>
        new(new DbContextOptionsBuilder()
            .UseSqlServer(GetConnectionString(DbName))
            .UseAutoConfigModel(op => op.ScanFrom(typeof(MyDbContext).Assembly))
            .UseAutoDataSeeding()
            .Options);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}