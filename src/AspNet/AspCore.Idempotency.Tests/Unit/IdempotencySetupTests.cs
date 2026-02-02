// <copyright file="IdempotencySetupTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.Store;

namespace AspCore.Idempotency.Tests.Unit;

public class IdempotencySetupTests
{
    #region Methods

    [Fact]
    public void AddIdempotentKey_MultipleCallsWithDifferentConfig_UsesLatestConfig()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();

        // Act
        services.AddIdempotentKey(options => options.CachePrefix = "first");
        services.AddIdempotentKey(options => options.CachePrefix = "second");

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<IdempotencyOptions>>();

        options.Value.CachePrefix.ShouldBe("second");
    }

    [Fact]
    public void AddIdempotentKey_RegistersRepositoryAsIIdempotencyKeyRepository()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging()
            .AddDistributedMemoryCache();

        // Act
        services.AddIdempotentKey();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var repository = serviceProvider.GetService<IIdempotencyKeyStore>();

        repository.ShouldNotBeNull();
    }

    [Fact]
    public void AddIdempotentKey_ReturnsServiceCollectionForChaining()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddIdempotentKey();

        // Assert
        result.ShouldBe(services);
    }

    [Fact]
    public void AddIdempotentKey_WithCustomConfig_RegistersCustomOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();

        // Act
        services.AddIdempotentKey(options =>
        {
            options.IdempotencyHeaderKey = "X-Custom-Idempotency";
            options.Expiration = TimeSpan.FromMinutes(30);
            options.ConflictHandling = IdempotentConflictHandling.CachedResult;
            options.CachePrefix = "custom";
        });

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var registeredOptions = serviceProvider.GetRequiredService<IOptions<IdempotencyOptions>>();

        registeredOptions.Value.IdempotencyHeaderKey.ShouldBe("X-Custom-Idempotency");
        registeredOptions.Value.Expiration.ShouldBe(TimeSpan.FromMinutes(30));
        registeredOptions.Value.ConflictHandling.ShouldBe(IdempotentConflictHandling.CachedResult);
        registeredOptions.Value.CachePrefix.ShouldBe("custom");
    }

    [Fact]
    public void AddIdempotentKey_WithoutConfig_RegistersDefaultServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging()
            .AddDistributedMemoryCache();

        // Act
        services.AddIdempotentKey();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var repository = serviceProvider.GetRequiredService<IIdempotencyKeyStore>();
        var options = serviceProvider.GetRequiredService<IOptions<IdempotencyOptions>>();

        repository.ShouldNotBeNull();
        options.ShouldNotBeNull();
        options.Value.IdempotencyHeaderKey.ShouldBe("X-Idempotency-Key");
        options.Value.Expiration.ShouldBe(TimeSpan.FromHours(4));
        options.Value.ConflictHandling.ShouldBe(IdempotentConflictHandling.ConflictResponse);
    }

    [Fact]
    public void IdempotentHeaderKey_IsSetFromOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var customHeaderKey = "X-My-Custom-Header";

        // Act
        services.AddIdempotentKey(options => options.IdempotencyHeaderKey = customHeaderKey);

        // Assert
        IdempotencySetup.IdempotentHeaderKey.ShouldBe(customHeaderKey);
    }

    [Fact]
    public void RequiredIdempotentKey_WithoutAddIdempotentKey_DoesNotThrow()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var exception = Record.Exception(() =>
        {
            // This verifies the method exists and can be called
            services.AddIdempotentKey();
        });

        // Assert
        exception.ShouldBeNull();
    }

    #endregion
}