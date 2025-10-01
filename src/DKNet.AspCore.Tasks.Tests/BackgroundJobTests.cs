using System.Collections.Concurrent;
using System.Reflection;
using DKNet.AspCore.Tasks.Internals;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shouldly;

// ReSharper disable ClassNeverInstantiated.Local

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace DKNet.AspCore.Tasks.Tests;

public class BackgroundJobTests
{
    private static void ResetHostAddedFlag()
    {
        var type = typeof(TaskSetups);
        var field = type.GetField("_added", BindingFlags.Static | BindingFlags.NonPublic);
        field!.SetValue(null, false);
    }

    private sealed class CountingTask(TaskCompletionSource<bool> tcs, Counter counter) : IBackgroundTask
    {
        public Task RunAsync(CancellationToken cancellationToken = default)
        {
            counter.Increment();
            tcs.TrySetResult(true);
            return Task.CompletedTask;
        }
    }

    private sealed class DelayedTask(Counter counter) : IBackgroundTask
    {
        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            await Task.Delay(50, cancellationToken);
            counter.Increment();
        }
    }

    private sealed class FailingTask(Counter counter) : IBackgroundTask
    {
        public Task RunAsync(CancellationToken cancellationToken = default)
        {
            counter.Increment();
            throw new InvalidOperationException("Boom");
        }
    }

    private sealed class ScannedTask(Counter counter) : IBackgroundTask
    {
        public Task RunAsync(CancellationToken cancellationToken = default)
        {
            counter.Increment();
            return Task.CompletedTask;
        }
    }

    private sealed class Counter
    {
        private int _count;
        public void Increment() => Interlocked.Increment(ref _count);
        public int Value => _count;
    }

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

    private sealed class TestLoggerProvider(ConcurrentBag<LogEntry> sink) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new TestLogger(sink);

        public void Dispose()
        {
        }

        private sealed class TestLogger(ConcurrentBag<LogEntry> sink) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                sink.Add(new LogEntry(logLevel, formatter(state, exception), exception));
            }
        }
    }

    [Fact]
    public async Task HostExecutesRegisteredJobOnStart()
    {
        ResetHostAddedFlag();
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var counter = new Counter();

        using var host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(tcs);
                services.AddSingleton(counter);
                services.AddBackgroundJob<CountingTask>();
            })
            .Build();

        await host.StartAsync();
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(tcs.Task, completed);
        Assert.True(tcs.Task.Result);
        Assert.Equal(1, counter.Value);
        await host.StopAsync();
    }

    [Fact]
    public async Task MultipleJobsExecuteConcurrentlyAndComplete()
    {
        ResetHostAddedFlag();
        var counter = new Counter();
        using var host = new HostBuilder()
            .ConfigureServices(s =>
            {
                s.AddSingleton(counter);
                s.AddBackgroundJob<DelayedTask>();
                s.AddBackgroundJob<CountingTask>(); // Needs TCS and Counter
                s.AddSingleton(new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
            })
            .Build();

        await host.StartAsync();
        // Allow some time for jobs to run
        await Task.Delay(300);
        Assert.Equal(2, counter.Value); // Both jobs incremented
        await host.StopAsync();
    }

    [Fact]
    public async Task FailingJobDoesNotPreventOtherJobsAndLogsError()
    {
        ResetHostAddedFlag();
        var counter = new Counter();
        var logs = new ConcurrentBag<LogEntry>();
        using var host = new HostBuilder()
            .ConfigureLogging(b => b.ClearProviders().AddProvider(new TestLoggerProvider(logs)))
            .ConfigureServices(s =>
            {
                s.AddSingleton(counter);
                s.AddBackgroundJob<FailingTask>();
                s.AddBackgroundJob<DelayedTask>();
            })
            .Build();

        await host.StartAsync();
        await Task.Delay(300);
        await host.StopAsync();

        // Failing job increments then throws, delayed increments after
        Assert.Equal(2, counter.Value);
        Assert.Contains(logs,
            l => l.Level == LogLevel.Information && l.Message.Contains("started", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(logs,
            l => l.Level == LogLevel.Information && l.Message.Contains("finished", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(logs, l => l.Level == LogLevel.Error && l.Exception is InvalidOperationException);
    }

    [Fact]
    public void AddBackgroundJobRegistersHostOnlyOnce()
    {
        ResetHostAddedFlag();
        var services = new ServiceCollection();
        services.AddBackgroundJob<CountingTask>();
        services.AddBackgroundJob<DelayedTask>();
        var hostDescriptors = services.Where(d =>
            d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(BackgroundJobHost)).ToList();
        Assert.Single(hostDescriptors);
    }

    [Fact]
    public void AddBackgroundJobFromScansAssemblyForJobs()
    {
        ResetHostAddedFlag();
        var services = new ServiceCollection();
        services.AddSingleton(new Counter());
        services.AddSingleton(new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
        services.AddBackgroundJobFrom([typeof(BackgroundJobTests).Assembly]);

        var provider = services.BuildServiceProvider();
        var jobs = provider.GetServices<IBackgroundTask>().ToList();
        jobs.Count.ShouldBeGreaterThan(1);
        jobs.Any(j => j is ScannedTask).ShouldBeTrue();
    }
}