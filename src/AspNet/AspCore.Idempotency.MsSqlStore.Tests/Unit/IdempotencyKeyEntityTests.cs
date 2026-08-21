// <copyright file="IdempotencyKeyEntityTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.AspCore.Idempotency;

namespace AspCore.Idempotency.MsSqlStore.Tests.Unit;

/// <summary>
///     Unit tests for <see cref="IdempotencyKeyEntity.SanitizeKey" />.
/// </summary>
public sealed class IdempotencyKeyEntityTests
{
    #region Methods

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SanitizeKey_NullOrWhiteSpaceKey_ThrowsArgumentException(string? key)
    {
        // Act
        var act = () => IdempotencyKeyEntity.SanitizeKey(key!);

        // Assert
        Should.Throw<ArgumentException>(act);
    }

    [Fact]
    public void SanitizeKey_ExactCollisionFromFinding_ProducesDifferentResults()
    {
        // Arrange
        const string keyA = "POST:/ab:cd";
        const string keyB = "POST:/a:bcd";

        // Act
        var sanitizedA = IdempotencyKeyEntity.SanitizeKey(keyA);
        var sanitizedB = IdempotencyKeyEntity.SanitizeKey(keyB);

        // Assert
        sanitizedA.ShouldNotBe(sanitizedB);
    }

    [Fact]
    public void SanitizeKey_SameInputTwice_IsDeterministicBoundedAndNonEmpty()
    {
        // Arrange
        const string key = "GET:/api/orders:idem-key-123";

        // Act
        var first = IdempotencyKeyEntity.SanitizeKey(key);
        var second = IdempotencyKeyEntity.SanitizeKey(key);

        // Assert
        first.ShouldBe(second);
        first.ShouldNotBeNullOrEmpty();
        first.Length.ShouldBeLessThanOrEqualTo(128);
    }

    [Fact]
    public void SanitizeKey_StructurallySimilarKeys_AreAllPairwiseDistinct()
    {
        // Arrange
        string[] keys =
        [
            "GET:/a/b:x",
            "GET:/a:b/x",
            "GET:/ab:x"
        ];

        // Act
        var sanitized = keys.Select(IdempotencyKeyEntity.SanitizeKey).ToArray();

        // Assert
        sanitized.Distinct().Count().ShouldBe(sanitized.Length);
    }

    [Fact]
    public void SanitizeKey_VeryLongCompositeKey_HashesSuccessfully()
    {
        // Arrange
        var key = $"{new string('M', 20)}:{new string('E', 250)}:{new string('K', 150)}";

        // Act
        var sanitized = IdempotencyKeyEntity.SanitizeKey(key);

        // Assert
        sanitized.ShouldNotBeNullOrEmpty();
        sanitized.Length.ShouldBeLessThanOrEqualTo(128);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Constructor_NullOrEmptyIdempotentKey_ThrowsArgumentException(string? idempotentKey)
    {
        // Arrange
        var info = new IdempotentKeyInfo
        {
            IdempotentKey = idempotentKey!, Endpoint = "/api/orders", Method = "POST"
        };
        var response = new CachedResponse
        {
            StatusCode = 200,
            Body = null,
            ContentType = "application/json",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = null
        };

        // Act
        var act = () => new IdempotencyKeyEntity(info, response);

        // Assert - the trust-boundary guard rejects a caller that bypasses the endpoint filter's own
        // "X-Idempotency-Key header is required" validation and constructs the entity directly.
        Should.Throw<ArgumentException>(act);
    }

    #endregion
}
