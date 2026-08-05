// <copyright file="IdempotencyPostgresStore.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.AspCore.Idempotency.NpgsqlStore.Data;
using DKNet.AspCore.Idempotency.Store;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace DKNet.AspCore.Idempotency.NpgsqlStore.Store;

/// <summary>
///     PostgreSQL implementation of the idempotency key store using Entity Framework Core.
///     Provides persistent, reliable idempotency across application restarts and distributed environments.
/// </summary>
internal sealed class IdempotencyPostgresStore(
    IServiceProvider serviceProvider,
    ILogger<IdempotencyPostgresStore> logger,
    IOptions<IdempotencyOptions> options) : IIdempotencyKeyStore, IAsyncDisposable
{
    #region Fields

    private static int _dbMigrationsEnsured;
    private static readonly SemaphoreSlim MigrationLock = new(1, 1);

    private readonly AsyncServiceScope _scope = serviceProvider.CreateAsyncScope();

    #endregion

    #region Methods

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
    }

    private static async ValueTask EnsureDatabaseCreatedAsync(DbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _dbMigrationsEnsured, 0, 0) == 1) return;

        await MigrationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Interlocked.CompareExchange(ref _dbMigrationsEnsured, 0, 0) == 1) return;

            if ((await dbContext.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).Any())
                await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

            Interlocked.Exchange(ref _dbMigrationsEnsured, 1);
        }
        finally
        {
            MigrationLock.Release();
        }
    }

    /// <summary>
    ///     Determines whether a <see cref="DbUpdateException" /> was caused by the unique-key violation on
    ///     <c>CompositeKey</c> — Postgres/Npgsql reports this as SqlState 23505, not a message substring.
    /// </summary>
    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    /// <inheritdoc />
    public async ValueTask<(bool processed, CachedResponse? response)> IsKeyProcessedAsync(IdempotentKeyInfo keyInfo)
    {
        var sanitizedKey = IdempotencyKeyEntity.SanitizeKey(keyInfo.CompositeKey);

        logger.LogDebug("Checking if idempotency key has been processed: {Key}", sanitizedKey);

        var factory =
            _scope.ServiceProvider.GetRequiredService<IDbContextFactory<IdempotencyDbContext>>();
        await using var dbContext = await factory.CreateDbContextAsync();
        await EnsureDatabaseCreatedAsync(dbContext);

        var existing = await dbContext.IdempotencyKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.CompositeKey == sanitizedKey && k.ExpiresAt > DateTime.UtcNow)
            .ConfigureAwait(false);

        if (existing == null)
        {
            logger.LogDebug("Idempotency key not found or expired: {Key}", sanitizedKey);
            return await ReserveKeyAsync(dbContext, keyInfo, sanitizedKey).ConfigureAwait(false);
        }

        if (existing.IsExpired)
        {
            logger.LogDebug("Idempotency key has expired: {Key}", sanitizedKey);
            return await ReserveKeyAsync(dbContext, keyInfo, sanitizedKey).ConfigureAwait(false);
        }

        logger.LogInformation(
            "Idempotency key found with status code {StatusCode}: {Key}",
            existing.StatusCode,
            sanitizedKey);

        return (true, ToCachedResponse(existing));
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
        IdempotencyDbContext dbContext, IdempotentKeyInfo keyInfo, string sanitizedKey)
    {
        try
        {
            var reservation = new IdempotencyKeyEntity(keyInfo, new CachedResponse
            {
                StatusCode = 102,
                Body = null,
                ContentType = "application/json",
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow + options.Value.InFlightReservationTimeout
            });

            dbContext.IdempotencyKeys.Add(reservation);
            await dbContext.SaveChangesAsync().ConfigureAwait(false);

            return (false, null);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
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
                return (true, blocking.StatusCode == 102 ? null : ToCachedResponse(blocking));
            }

            // The row blocking our insert is itself expired (a stale reservation or completed entry
            // nothing ever purged). Treat this request as not-processed and let it run the handler;
            // MarkKeyAsProcessedAsync reclaims that row in place once processing completes.
            logger.LogDebug("Blocking idempotency key row has expired, proceeding as new: {Key}", sanitizedKey);
            return (false, null);
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
    public async ValueTask MarkKeyAsProcessedAsync(IdempotentKeyInfo keyInfo, CachedResponse cachedResponse)
    {
        var sanitizedKey = IdempotencyKeyEntity.SanitizeKey(keyInfo.CompositeKey);

        logger.LogDebug(
            "Marking idempotency key as processed with status code {StatusCode}: {Key}",
            cachedResponse.StatusCode,
            sanitizedKey);

        try
        {
            var factory =
                _scope.ServiceProvider.GetRequiredService<IDbContextFactory<IdempotencyDbContext>>();
            await using var dbContext = await factory.CreateDbContextAsync();
            await EnsureDatabaseCreatedAsync(dbContext);

            var reservation = await dbContext.IdempotencyKeys
                .FirstOrDefaultAsync(k => k.CompositeKey == sanitizedKey)
                .ConfigureAwait(false);

            if (reservation != null)
                reservation.Complete(cachedResponse);
            else
                // Defensive fallback only - should not happen now that IsKeyProcessedAsync always
                // reserves the row before the handler runs.
                dbContext.IdempotencyKeys.Add(new IdempotencyKeyEntity(keyInfo, cachedResponse));

            await dbContext.SaveChangesAsync().ConfigureAwait(false);

            logger.LogInformation(
                "Successfully stored idempotency key with status code {StatusCode}: {Key}",
                cachedResponse.StatusCode,
                sanitizedKey);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Handle race condition: Another concurrent request already inserted this key.
            // Unreachable in the common path now, kept as a defensive guard around the fallback Add() above.
            logger.LogInformation(
                "Idempotency key already processed by concurrent request: {Key}. Continuing without duplicate insert.",
                sanitizedKey);
        }
    }

    #endregion
}
