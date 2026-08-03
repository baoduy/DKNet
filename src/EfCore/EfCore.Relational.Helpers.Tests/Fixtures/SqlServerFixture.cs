using System.Runtime.InteropServices;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Testcontainers.MsSql;

namespace EfCore.Relational.Helpers.Tests.Fixtures;

public class SqlServerFixture : IAsyncLifetime
{
    #region Fields

    // azure-sql-edge runs on ARM64 (Apple Silicon); mssql/server has no ARM64 image.
    private static readonly string MssqlImage =
        RuntimeInformation.ProcessArchitecture == Architecture.Arm64
            ? "mcr.microsoft.com/azure-sql-edge:latest"
            : "mcr.microsoft.com/mssql/server:2022-latest";

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
        _container = new MsSqlBuilder(MssqlImage)
            .WithPassword($"A{Guid.NewGuid():N}a!")
            // azure-sql-edge has no sqlcmd, so the default readiness probe fails; wait on the log line instead.
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilMessageIsLogged("SQL Server is now ready for client connections"))

            //.WithReuse(true)
            .Build();

        await _container.StartAsync();

        // Wait for SQL Server to be ready
        await Task.Delay(TimeSpan.FromSeconds(20));
    }

    #endregion
}