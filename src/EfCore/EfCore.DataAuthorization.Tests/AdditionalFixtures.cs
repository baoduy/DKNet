using DKNet.EfCore.Hooks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EfCore.DataAuthorization.Tests;

public sealed class EmptyOwnerKeyFixture : IAsyncLifetime
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
            .AddDataOwnerProvider<DddContext, EmptyOwnerKeyProvider>()
            .AddDbContextWithHook<DddContext>(builder =>
                builder.UseSqlite(_connection)
                    .UseAutoConfigModel())
            .BuildServiceProvider();

        var db = Provider.GetRequiredService<DddContext>();
        await db.Database.EnsureCreatedAsync();
    }

    #endregion
}

public sealed class EmptyAccessibleKeysFixture : IAsyncLifetime
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
            .AddDataOwnerProvider<DddContext, EmptyAccessibleKeysProvider>()
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
///     A fixture that simulates unrestricted access, bypassing ownership filters
///     even when accessible keys are restricted.
/// </summary>
public sealed class UnrestrictedAccessFixture : IAsyncLifetime
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
            .AddDataOwnerProvider<UnrestrictedDddContext, NonEmptyAccessibleKeysProvider>()
            .AddDbContextWithHook<UnrestrictedDddContext>(builder =>
                builder.UseSqlite(_connection)
                    .UseAutoConfigModel())
            .BuildServiceProvider();

        var db = Provider.GetRequiredService<UnrestrictedDddContext>();
        await db.Database.EnsureCreatedAsync();
    }

    #endregion
}
