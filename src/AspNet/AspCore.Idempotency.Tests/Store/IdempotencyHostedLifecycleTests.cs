// <copyright file="IdempotencyHostedLifecycleTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Collections.Concurrent;
using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.Filtering;
using DKNet.AspCore.Idempotency.Store;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AspCore.Idempotency.Tests.Store;

/// <summary>
///     DRK-1507 §5: the idempotency startup warning must run through <see cref="IHostedLifecycleService" />,
///     offering all six lifecycle moments to a real host, doing work in the start moment only, and continuing
///     to work through the plain-singleton registration <see cref="IdempotencySetup.AddIdempotentKey()" />
///     already performs (<c>IdempotencySetup.cs:101</c>).
/// </summary>
public sealed class IdempotencyHostedLifecycleTests
{
    #region Test support

    /// <summary>
    ///     Wraps a real <see cref="IHostedService" /> instance and is what gets registered with the host, so the
    ///     host's hosted-service executor calls these six members directly on the wrapper. Each member records
    ///     its own name before forwarding to the equivalent member on <paramref name="inner" /> - the four added
    ///     moments via a hard cast to <see cref="IHostedLifecycleService" />, which throws today (the inner
    ///     service still only implements <see cref="IHostedService" />), giving a RED reason that names exactly
    ///     what is missing.
    /// </summary>
    private sealed class HostedServiceLifecycleSpy(IHostedService inner) : IHostedLifecycleService
    {
        public List<string> Invoked { get; } = [];

        public Task StartingAsync(CancellationToken cancellationToken)
        {
            Invoked.Add(nameof(StartingAsync));
            return ((IHostedLifecycleService)inner).StartingAsync(cancellationToken);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Invoked.Add(nameof(StartAsync));
            return inner.StartAsync(cancellationToken);
        }

        public Task StartedAsync(CancellationToken cancellationToken)
        {
            Invoked.Add(nameof(StartedAsync));
            return ((IHostedLifecycleService)inner).StartedAsync(cancellationToken);
        }

        public Task StoppingAsync(CancellationToken cancellationToken)
        {
            Invoked.Add(nameof(StoppingAsync));
            return ((IHostedLifecycleService)inner).StoppingAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Invoked.Add(nameof(StopAsync));
            return inner.StopAsync(cancellationToken);
        }

        public Task StoppedAsync(CancellationToken cancellationToken)
        {
            Invoked.Add(nameof(StoppedAsync));
            return ((IHostedLifecycleService)inner).StoppedAsync(cancellationToken);
        }
    }

    private static readonly string[] AllSixMoments =
    [
        nameof(IHostedLifecycleService.StartingAsync),
        nameof(IHostedService.StartAsync),
        nameof(IHostedLifecycleService.StartedAsync),
        nameof(IHostedLifecycleService.StoppingAsync),
        nameof(IHostedService.StopAsync),
        nameof(IHostedLifecycleService.StoppedAsync)
    ];

    private sealed record LogEntry(string Category, LogLevel Level, string Message);

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        public ConcurrentBag<LogEntry> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => new RecordingLogger(categoryName, Entries);

        public void Dispose()
        {
        }

        private sealed class RecordingLogger(string category, ConcurrentBag<LogEntry> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) =>
                entries.Add(new LogEntry(category, logLevel, formatter(state, exception)));
        }
    }

    private sealed class NoopDurableStore : IIdempotencyKeyStore
    {
        public ValueTask<(bool processed, CachedResponse? response)> IsKeyProcessedAsync(IdempotentKeyInfo keyInfo) =>
            ValueTask.FromResult<(bool, CachedResponse?)>((false, null));

        public ValueTask MarkKeyAsProcessedAsync(IdempotentKeyInfo keyInfo, CachedResponse cachedResponse) =>
            ValueTask.CompletedTask;
    }

    private static bool IsWarningLog(LogEntry entry) =>
        entry.Category.Contains(nameof(IdempotencyInMemoryStoreWarning), StringComparison.Ordinal) &&
        entry.Level == LogLevel.Warning;

    #endregion

    #region Methods

    [Fact]
    public async Task Warning_HostStartsAndStopsNormally_OffersAllSixMomentsAndLogsOnlyOnceInStartMoment()
    {
        // Arrange - a real minimal host, the in-memory store resolved exactly as production does
        var recordingLoggerProvider = new RecordingLoggerProvider();
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(recordingLoggerProvider);
        builder.Services.AddSingleton<IIdempotencyKeyStore>(sp =>
            new IdempotencyInMemoryStore(
                Options.Create(new IdempotencyOptions()),
                sp.GetRequiredService<ILogger<IdempotencyInMemoryStore>>()));

        HostedServiceLifecycleSpy? spy = null;
        builder.Services.AddSingleton<IHostedService>(sp =>
        {
            spy = new HostedServiceLifecycleSpy(ActivatorUtilities.CreateInstance<IdempotencyInMemoryStoreWarning>(sp));
            return spy;
        });

        using var host = builder.Build();

        // Act - drive the real host through a normal start and stop
        await host.StartAsync();
        await host.StopAsync();

        // Assert - all six moments reached the service instance, in host order (§3 row 1)
        spy.ShouldNotBeNull();
        spy!.Invoked.ShouldBe(AllSixMoments);

        // Assert - the warning still fires exactly once, and no other moment logged anything (§3 rows 4-5, R1, R3)
        recordingLoggerProvider.Entries.Count(IsWarningLog).ShouldBe(1);
    }

    [Fact]
    public async Task Warning_HostStartsWithDurableStoreRegistered_OffersAllSixMomentsButLogsNoWarning()
    {
        // Arrange - same host shape, but the resolved store is not IdempotencyInMemoryStore
        var recordingLoggerProvider = new RecordingLoggerProvider();
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(recordingLoggerProvider);
        builder.Services.AddSingleton<IIdempotencyKeyStore, NoopDurableStore>();

        HostedServiceLifecycleSpy? spy = null;
        builder.Services.AddSingleton<IHostedService>(sp =>
        {
            spy = new HostedServiceLifecycleSpy(ActivatorUtilities.CreateInstance<IdempotencyInMemoryStoreWarning>(sp));
            return spy;
        });

        using var host = builder.Build();

        // Act
        await host.StartAsync();
        await host.StopAsync();

        // Assert - the service still offers every moment; it just chooses not to log with a durable store (R1)
        spy.ShouldNotBeNull();
        spy!.Invoked.ShouldBe(AllSixMoments);
        recordingLoggerProvider.Entries.Count(IsWarningLog).ShouldBe(0);
    }

    [Fact]
    public void AddIdempotentKey_RegisteredWarningInstance_IsAssignableToHostedLifecycleService()
    {
        // Arrange - the exact plain-singleton registration IdempotencySetup.cs:101 performs today,
        // through IdempotencySetup.AddIdempotentKey() unmodified (R5)
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddIdempotentKey();

        // Assert
        using var provider = services.BuildServiceProvider();
        var warning = provider.GetServices<IHostedService>().OfType<IdempotencyInMemoryStoreWarning>().Single();
        warning.ShouldBeAssignableTo<IHostedLifecycleService>();
    }

    #endregion
}
