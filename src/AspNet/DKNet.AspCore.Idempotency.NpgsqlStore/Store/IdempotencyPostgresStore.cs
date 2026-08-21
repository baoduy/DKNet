// <copyright file="IdempotencyPostgresStore.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.AspCore.Idempotency.NpgsqlStore.Data;
using DKNet.AspCore.Idempotency.Relational.Store;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace DKNet.AspCore.Idempotency.NpgsqlStore.Store;

/// <summary>
///     PostgreSQL implementation of the idempotency key store. The reserve/check/complete flow,
///     migration guard and expired-reservation reclaim are all shared — see
///     <see cref="IdempotencyRelationalStore{TContext}" />; this type only supplies PostgreSQL's own
///     unique-violation detection.
/// </summary>
internal sealed class IdempotencyPostgresStore(
    IServiceProvider serviceProvider,
    ILogger<IdempotencyPostgresStore> logger,
    IOptions<IdempotencyOptions> options)
    : IdempotencyRelationalStore<IdempotencyDbContext>(serviceProvider, options, logger)
{
    #region Methods

    /// <summary>
    ///     Determines whether a <see cref="DbUpdateException" /> was caused by the unique-key violation on
    ///     <c>CompositeKey</c> — Postgres/Npgsql reports this as SqlState 23505, not a message substring.
    /// </summary>
    protected override bool IsProviderUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    #endregion
}
