using Testcontainers.MsSql;

namespace EfCore.Events.Tests;

public static class SqlServerTestHelper
{
    public static async Task<MsSqlContainer> StartSqlContainerAsync()
    {
        var container = new MsSqlBuilder()
            .WithPassword("a1ckZmGjwV8VqNdBUexV")
            .WithAutoRemove(true)
            .Build();
        
        await container.StartAsync();
        // Wait for SQL Server to be ready
        await Task.Delay(TimeSpan.FromSeconds(20));
        return container;
    }

    public static DbContextOptionsBuilder<T> UseSqlServerTestContainer<T>(
        this DbContextOptionsBuilder<T> builder, string connectionString) where T : DbContext
    {
        return builder.UseSqlServer(connectionString);
    }

    public static async Task CleanupContainerAsync(MsSqlContainer container)
    {
        if (container != null)
        {
            await container.StopAsync();
            await container.DisposeAsync();
        }
    }
}