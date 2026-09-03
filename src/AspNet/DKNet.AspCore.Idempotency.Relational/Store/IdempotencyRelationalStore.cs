// <copyright file="IdempotencyRelationalStore.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Collections.Concurrent;
using DKNet.AspCore.Idempotency.Filtering;
using DKNet.AspCore.Idempotency.Relational.Data;
using DKNet.AspCore.Idempotency.Store;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DKNet.AspCore.Idempotency.Relational.Store;

/// <summary>
///     Shared EF Core implementation of the idempotency key store, backing every relational provider
///     (SQL Server, PostgreSQL). Provides persistent, reliable idempotency across application restarts
///     and distributed environments.
/// </summary>
/// <typeparam name="TContext">The provider's own concrete <see cref="IdempotencyDbContext" />.</typeparam>
/// <param name="serviceProvider">The root service provider, used to create a scope per operation.</param>
/// <param name="options">The idempotency options.</param>
/// <param name="logger">The logger, typed to the concrete provider store by the derived constructor.</param>
internal abstract class IdempotencyRelationalStore<TContext>(
    IServiceProvider serviceProvider,
    IOptions<IdempotencyOptions> options,
    ILogger logger) : IIdempotencyKeyStore, IAsyncDisposable
    where TContext : IdempotencyDbContext
{
    #region Fields

    /// <summary>
    ///     HTTP 102 (Processing) is used as the sentinel status code for an in-flight reservation row —
    ///     legal under <c>CK_StatusCode_Valid</c> (100-599) and outside the range any real completed
    ///     response would use.
    /// </summary>
    private const int ReservationStatusCode = 102;

    // Migration now runs once at startup via IdempotencyMigrationHostedService, registered by the
    // MsSql/Npgsql setup extensions. This guard is a cheap defensive fallback only - for a host that
    // for some reason skips/reorders hosted services, the first request still self-heals the schema
    // instead of failing outright. Keyed by connection string rather than a single process-wide flag:
    // a process that targets more than one database over its lifetime (e.g. per-tenant databases, or
    // multiple test fixtures sharing one test host) must ensure migrations separately for each one, not
    // skip every database after the first is ensured. Declared per closed TContext, so each provider
    // gets its own guard.
    private static readonly ConcurrentDictionary<string, bool> DbMigrationsEnsured = new(StringComparer.Ordinal);
    private static readonly SemaphoreSlim MigrationLock = new(1, 1);

    private readonly IdempotencyOptions _options = options.Value;
    private readonly AsyncServiceScope _scope = serviceProvider.CreateAsyncScope();

    // Cached lazily on first use rather than read from DbContext.Database.GetConnectionString() (which
    // allocates) on every request; a given store instance always targets the same connection string.
    private string? _cachedConnectionString;

    #endregion

    #region Methods

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
    }

    /// <summary>
    ///     Determines whether a <see cref="DbUpdateException" /> was caused by the provider's own
    ///     unique-key violation on <c>CompositeKey</c>. Genuinely provider-specific: each database
    ///     reports this differently (error number, SQL state), never by a message substring, which is
    ///     localized by the server's login language.
    /// </summary>
    protected abstract bool IsProviderUniqueViolation(DbUpdateException ex);

    private async ValueTask EnsureDatabaseCreatedAsync(DbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var connectionString = _cachedConnectionString ??= dbContext.Database.GetConnectionString() ?? string.Empty;
        if (DbMigrationsEnsured.ContainsKey(connectionString)) return;

        await MigrationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (DbMigrationsEnsured.ContainsKey(connectionString)) return;

            if ((await dbContext.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).Any())
                await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

            DbMigrationsEnsured[connectionString] = true;
        }
        finally
        {
            MigrationLock.Release();
        }
    }

    private static CachedResponse ToCachedResponse(IdempotencyKeyEntity entity) =>
        new()
        {
            StatusCode = entity.StatusCode,
            Body = entity.Body,
            ContentType = entity.ContentType ?? "application/json",
            CreatedAt = entity.CreatedAt,
            ExpiresAt = entity.ExpiresAt
        };

    /// <inheritdoc />
    /// <remarks>
    ///     Reserves by inserting first rather than checking with a preliminary <c>SELECT</c>: the
    ///     unique index on <c>CompositeKey</c> already gives <see cref="ReserveKeyAsync" /> its
    ///     single-winner guarantee regardless of whether a row exists beforehand, so a prior read adds a
    ///     round trip without adding any atomicity a fresh caller does not already have. A brand-new key
    ///     now costs one round trip (the insert) instead of two (select, then insert); a key that already
    ///     has a result costs the same insert-then-collision-read the unique-violation path always used
    ///     for the racing case.
    /// </remarks>
    public async ValueTask<(bool processed, CachedResponse? response)> IsKeyProcessedAsync(IdempotentKeyInfo keyInfo)
    {
        var sanitizedKey = IdempotencyKeyEntity.SanitizeKey(keyInfo.CompositeKey);

        logger.LogDebug("Checking if idempotency key has been processed: {Key}", sanitizedKey);

        var factory = _scope.ServiceProvider.GetRequiredService<IDbContextFactory<TContext>>();
        await using var dbContext = await factory.CreateDbContextAsync();
        await EnsureDatabaseCreatedAsync(dbContext);

        return await ReserveKeyAsync(dbContext, keyInfo, sanitizedKey).ConfigureAwait(false);
    }

    /// <summary>
    ///     Attempts to atomically reserve <paramref name="sanitizedKey" /> by inserting a
    ///     <c>StatusCode == 102</c> placeholder row, relying on the <c>UX_CompositeKey</c> unique index to
    ///     serialize concurrent callers — only the caller whose insert succeeds proceeds to run the
    ///     protected handler.
    /// </summary>
    /// <param name="dbContext">The open database context to reserve the key on.</param>
    /// <param name="keyInfo">The idempotency key information for the reservation row.</param>
    /// <param name="sanitizedKey">The pre-computed, sanitized composite key.</param>
    /// <returns>
    ///     <c>(false, null)</c> if this call reserved the key and the caller should proceed with
    ///     processing; otherwise <c>(true, response)</c> with the completed duplicate's cached response,
    ///     or <c>(true, null)</c> if another caller currently holds an unexpired in-flight reservation.
    /// </returns>
    private async ValueTask<(bool processed, CachedResponse? response)> ReserveKeyAsync(
        TContext dbContext, IdempotentKeyInfo keyInfo, string sanitizedKey)
    {
        try
        {
            var reservation = new IdempotencyKeyEntity(keyInfo, new CachedResponse
            {
                StatusCode = ReservationStatusCode,
                Body = null,
                ContentType = "application/json",
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow + _options.InFlightReservationTimeout
            });

            dbContext.IdempotencyKeys.Add(reservation);
            await dbContext.SaveChangesAsync().ConfigureAwait(false);

            return (false, null);
        }
        catch (DbUpdateException ex) when (IsProviderUniqueViolation(ex))
        {
            // A concurrent request already holds this composite key - find out what state it's in.
            var blocking = await dbContext.IdempotencyKeys
                .AsNoTracking()
                .FirstOrDefaultAsync(k => k.CompositeKey == sanitizedKey)
                .ConfigureAwait(false);

            if (blocking is { IsExpired: false })
            {
                logger.LogInformation(
                    "Idempotency key already reserved or processed by a concurrent request: {Key}",
                    sanitizedKey);
                return (true, blocking.StatusCode == ReservationStatusCode ? null : ToCachedResponse(blocking));
            }

            // The row blocking our insert is itself expired (a stale reservation or completed entry
            // nothing ever purged). Reclaiming it with a plain read-then-write would reopen the same
            // race this method exists to close, so reclaim it atomically: a conditional UPDATE that
            // only matches while the row is still expired. Its affected-row count gives the same
            // single-winner guarantee the unique index gives the fresh-insert path - only the caller
            // whose UPDATE actually flips the row wins; every other concurrent racer's UPDATE affects
            // zero rows once the winner has moved ExpiresAt into the future.
            var now = DateTime.UtcNow;
            var reclaimed = await dbContext.IdempotencyKeys
                .Where(k => k.CompositeKey == sanitizedKey && k.ExpiresAt != null && k.ExpiresAt <= now)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(k => k.StatusCode, ReservationStatusCode)
                    .SetProperty(k => k.Body, (string?)null)
                    .SetProperty(k => k.ContentType, "application/json")
                    .SetProperty(k => k.CreatedAt, now)
                    .SetProperty(k => k.ExpiresAt, now + _options.InFlightReservationTimeout))
                .ConfigureAwait(false);

            if (reclaimed == 1)
            {
                logger.LogDebug("Reclaimed expired idempotency key row, proceeding as new: {Key}", sanitizedKey);
                return (false, null);
            }

            // Another caller reclaimed (or completed) the row between our insert collision and this
            // reclaim attempt - re-read its current state and branch exactly like the unexpired path.
            var current = await dbContext.IdempotencyKeys
                .AsNoTracking()
                .FirstOrDefaultAsync(k => k.CompositeKey == sanitizedKey)
                .ConfigureAwait(false);

            logger.LogInformation(
                "Idempotency key already reserved or processed by a concurrent request: {Key}",
                sanitizedKey);
            return (true,
                current is null || current.StatusCode == ReservationStatusCode ? null : ToCachedResponse(current));
        }
    }

    /// <inheritdoc />
    /// <remarks>
    ///     Completes the reservation with a single <c>ExecuteUpdateAsync</c> statement instead of a
    ///     tracked <c>SELECT</c> followed by <c>SaveChanges</c> — the caller reaching this method already
    ///     holds the reservation (see <see cref="IsKeyProcessedAsync" />), so there is no concurrent
    ///     competitor to read-before-write against; only round trips are being removed, not a race window.
    /// </remarks>
    public async ValueTask MarkKeyAsProcessedAsync(IdempotentKeyInfo keyInfo, CachedResponse cachedResponse)
    {
        var sanitizedKey = IdempotencyKeyEntity.SanitizeKey(keyInfo.CompositeKey);

        logger.LogDebug(
            "Marking idempotency key as processed with status code {StatusCode}: {Key}",
            cachedResponse.StatusCode,
            sanitizedKey);

        try
        {
            var factory = _scope.ServiceProvider.GetRequiredService<IDbContextFactory<TContext>>();
            await using var dbContext = await factory.CreateDbContextAsync();
            await EnsureDatabaseCreatedAsync(dbContext);

            var updated = await dbContext.IdempotencyKeys
                .Where(k => k.CompositeKey == sanitizedKey)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(k => k.StatusCode, cachedResponse.StatusCode)
                    .SetProperty(k => k.Body, cachedResponse.Body)
                    .SetProperty(k => k.ContentType, cachedResponse.ContentType)
                    .SetProperty(k => k.ExpiresAt, cachedResponse.ExpiresAt))
                .ConfigureAwait(false);

            if (updated == 0)
            {
                // Defensive only: should not happen once IsKeyProcessedAsync always reserves first.
                dbContext.IdempotencyKeys.Add(new IdempotencyKeyEntity(keyInfo, cachedResponse));
                await dbContext.SaveChangesAsync().ConfigureAwait(false);
            }

            logger.LogInformation(
                "Successfully stored idempotency key with status code {StatusCode}: {Key}",
                cachedResponse.StatusCode,
                sanitizedKey);
        }
        catch (DbUpdateException ex) when (IsProviderUniqueViolation(ex))
        {
            // Handle race condition: another concurrent request already inserted this key.
            // Unreachable in the common path now, kept as a defensive guard around the fallback Add() above.
            logger.LogInformation(
                "Idempotency key already processed by concurrent request: {Key}. Continuing without duplicate insert.",
                sanitizedKey);
        }
    }

    #endregion
}
