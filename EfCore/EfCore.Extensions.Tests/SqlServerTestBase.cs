using DotNet.Testcontainers.Containers;

namespace EfCore.Extensions.Tests;

/// <summary>
/// Base class for tests that need SQL Server TestContainer setup.
/// Follow this pattern for isolated test methods that need fresh database instances.
/// </summary>
public abstract class SqlServerTestBase
{
    private static MsSqlContainer _container = null!;

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

    protected static Task EnsureSqlStartedAsync() =>
        _container.State != TestcontainersStates.Running ? _container.StartAsync() : Task.CompletedTask;

    protected static string GetConnectionString(string dbName) =>
        _container.GetConnectionString()
            .Replace("Database=master;", $"Database={dbName};", StringComparison.OrdinalIgnoreCase);

    protected static MyDbContext CreateDbContext(string dbName) =>
        new(new DbContextOptionsBuilder()
            .UseSqlServer(GetConnectionString(dbName))
            .UseAutoConfigModel(op => op.ScanFrom(typeof(MyDbContext).Assembly))
            .UseAutoDataSeeding()
            .Options);
}