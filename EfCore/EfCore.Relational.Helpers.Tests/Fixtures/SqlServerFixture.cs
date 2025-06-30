using DotNet.Testcontainers.Containers;
using Testcontainers.MsSql;

namespace EfCore.Relational.Helpers.Tests.Fixtures;

public class SqlServerFixture : IAsyncLifetime
{
    private MsSqlContainer? _container;

    public async Task InitializeAsync()
    {
        _container = new MsSqlBuilder()
            .WithPassword("a1ckZmGjwV8VqNdBUexV")
            //.WithReuse(true)
            .Build();

        await _container.StartAsync();
        // Wait for SQL Server to be ready
        await Task.Delay(TimeSpan.FromSeconds(20));
    }

    public string GetConnectionString() => _container?.GetConnectionString().Replace("Database=master", "Database=TestDb", StringComparison.OrdinalIgnoreCase) ??
                                           throw new InvalidOperationException(
                                               "SQL Server container is not initialized.");

    public async Task EnsureSqlReadyAsync()
    {
        if (_container is null) return;
        if(_container.State == TestcontainersStates.Running)return;
        await _container.StartAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}