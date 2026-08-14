// Acceptance tests for inline (awaited) audit-log publishing in EfCoreAuditHook.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.AuditLogs;
using DKNet.EfCore.Hooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace EfCore.AuditLogs.Tests;

public class EfCoreAuditHookPublishingTests
{
    #region Helpers

    private static string NewDbPath() =>
        Path.Combine(Path.GetTempPath(), $"auditlog_publish_{Guid.NewGuid():N}.db");

    private static ServiceProvider BuildProvider(Action<IServiceCollection> registerPublishers, string dbPath)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEfCoreAuditHook<TestAuditDbContext>();
        registerPublishers(services);
        services.AddDbContextWithHook<TestAuditDbContext>((_, o) =>
            o.UseSqlite($"Data Source={dbPath}"));
        return services.BuildServiceProvider();
    }

    private static void RegisterKeyedPublisher(IServiceCollection services, IAuditLogPublisher publisher) =>
        services.AddKeyedScoped<IAuditLogPublisher>(typeof(TestAuditDbContext).FullName, (_, _) => publisher);

    #endregion

    #region Tests

    [Fact]
    public async Task PublishAsync_IsAwaitedInline_BeforeSaveChangesReturns()
    {
        var gated = new GatedPublisher();
        await using var provider = BuildProvider(s => RegisterKeyedPublisher(s, gated), NewDbPath());
        await using var scope = provider.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<TestAuditDbContext>();
        await ctx.Database.EnsureCreatedAsync();

        var e = new TestAuditEntity { Name = "Gated", Age = 1, IsActive = true, Balance = 1m };
        e.SetCreatedOn("creator");
        ctx.Add(e);

        var saveTask = ctx.SaveChangesAsync();

        var sw = Stopwatch.StartNew();
        while (!gated.EnteredPublish && sw.ElapsedMilliseconds < 5000) await Task.Delay(10);
        gated.EnteredPublish.ShouldBeTrue();
        saveTask.IsCompleted.ShouldBeFalse();

        gated.Release();
        await saveTask;
        gated.PublishCompleted.ShouldBeTrue();
    }

    [Fact]
    public async Task OneFailingPublisher_DoesNotBlockRemaining_AndDoesNotThrow()
    {
        var capturing = new CapturingPublisher();
        await using var provider = BuildProvider(s =>
        {
            RegisterKeyedPublisher(s, new FailingPublisher());
            RegisterKeyedPublisher(s, capturing);
        }, NewDbPath());
        await using var scope = provider.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<TestAuditDbContext>();
        await ctx.Database.EnsureCreatedAsync();

        var e = new TestAuditEntity { Name = "OneFail", Age = 1, IsActive = true, Balance = 1m };
        e.SetCreatedOn("creator");
        ctx.Add(e);

        await ctx.SaveChangesAsync(); // must not throw despite the failing publisher

        capturing.CallCount.ShouldBe(1);
        capturing.Received.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task FailingPublisher_LogsError_ContainingSerializedEntries()
    {
        var logProvider = new CapturingLoggerProvider();
        await using var provider = BuildProvider(s =>
        {
            s.AddLogging(b => b.AddProvider(logProvider));
            RegisterKeyedPublisher(s, new AsyncFailingPublisher());
        }, NewDbPath());
        await using var scope = provider.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<TestAuditDbContext>();
        await ctx.Database.EnsureCreatedAsync();

        var e = new TestAuditEntity { Name = "PayloadUser", Age = 7, IsActive = true, Balance = 9.5m };
        e.SetCreatedOn("payload-creator");
        ctx.Add(e);

        await ctx.SaveChangesAsync(); // must not throw

        logProvider.Messages.ShouldContain(m =>
            m.Level == LogLevel.Error &&
            m.Message.Contains("TestAuditEntity") &&
            m.Message.Contains("payload-creator"));
    }

    [Fact]
    public async Task PublishAsync_ReceivesCallersCancellationToken_NotNone()
    {
        var capturing = new CapturingPublisher();
        await using var provider = BuildProvider(s => RegisterKeyedPublisher(s, capturing), NewDbPath());
        await using var scope = provider.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<TestAuditDbContext>();
        await ctx.Database.EnsureCreatedAsync();

        var e = new TestAuditEntity { Name = "Token", Age = 1, IsActive = true, Balance = 1m };
        e.SetCreatedOn("creator");
        ctx.Add(e);

        using var cts = new CancellationTokenSource();
        await ctx.SaveChangesAsync(cts.Token);

        capturing.ReceivedToken.ShouldNotBe(CancellationToken.None);
        capturing.ReceivedToken.ShouldBe(cts.Token);
    }

    [Fact]
    public async Task FailingPublisher_WithUnserializableEntry_LogsCountFallback()
    {
        var logProvider = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(logProvider));
        services.AddEfCoreAuditHook<DoubleValuedDbContext>();
        services.AddKeyedScoped<IAuditLogPublisher>(typeof(DoubleValuedDbContext).FullName,
            (_, _) => (IAuditLogPublisher)new AsyncFailingPublisher());
        services.AddDbContextWithHook<DoubleValuedDbContext>((_, o) =>
            o.UseSqlite($"Data Source={NewDbPath()}"));
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<DoubleValuedDbContext>();
        await ctx.Database.EnsureCreatedAsync();

        var e = new DoubleValuedAuditEntity { Score = 1.0 };
        e.SetCreatedOn("creator");
        ctx.Add(e);
        await ctx.SaveChangesAsync();

        e.Score = double.NaN; // NaN cannot be serialized to JSON -> payload fallback
        e.SetUpdatedOn("updater");
        await ctx.SaveChangesAsync(); // must not throw

        logProvider.Messages.ShouldContain(m =>
            m.Level == LogLevel.Error && m.Message.Contains("Entries count"));
    }

    [Fact]
    public async Task ZeroPublishers_SaveChangesCompletesWithoutError()
    {
        await using var provider = BuildProvider(_ => { }, NewDbPath());
        await using var scope = provider.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<TestAuditDbContext>();
        await ctx.Database.EnsureCreatedAsync();

        var e = new TestAuditEntity { Name = "NoPubs", Age = 1, IsActive = true, Balance = 1m };
        e.SetCreatedOn("creator");
        ctx.Add(e);

        await ctx.SaveChangesAsync(); // must not throw with no publishers registered
    }

    #endregion
}

#region Test doubles

internal sealed class GatedPublisher : IAuditLogPublisher
{
    #region Fields

    private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _entered;
    private int _completed;

    #endregion

    #region Properties

    public bool EnteredPublish => Volatile.Read(ref _entered) == 1;

    public bool PublishCompleted => Volatile.Read(ref _completed) == 1;

    public CancellationToken ReceivedToken { get; private set; }

    #endregion

    #region Methods

    public void Release() => _gate.TrySetResult();

    public async Task PublishAsync(IEnumerable<AuditLogEntry> logs, CancellationToken cancellationToken = default)
    {
        ReceivedToken = cancellationToken;
        Volatile.Write(ref _entered, 1);
        await _gate.Task;
        Volatile.Write(ref _completed, 1);
    }

    #endregion
}

internal sealed class CapturingPublisher : IAuditLogPublisher
{
    #region Properties

    public int CallCount { get; private set; }

    public List<AuditLogEntry> Received { get; } = [];

    public CancellationToken ReceivedToken { get; private set; }

    #endregion

    #region Methods

    public Task PublishAsync(IEnumerable<AuditLogEntry> logs, CancellationToken cancellationToken = default)
    {
        CallCount++;
        ReceivedToken = cancellationToken;
        Received.AddRange(logs);
        return Task.CompletedTask;
    }

    #endregion
}

internal sealed class DoubleValuedAuditEntity : AuditedEntity<Guid>
{
    #region Properties

    public double Score { get; set; }

    #endregion

    #region Methods

    public void SetCreatedOn(string byUser, DateTimeOffset? on = null) => SetCreatedBy(byUser, on);
    public void SetUpdatedOn(string byUser, DateTimeOffset? on = null) => SetUpdatedBy(byUser, on);

    #endregion
}

internal sealed class DoubleValuedDbContext(DbContextOptions<DoubleValuedDbContext> options) : DbContext(options)
{
    #region Properties

    public DbSet<DoubleValuedAuditEntity> Entities => Set<DoubleValuedAuditEntity>();

    #endregion

    #region Methods

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Store the double as TEXT so NaN/Infinity survive the SQLite write (which rejects them as REAL);
        // the change tracker still sees the CLR double, letting the audit entry carry a non-serializable NaN.
        modelBuilder.Entity<DoubleValuedAuditEntity>()
            .Property(e => e.Score)
            .HasConversion(
                v => v.ToString(CultureInfo.InvariantCulture),
                s => double.Parse(s, CultureInfo.InvariantCulture));
    }

    #endregion
}

internal sealed class AsyncFailingPublisher : IAuditLogPublisher
{
    #region Methods

    public async Task PublishAsync(IEnumerable<AuditLogEntry> logs, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        throw new InvalidOperationException("Simulated async failure");
    }

    #endregion
}

internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    #region Fields

    private readonly ConcurrentQueue<(LogLevel Level, string Message)> _messages = new();

    #endregion

    #region Properties

    public IReadOnlyCollection<(LogLevel Level, string Message)> Messages => _messages;

    #endregion

    #region Methods

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(_messages);

    public void Dispose()
    {
    }

    #endregion

    private sealed class CapturingLogger(ConcurrentQueue<(LogLevel Level, string Message)> messages) : ILogger
    {
        #region Methods

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            messages.Enqueue((logLevel, formatter(state, exception)));

        #endregion
    }
}

#endregion
