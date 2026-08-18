using DKNet.EfCore.Abstractions.Events;
using Mapster;
using MapsterMapper;
using Microsoft.Data.Sqlite;

namespace EfCore.Events.Tests;

/// <summary>
///     Publisher that records everything it receives for the declared-event integration tests.
/// </summary>
public sealed class DeclaredEventPublisher : DefaultEventPublisher
{
    #region Properties

    public static IList<object> Events { get; } = [];

    #endregion

    #region Methods

    public override Task PublishAsync(object eventObj, CancellationToken cancellationToken = default)
    {
        Events.Add(eventObj);
        return Task.CompletedTask;
    }

    #endregion
}

/// <summary>
///     Isolated SQLite fixture for the <c>[GenerateEvent]</c> integration scenarios. Separate from
///     <see cref="EventRunnerFixture" /> so the declared-event scenarios never share database or
///     publisher state with the pre-existing event tests.
/// </summary>
public sealed class DeclaredEventFixture : IAsyncLifetime
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
    }

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        TypeAdapterConfig.GlobalSettings.Default.MapToConstructor(true);

        Provider = new ServiceCollection()
            .AddLogging()
            .AddSingleton(TypeAdapterConfig.GlobalSettings)
            .AddScoped<IMapper, ServiceMapper>()
            .AddDbContextWithHook<DddContext>(o =>
                o.UseSqlite(_connection).UseAutoConfigModel())
            .AddEventPublisher<DddContext, DeclaredEventPublisher>()
            .BuildServiceProvider();

        var db = Provider.GetRequiredService<DddContext>();
        await db.Database.EnsureCreatedAsync();
    }

    #endregion
}