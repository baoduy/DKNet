// <copyright file="IdempotencySqlServerStore.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.AspCore.Idempotency.MsSqlStore.Data;
using DKNet.AspCore.Idempotency.Store;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DKNet.AspCore.Idempotency.MsSqlStore.Store;

/// <summary>
///     MS SQL Server implementation of the idempotency key store using Entity Framework Core.
///     Provides persistent, reliable idempotency across application restarts and distributed environments.
/// </summary>
internal sealed class IdempotencySqlServerStore(
    IServiceProvider serviceProvider,
    IOptions<IdempotencyOptions> options,
    ILogger<IdempotencySqlServerStore> logger) : IIdempotencyKeyStore, IAsyncDisposable
{
    #region Fields

    /// <summary>
    ///     HTTP 102 (Processing) is used as the sentinel status code for an in-flight reservation row —
    ///     legal under <c>CK_StatusCode_Valid</c> (100-599) and outside the range any real completed
    ///     response would use.
    /// </summary>
    private const int ReservationStatusCode = 102;

    private static int _dbMigrationsEnsured;
    private static readonly SemaphoreSlim MigrationLock = new(1, 1);

    private readonly IdempotencyOptions _options = options.Value;
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
    ///     Maps a stored entity to its <see cref="CachedResponse" /> representation for replay.
    /// </summary>
    private static CachedResponse ToCachedResponse(IdempotencyKeyEntity entity) =>
        new()
        {
            StatusCode = entity.StatusCode,
            Body = entity.Body,
            ContentType = entity.ContentType ?? "application/json",
            CreatedAt = entity.CreatedAt,
            ExpiresAt = entity.ExpiresAt
        };

    /// <summary>
    ///     Determines whether a <see cref="DbUpdateException" /> was caused by the unique-key violation on
    ///     <c>CompositeKey</c> — SQL Server reports this as error number 2601 (duplicate key in a unique
    ///     index, e.g. <c>UX_CompositeKey</c>) or 2627 (unique/primary key constraint violation), not a
    ///     message substring, which is localized by the server's login language.
    /// </summary>
    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };

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

        if (existing != null && !existing.IsExpired)
        {
            if (existing.StatusCode == ReservationStatusCode)
            {
                logger.LogDebug("Idempotency key reservation still in-flight: {Key}", sanitizedKey);
                return (true, null);
            }

            logger.LogInformation(
                "Idempotency key found with status code {StatusCode}: {Key}",
                existing.StatusCode,
                sanitizedKey);

            return (true, ToCachedResponse(existing));
        }

        // No completed row (and no live reservation) found — reserve this key for the current caller so a
        // concurrent request for the identical key can never also observe an empty slot.
        logger.LogDebug("Idempotency key not found or expired, reserving: {Key}", sanitizedKey);

        var reservation = new IdempotencyKeyEntity(keyInfo, new CachedResponse
        {
            StatusCode = ReservationStatusCode,
            Body = null,
            ContentType = string.Empty,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.Add(_options.InFlightReservationTimeout)
        });

        try
        {
            dbContext.IdempotencyKeys.Add(reservation);
            await dbContext.SaveChangesAsync().ConfigureAwait(false);
            return (false, null);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Handle race condition: another concurrent request already reserved or completed this key.
            logger.LogInformation(
                "Idempotency key reservation collided with a concurrent request: {Key}. Re-checking status.",
                sanitizedKey);

            var concurrent = await dbContext.IdempotencyKeys
                .AsNoTracking()
                .FirstOrDefaultAsync(k => k.CompositeKey == sanitizedKey)
                .ConfigureAwait(false);

            if (concurrent is null || concurrent.IsExpired)
            {
                // The competing row is gone or has since expired — treat the key as not found, matching
                // Rule R1: an abandoned reservation must not permanently block retries.
                return (false, null);
            }

            if (concurrent.StatusCode == ReservationStatusCode) return (true, null);

            return (true, ToCachedResponse(concurrent));
        }
    }

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

            var entity = await dbContext.IdempotencyKeys
                .FirstOrDefaultAsync(k => k.CompositeKey == sanitizedKey)
                .ConfigureAwait(false);

            if (entity is null)
            {
                // Defensive only: should not happen once IsKeyProcessedAsync always reserves first.
                entity = new IdempotencyKeyEntity(keyInfo, cachedResponse);
                dbContext.IdempotencyKeys.Add(entity);
            }
            else
            {
                entity.Complete(cachedResponse);
            }

            await dbContext.SaveChangesAsync().ConfigureAwait(false);

            logger.LogInformation(
                "Successfully stored idempotency key with status code {StatusCode}: {Key}",
                cachedResponse.StatusCode,
                sanitizedKey);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
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
