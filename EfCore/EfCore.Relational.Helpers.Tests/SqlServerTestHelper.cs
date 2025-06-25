using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace EfCore.Relational.Helpers.Tests;

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

    public static async Task CleanupContainerAsync(MsSqlContainer container)
    {
        if (container != null)
        {
            await container.StopAsync();
            await container.DisposeAsync();
        }
    }
}