namespace EfCore.Extensions.Tests;

/// <summary>
/// Base class for tests that need SQL Server TestContainer setup.
/// Follow this pattern for isolated test methods that need fresh database instances.
/// </summary>
public abstract class SqlServerTestBase
{
    protected static MsSqlContainer CreateSqlContainer()
    {
        return new MsSqlBuilder()
            .WithPassword("a1ckZmGjwV8VqNdBUexV")
            .WithAutoRemove(true)
            .Build();
    }

    protected static async Task<MsSqlContainer> StartSqlContainerAsync()
    {
        var container = CreateSqlContainer();
        await container.StartAsync();
        // Wait for SQL Server to be ready
        await Task.Delay(TimeSpan.FromSeconds(20));
        return container;
    }

    protected static MyDbContext CreateDbContext(string connectionString)
    {
        return new MyDbContext(new DbContextOptionsBuilder()
            .UseSqlServer(connectionString)
            .UseAutoConfigModel(op => op.ScanFrom(typeof(MyDbContext).Assembly))
            .UseAutoDataSeeding()
            .Options);
    }

    protected static async Task CleanupContainerAsync(MsSqlContainer container)
    {
        if (container != null)
        {
            await container.StopAsync();
            await container.DisposeAsync();
        }
    }
}