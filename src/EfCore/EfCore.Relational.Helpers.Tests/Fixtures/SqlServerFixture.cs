using DotNet.Testcontainers.Containers;
using Testcontainers.MsSql;

namespace EfCore.Relational.Helpers.Tests.Fixtures;

public class SqlServerFixture : IAsyncLifetime
{
    #region Fields

    private MsSqlContainer? _container;

    #endregion

    #region Methods

    public Task DisposeAsync() => Task.CompletedTask;

    public async Task EnsureSqlReadyAsync()
    {
        if (this._container is null)
        {
            return;
        }

        if (this._container.State == TestcontainersStates.Running)
        {
            return;
        }

        await this._container.StartAsync();
    }

    public string GetConnectionString() =>
        this._container?.GetConnectionString()
            .Replace("Database=master", "Database=TestDb", StringComparison.OrdinalIgnoreCase) ??
        throw new InvalidOperationException("SQL Server container is not initialized.");

    public async Task InitializeAsync()
    {
        this._container = new MsSqlBuilder()
            .WithPassword("a1ckZmGjwV8VqNdBUexV")

            //.WithReuse(true)
            .Build();

        await this._container.StartAsync();

        // Wait for SQL Server to be ready
        await Task.Delay(TimeSpan.FromSeconds(20));
    }

    #endregion
}