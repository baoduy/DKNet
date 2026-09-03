// <copyright file="IdempotencyInMemoryStoreWarning.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DKNet.AspCore.Idempotency.Store;

/// <summary>
///     Logs a single startup warning when the resolved <see cref="IIdempotencyKeyStore" /> is the process-local
///     <see cref="IdempotencyInMemoryStore" />, so operators are told that idempotency keys are not durable and
///     not shared between instances. Resolving any named store instead emits no warning.
/// </summary>
internal sealed class IdempotencyInMemoryStoreWarning(
    IServiceProvider serviceProvider,
    ILogger<IdempotencyInMemoryStoreWarning> logger) : IHostedService
{
    #region Methods

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var store = serviceProvider.GetRequiredService<IIdempotencyKeyStore>();

        if (store is IdempotencyInMemoryStore)
            logger.LogWarning(
                "Idempotency keys are stored in the process's memory (IdempotencyInMemoryStore): they are lost " +
                "on restart and are not shared between instances. This default is intended for local development " +
                "and unit tests - register a durable store (SQL Server, PostgreSQL, or Redis) for production.");

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    #endregion
}
