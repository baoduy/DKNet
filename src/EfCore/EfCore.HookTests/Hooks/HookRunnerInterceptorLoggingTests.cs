using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using HookContext = EfCore.HookTests.Data.HookContext;

namespace EfCore.HookTests.Hooks;

/// <summary>
///     Covers <see cref="HookRunnerInterceptor" />'s source-generated <c>LoggerMessage</c> methods, in
///     particular the "hooks disabled" line — previously a hand-written <c>LogInformation</c> call with no
///     <c>IsEnabled</c> guard at all. The generated method must still emit the same message text, at the
///     same level, for both the before-save and after-save runs.
/// </summary>
public class HookRunnerInterceptorLoggingTests : IAsyncLifetime
{
    #region Fields

    private readonly CapturingLoggerProvider _capturingProvider = new();
    private SqliteConnection? _connection;
    private ServiceProvider _provider = null!;

    #endregion

    #region Methods

    public async Task DisposeAsync()
    {
        if (_provider != null!) await _provider.DisposeAsync();
        if (_connection != null) await _connection.DisposeAsync();
    }

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        _provider = new ServiceCollection()
            .AddLogging(b => b.SetMinimumLevel(LogLevel.Information).AddProvider(_capturingProvider))
            .AddDbContextWithHook<HookContext>(o => o.UseSqlite(_connection).UseAutoConfigModel())
            .AddHook<HookContext, HookTest>()
            .BuildServiceProvider();

        var db = _provider.GetRequiredService<HookContext>();
        await db.Database.EnsureCreatedAsync();
    }

    [Fact]
    public async Task SaveChanges_WithHooksDisabled_LogsDisabledMessageForBothRuns()
    {
        var db = _provider.GetRequiredService<HookContext>();

        await using (db.DisableHooks())
        {
            db.Set<CustomerProfile>().Add(new CustomerProfile { Name = "Disabled Logging" });
            await db.SaveChangesAsync();
        }

        var entries = _capturingProvider.Entries.Where(e => e.Message.Contains("hooks is disabled")).ToList();

        entries.Count.ShouldBe(2);
        entries.ShouldContain(e => e.Message.Contains("The BeforeSave hooks is disabled"));
        entries.ShouldContain(e => e.Message.Contains("The AfterSave hooks is disabled"));
        entries.ShouldAllBe(e => e.LogLevel == LogLevel.Information);
    }

    #endregion

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        #region Properties

        public List<(LogLevel LogLevel, string Message)> Entries { get; } = [];

        #endregion

        #region Methods

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        public void Dispose()
        {
        }

        #endregion

        private sealed class CapturingLogger(CapturingLoggerProvider owner) : ILogger
        {
            #region Methods

            public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) =>
                owner.Entries.Add((logLevel, formatter(state, exception)));

            #endregion

            private sealed class NullScope : IDisposable
            {
                #region Fields

                public static readonly NullScope Instance = new();

                #endregion

                #region Methods

                public void Dispose()
                {
                }

                #endregion
            }
        }
    }
}
