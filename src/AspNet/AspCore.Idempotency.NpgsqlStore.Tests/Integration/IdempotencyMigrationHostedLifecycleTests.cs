// <copyright file="IdempotencyMigrationHostedLifecycleTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Collections.Concurrent;
using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.Filtering;
using DKNet.AspCore.Idempotency.Relational.Store;
using DKNet.AspCore.Idempotency.Store;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;

namespace AspCore.Idempotency.NpgsqlStore.Tests.Integration;

/// <summary>
///     DRK-1507 §5: the idempotency schema migration must run through <see cref="IHostedLifecycleService" />,
///     offering all six lifecycle moments to a real host with a real PostgreSQL database (§3 row 3), and the
///     warning service must log nothing once a durable PostgreSQL store replaces the in-memory default (§3 row
///     5). Runs against its own container - not the shared <c>ApiFixture</c>/<c>ApiCollection</c> - so it can
///     observe the host before and after an explicit, controlled start/stop.
/// </summary>
public sealed class IdempotencyMigrationHostedLifecycleTests : IAsyncLifetime
{
    #region Fields

    private readonly string _databaseName = $"Idem_{Guid.NewGuid():N}";
    private PostgreSqlContainer? _container;
    private string _connectionString = string.Empty;

    #endregion

    #region Test support

    /// <summary>
    ///     See <c>AspCore.Idempotency.Tests.Store.IdempotencyHostedLifecycleTests.HostedServiceLifecycleSpy</c> for
    ///     the full rationale: this wraps the real, DI-constructed migration service and is what the host calls
    ///     directly, recording each of the six moments before forwarding to the real instance.
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

    private sealed record LogEntry(string Category, LogLevel Level);

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
                entries.Add(new LogEntry(category, logLevel));
        }
    }

    #endregion

    #region Methods

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder("postgres:16-alpine").WithCleanUp(true).Build();
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString()
            .Replace("Database=postgres", $"Database={_databaseName}", StringComparison.OrdinalIgnoreCase);
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.StopAsync();
            await _container.DisposeAsync();
        }
    }

    [Fact]
    public async Task Migration_HostStartsAndStopsNormally_OffersAllSixLifecycleMoments()
    {
        // Arrange - the exact production registration, then swap the resolved hosted-service instance for
        // an observing spy so double migration work isn't run against the same database (§3 row 3)
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddIdempotencyNpgsqlStore(_connectionString);

        var originalDescriptor = builder.Services.Single(sd =>
            sd.ServiceType == typeof(IHostedService) &&
            sd.ImplementationType == typeof(IdempotencyMigrationHostedService<IdempotencyDbContext>));
        builder.Services.Remove(originalDescriptor);

        HostedServiceLifecycleSpy? spy = null;
        builder.Services.AddSingleton<IHostedService>(sp =>
        {
            spy = new HostedServiceLifecycleSpy(
                ActivatorUtilities.CreateInstance<IdempotencyMigrationHostedService<IdempotencyDbContext>>(sp));
            return spy;
        });

        using var host = builder.Build();

        // Act
        await host.StartAsync();
        await host.StopAsync();

        // Assert
        spy.ShouldNotBeNull();
        spy!.Invoked.ShouldBe(AllSixMoments);

        await using var dbContext = host.Services.GetRequiredService<IDbContextFactory<IdempotencyDbContext>>()
            .CreateDbContext();
        (await dbContext.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Warning_HostStartsWithPostgresStoreRegistered_LogsNoWarning()
    {
        // Arrange - an app that registers the default (in-memory + warning) first, then swaps in the durable
        // PostgreSQL store, exactly as IdempotencySetup's replacement rules allow (§3 row 5)
        var recordingLoggerProvider = new RecordingLoggerProvider();
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(recordingLoggerProvider);

        builder.Services.AddIdempotentKey();
        builder.Services.AddIdempotencyWithNpgsqlStore(_connectionString);

        using var host = builder.Build();

        // Act
        await host.StartAsync();
        await host.StopAsync();

        // Assert - the warning hosted service is still present and ran (two hosted services: warning +
        // migration), but the resolved store is no longer IdempotencyInMemoryStore, so it logs nothing (R1).
        // Referenced by category name, not type: IdempotencyInMemoryStoreWarning is internal to
        // DKNet.AspCore.Idempotency, which has no InternalsVisibleTo for this project.
        host.Services.GetServices<IHostedService>().Count().ShouldBe(2);
        recordingLoggerProvider.Entries.Count(e =>
                e.Category.Contains("IdempotencyInMemoryStoreWarning", StringComparison.Ordinal) &&
                e.Level == LogLevel.Warning)
            .ShouldBe(0);
    }

    [Fact]
    public void IdempotencyMigrationHostedService_StaysInternal()
    {
        // §3 row 7 - the migration service's half of "an application needs no source change". Lives here rather
        // than AspCore.Idempotency.Tests/Architecture/ (the brief's literal location) because that project has no
        // InternalsVisibleTo from DKNet.AspCore.Idempotency.Relational and adding one is a production-file change
        // out of scope for an acceptance-tests-only run; this project already has that visibility for the
        // Npgsql-store checks above.
        typeof(IdempotencyMigrationHostedService<>).IsPublic.ShouldBeFalse();
        typeof(IdempotencyMigrationHostedService<>).IsVisible.ShouldBeFalse();
    }

    #endregion
}
