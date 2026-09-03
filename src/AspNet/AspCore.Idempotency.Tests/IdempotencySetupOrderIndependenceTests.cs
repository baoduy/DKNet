// <copyright file="IdempotencySetupOrderIndependenceTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.Filtering;
using DKNet.AspCore.Idempotency.Store;
using Microsoft.Extensions.Hosting;

namespace AspCore.Idempotency.Tests.Unit;

/// <summary>
///     Order-independence is the second load-bearing behaviour DRK-1005 exists to prove: whichever order
///     <c>AddIdempotentKey()</c> and a named store are called in, a named store always ends up serving
///     requests, and between two named stores the first registration wins.
/// </summary>
public sealed class IdempotencySetupOrderIndependenceTests
{
    #region Test doubles

    private sealed class FakeStoreA : IIdempotencyKeyStore
    {
        public ValueTask<(bool processed, CachedResponse? response)> IsKeyProcessedAsync(IdempotentKeyInfo keyInfo) =>
            ValueTask.FromResult<(bool, CachedResponse?)>((false, null));

        public ValueTask MarkKeyAsProcessedAsync(IdempotentKeyInfo keyInfo, CachedResponse cachedResponse) =>
            ValueTask.CompletedTask;
    }

    private sealed class FakeStoreB : IIdempotencyKeyStore
    {
        public ValueTask<(bool processed, CachedResponse? response)> IsKeyProcessedAsync(IdempotentKeyInfo keyInfo) =>
            ValueTask.FromResult<(bool, CachedResponse?)>((false, null));

        public ValueTask MarkKeyAsProcessedAsync(IdempotentKeyInfo keyInfo, CachedResponse cachedResponse) =>
            ValueTask.CompletedTask;
    }

    #endregion

    #region Methods

    [Fact]
    public void AddIdempotentKey_DefaultThenNamedStore_NamedStoreWins()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddIdempotentKey();
        services.AddIdempotentKey<FakeStoreA>();

        // Assert
        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IIdempotencyKeyStore>().ShouldBeOfType<FakeStoreA>();
    }

    [Fact]
    public void AddIdempotentKey_NamedStoreThenDefault_NamedStoreStillWins()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddIdempotentKey<FakeStoreA>();
        services.AddIdempotentKey();

        // Assert - the parameterless overload is a complete no-op once any store is registered
        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IIdempotencyKeyStore>().ShouldBeOfType<FakeStoreA>();
    }

    [Fact]
    public void AddIdempotentKey_CalledTwice_SecondCallIsNoOpAndRegistersOnlyOneWarningHostedService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddIdempotentKey();
        services.AddIdempotentKey();

        // Assert
        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IIdempotencyKeyStore>().ShouldBeOfType<IdempotencyInMemoryStore>();
        provider.GetServices<IHostedService>()
            .Count(s => s is IdempotencyInMemoryStoreWarning)
            .ShouldBe(1);
    }

    [Fact]
    public void AddIdempotentKey_TwoNamedStores_FirstRegistrationWins()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddIdempotentKey<FakeStoreA>();
        services.AddIdempotentKey<FakeStoreB>();

        // Assert
        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IIdempotencyKeyStore>().ShouldBeOfType<FakeStoreA>();
    }

    [Fact]
    public void AddIdempotentKey_DefaultThenNamedStore_NamedStoreConfigAppliesAfterDefaultConfig()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddIdempotentKey(o => o.CachePrefix = "default");
        services.AddIdempotentKey<FakeStoreA>(o => o.CachePrefix = "named");

        // Assert - when a named store replaces the default, its config decides any option both set
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<IdempotencyOptions>>();
        options.Value.CachePrefix.ShouldBe("named");
    }

    #endregion
}
