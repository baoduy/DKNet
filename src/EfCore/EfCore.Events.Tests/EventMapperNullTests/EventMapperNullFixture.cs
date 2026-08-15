using DKNet.EfCore.Abstractions.Events;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EfCore.Events.Tests.EventMapperNullTests;

/// <summary>
///     Enables all log levels so the EventHook's Information logging branch is exercised.
/// </summary>
public sealed class EnabledLoggerProvider : ILoggerProvider
{
    #region Methods

    public ILogger CreateLogger(string categoryName) => new EnabledLogger();

    public void Dispose()
    {
    }

    #endregion

    private sealed class EnabledLogger : ILogger
    {
        #region Methods

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }

        #endregion
    }
}

/// <summary>
///     Captures events published by the null-mapper hook; isolated from other test classes.
/// </summary>
public sealed class RecordingMapperNullPublisher : DefaultEventPublisher
{
    #region Properties

    public static IList<object> Published { get; } = [];

    #endregion

    #region Methods

    public override Task PublishAsync(object eventObj, CancellationToken cancellationToken = default)
    {
        Published.Add(eventObj);
        return Task.CompletedTask;
    }

    #endregion
}

/// <summary>
///     Fixture with NO <c>IMapper</c> registered, so <c>EventHook</c> resolves an empty
///     <c>IEnumerable&lt;IMapper&gt;</c> and dispatches with a null mapper.
/// </summary>
public sealed class EventMapperNullFixture : IAsyncLifetime
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

        Provider = new ServiceCollection()
            .AddLogging(builder => builder
                .AddProvider(new EnabledLoggerProvider())
                .SetMinimumLevel(LogLevel.Information))
            .AddEventPublisher<DddContext, RecordingMapperNullPublisher>()
            .AddDbContextWithHook<DddContext>(o =>
                o.UseSqlite(_connection).UseAutoConfigModel())
            .BuildServiceProvider();

        var db = Provider.GetRequiredService<DddContext>();
        await db.Database.EnsureCreatedAsync();
    }

    #endregion
}