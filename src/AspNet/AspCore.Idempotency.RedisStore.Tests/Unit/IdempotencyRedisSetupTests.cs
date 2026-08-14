// <copyright file="IdempotencyRedisSetupTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.Store;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

namespace AspCore.Idempotency.RedisStore.Tests.Unit;

/// <summary>
///     Tests for <see cref="IdempotencyRedisSetup" />'s DI registration extension methods.
/// </summary>
public sealed class IdempotencyRedisSetupTests
{
    #region Methods

    [Fact]
    public void AddIdempotencyRedisStore_WithConnectionString_NullServices_Throws()
    {
        IServiceCollection services = null!;

        Should.Throw<ArgumentNullException>(() => services.AddIdempotencyRedisStore("localhost:6379"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddIdempotencyRedisStore_WithConnectionString_InvalidConnectionString_Throws(string? connectionString)
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentException>(() => services.AddIdempotencyRedisStore(connectionString!));
    }

    [Fact]
    public void AddIdempotencyRedisStore_WithConnectionString_RegistersConfiguredDistributedCache()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddIdempotencyRedisStore("localhost:6379");

        // Assert
        result.ShouldBe(services);
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IDistributedCache>().ShouldNotBeNull();
        provider.GetRequiredService<IOptions<RedisCacheOptions>>().Value.Configuration.ShouldBe("localhost:6379");
    }

    [Fact]
    public void AddIdempotencyRedisStore_WithMultiplexer_NullServices_Throws()
    {
        IServiceCollection services = null!;
        var multiplexer = new Mock<IConnectionMultiplexer>().Object;

        Should.Throw<ArgumentNullException>(() => services.AddIdempotencyRedisStore(multiplexer));
    }

    [Fact]
    public void AddIdempotencyRedisStore_WithMultiplexer_NullMultiplexer_Throws()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() => services.AddIdempotencyRedisStore((IConnectionMultiplexer)null!));
    }

    [Fact]
    public void AddIdempotencyRedisStore_WithMultiplexer_RegistersSameInstanceAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        var multiplexer = new Mock<IConnectionMultiplexer>().Object;

        // Act
        var result = services.AddIdempotencyRedisStore(multiplexer);

        // Assert
        result.ShouldBe(services);
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IConnectionMultiplexer>().ShouldBeSameAs(multiplexer);
    }

    [Fact]
    public void AddIdempotencyWithRedisStore_RegistersIdempotencyRedisStoreAsTheKeyStore()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        var multiplexer = new Mock<IConnectionMultiplexer>().Object;
        services.AddIdempotencyRedisStore(multiplexer);

        // Act - the multiplexer-based registration path is the one the README documents as already-wired;
        // AddIdempotentKey<IdempotencyRedisStore>() only needs IConnectionMultiplexer, which this path provides.
        services.AddIdempotentKey<IdempotencyRedisStore>();

        // Assert
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IIdempotencyKeyStore>().ShouldBeOfType<IdempotencyRedisStore>();
    }

    [Fact]
    public void AddIdempotencyWithRedisStore_ConnectionStringQuickStart_RegistersConnectionMultiplexer()
    {
        // Arrange - this is the README's own "Quick Start" call shape: a single
        // AddIdempotencyWithRedisStore(connectionString, config) call with nothing else registered.
        var services = new ServiceCollection();

        // Act
        services.AddIdempotencyWithRedisStore("localhost:6379", o => o.Expiration = TimeSpan.FromHours(24));

        // Assert - the connection-string overload now registers an IConnectionMultiplexer alongside
        // IDistributedCache, so IdempotencyRedisStore's constructor dependency is satisfied. Asserting on the
        // descriptor (not resolving it) keeps this test free of a live Redis dependency: actually resolving
        // IConnectionMultiplexer opens a real socket to "localhost:6379", which no CI runner provides.
        services.ShouldContain(sd => sd.ServiceType == typeof(IConnectionMultiplexer));
        services.ShouldContain(sd => sd.ServiceType == typeof(IIdempotencyKeyStore));
    }

    #endregion
}
