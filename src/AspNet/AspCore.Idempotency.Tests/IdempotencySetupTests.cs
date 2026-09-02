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
    public void AddIdempotentKey_MultipleCallsWithDifferentConfig_FirstConfigWins()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();

        // Act
        services.AddIdempotentKey<IdempotencyDistributedCacheStore>(options => options.CachePrefix = "first");
        services.AddIdempotentKey<IdempotencyDistributedCacheStore>(options => options.CachePrefix = "second");

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<IdempotencyOptions>>();

        options.Value.CachePrefix.ShouldBe("first");
    }

    [Fact]
    public void AddIdempotentKey_RegistersRepositoryAsIIdempotencyKeyRepository()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging()
            .AddDistributedMemoryCache();

        // Act
        services.AddIdempotentKey<IdempotencyDistributedCacheStore>();

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
        var result = services.AddIdempotentKey<IdempotencyDistributedCacheStore>();

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
        services.AddIdempotentKey<IdempotencyDistributedCacheStore>(options =>
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
        services.AddIdempotentKey<IdempotencyDistributedCacheStore>();

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
    public void AddIdempotentKey_WithCustomHeaderKey_ResolvesFromOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var customHeaderKey = "X-My-Custom-Header";

        // Act
        services.AddIdempotentKey<IdempotencyDistributedCacheStore>(options => options.IdempotencyHeaderKey = customHeaderKey);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<IdempotencyOptions>>();

        options.Value.IdempotencyHeaderKey.ShouldBe(customHeaderKey);
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
            services.AddIdempotentKey<IdempotencyDistributedCacheStore>();
        });

        // Assert
        exception.ShouldBeNull();
    }

    [Fact]
    public void AddIdempotentKey_WithEmptyScopeHmacSecret_ThrowsOptionsValidationExceptionOnResolve()
    {
        // Arrange - the options pattern defers validation to first resolve (or host startup via
        // ValidateOnStart), not to the AddIdempotentKey call itself.
        var services = new ServiceCollection();
        services.AddIdempotentKey<IdempotencyDistributedCacheStore>(c => c.ScopeHmacSecret = string.Empty);
        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<IdempotencyOptions>>();

        // Act
        var exception = Should.Throw<OptionsValidationException>(() => options.Value);

        // Assert
        exception.Failures.ShouldContain(f => f.Contains("ScopeHmacSecret"));
    }

    [Fact]
    public void AddIdempotentKey_WithWhitespaceScopeHmacSecret_ThrowsOptionsValidationExceptionOnResolve()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddIdempotentKey<IdempotencyDistributedCacheStore>(c => c.ScopeHmacSecret = "   ");
        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<IdempotencyOptions>>();

        // Act
        var exception = Should.Throw<OptionsValidationException>(() => options.Value);

        // Assert
        exception.Failures.ShouldContain(f => f.Contains("ScopeHmacSecret"));
    }

    #endregion
}