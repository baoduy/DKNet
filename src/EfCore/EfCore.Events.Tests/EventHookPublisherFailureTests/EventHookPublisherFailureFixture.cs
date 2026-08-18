using DKNet.EfCore.Abstractions.Events;
using Mapster;
using MapsterMapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace EfCore.Events.Tests.EventHookPublisherFailureTests;

public sealed class EventHookPublisherFailureFixture : IAsyncLifetime
{
    #region Fields

    private SqliteConnection? _connection;

    #endregion

    #region Properties

    public ServiceProvider Provider { get; private set; } = null!;

    public TestLoggerProvider LoggerProvider { get; } = new();

    #endregion

    #region Methods

    public async Task DisposeAsync()
    {
        if (_connection != null) await _connection.DisposeAsync();
    }

    public async Task InitializeAsync()
    {
        // Use a shared connection for SQLite in-memory database
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        TypeAdapterConfig.GlobalSettings.Default.MapToConstructor(true);
        TypeAdapterConfig.GlobalSettings.NewConfig<Root, EntityAddedEvent>()
            .Map(dest => dest.Id, src => src.Id)
            .Map(dest => dest.Name, src => src.Name);

        Provider = new ServiceCollection()
            .AddLogging(builder => builder.AddProvider(LoggerProvider))
            .AddSingleton(TypeAdapterConfig.GlobalSettings)
            .AddScoped<IMapper, ServiceMapper>()
            .AddDbContextWithHook<DddContext>(o =>
                o.UseSqlite(_connection).UseAutoConfigModel())
            // Failing publisher registered first, recording publisher second
            .AddEventPublisher<DddContext, FailingEventPublisher>()
            .AddEventPublisher<DddContext, RecordingEventPublisher>()
            .BuildServiceProvider();

        //Ensure Db Created
        var db = Provider.GetRequiredService<DddContext>();
        await db.Database.EnsureCreatedAsync();
    }

    #endregion
}

/// <summary>
///     Publisher that throws on every publish.
/// </summary>
public sealed class FailingEventPublisher : DefaultEventPublisher
{
    #region Methods

    public override Task PublishAsync(object eventObj, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("publisher failure");

    #endregion
}

/// <summary>
///     Publisher that records published events.
/// </summary>
public sealed class RecordingEventPublisher : DefaultEventPublisher
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
///     Captures log entries for assertion.
/// </summary>
public sealed class TestLoggerProvider : ILoggerProvider, ILogger
{
    #region Properties

    public List<(LogLevel Level, string Message, IReadOnlyList<KeyValuePair<string, object?>> State)> Entries { get; } = [];

    #endregion

    #region Methods

    public ILogger CreateLogger(string categoryName) => this;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (logLevel == LogLevel.None) return;
        Entries.Add((logLevel, formatter(state, exception), state as IReadOnlyList<KeyValuePair<string, object?>> ?? []));
    }

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public void Dispose()
    {
    }

    #endregion
}