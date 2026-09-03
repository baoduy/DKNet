// <copyright file="IdempotencyInMemoryStoreWarningTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.Filtering;
using DKNet.AspCore.Idempotency.Store;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AspCore.Idempotency.Tests.Store;

/// <summary>
///     The startup warning is conditional both ways: present while the in-process store serves requests,
///     absent once a named store replaces it. Both directions are asserted here.
/// </summary>
public sealed class IdempotencyInMemoryStoreWarningTests
{
    #region Methods

    private sealed class RecordingLogger : ILogger<IdempotencyInMemoryStoreWarning>
    {
        public List<LogLevel> Levels { get; } = new();

        public List<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Levels.Add(logLevel);
            Messages.Add(formatter(state, exception));
        }
    }

    private sealed class NoopStore : IIdempotencyKeyStore
    {
        public ValueTask<(bool processed, CachedResponse? response)> IsKeyProcessedAsync(IdempotentKeyInfo keyInfo) =>
            ValueTask.FromResult<(bool, CachedResponse?)>((false, null));

        public ValueTask MarkKeyAsProcessedAsync(IdempotentKeyInfo keyInfo, CachedResponse cachedResponse) =>
            ValueTask.CompletedTask;
    }

    [Fact]
    public async Task StartAsync_WhenResolvedStoreIsInMemoryStore_LogsWarning()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IIdempotencyKeyStore>(
            new IdempotencyInMemoryStore(
                Microsoft.Extensions.Options.Options.Create(new IdempotencyOptions()),
                NullLogger<IdempotencyInMemoryStore>.Instance));
        var logger = new RecordingLogger();
        var sut = new IdempotencyInMemoryStoreWarning(services.BuildServiceProvider(), logger);

        // Act
        await sut.StartAsync(CancellationToken.None);

        // Assert - not just that some warning fired, but that its message conveys all three properties
        // DRK-1005 §5 requires: process-local, lost on restart, not shared between instances. A logger that
        // discards state/formatter (as this one used to) would pass for any warning whatsoever.
        logger.Levels.ShouldContain(LogLevel.Warning);
        var message = logger.Messages[logger.Levels.IndexOf(LogLevel.Warning)];
        message.ShouldContain("process's memory");
        message.ShouldContain("lost on restart");
        message.ShouldContain("not shared between instances");
    }

    [Fact]
    public async Task StartAsync_WhenResolvedStoreIsNamedStore_DoesNotLogWarning()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IIdempotencyKeyStore>(new NoopStore());
        var logger = new RecordingLogger();
        var sut = new IdempotencyInMemoryStoreWarning(services.BuildServiceProvider(), logger);

        // Act
        await sut.StartAsync(CancellationToken.None);

        // Assert
        logger.Levels.ShouldNotContain(LogLevel.Warning);
    }

    [Fact]
    public async Task StopAsync_CompletesSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IIdempotencyKeyStore>(new NoopStore());
        var sut = new IdempotencyInMemoryStoreWarning(services.BuildServiceProvider(), new RecordingLogger());

        // Act
        var exception = await Record.ExceptionAsync(() => sut.StopAsync(CancellationToken.None));

        // Assert
        exception.ShouldBeNull();
    }

    #endregion
}
