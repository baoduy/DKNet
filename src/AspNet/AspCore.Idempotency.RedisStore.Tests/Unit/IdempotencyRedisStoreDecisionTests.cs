// <copyright file="IdempotencyRedisStoreDecisionTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.Filtering;
using DKNet.AspCore.Idempotency.Store;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

namespace AspCore.Idempotency.RedisStore.Tests.Unit;

/// <summary>
///     Proves <see cref="IdempotencyRedisStore" />'s reservation/collision decision logic deterministically by
///     stubbing <see cref="IConnectionMultiplexer" />/<see cref="IDatabase" /> - no live Redis involved. Mirrors the
///     branch table from the ticket: reservation acquired, in-flight collision, completed collision, gone/expired
///     collision, and the expired-entry delete path.
/// </summary>
/// <remarks>
///     Setup/Verify calls below intentionally use the same argument arity as <see cref="IdempotencyRedisStore" />
///     itself (e.g. the 4-arg reservation <c>StringSetAsync(key, value, expiry, when)</c> vs. the 3-arg
///     unconditional <c>StringSetAsync(key, value, expiry)</c>): <see cref="IDatabase" /> overloads
///     <c>StringSetAsync</c> on argument count, so matching arity is what pins Moq to the exact overload the
///     store actually calls.
/// </remarks>
public sealed class IdempotencyRedisStoreDecisionTests
{
    #region Fields

    private readonly Mock<IDatabase> _database = new();
    private readonly IdempotencyOptions _options = new();

    #endregion

    #region Constructors

    public IdempotencyRedisStoreDecisionTests()
    {
        var multiplexer = new Mock<IConnectionMultiplexer>();
        multiplexer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_database.Object);
        Multiplexer = multiplexer.Object;
    }

    #endregion

    #region Properties

    private IConnectionMultiplexer Multiplexer { get; }

    #endregion

    #region Methods

    private IdempotencyRedisStore CreateStore() =>
        new(Multiplexer, Options.Create(_options), NullLogger<IdempotencyRedisStore>.Instance);

    private static IdempotentKeyInfo MakeKey() =>
        new() { Endpoint = "/api/orders", Method = "POST", IdempotentKey = Guid.NewGuid().ToString() };

    private string Serialize(CachedResponse response) =>
        JsonSerializer.Serialize(response, _options.JsonSerializerOptions);

    private static CachedResponse CreateResponse(int statusCode, DateTimeOffset? expiresAt) =>
        new()
        {
            StatusCode = statusCode,
            Body = "{\"id\":1}",
            ContentType = "application/json",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt
        };

    /// <summary>Setup for the reservation attempt: <c>StringSetAsync(key, value, expiry, when)</c>.</summary>
    private void SetupReservationAttempt(bool reserved) =>
        _database.Setup(d => d.StringSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), When.NotExists))
            .ReturnsAsync(reserved);

    [Fact]
    public async Task IsKeyProcessedAsync_NoExistingEntry_ReservesAtomicallyAndReturnsFalse()
    {
        // Arrange - no cached entry, and the SET NX succeeds (this caller wins the reservation)
        _database.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);
        SetupReservationAttempt(true);

        // Act
        var result = await CreateStore().IsKeyProcessedAsync(MakeKey());

        // Assert
        result.processed.ShouldBeFalse();
        result.response.ShouldBeNull();
        _database.Verify(d => d.StringSetAsync(
            It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), _options.InFlightReservationTimeout, When.NotExists),
            Times.Once);
    }

    [Fact]
    public async Task IsKeyProcessedAsync_ReservationCollidesWithInFlightEntry_ReturnsTrueWithNullResponse()
    {
        // Arrange - initial read finds nothing, SET NX fails (another caller reserved first), re-read finds
        // that caller's still-active reservation (HTTP 102 sentinel).
        var reservation = CreateResponse(102, DateTimeOffset.UtcNow.AddSeconds(30));
        _database.SetupSequence(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null)
            .ReturnsAsync((RedisValue)Serialize(reservation));
        SetupReservationAttempt(false);

        // Act
        var result = await CreateStore().IsKeyProcessedAsync(MakeKey());

        // Assert
        result.processed.ShouldBeTrue();
        result.response.ShouldBeNull();
    }

    [Fact]
    public async Task IsKeyProcessedAsync_ReservationCollidesWithCompletedEntry_ReturnsCachedResponse()
    {
        // Arrange - the competing caller already finished and stored its response.
        var completed = CreateResponse(201, DateTimeOffset.UtcNow.AddHours(1));
        _database.SetupSequence(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null)
            .ReturnsAsync((RedisValue)Serialize(completed));
        SetupReservationAttempt(false);

        // Act
        var result = await CreateStore().IsKeyProcessedAsync(MakeKey());

        // Assert
        result.processed.ShouldBeTrue();
        result.response.ShouldNotBeNull();
        result.response!.StatusCode.ShouldBe(201);
    }

    [Fact]
    public async Task IsKeyProcessedAsync_ReservationCollisionButEntryGoneOnReread_ReturnsFalseWithNullResponse()
    {
        // Arrange - SET NX fails, but the competing entry has already been evicted/expired-out of Redis by the
        // time we re-read it. The caller is allowed to proceed as if it were new.
        _database.SetupSequence(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null)
            .ReturnsAsync(RedisValue.Null);
        SetupReservationAttempt(false);

        // Act
        var result = await CreateStore().IsKeyProcessedAsync(MakeKey());

        // Assert
        result.processed.ShouldBeFalse();
        result.response.ShouldBeNull();
    }

    [Fact]
    public async Task IsKeyProcessedAsync_ReservationCollisionWithExpiredEntry_DeletesEntryAndReturnsFalse()
    {
        // Arrange - the competing entry is logically expired (IsExpired) though still physically present;
        // the store must delete it before reporting the caller can proceed.
        var expired = CreateResponse(200, DateTimeOffset.UtcNow.AddHours(-1));
        _database.SetupSequence(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null)
            .ReturnsAsync((RedisValue)Serialize(expired));
        SetupReservationAttempt(false);
        _database.Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync(true);

        // Act
        var result = await CreateStore().IsKeyProcessedAsync(MakeKey());

        // Assert
        result.processed.ShouldBeFalse();
        result.response.ShouldBeNull();
        _database.Verify(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task IsKeyProcessedAsync_ExistingExpiredEntryOnInitialRead_DeletesThenReserves()
    {
        // Arrange - the first read (before any SET NX attempt) already finds an expired entry.
        var expired = CreateResponse(200, DateTimeOffset.UtcNow.AddHours(-1));
        _database.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)Serialize(expired));
        _database.Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync(true);
        SetupReservationAttempt(true);

        // Act
        var result = await CreateStore().IsKeyProcessedAsync(MakeKey());

        // Assert
        result.processed.ShouldBeFalse();
        result.response.ShouldBeNull();
        _database.Verify(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task IsKeyProcessedAsync_ExistingInFlightReservation_ReturnsTrueWithNullResponseWithoutReserving()
    {
        // Arrange - the very first read already finds a live in-flight reservation - no SET NX should be
        // attempted at all.
        var reservation = CreateResponse(102, DateTimeOffset.UtcNow.AddSeconds(30));
        _database.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)Serialize(reservation));

        // Act
        var result = await CreateStore().IsKeyProcessedAsync(MakeKey());

        // Assert
        result.processed.ShouldBeTrue();
        result.response.ShouldBeNull();
        _database.Verify(d => d.StringSetAsync(
            It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<When>()), Times.Never);
    }

    [Fact]
    public async Task IsKeyProcessedAsync_ExistingCompletedEntry_ReturnsCachedResponseWithoutReserving()
    {
        // Arrange
        var completed = CreateResponse(200, DateTimeOffset.UtcNow.AddHours(1));
        _database.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)Serialize(completed));

        // Act
        var result = await CreateStore().IsKeyProcessedAsync(MakeKey());

        // Assert
        result.processed.ShouldBeTrue();
        result.response.ShouldNotBeNull();
        result.response!.StatusCode.ShouldBe(200);
        _database.Verify(d => d.StringSetAsync(
            It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<When>()), Times.Never);
    }

    [Fact]
    public async Task IsKeyProcessedAsync_SanitizesCompositeKeyToPreviousHexFormat()
    {
        // Arrange - pins Convert.ToHexStringLower's output against the pre-refactor
        // Convert.ToHexString(...).ToLowerInvariant() formula for the same input. SanitizeKey's output is the
        // literal Redis key, so any drift here would orphan every key already stored under the old format.
        var keyInfo = MakeKey();
        var expectedHash =
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(keyInfo.CompositeKey))).ToLowerInvariant();
        RedisKey? capturedKey = null;
        _database.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Callback<RedisKey, CommandFlags>((key, _) => capturedKey = key)
            .ReturnsAsync(RedisValue.Null);
        SetupReservationAttempt(true);

        // Act
        await CreateStore().IsKeyProcessedAsync(keyInfo);

        // Assert
        capturedKey.ShouldNotBeNull();
        ((string?)capturedKey).ShouldBe($"{_options.CachePrefix}{expectedHash}");
    }

    [Fact]
    public async Task MarkKeyAsProcessedAsync_AlwaysSetsUnconditionallyWithConfiguredExpiration()
    {
        // Arrange - MarkKeyAsProcessedAsync calls the 3-arg StringSetAsync(key, value, expiry) overload: no
        // "when" condition at all, i.e. an unconditional SET regardless of whatever reservation preceded it.
        _database.Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), _options.Expiration))
            .ReturnsAsync(true);
        var response = CreateResponse(201, DateTimeOffset.UtcNow.AddHours(1));

        // Act
        await CreateStore().MarkKeyAsProcessedAsync(MakeKey(), response);

        // Assert
        _database.Verify(
            d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), _options.Expiration), Times.Once);
    }

    #endregion
}
