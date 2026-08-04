using DotNet.Testcontainers.Containers;
using Testcontainers.PostgreSql;

namespace EfCore.Relational.Helpers.Tests.Fixtures;

public class PostgresFixture : IAsyncLifetime
{
    #region Fields

    private readonly string _databaseName = $"TestDb_{Guid.NewGuid():N}";
    private PostgreSqlContainer? _container;

    #endregion

    #region Methods

    public async Task DisposeAsync()
    {
        if (_container == null) return;

        await _container.StopAsync();
        await _container.DisposeAsync();
    }

    public async Task EnsureReadyAsync()
    {
        if (_container is null) return;

        if (_container.State == TestcontainersStates.Running) return;

        await _container.StartAsync();
    }

    public string GetConnectionString() =>
        _container?.GetConnectionString()
            .Replace("Database=postgres", $"Database={_databaseName}", StringComparison.OrdinalIgnoreCase) ??
        throw new InvalidOperationException("PostgreSQL container is not initialized.");

    public string CreateIsolatedConnectionString() =>
        _container?.GetConnectionString()
            .Replace("Database=postgres", $"Database=TestDb_{Guid.NewGuid():N}", StringComparison.OrdinalIgnoreCase) ??
        throw new InvalidOperationException("PostgreSQL container is not initialized.");

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder("postgres:16-alpine").Build();

        await _container.StartAsync();
    }

    #endregion
}
