using DotNet.Testcontainers.Containers;
using Testcontainers.MsSql;

namespace EfCore.Relational.Helpers.Tests.Fixtures;

public class SqlServerFixture : IAsyncLifetime
{
    #region Fields

    private readonly string _databaseName = $"TestDb_{Guid.NewGuid():N}";
    private MsSqlContainer? _container;

    #endregion

    #region Methods

    public async Task DisposeAsync()
    {
        if (_container == null) return;

        await _container.StopAsync();
        await _container.DisposeAsync();
    }

    public async Task EnsureSqlReadyAsync()
    {
        if (_container is null) return;

        if (_container.State == TestcontainersStates.Running) return;

        await _container.StartAsync();
    }

    public string GetConnectionString() =>
        _container?.GetConnectionString()
            .Replace("Database=master", $"Database={_databaseName}", StringComparison.OrdinalIgnoreCase) ??
        throw new InvalidOperationException("SQL Server container is not initialized.");

    public string CreateIsolatedConnectionString() =>
        _container?.GetConnectionString()
            .Replace("Database=master", $"Database=TestDb_{Guid.NewGuid():N}", StringComparison.OrdinalIgnoreCase) ??
        throw new InvalidOperationException("SQL Server container is not initialized.");

    public async Task InitializeAsync()
    {
        _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword($"A{Guid.NewGuid():N}a!")

            //.WithReuse(true)
            .Build();

        await _container.StartAsync();

        // Wait for SQL Server to be ready
        await Task.Delay(TimeSpan.FromSeconds(20));
    }

    #endregion
}