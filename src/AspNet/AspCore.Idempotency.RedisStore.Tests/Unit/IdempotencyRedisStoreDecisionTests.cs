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
///     stubbing <see cref="IConnectionMultiplexer" />/<see cref="IDatabase" /> - no live Redis involved. Covers the
///     branch table for the single-round-trip <c>SET NX GET</c> reservation (<c>StringSetAndGetAsync</c>):
///     reservation acquired, in-flight collision, completed collision, and the expired-entry delete-then-retry path.
/// </summary>
/// <remarks>
///     Setup/Verify calls below target the exact overload <see cref="IdempotencyRedisStore" /> calls -
///     <c>StringSetAndGetAsync(key, value, expiry, keepTtl, when, flags)</c> - so matching arity is what pins Moq
///     to the right overload the store actually calls.
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

    /// <summary>Sets up the atomic <c>SET NX GET</c> call to return <paramref name="previous" /> in sequence.</summary>
    private void SetupReservationAttempt(params RedisValue[] previous)
    {
        var sequence = _database.SetupSequence(d => d.StringSetAndGetAsync(
            It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(),
            When.NotExists, It.IsAny<CommandFlags>()));
        foreach (var value in previous) sequence = sequence.ReturnsAsync(value);
    }

    [Fact]
    public async Task IsKeyProcessedAsync_NoExistingEntry_ReservesAtomicallyAndReturnsFalse()
    {
        // Arrange - SET NX GET reports no previous value: this call reserved the key.
        SetupReservationAttempt(RedisValue.Null);

        // Act
        var result = await CreateStore().IsKeyProcessedAsync(MakeKey());

        // Assert
        result.processed.ShouldBeFalse();
        result.response.ShouldBeNull();
        _database.Verify(d => d.StringSetAndGetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), _options.InFlightReservationTimeout,
                It.IsAny<bool>(), When.NotExists, It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task IsKeyProcessedAsync_ReservationCollidesWithInFlightEntry_ReturnsTrueWithNullResponse()
    {
        // Arrange - SET NX GET reports another caller's still-active reservation (HTTP 102 sentinel) as the
        // pre-existing value, in the same round trip that attempted our own reservation.
        var reservation = CreateResponse(102, DateTimeOffset.UtcNow.AddSeconds(30));
        SetupReservationAttempt((RedisValue)Serialize(reservation));

        // Act
        var result = await CreateStore().IsKeyProcessedAsync(MakeKey());

        // Assert
        result.processed.ShouldBeTrue();
        result.response.ShouldBeNull();
    }

    [Fact]
    public async Task IsKeyProcessedAsync_ReservationCollidesWithCompletedEntry_ReturnsCachedResponse()
    {
        // Arrange - SET NX GET reports the competing caller's already-completed response.
        var completed = CreateResponse(201, DateTimeOffset.UtcNow.AddHours(1));
        SetupReservationAttempt((RedisValue)Serialize(completed));

        // Act
        var result = await CreateStore().IsKeyProcessedAsync(MakeKey());

        // Assert
        result.processed.ShouldBeTrue();
        result.response.ShouldNotBeNull();
        result.response!.StatusCode.ShouldBe(201);
    }

    [Fact]
    public async Task IsKeyProcessedAsync_CollidesWithExpiredEntry_DeletesEntryAndRetriesReservation()
    {
        // Arrange - the pre-existing value is logically expired (IsExpired) though still physically present in
        // Redis (TTL hasn't fired yet). The store must delete it, then retry the atomic SET NX GET, which this
        // time reserves cleanly.
        var expired = CreateResponse(200, DateTimeOffset.UtcNow.AddHours(-1));
        SetupReservationAttempt((RedisValue)Serialize(expired), RedisValue.Null);
        _database.Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync(true);

        // Act
        var result = await CreateStore().IsKeyProcessedAsync(MakeKey());

        // Assert
        result.processed.ShouldBeFalse();
        result.response.ShouldBeNull();
        _database.Verify(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Once);
        _database.Verify(d => d.StringSetAndGetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(),
                When.NotExists, It.IsAny<CommandFlags>()),
            Times.Exactly(2));
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
        _database.Setup(d => d.StringSetAndGetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(),
                When.NotExists, It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, bool, When, CommandFlags>((key, _, _, _, _, _) =>
                capturedKey = key)
            .ReturnsAsync(RedisValue.Null);

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
