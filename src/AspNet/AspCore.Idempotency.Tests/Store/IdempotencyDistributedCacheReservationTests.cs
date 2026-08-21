// <copyright file="IdempotencyDistributedCacheReservationTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.Filtering;
using DKNet.AspCore.Idempotency.Store;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace AspCore.Idempotency.Tests.Store;

/// <summary>
///     Covers the in-flight reservation placeholder introduced into
///     <see cref="IdempotencyDistributedCacheStore.IsKeyProcessedAsync" />, which the pre-existing
///     mark-then-check tests never exercise because they always mark a key as processed before checking it.
/// </summary>
public sealed class IdempotencyDistributedCacheReservationTests
{
    #region Fields

    private readonly IDistributedCache _cache;
    private readonly ILogger<IdempotencyEndpointFilter> _logger;

    #endregion

    #region Constructors

    public IdempotencyDistributedCacheReservationTests()
    {
        _cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        _logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<IdempotencyEndpointFilter>();
    }

    #endregion

    #region Methods

    private IdempotencyDistributedCacheStore CreateStore(IdempotencyOptions? options = null) =>
        new(_cache, Options.Create(options ?? new IdempotencyOptions()), _logger);

    private static IdempotentKeyInfo MakeKey(string key) =>
        new() { IdempotentKey = key, Endpoint = "/api/test", Method = "POST" };

    [Fact]
    public async Task IsKeyProcessedAsync_FreshKey_WritesReservationSeenAsInFlightByNextCall()
    {
        // Arrange
        var store = CreateStore();
        var keyInfo = MakeKey(Guid.NewGuid().ToString());

        // Act - first call is a genuine miss and must write the in-flight placeholder itself
        var first = await store.IsKeyProcessedAsync(keyInfo);
        var second = await store.IsKeyProcessedAsync(keyInfo);

        // Assert - the miss reports "not processed yet"; the placeholder it wrote makes the very next
        // call for the same key see an in-flight reservation (409 path) rather than another miss.
        first.processed.ShouldBeFalse();
        first.response.ShouldBeNull();

        second.processed.ShouldBeTrue();
        second.response.ShouldBeNull();
    }

    [Fact]
    public async Task IsKeyProcessedAsync_AfterReservationCompletes_ReturnsCachedResponse()
    {
        // Arrange
        var store = CreateStore();
        var keyInfo = MakeKey(Guid.NewGuid().ToString());
        var response = new CachedResponse
        {
            StatusCode = 201,
            Body = "{\"id\":1}",
            ContentType = "application/json",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };

        // Act - reserve, complete, then replay
        await store.IsKeyProcessedAsync(keyInfo);
        await store.MarkKeyAsProcessedAsync(keyInfo, response);
        var completed = await store.IsKeyProcessedAsync(keyInfo);

        // Assert - the completed response overwrites the reservation placeholder
        completed.processed.ShouldBeTrue();
        completed.response.ShouldNotBeNull();
        completed.response!.StatusCode.ShouldBe(201);
        completed.response.Body.ShouldBe("{\"id\":1}");
    }

    [Fact]
    public async Task IsKeyProcessedAsync_AfterReservationTimeoutElapses_TreatsKeyAsFreshMiss()
    {
        // Arrange - a very short reservation timeout so the placeholder expires almost immediately,
        // exercising InFlightReservationTimeout's actual expiry behavior rather than just its default value.
        var store = CreateStore(new IdempotencyOptions
        {
            InFlightReservationTimeout = TimeSpan.FromMilliseconds(1)
        });
        var keyInfo = MakeKey(Guid.NewGuid().ToString());

        // Act
        await store.IsKeyProcessedAsync(keyInfo); // reserves, expiring almost immediately
        await Task.Delay(50);
        var result = await store.IsKeyProcessedAsync(keyInfo);

        // Assert - an abandoned reservation must not permanently block retries
        result.processed.ShouldBeFalse();
        result.response.ShouldBeNull();
    }

    #endregion
}
