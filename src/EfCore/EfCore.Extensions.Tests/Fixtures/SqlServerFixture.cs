using System.Runtime.InteropServices;
using DotNet.Testcontainers.Builders;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace EfCore.Extensions.Tests.Fixtures;

public class SqlServerFixture : IAsyncLifetime
{
    #region Fields

    // mssql/server ships x64-only images, so ARM64 hosts fall back to azure-sql-edge. A green run on
    // azure-sql-edge is not proof against real SQL Server; that gate is the x64 remote-tests.yml run.
    private static readonly string MssqlImage =
        RuntimeInformation.ProcessArchitecture == Architecture.Arm64
            ? "mcr.microsoft.com/azure-sql-edge:latest"
            : "mcr.microsoft.com/mssql/server:2022-latest";

    private readonly MsSqlContainer _sql = new MsSqlBuilder(MssqlImage)
        .WithPassword($"A{Guid.NewGuid():N}a!")
        .WithWaitStrategy(Wait.ForUnixContainer()
            .UntilMessageIsLogged("SQL Server is now ready for client connections"))
        .Build();

    #endregion

    #region Properties

    public MyDbContext? Db { get; private set; }

    #endregion

    #region Methods

    public async Task DisposeAsync()
    {
        if (Db != null) await Db.DisposeAsync();

        await _sql.StopAsync();
        await _sql.DisposeAsync();
    }

    public async Task InitializeAsync()
    {
        await _sql.StartAsync();

        // SQL Server does not create a database on first connection; EnsureCreatedAsync creates it from this name.
        var connectionString = new SqlConnectionStringBuilder(_sql.GetConnectionString())
            { InitialCatalog = $"EfExt_{Guid.NewGuid():N}" }.ConnectionString;

        var options = new DbContextOptionsBuilder()
            .UseSqlServer(connectionString)
            .UseAutoConfigModel([typeof(MyDbContext).Assembly])
            .Options;

        Db = new MyDbContext(options);

        // ponytail: SQL Server can log "ready for client connections" a moment before the sa password is
        // applied, so the first login may fail with error 18456. Ceiling: 5 attempts / 2s apart. No
        // EnableRetryOnFailure: that strategy rejects the user-initiated transactions these tests open.
        for (var attempt = 1;; attempt++)
            try
            {
                await Db.Database.EnsureCreatedAsync();
                return;
            }
            catch (SqlException ex) when (ex.Number == 18456 && attempt < 5)
            {
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
    }

    #endregion
}
