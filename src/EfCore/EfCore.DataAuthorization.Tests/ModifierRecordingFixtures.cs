using DKNet.EfCore.Hooks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EfCore.DataAuthorization.Tests;

/// <summary>
///     Shared <see cref="DddContext" /> fixture wired with <see cref="MultiKeyOwnerProvider" /> ("Steven", also
///     accessible "Bob") — used across the automatic-modifier-recording scenarios that don't need a throwing
///     save path, so a class-wide fixture is safe.
/// </summary>
public sealed class ModifierRecordingFixture : IAsyncLifetime
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
        if (_connection != null) await _connection.DisposeAsync();
        await Provider.DisposeAsync();
    }

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        Provider = new ServiceCollection()
            .AddLogging()
            .AddDataOwnerProvider<DddContext, MultiKeyOwnerProvider>()
            .AddDbContextWithHook<DddContext>(builder =>
                builder.UseSqlite(_connection)
                    .UseAutoConfigModel())
            .BuildServiceProvider();

        var db = Provider.GetRequiredService<DddContext>();
        await db.Database.EnsureCreatedAsync();
    }

    #endregion
}

/// <summary>
///     A <see cref="PlainDbContext" /> fixture with no data-authorization setup at all, used to prove a
///     consumer that has not opted in gets no automatic modifier recording.
/// </summary>
public sealed class OptedOutConsumerFixture : IAsyncLifetime
{
    #region Fields

    private SqliteConnection? _connection;

    #endregion

    #region Properties

    public PlainDbContext Db { get; private set; } = null!;

    #endregion

    #region Methods

    public async Task DisposeAsync()
    {
        await Db.DisposeAsync();
        if (_connection != null) await _connection.DisposeAsync();
    }

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        Db = new PlainDbContext(new DbContextOptionsBuilder<PlainDbContext>().UseSqlite(_connection).Options);
        await Db.Database.EnsureCreatedAsync();
    }

    #endregion
}
