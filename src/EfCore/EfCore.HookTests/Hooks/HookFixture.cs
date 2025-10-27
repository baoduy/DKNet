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
        if (_connection != null)
            await _connection.DisposeAsync();
    }

    public async Task InitializeAsync()
    {
        // Use a shared connection for SQLite in-memory database
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        Provider = new ServiceCollection()
            .AddLogging()
            .AddDbContextWithHook<HookContext>(o =>
                o.UseSqlite(_connection).UseAutoConfigModel())
            .AddHook<HookContext, HookTest>()
            .BuildServiceProvider();

        //Ensure Db Created
        var db = Provider.GetRequiredService<HookContext>();
        await db.Database.EnsureCreatedAsync();
    }

    #endregion
}