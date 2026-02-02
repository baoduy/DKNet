// <copyright file="IdempotencySqlServerStore.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.AspCore.Idempotency.MsSqlStore.Data;
using DKNet.AspCore.Idempotency.Store;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DKNet.AspCore.Idempotency.MsSqlStore.Store;

/// <summary>
///     MS SQL Server implementation of the idempotency key store using Entity Framework Core.
///     Provides persistent, reliable idempotency across application restarts and distributed environments.
/// </summary>
internal sealed class IdempotencySqlServerStore(
    IServiceProvider serviceProvider,
    ILogger<IdempotencySqlServerStore> logger) : IIdempotencyKeyStore, IAsyncDisposable
{
    #region Fields

    private static bool _dbMigrationsEnsured;

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
        if (_dbMigrationsEnsured) return;
        if ((await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
            await dbContext.Database.MigrateAsync(cancellationToken);
        _dbMigrationsEnsured = true;
    }

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
            return (false, null);
        }

        if (existing.IsExpired)
        {
            logger.LogDebug("Idempotency key has expired: {Key}", sanitizedKey);
            return (false, null);
        }

        logger.LogInformation(
            "Idempotency key found with status code {StatusCode}: {Key}",
            existing.StatusCode,
            sanitizedKey);

        var cachedResponse = new CachedResponse
        {
            StatusCode = existing.StatusCode,
            Body = existing.Body,
            ContentType = existing.ContentType ?? "application/json",
            CreatedAt = existing.CreatedAt,
            ExpiresAt = existing.ExpiresAt
        };

        return (true, cachedResponse);
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

            var entity = new IdempotencyKeyEntity(keyInfo, cachedResponse);
            await EnsureDatabaseCreatedAsync(dbContext);

            dbContext.IdempotencyKeys.Add(entity);
            await dbContext.SaveChangesAsync().ConfigureAwait(false);

            logger.LogInformation(
                "Successfully stored idempotency key with status code {StatusCode}: {Key}",
                cachedResponse.StatusCode,
                sanitizedKey);
        }
        catch (DbUpdateException ex) when ((ex.InnerException?.Message??ex.Message).Contains("UNIQUE",StringComparison.OrdinalIgnoreCase))
        {
            // Handle race condition: Another concurrent request already inserted this key
            logger.LogInformation(
                "Idempotency key already processed by concurrent request: {Key}. Continuing without duplicate insert.",
                sanitizedKey);
        }
    }

    #endregion
}