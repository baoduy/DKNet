using DKNet.EfCore.Hooks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EfCore.DataAuthorization.Tests;

public sealed class DataKeyFixture : IAsyncLifetime
{
    #region Fields

    private SqliteConnection? _connection;

    #endregion

    #region Properties

    public ServiceProvider Provider { get; private set; } = null!;

    #endregion

    #region Methods

    public async Task DisposeAsync()
    {
        if (this._connection != null)
        {
            await this._connection.DisposeAsync();
        }
    }

    public async Task InitializeAsync()
    {
        // Use a shared connection for SQLite in-memory database
        this._connection = new SqliteConnection("DataSource=:memory:");
        await this._connection.OpenAsync();

        this.Provider = new ServiceCollection()
            .AddLogging()
            .AddDataOwnerProvider<DddContext, TestDataKeyProvider>()
            .AddDbContextWithHook<DddContext>(builder =>
                builder.UseSqlite(this._connection)
                    .UseAutoConfigModel())
            .BuildServiceProvider();

        var db = this.Provider.GetRequiredService<DddContext>();
        await db.Database.EnsureCreatedAsync();
    }

    #endregion
}