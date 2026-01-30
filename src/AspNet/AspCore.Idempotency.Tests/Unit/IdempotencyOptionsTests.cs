// <copyright file="IdempotencyOptionsTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Text.Json;
using DKNet.AspCore.Idempotency;

namespace AspCore.Idempotency.Tests.Unit;

public class IdempotencyOptionsTests
{
    #region Methods

    [Fact]
    public void IdempotencyOptions_CanSetConflictHandlingToCachedResult()
    {
        // Arrange & Act
        var options = new IdempotencyOptions { ConflictHandling = IdempotentConflictHandling.CachedResult };

        // Assert
        options.ConflictHandling.ShouldBe(IdempotentConflictHandling.CachedResult);
    }

    [Fact]
    public void IdempotencyOptions_CanSetCustomCachePrefix()
    {
        // Arrange & Act
        var options = new IdempotencyOptions { CachePrefix = "custom-prefix" };

        // Assert
        options.CachePrefix.ShouldBe("custom-prefix");
    }

    [Fact]
    public void IdempotencyOptions_CanSetCustomExpiration()
    {
        // Arrange
        var customExpiration = TimeSpan.FromMinutes(30);

        // Act
        var options = new IdempotencyOptions { Expiration = customExpiration };

        // Assert
        options.Expiration.ShouldBe(customExpiration);
    }

    [Fact]
    public void IdempotencyOptions_CanSetCustomHeaderKey()
    {
        // Arrange & Act
        var options = new IdempotencyOptions { IdempotencyHeaderKey = "X-Custom-Key" };

        // Assert
        options.IdempotencyHeaderKey.ShouldBe("X-Custom-Key");
    }

    [Fact]
    public void IdempotencyOptions_CanSetCustomJsonSerializerOptions()
    {
        // Arrange
        var customOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

        // Act
        var options = new IdempotencyOptions { JsonSerializerOptions = customOptions };

        // Assert
        options.JsonSerializerOptions.ShouldBe(customOptions);
        options.JsonSerializerOptions.PropertyNamingPolicy.ShouldBe(JsonNamingPolicy.SnakeCaseLower);
    }

    [Fact]
    public void IdempotencyOptions_HasDefaultValues()
    {
        // Arrange & Act
        var options = new IdempotencyOptions();

        // Assert
        options.IdempotencyHeaderKey.ShouldBe("X-Idempotency-Key");
        options.CachePrefix.ShouldBe("idem");
        options.Expiration.ShouldBe(TimeSpan.FromHours(4));
        options.ConflictHandling.ShouldBe(IdempotentConflictHandling.ConflictResponse);
        options.JsonSerializerOptions.ShouldNotBeNull();
        options.JsonSerializerOptions.PropertyNamingPolicy.ShouldBe(JsonNamingPolicy.CamelCase);
    }

    [Fact]
    public void IdempotencyOptions_MultiplePropertiesCanBeConfiguredTogether()
    {
        // Arrange & Act
        var options = new IdempotencyOptions
        {
            IdempotencyHeaderKey = "X-Req-Id",
            CachePrefix = "req-cache",
            Expiration = TimeSpan.FromHours(1),
            ConflictHandling = IdempotentConflictHandling.CachedResult
        };

        // Assert
        options.IdempotencyHeaderKey.ShouldBe("X-Req-Id");
        options.CachePrefix.ShouldBe("req-cache");
        options.Expiration.ShouldBe(TimeSpan.FromHours(1));
        options.ConflictHandling.ShouldBe(IdempotentConflictHandling.CachedResult);
    }

    [Fact]
    public void IdempotentConflictHandling_HasCachedResultValue()
    {
        // Arrange & Act
        var conflictHandling = IdempotentConflictHandling.CachedResult;

        // Assert
        conflictHandling.ShouldBe(IdempotentConflictHandling.CachedResult);
    }

    [Fact]
    public void IdempotentConflictHandling_HasConflictResponseValue()
    {
        // Arrange & Act
        var conflictHandling = IdempotentConflictHandling.ConflictResponse;

        // Assert
        conflictHandling.ShouldBe(IdempotentConflictHandling.ConflictResponse);
    }

    #endregion
}