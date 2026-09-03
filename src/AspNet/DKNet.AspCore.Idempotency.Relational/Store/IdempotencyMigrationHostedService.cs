// <copyright file="IdempotencyMigrationHostedService.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace DKNet.AspCore.Idempotency.Relational.Store;

/// <summary>
///     Applies any pending idempotency schema migrations once, before the host starts serving requests.
/// </summary>
/// <typeparam name="TContext">The provider's own concrete <see cref="Data.IdempotencyDbContext" />.</typeparam>
/// <param name="dbContextFactory">Factory used to create a short-lived context for the migration.</param>
/// <remarks>
///     Moves <c>MigrateAsync</c> off the request path: previously <see cref="IdempotencyRelationalStore{TContext}" />
///     ran the pending-migration check (and the migration itself) from the first store call after startup, while
///     holding a process-wide lock, which could block every concurrent request behind a schema migration. That
///     store still carries a defensive, per-instance-cached guard as a cheap fallback for hosts that bypass
///     regular startup (e.g. manual <see cref="IHostedService" /> ordering), but this hosted service is now the
///     primary mechanism.
/// </remarks>
internal sealed class IdempotencyMigrationHostedService<TContext>(IDbContextFactory<TContext> dbContextFactory)
    : IHostedService
    where TContext : DbContext
{
    #region Methods

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        if ((await dbContext.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).Any())
            await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    #endregion
}
