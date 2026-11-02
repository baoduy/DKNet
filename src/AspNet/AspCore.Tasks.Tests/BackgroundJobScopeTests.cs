using System.Collections.Concurrent;
using DKNet.AspCore.Tasks;
using DKNet.AspCore.Tasks.Internals;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shouldly;

// ReSharper disable ClassNeverInstantiated.Local

namespace AspCore.Tasks.Tests;

/// <summary>
///     DRK-2484: every start-up job runs in its own DI scope, shutdown cancellation is not a job failure and the
///     failure log is a structured template. Each test awaits the host's <c>ExecuteTask</c> instead of sleeping.
/// </summary>
public class BackgroundJobScopeTests
{
    #region Fields

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    #endregion

    #region Methods

    [Fact]
    public async Task Repro_JobsSharingScopedDependency_GetDistinctInstances()
    {
        var probe = new Probe();
        var logger = new CapturingLoggerProvider();
        using var host = await StartHostAsync(logger, s =>
        {
            s.AddSingleton(probe);
            s.AddScoped<ScopedMarker>();
            s.AddBackgroundJob<MarkerJobA>();
            s.AddBackgroundJob<MarkerJobB>();
        });

        await RunOf(host).WaitAsync(Timeout);
        await host.StopAsync();

        probe.Markers.Count.ShouldBe(2);
        var distinct = probe.Markers.Distinct().Count();
        distinct.ShouldBe(2, $"REPRO-C1 distinctScopedInstances={distinct} of 2 jobs");
    }

    [Fact]
    public async Task ShutdownCancellation_IsNotLoggedAsJobFailure()
    {
        var probe = new Probe();
        var logger = new CapturingLoggerProvider();
        using var host = await StartHostAsync(logger, s =>
        {
            s.AddSingleton(probe);
            s.AddBackgroundJob<WaitForStopTask>();
        });

        await probe.Started.Task.WaitAsync(Timeout);
        await host.StopAsync();

        logger.Entries.ShouldNotContain(e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task NonCancellationFailureAfterStop_IsLoggedAsJobFailure()
    {
        var probe = new Probe();
        var logger = new CapturingLoggerProvider();
        using var host = await StartHostAsync(logger, s =>
        {
            s.AddSingleton(probe);
            s.AddBackgroundJob<FailOnStopTask>();
        });

        await probe.Started.Task.WaitAsync(Timeout);
        await host.StopAsync();

        var error = logger.Entries.Where(e => e.Level == LogLevel.Error).ShouldHaveSingleItem();
        error.Message.ShouldBe("AspCore.Tasks.Tests.BackgroundJobScopeTests+FailOnStopTask job failed");
        error.Exception.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("Cleanup failed after stop");
    }

    [Fact]
    public async Task CancellationWithoutStop_IsLoggedAsJobFailureAndSiblingCompletes()
    {
        var probe = new Probe();
        var logger = new CapturingLoggerProvider();
        using var host = await StartHostAsync(logger, s =>
        {
            s.AddSingleton(probe);
            s.AddBackgroundJob<TimeoutTask>();
            s.AddBackgroundJob<OnceTask>();
        });

        await RunOf(host).WaitAsync(Timeout);
        await host.StopAsync();

        probe.OnceRuns.ShouldBe(1);
        var error = logger.Entries.Single(e => e.Level == LogLevel.Error);
        error.Message.ShouldBe("AspCore.Tasks.Tests.BackgroundJobScopeTests+TimeoutTask job failed");
        error.Exception.ShouldBeOfType<TaskCanceledException>().Message.ShouldBe("HttpClient timeout");
    }

    [Fact]
    public async Task FailingJob_LogsStructuredJobType()
    {
        var logger = new CapturingLoggerProvider();
        using var host = await StartHostAsync(logger, s => s.AddBackgroundJob<TimeoutTask>());

        await RunOf(host).WaitAsync(Timeout);
        await host.StopAsync();

        var error = logger.Entries.Single(e => e.Level == LogLevel.Error);
        error.State.ShouldContainKeyAndValue("{OriginalFormat}", "{JobType} job failed");
        error.State.ShouldContainKeyAndValue("JobType", "AspCore.Tasks.Tests.BackgroundJobScopeTests+TimeoutTask");
    }

    [Fact]
    public async Task NoRegisteredJobs_HostStartsAndFinishesWithoutError()
    {
        var logger = new CapturingLoggerProvider();
        using var host = await StartHostAsync(logger, s => s.AddBackgroundJobFrom([]));

        await RunOf(host).WaitAsync(Timeout);
        await host.StopAsync();

        logger.Entries.ShouldContain(e => e.Level == LogLevel.Information && e.Message == "Background job host started");
        logger.Entries.ShouldContain(e => e.Level == LogLevel.Information && e.Message == "Background job host finished");
        logger.Entries.ShouldNotContain(e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task UnresolvableJob_FaultsHostRunAndIsNotLoggedAsJobFailure()
    {
        var logger = new CapturingLoggerProvider();
        using var host = await StartHostAsync(logger, s => s.AddBackgroundJob<UnresolvableTask>());

        await Should.ThrowAsync<InvalidOperationException>(RunOf(host).WaitAsync(Timeout));
        await host.StopAsync();

        logger.Entries.ShouldNotContain(e =>
            e.Message == "AspCore.Tasks.Tests.BackgroundJobScopeTests+UnresolvableTask job failed");
        logger.Entries.ShouldNotContain(e => e.Message == "Background job host finished");
    }

    [Fact]
    public async Task SameJobRegisteredTwice_RunsOnce()
    {
        var probe = new Probe();
        var logger = new CapturingLoggerProvider();
        using var host = await StartHostAsync(logger, s =>
        {
            s.AddSingleton(probe);
            s.AddBackgroundJob<OnceTask>();
            s.AddBackgroundJob<OnceTask>();
        });

        await RunOf(host).WaitAsync(Timeout);
        await host.StopAsync();

        probe.OnceRuns.ShouldBe(1);
    }

    [Fact]
    public void JobRegisteredByBothMethods_IsRegisteredOnce()
    {
        var scannedFirst = new ServiceCollection();
        scannedFirst.AddBackgroundJobFrom([typeof(BackgroundJobScopeTests).Assembly]);
        scannedFirst.AddBackgroundJob<OnceTask>();

        var typedFirst = new ServiceCollection();
        typedFirst.AddBackgroundJob<OnceTask>();
        typedFirst.AddBackgroundJobFrom([typeof(BackgroundJobScopeTests).Assembly]);

        scannedFirst.Count(IsOnceTaskJob).ShouldBe(1);
        typedFirst.Count(IsOnceTaskJob).ShouldBe(1);
    }

    [Fact]
    public async Task DisposableJob_IsDisposedAfterItsRun()
    {
        var probe = new Probe();
        var logger = new CapturingLoggerProvider();
        using var host = await StartHostAsync(logger, s =>
        {
            s.AddSingleton(probe);
            s.AddBackgroundJob<DisposableTask>();
        });

        await RunOf(host).WaitAsync(Timeout);
        await host.StopAsync();

        probe.Disposed.Task.IsCompleted.ShouldBeTrue();
        (await probe.Disposed.Task).ShouldBeTrue("disposed after RunAsync completed");
    }

    private static bool IsOnceTaskJob(ServiceDescriptor d) =>
        d.ServiceType == typeof(IBackgroundTask) && d.ImplementationType == typeof(OnceTask);

    private static Task RunOf(IHost host) =>
        host.Services.GetServices<IHostedService>().OfType<BackgroundJobHost>().Single().ExecuteTask!;

    private static async Task<IHost> StartHostAsync(CapturingLoggerProvider logger, Action<IServiceCollection> configure)
    {
        var host = new HostBuilder()
            .ConfigureLogging(b => b.ClearProviders().AddProvider(logger))
            .ConfigureServices(configure)
            .Build();
        await host.StartAsync();
        return host;
    }

    #endregion

    /// <summary>Shared observation point for the test jobs below.</summary>
    internal sealed class Probe
    {
        #region Fields

        private int _onceRuns;

        #endregion

        #region Properties

        public TaskCompletionSource<bool> Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ConcurrentBag<ScopedMarker> Markers { get; } = [];

        public int OnceRuns => _onceRuns;

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        #endregion

        #region Methods

        public void RecordRun() => Interlocked.Increment(ref _onceRuns);

        #endregion
    }

    /// <summary>A scoped dependency: one instance per DI scope.</summary>
    internal sealed class ScopedMarker;

    /// <summary>Never registered by the host under test, so a job injecting it cannot be resolved.</summary>
    internal sealed class UnregisteredDependency;

    private sealed record LogEntry(
        LogLevel Level,
        string Message,
        Exception? Exception,
        IReadOnlyDictionary<string, object?> State);

    /// <summary>Records every log call of every category, with its structured state.</summary>
    private sealed class CapturingLoggerProvider : ILoggerProvider, ILogger
    {
        #region Properties

        public ConcurrentQueue<LogEntry> Entries { get; } = new();

        #endregion

        #region Methods

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public ILogger CreateLogger(string categoryName) => this;

        public void Dispose()
        {
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var pairs = state as IEnumerable<KeyValuePair<string, object?>> ?? [];
            Entries.Enqueue(new LogEntry(logLevel, formatter(state, exception), exception,
                pairs.ToDictionary(p => p.Key, p => p.Value)));
        }

        #endregion
    }

    private sealed class MarkerJobA(ScopedMarker marker, Probe probe) : IBackgroundTask
    {
        public Task RunAsync(CancellationToken cancellationToken = default)
        {
            probe.Markers.Add(marker);
            return Task.CompletedTask;
        }
    }

    private sealed class MarkerJobB(ScopedMarker marker, Probe probe) : IBackgroundTask
    {
        public Task RunAsync(CancellationToken cancellationToken = default)
        {
            probe.Markers.Add(marker);
            return Task.CompletedTask;
        }
    }

    private sealed class WaitForStopTask(Probe probe) : IBackgroundTask
    {
        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            probe.Started.TrySetResult();
            await Task.Delay(System.Threading.Timeout.Infinite, cancellationToken);
        }
    }

    private sealed class FailOnStopTask(Probe probe) : IBackgroundTask
    {
        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            probe.Started.TrySetResult();
            try
            {
                await Task.Delay(System.Threading.Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw new InvalidOperationException("Cleanup failed after stop");
            }
        }
    }

    private sealed class TimeoutTask : IBackgroundTask
    {
        public Task RunAsync(CancellationToken cancellationToken = default) =>
            throw new TaskCanceledException("HttpClient timeout");
    }

    private sealed class OnceTask(Probe probe) : IBackgroundTask
    {
        public Task RunAsync(CancellationToken cancellationToken = default)
        {
            probe.RecordRun();
            return Task.CompletedTask;
        }
    }

    private sealed class UnresolvableTask(UnregisteredDependency dependency) : IBackgroundTask
    {
        public Task RunAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(dependency);
    }

    private sealed class DisposableTask(Probe probe) : IBackgroundTask, IAsyncDisposable
    {
        private bool _ran;

        public ValueTask DisposeAsync()
        {
            probe.Disposed.TrySetResult(_ran);
            return ValueTask.CompletedTask;
        }

        public Task RunAsync(CancellationToken cancellationToken = default)
        {
            _ran = true;
            return Task.CompletedTask;
        }
    }
}
