// <copyright file="IdempotencyRedisStoreConcurrencyTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Collections.Concurrent;
using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.Filtering;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

namespace AspCore.Idempotency.RedisStore.Tests.Unit;

/// <summary>
///     Proves the atomicity contract that is the point of DRK-329: N truly concurrent callers for the same key
///     must yield exactly one reservation winner. Backs the mocked <see cref="IDatabase" />'s NX-set behaviour
///     with a real <see cref="ConcurrentDictionary{TKey,TValue}" />, whose <c>TryAdd</c> gives the same
///     "only one caller wins" guarantee as Redis's <c>SET NX</c> - so the reservation race is genuinely exercised
///     rather than asserted from a canned mock sequence. No live Redis is involved.
/// </summary>
public sealed class IdempotencyRedisStoreConcurrencyTests
{
    #region Methods

    private static IdempotencyRedisStore CreateStore()
    {
        var backingStore = new ConcurrentDictionary<string, string>();
        var database = new Mock<IDatabase>();

        database.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey key, CommandFlags _) =>
                backingStore.TryGetValue(key!, out var value) ? (RedisValue)value : RedisValue.Null);

        database.Setup(d => d.StringSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), When.NotExists))
            .ReturnsAsync((RedisKey key, RedisValue value, TimeSpan? _, When _) =>
                backingStore.TryAdd(key!, value!));

        database.Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey key, CommandFlags flags) => backingStore.TryRemove(key!, out var _));

        var multiplexer = new Mock<IConnectionMultiplexer>();
        multiplexer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(database.Object);

        return new IdempotencyRedisStore(
            multiplexer.Object,
            Options.Create(new IdempotencyOptions()),
            NullLogger<IdempotencyRedisStore>.Instance);
    }

    [Fact]
    public async Task IsKeyProcessedAsync_ConcurrentRequestsWithSameKey_OnlyOneReservationWins()
    {
        // Arrange
        var store = CreateStore();
        var keyInfo = new IdempotentKeyInfo
        {
            Endpoint = "/api/items",
            Method = "POST",
            IdempotentKey = Guid.NewGuid().ToString()
        };

        // Act - fire 10 genuinely concurrent reservation attempts for the identical composite key.
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => store.IsKeyProcessedAsync(keyInfo).AsTask())
            .ToArray();
        var results = await Task.WhenAll(tasks);

        // Assert - exactly one caller wins the reservation (false, null); every other concurrent caller
        // observes the in-flight collision path.
        results.Count(r => !r.processed).ShouldBe(1);
        results.Count(r => r.processed).ShouldBe(9);
    }

    #endregion
}
