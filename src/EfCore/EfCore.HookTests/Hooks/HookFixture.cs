using Microsoft.Data.Sqlite;

namespace EfCore.HookTests.Hooks;

public sealed class HookFixture : IAsyncLifetime
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
            .AddDbContextWithHook<HookContext>(o =>
                o.UseSqlite(this._connection).UseAutoConfigModel())
            .AddHook<HookContext, HookTest>()
            .BuildServiceProvider();

        //Ensure Db Created
        var db = this.Provider.GetRequiredService<HookContext>();
        await db.Database.EnsureCreatedAsync();
    }

    #endregion
}