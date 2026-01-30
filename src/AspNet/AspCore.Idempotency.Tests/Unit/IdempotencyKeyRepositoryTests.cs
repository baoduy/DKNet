// <copyright file="IdempotencyKeyRepositoryTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.Store;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace AspCore.Idempotency.Tests.Unit;

public class IdempotencyKeyRepositoryTests
{
    #region Fields

    private readonly IDistributedCache _cache;
    private readonly ILogger<IdempotencyEndpointFilter> _logger;

    #endregion

    #region Constructors

    public IdempotencyKeyRepositoryTests()
    {
        _cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        _logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<IdempotencyEndpointFilter>();
    }

    #endregion

    #region Methods

    private static CachedResponse CreateCachedResponse(int statusCode, string? body)
    {
        var now = DateTimeOffset.UtcNow;
        return new CachedResponse
        {
            StatusCode = statusCode,
            Body = body,
            ContentType = "application/json",
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(5)
        };
    }

    private IdempotencyDistributedCacheStore CreateRepository(IdempotencyOptions? options = null)
    {
        var opts = options ?? new IdempotencyOptions();
        return new IdempotencyDistributedCacheStore(
            _cache,
            Options.Create(opts),
            _logger);
    }

    [Fact]
    public async Task IsKeyProcessedAsync_CacheKeyNormalization_CaseSensitive()
    {
        // Arrange
        var repository = CreateRepository();
        var idempotencyKey1 = "test-KEY-123";
        var idempotencyKey2 = "TEST-key-123";
        var cachedResponse = CreateCachedResponse(200, "{\"id\": 1}");

        // Act
        await repository.MarkKeyAsProcessedAsync(idempotencyKey1, cachedResponse);
        var result = await repository.IsKeyProcessedAsync(idempotencyKey2);

        // Assert
        result.processed.ShouldBeTrue();
        result.response.ShouldNotBeNull();
        result.response!.Body.ShouldBe(cachedResponse.Body);
        result.response.StatusCode.ShouldBe(cachedResponse.StatusCode);
    }

    [Fact]
    public async Task IsKeyProcessedAsync_WhenKeyExistsWithoutResult_ReturnsTrue()
    {
        // Arrange
        var repository = CreateRepository();
        var idempotencyKey = Guid.NewGuid().ToString();
        var cachedResponse = CreateCachedResponse(204, null);

        // Act
        await repository.MarkKeyAsProcessedAsync(idempotencyKey, cachedResponse);
        var result = await repository.IsKeyProcessedAsync(idempotencyKey);

        // Assert
        result.processed.ShouldBeTrue();
        result.response.ShouldNotBeNull();
    }

    [Fact]
    public async Task IsKeyProcessedAsync_WhenKeyExistsWithResult_ReturnsTrueAndResult()
    {
        // Arrange
        var repository = CreateRepository();
        var idempotencyKey = Guid.NewGuid().ToString();
        var cachedResponse = CreateCachedResponse(200, "{\"id\": 1, \"name\": \"test\"}");

        // Act
        await repository.MarkKeyAsProcessedAsync(idempotencyKey, cachedResponse);
        var result = await repository.IsKeyProcessedAsync(idempotencyKey);

        // Assert
        result.processed.ShouldBeTrue();
        result.response.ShouldNotBeNull();
        result.response!.Body.ShouldBe(cachedResponse.Body);
        result.response.StatusCode.ShouldBe(cachedResponse.StatusCode);
    }

    [Fact]
    public async Task IsKeyProcessedAsync_WhenKeyExpired_ReturnsFalse()
    {
        // Arrange
        var options = new IdempotencyOptions { Expiration = TimeSpan.FromMilliseconds(100) };
        var repository = CreateRepository(options);
        var idempotencyKey = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow;
        var cachedResponse = new CachedResponse
        {
            StatusCode = 200,
            Body = "{\"id\": 1}",
            ContentType = "application/json",
            CreatedAt = now,
            ExpiresAt = now.AddMilliseconds(100)
        };

        // Act
        await repository.MarkKeyAsProcessedAsync(idempotencyKey, cachedResponse);
        await Task.Delay(150); // Wait for expiration
        var result = await repository.IsKeyProcessedAsync(idempotencyKey);

        // Assert
        result.processed.ShouldBeFalse();
    }

    [Fact]
    public async Task IsKeyProcessedAsync_WhenKeyNotExists_ReturnsFalse()
    {
        // Arrange
        var repository = CreateRepository();
        var idempotencyKey = Guid.NewGuid().ToString();

        // Act
        var result = await repository.IsKeyProcessedAsync(idempotencyKey);

        // Assert
        result.processed.ShouldBeFalse();
        result.response.ShouldBeNull();
    }

    [Fact]
    public async Task IsKeyProcessedAsync_WithSpecialCharacters_SanitizesKey()
    {
        // Arrange
        var repository = CreateRepository();
        var idempotencyKey = "test-key/with\nnewlines\rand/slashes";
        var cachedResponse = CreateCachedResponse(200, "{\"id\": 1}");

        // Act
        await repository.MarkKeyAsProcessedAsync(idempotencyKey, cachedResponse);
        var result = await repository.IsKeyProcessedAsync(idempotencyKey);

        // Assert
        result.processed.ShouldBeTrue();
        result.response.ShouldNotBeNull();
        result.response!.Body.ShouldBe(cachedResponse.Body);
        result.response.StatusCode.ShouldBe(cachedResponse.StatusCode);
    }

    [Fact]
    public async Task MarkKeyAsProcessedAsync_WithCustomPrefix_IncludesPrefixInCacheKey()
    {
        // Arrange
        var options = new IdempotencyOptions { CachePrefix = "custom-prefix" };
        var repository = CreateRepository(options);
        var idempotencyKey = "test-key";
        var cachedResponse = CreateCachedResponse(200, "{\"id\": 1}");

        // Act
        await repository.MarkKeyAsProcessedAsync(idempotencyKey, cachedResponse);
        var result = await repository.IsKeyProcessedAsync(idempotencyKey);

        // Assert
        result.processed.ShouldBeTrue();
        result.response.ShouldNotBeNull();
        result.response!.Body.ShouldBe(cachedResponse.Body);
    }

    [Fact]
    public async Task MarkKeyAsProcessedAsync_WithEmptyString_TreatAsNull()
    {
        // Arrange
        var repository = CreateRepository();
        var idempotencyKey = Guid.NewGuid().ToString();
        var cachedResponse = CreateCachedResponse(200, string.Empty);

        // Act
        await repository.MarkKeyAsProcessedAsync(idempotencyKey, cachedResponse);

        // Assert
        var result = await repository.IsKeyProcessedAsync(idempotencyKey);
        result.processed.ShouldBeTrue();
        result.response.ShouldNotBeNull();
        result.response!.Body.ShouldBe(cachedResponse.Body);
    }

    [Fact]
    public async Task MarkKeyAsProcessedAsync_WithoutResult_SetsKeyInCache()
    {
        // Arrange
        var repository = CreateRepository();
        var idempotencyKey = Guid.NewGuid().ToString();
        var cachedResponse = CreateCachedResponse(204, null);

        // Act
        await repository.MarkKeyAsProcessedAsync(idempotencyKey, cachedResponse);

        // Assert
        var result = await repository.IsKeyProcessedAsync(idempotencyKey);
        result.processed.ShouldBeTrue();
        result.response.ShouldNotBeNull();
    }

    [Fact]
    public async Task MarkKeyAsProcessedAsync_WithResult_SetsCachedResult()
    {
        // Arrange
        var repository = CreateRepository();
        var idempotencyKey = Guid.NewGuid().ToString();
        var cachedResponse = CreateCachedResponse(201, "{\"id\": 1, \"message\": \"created\"}");

        // Act
        await repository.MarkKeyAsProcessedAsync(idempotencyKey, cachedResponse);

        // Assert
        var result = await repository.IsKeyProcessedAsync(idempotencyKey);
        result.processed.ShouldBeTrue();
        result.response.ShouldNotBeNull();
        result.response!.Body.ShouldBe(cachedResponse.Body);
        result.response.StatusCode.ShouldBe(cachedResponse.StatusCode);
    }

    [Fact]
    public async Task MarkKeyAsProcessedAsync_WithWhitespace_TreatAsNull()
    {
        // Arrange
        var repository = CreateRepository();
        var idempotencyKey = Guid.NewGuid().ToString();
        var cachedResponse = CreateCachedResponse(200, "   ");

        // Act
        await repository.MarkKeyAsProcessedAsync(idempotencyKey, cachedResponse);

        // Assert
        var result = await repository.IsKeyProcessedAsync(idempotencyKey);
        result.processed.ShouldBeTrue();
        result.response.ShouldNotBeNull();
        result.response!.Body.ShouldBe(cachedResponse.Body);
    }

    #endregion
}