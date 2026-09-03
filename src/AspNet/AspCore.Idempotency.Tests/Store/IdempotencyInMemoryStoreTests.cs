// <copyright file="IdempotencyInMemoryStoreTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Collections.Concurrent;
using System.Reflection;
using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.Filtering;
using DKNet.AspCore.Idempotency.Store;
using Microsoft.Extensions.Logging.Abstractions;

namespace AspCore.Idempotency.Tests.Store;

/// <summary>
///     Proves the atomicity contract that DRK-1005 exists to satisfy - the store this one replaces was
///     withdrawn for exactly one defect: two simultaneous callers for the same key both proceeding - plus
///     expiry and the memory-bounding sweep.
/// </summary>
public sealed class IdempotencyInMemoryStoreTests
{
    #region Methods

    private static IdempotencyInMemoryStore CreateStore(IdempotencyOptions? options = null) =>
        new(Microsoft.Extensions.Options.Options.Create(options ?? new IdempotencyOptions()),
            NullLogger<IdempotencyInMemoryStore>.Instance);

    private static IdempotentKeyInfo NewKeyInfo(string? key = null) => new()
    {
        Endpoint = "/api/items",
        Method = "POST",
        IdempotentKey = key ?? Guid.NewGuid().ToString()
    };

    /// <summary>
    ///     Reads the private backing dictionary via reflection - an entry-count assertion on the store's own
    ///     internals is the stable way to prove the sweep bounds memory, versus a flaky GC.GetTotalMemory delta.
    /// </summary>
    private static ConcurrentDictionary<string, CachedResponse> GetBackingStore(IdempotencyInMemoryStore store)
    {
        var field = typeof(IdempotencyInMemoryStore)
            .GetField("_store", BindingFlags.NonPublic | BindingFlags.Instance);
        return (ConcurrentDictionary<string, CachedResponse>)field!.GetValue(store)!;
    }

    [Fact]
    public async Task IsKeyProcessedAsync_ManyConcurrentCallersAcrossManyRounds_ExactlyOneWinnerPerRound()
    {
        // Arrange
        var store = CreateStore();

        // Act & Assert - repeated rounds, each with a fresh key and many genuinely concurrent callers,
        // rather than a single sequential Get-then-Set pair that could pass on a non-atomic implementation.
        for (var round = 0; round < 20; round++)
        {
            var keyInfo = NewKeyInfo();

            var tasks = Enumerable.Range(0, 25)
                .Select(_ => store.IsKeyProcessedAsync(keyInfo).AsTask())
                .ToArray();
            var results = await Task.WhenAll(tasks);

            results.Count(r => !r.processed).ShouldBe(1);
            results.Count(r => r.processed).ShouldBe(24);
        }
    }

    [Fact]
    public async Task IsKeyProcessedAsync_WhenKeyIsNew_ReturnsNotProcessedAndReserves()
    {
        // Arrange
        var store = CreateStore();
        var keyInfo = NewKeyInfo();

        // Act
        var (processed, response) = await store.IsKeyProcessedAsync(keyInfo);

        // Assert
        processed.ShouldBeFalse();
        response.ShouldBeNull();
    }

    [Fact]
    public async Task IsKeyProcessedAsync_WhenReservationInFlight_ReturnsProcessedWithNullResponse()
    {
        // Arrange
        var store = CreateStore();
        var keyInfo = NewKeyInfo();
        await store.IsKeyProcessedAsync(keyInfo); // first caller reserves

        // Act - second caller for the identical key while the reservation is still in-flight
        var (processed, response) = await store.IsKeyProcessedAsync(keyInfo);

        // Assert
        processed.ShouldBeTrue();
        response.ShouldBeNull();
    }

    [Fact]
    public async Task IsKeyProcessedAsync_WhenKeyAlreadyMarkedProcessed_ReturnsCachedResponse()
    {
        // Arrange
        var store = CreateStore();
        var keyInfo = NewKeyInfo();
        var cached = new CachedResponse
        {
            StatusCode = 201,
            Body = "{\"id\":1}",
            ContentType = "application/json",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };
        await store.MarkKeyAsProcessedAsync(keyInfo, cached);

        // Act
        var (processed, response) = await store.IsKeyProcessedAsync(keyInfo);

        // Assert
        processed.ShouldBeTrue();
        response.ShouldBe(cached);
    }

    [Fact]
    public async Task IsKeyProcessedAsync_WhenCachedResponseExpired_TreatsKeyAsNewAndReserves()
    {
        // Arrange
        var store = CreateStore();
        var keyInfo = NewKeyInfo();
        var expired = new CachedResponse
        {
            StatusCode = 201,
            Body = "{}",
            ContentType = "application/json",
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
            ExpiresAt = DateTimeOffset.UtcNow.AddMilliseconds(-1)
        };
        await store.MarkKeyAsProcessedAsync(keyInfo, expired);

        // Act
        var (processed, response) = await store.IsKeyProcessedAsync(keyInfo);

        // Assert - an expired entry is a reservation win for a fresh caller, exactly like a brand-new key
        processed.ShouldBeFalse();
        response.ShouldBeNull();
    }

    [Fact]
    public async Task MarkKeyAsProcessedAsync_OverwritesInFlightReservation()
    {
        // Arrange
        var store = CreateStore();
        var keyInfo = NewKeyInfo();
        await store.IsKeyProcessedAsync(keyInfo); // reserve
        var cached = new CachedResponse
        {
            StatusCode = 200,
            Body = "done",
            ContentType = "text/plain",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };

        // Act
        await store.MarkKeyAsProcessedAsync(keyInfo, cached);
        var (processed, response) = await store.IsKeyProcessedAsync(keyInfo);

        // Assert
        processed.ShouldBeTrue();
        response.ShouldBe(cached);
    }

    [Fact]
    public async Task IsKeyProcessedAsync_DifferentKeys_ReserveIndependently()
    {
        // Arrange
        var store = CreateStore();

        // Act
        var (processedA, _) = await store.IsKeyProcessedAsync(NewKeyInfo("key-a"));
        var (processedB, _) = await store.IsKeyProcessedAsync(NewKeyInfo("key-b"));

        // Assert
        processedA.ShouldBeFalse();
        processedB.ShouldBeFalse();
    }

    /// <summary>
    ///     Memory-bound tolerance chosen for this cycle: the sweep runs on a write-count cadence (every
    ///     <c>SweepWatermark</c> = 256 writes) rather than a per-entry timer, so the bound is asserted as an
    ///     entry count immediately after the watermark write, not a time- or GC-based measurement. Write 250
    ///     already-expired entries (below the watermark, no sweep yet) then 6 live entries; the 256th write
    ///     crosses the watermark and the sweep must have discarded exactly the 250 expired ones.
    /// </summary>
    [Fact]
    public async Task MarkKeyAsProcessedAsync_CrossingSweepWatermark_RemovesOnlyExpiredEntries()
    {
        // Arrange
        var store = CreateStore();
        var expired = new CachedResponse
        {
            StatusCode = 200,
            Body = "gone",
            ContentType = "text/plain",
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
            ExpiresAt = DateTimeOffset.UtcNow.AddMilliseconds(-1)
        };
        var live = new CachedResponse
        {
            StatusCode = 200,
            Body = "kept",
            ContentType = "text/plain",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };

        // Act
        for (var i = 0; i < 250; i++)
            await store.MarkKeyAsProcessedAsync(NewKeyInfo($"expired-{i}"), expired);
        for (var i = 0; i < 6; i++)
            await store.MarkKeyAsProcessedAsync(NewKeyInfo($"live-{i}"), live);

        // Assert - the 256th write triggered the sweep; only the 6 live entries remain
        var backing = GetBackingStore(store);
        backing.Count.ShouldBe(6);
        backing.Values.ShouldAllBe(v => v == live);
    }

    #endregion
}
