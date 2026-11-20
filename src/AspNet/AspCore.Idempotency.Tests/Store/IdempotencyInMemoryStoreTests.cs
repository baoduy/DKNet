// <copyright file="IdempotencyInMemoryStoreTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Collections.Concurrent;
using System.Diagnostics;
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

        // Act & Assert - repeated rounds, each with a fresh key and 25 callers that genuinely overlap: each
        // gets its own dedicated thread (TaskCreationOptions.LongRunning, not a plain Task.Run) so all 25
        // exist immediately regardless of the thread pool's min-thread count or injection rate - a bare
        // Task.Run here would make the first round's overlap depend on how fast the pool grows past its
        // starting size, which varies with core count and can starve on a small CI box. Each is held at a
        // Barrier until all 25 have arrived, so they hit the store's reserve loop at effectively the same
        // instant. A single sequential Get-then-Set pair - or the previous version of this test, which
        // called IsKeyProcessedAsync directly inside Select with no thread hop at all - would pass even
        // against a non-atomic implementation.
        for (var round = 0; round < 20; round++)
        {
            var keyInfo = NewKeyInfo();
            using var barrier = new Barrier(25);

            var tasks = Enumerable.Range(0, 25)
                .Select(_ => Task.Factory.StartNew(() =>
                {
                    barrier.SignalAndWait();
                    return store.IsKeyProcessedAsync(keyInfo).AsTask();
                }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap())
                .ToArray();
            var results = await Task.WhenAll(tasks);

            results.Count(r => !r.processed).ShouldBe(1);
            results.Count(r => r.processed).ShouldBe(24);
        }
    }

    /// <summary>
    ///     Negative control demonstrating the harness above is actually capable of failing: a check-then-act
    ///     store (look up "already reserved?", hold the write until another caller has also passed that check,
    ///     then write "reserved") reproduces the exact race DRK-1005 replaced the previous store for. Run
    ///     against the same 25-caller/Barrier harness, it must let more than one caller believe it won the
    ///     reservation - proving the harness contends the race rather than merely calling the store 25 times.
    /// </summary>
    [Fact]
    public void NonAtomicCheckThenActStandIn_ManyConcurrentCallers_MoreThanOneWinnerPerRound()
    {
        // Arrange
        var stub = new NonAtomicCheckThenActStore();
        var key = Guid.NewGuid().ToString();
        using var barrier = new Barrier(25);

        // Act - same dedicated-thread dispatch as the harness above, for the same reason: independence from
        // thread pool growth
        var tasks = Enumerable.Range(0, 25)
            .Select(_ => Task.Factory.StartNew(() =>
            {
                barrier.SignalAndWait();
                return stub.TryReserve(key);
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default))
            .ToArray();
        Task.WaitAll(tasks);

        // Assert - a check-then-act race lets more than one caller past the "not yet reserved" check
        tasks.Count(t => t.Result).ShouldBeGreaterThan(1);
    }

    /// <summary>
    ///     Reproduces the scheduler skew that made the negative control flaky under CPU load (DRK-2564): the
    ///     callers are released together, but every caller except the first is held 200 ms before its check -
    ///     far past a fixed 10 ms window, yet still overlapping the first caller's reservation attempt. A
    ///     window that only closes after another caller has also passed the check must still let more than
    ///     one caller win.
    /// </summary>
    [Fact]
    public void NonAtomicCheckThenActStandIn_CallersSkewedPastFixedWindow_MoreThanOneWinner()
    {
        // Arrange
        var stub = new NonAtomicCheckThenActStore();
        var key = Guid.NewGuid().ToString();
        using var barrier = new Barrier(25);

        // Act - caller 0 checks at once; callers 1..24 check 200 ms later
        var tasks = Enumerable.Range(0, 25)
            .Select(caller => Task.Factory.StartNew(() =>
            {
                barrier.SignalAndWait();
                if (caller > 0)
                    Thread.Sleep(200);
                return stub.TryReserve(key);
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default))
            .ToArray();
        Task.WaitAll(tasks);

        // Assert - the skewed callers overlapped the first caller's reservation, so more than one wins
        tasks.Count(t => t.Result).ShouldBeGreaterThan(1);
    }

    /// <summary>
    ///     Callers run one after another, so none overlaps another's check: the stand-in must give exactly one
    ///     winner - the negative control can still fail - and its bounded wait must not hang the run.
    /// </summary>
    [Fact]
    public void NonAtomicCheckThenActStandIn_SequentialCallers_ExactlyOneWinnerWithinBound()
    {
        // Arrange
        var stub = new NonAtomicCheckThenActStore();
        var key = Guid.NewGuid().ToString();
        var stopwatch = Stopwatch.StartNew();

        // Act
        var winners = Enumerable.Range(0, 25).Count(_ => stub.TryReserve(key));
        stopwatch.Stop();

        // Assert - only the first caller passes the check; it writes once its wait for a second checker
        // times out, so the elapsed time is that bound plus a generous margin for a loaded machine
        winners.ShouldBe(1);
        stopwatch.Elapsed.ShouldBeLessThan(NonAtomicCheckThenActStore.SecondCheckerWait + TimeSpan.FromSeconds(3));
    }

    /// <summary>
    ///     Deliberately non-atomic stand-in: separates the "is this key already reserved?" check from the
    ///     "reserve it" write, instead of a single compare-and-swap. After passing the check, a caller holds
    ///     its write until another caller has also passed the check, or until <see cref="SecondCheckerWait" />
    ///     expires - so overlapping callers race whatever the scheduler's skew, and a caller with no one
    ///     overlapping it still wins alone. Used only to prove the concurrency harness above can fail - never
    ///     a substitute for <see cref="IdempotencyInMemoryStore" />.
    /// </summary>
    private sealed class NonAtomicCheckThenActStore
    {
        public static readonly TimeSpan SecondCheckerWait = TimeSpan.FromSeconds(2);

        private readonly ConcurrentDictionary<string, bool> _seen = new();
        private int _passedCheck;

        public bool TryReserve(string key)
        {
            if (_seen.ContainsKey(key))
                return false; // someone else already reserved - this caller loses

            // window in which another caller can also observe "not yet reserved"
            Interlocked.Increment(ref _passedCheck);
            SpinWait.SpinUntil(() => Volatile.Read(ref _passedCheck) >= 2, SecondCheckerWait);
            _seen[key] = true;
            return true; // this caller believes it won the reservation
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
