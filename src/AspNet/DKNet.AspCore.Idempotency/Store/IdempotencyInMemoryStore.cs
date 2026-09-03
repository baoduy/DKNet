// <copyright file="IdempotencyInMemoryStore.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using DKNet.AspCore.Idempotency.Filtering;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DKNet.AspCore.Idempotency.Store;

/// <summary>
///     Process-local, in-memory implementation of <see cref="IIdempotencyKeyStore" /> backed by a
///     <see cref="ConcurrentDictionary{TKey,TValue}" />. This is the default store registered by the
///     parameterless <see cref="IdempotencySetup.AddIdempotentKey(Microsoft.Extensions.DependencyInjection.IServiceCollection,System.Action{IdempotencyOptions}?)" />
///     overload: no external service, no persistence, not shared across instances.
/// </summary>
internal sealed class IdempotencyInMemoryStore : IIdempotencyKeyStore
{
    #region Fields

    /// <summary>
    ///     HTTP 102 (Processing) is used as the sentinel status code for an in-flight reservation entry,
    ///     matching the convention used by the other <see cref="IIdempotencyKeyStore" /> implementations.
    /// </summary>
    private const int ReservationStatusCode = 102;

    /// <summary>
    ///     ponytail: full-dictionary sweep every <see cref="SweepWatermark" /> writes bounds memory with an
    ///     O(n) scan on a cadence rather than per-entry timers. Move to a time-wheel or a background sweep
    ///     if the dictionary grows large enough that this scan shows up in profiling.
    /// </summary>
    private const int SweepWatermark = 256;

    private readonly ConcurrentDictionary<string, CachedResponse> _store = new();
    private readonly IdempotencyOptions _options;
    private readonly ILogger<IdempotencyInMemoryStore> _logger;
    private long _writeCount;

    #endregion

    #region Constructors

    /// <summary>
    ///     Initializes a new instance of the <see cref="IdempotencyInMemoryStore" /> class.
    /// </summary>
    public IdempotencyInMemoryStore(IOptions<IdempotencyOptions> options, ILogger<IdempotencyInMemoryStore> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    /// <remarks>
    ///     Reserves via a <see cref="ConcurrentDictionary{TKey,TValue}.TryAdd" /> / <c>TryUpdate</c>
    ///     compare-and-swap loop instead of a separate lookup and insert: exactly one concurrent caller for a
    ///     given key ever observes the dictionary in the "absent, or expired" state and wins the swap, so
    ///     exactly one caller per key gets <c>(false, null)</c>.
    /// </remarks>
    public ValueTask<(bool processed, CachedResponse? response)> IsKeyProcessedAsync(IdempotentKeyInfo keyInfo)
    {
        var cacheKey = SanitizeKey(keyInfo.CompositeKey);

        while (true)
        {
            var reservation = NewReservation();

            if (_store.TryAdd(cacheKey, reservation))
            {
                _logger.LogDebug("No cached response found for key: {CacheKey}. Reserving.", cacheKey);
                OnWrite();
                return ValueTask.FromResult<(bool, CachedResponse?)>((false, null));
            }

            if (!_store.TryGetValue(cacheKey, out var existing))
                continue; // concurrently removed between TryAdd and TryGetValue - retry the reservation

            if (existing.IsExpired)
            {
                if (!_store.TryUpdate(cacheKey, reservation, existing))
                    continue; // another caller already replaced it - retry against the new value

                _logger.LogDebug("Cached response had expired for key: {CacheKey}. Reserving.", cacheKey);
                OnWrite();
                return ValueTask.FromResult<(bool, CachedResponse?)>((false, null));
            }

            if (existing.StatusCode == ReservationStatusCode)
            {
                _logger.LogDebug("Reservation still in-flight for key: {CacheKey}", cacheKey);
                return ValueTask.FromResult<(bool, CachedResponse?)>((true, null));
            }

            _logger.LogDebug("Cached response found for key: {CacheKey} with status code: {StatusCode}",
                cacheKey, existing.StatusCode);
            return ValueTask.FromResult<(bool, CachedResponse?)>((true, existing));
        }
    }

    /// <inheritdoc />
    public ValueTask MarkKeyAsProcessedAsync(IdempotentKeyInfo keyInfo, CachedResponse cachedResponse)
    {
        var cacheKey = SanitizeKey(keyInfo.CompositeKey);
        _store[cacheKey] = cachedResponse;

        _logger.LogInformation("Response cached for key: {CacheKey} with status code: {StatusCode}",
            cacheKey, cachedResponse.StatusCode);

        OnWrite();
        return ValueTask.CompletedTask;
    }

    private CachedResponse NewReservation()
    {
        var now = DateTimeOffset.UtcNow;
        return new CachedResponse
        {
            StatusCode = ReservationStatusCode,
            Body = null,
            ContentType = string.Empty,
            CreatedAt = now,
            ExpiresAt = now.Add(_options.InFlightReservationTimeout)
        };
    }

    /// <summary>
    ///     Bumps the write counter and, once it crosses <see cref="SweepWatermark" />, sweeps every expired
    ///     entry out of the dictionary so memory held does not grow with keys older than one retention window.
    /// </summary>
    private void OnWrite()
    {
        if (Interlocked.Increment(ref _writeCount) % SweepWatermark != 0) return;

        foreach (var entry in _store)
        {
            if (entry.Value.IsExpired)
                _store.TryRemove(new KeyValuePair<string, CachedResponse>(entry.Key, entry.Value));
        }
    }

    /// <summary>
    ///     Sanitizes an idempotency key for use as a dictionary key by hashing it, mirroring the scheme the
    ///     other <see cref="IIdempotencyKeyStore" /> implementations use.
    /// </summary>
    private string SanitizeKey(string key) =>
        $"{_options.CachePrefix}{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)))}";

    #endregion
}
