// <copyright file="IdempotencyKeyTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace AspCore.Idempotency.Tests.Unit;

/// <summary>
///     Unit tests for IdempotencyKey value object validation.
/// </summary>
public class IdempotencyKeyTests
{
    #region Methods

    [Fact]
    public void ImplicitConversion_ReturnsStringValue()
    {
        // Arrange
        IdempotencyKey.TryCreate("test-key", out var key);

        // Act
        string converted = key;

        // Assert
        converted.ShouldBe("test-key");
    }

    [Fact]
    public void ToString_ReturnsKeyValue()
    {
        // Arrange
        IdempotencyKey.TryCreate("test-key", out var key);

        // Act
        var result = key.ToString();

        // Assert
        result.ShouldBe("test-key");
    }

    [Fact]
    public void TryCreate_TrimsWhitespace()
    {
        // Act
        var result = IdempotencyKey.TryCreate("  valid-key  ", out var key);

        // Assert
        result.ShouldBeTrue();
        key.Value.ShouldBe("valid-key");
    }

    [Fact]
    public void TryCreate_WithCustomMaxLength_RespectsBoundary()
    {
        // Arrange
        var key = new string('a', 50);

        // Act
        var result = IdempotencyKey.TryCreate(key, out _, 49);

        // Assert
        result.ShouldBeFalse();
    }

    [Theory]
    [InlineData("key-with-special!chars")]
    [InlineData("key@domain.com")]
    [InlineData("key with spaces")]
    [InlineData("key/path")]
    public void TryCreate_WithInvalidCharacters_ReturnsFalse(string invalidKey)
    {
        // Act
        var result = IdempotencyKey.TryCreate(invalidKey, out _);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void TryCreate_WithKeyExceedingMaxLength_ReturnsFalse()
    {
        // Arrange
        var longKey = new string('a', 257);

        // Act
        var result = IdempotencyKey.TryCreate(longKey, out _, 256);

        // Assert
        result.ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryCreate_WithNullOrEmpty_ReturnsFalse(string? invalidKey)
    {
        // Act
        var result = IdempotencyKey.TryCreate(invalidKey, out _);

        // Assert
        result.ShouldBeFalse();
    }

    [Theory]
    [InlineData("valid-key-123")]
    [InlineData("key_with_underscore")]
    [InlineData("UPPERCASE")]
    [InlineData("123")]
    [InlineData("a")]
    public void TryCreate_WithValidKey_ReturnsTrue(string validKey)
    {
        // Act
        var result = IdempotencyKey.TryCreate(validKey, out var key);

        // Assert
        result.ShouldBeTrue();
        key.Value.ShouldBe(validKey);
    }

    #endregion
}