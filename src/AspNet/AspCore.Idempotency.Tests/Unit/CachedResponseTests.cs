// <copyright file="CachedResponseTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace AspCore.Idempotency.Tests.Unit;

/// <summary>
///     Unit tests for CachedResponse entity.
/// </summary>
public class CachedResponseTests
{
    #region Methods

    [Fact]
    public void CanCreateWithNullBody()
    {
        // Act
        var response = new CachedResponse
        {
            StatusCode = 204,
            Body = null,
            ContentType = "application/json",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };

        // Assert
        response.Body.ShouldBeNull();
        response.StatusCode.ShouldBe(204);
    }

    [Fact]
    public void CanCreateWithRequestBodyHash()
    {
        // Arrange
        var hash = "sha256_hash_here";

        // Act
        var response = new CachedResponse
        {
            StatusCode = 200,
            Body = "test",
            ContentType = "application/json",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            RequestBodyHash = hash
        };

        // Assert
        response.RequestBodyHash.ShouldBe(hash);
    }

    [Fact]
    public void IsExpired_WhenExpiryInFuture_ReturnsFalse()
    {
        // Arrange
        var response = new CachedResponse
        {
            StatusCode = 200,
            Body = "test",
            ContentType = "application/json",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };

        // Act
        var result = response.IsExpired;

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsExpired_WhenExpiryInPast_ReturnsTrue()
    {
        // Arrange
        var response = new CachedResponse
        {
            StatusCode = 200,
            Body = "test",
            ContentType = "application/json",
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-2),
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };

        // Act
        var result = response.IsExpired;

        // Assert
        result.ShouldBeTrue();
    }

    #endregion
}