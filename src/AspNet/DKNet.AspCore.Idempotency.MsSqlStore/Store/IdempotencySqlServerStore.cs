// <copyright file="IdempotencySqlServerStore.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.AspCore.Idempotency.MsSqlStore.Data;
using DKNet.AspCore.Idempotency.Relational.Store;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DKNet.AspCore.Idempotency.MsSqlStore.Store;

/// <summary>
///     MS SQL Server implementation of the idempotency key store. The reserve/check/complete flow,
///     migration guard and expired-reservation reclaim are all shared — see
///     <see cref="IdempotencyRelationalStore{TContext}" />; this type only supplies SQL Server's own
///     unique-violation detection.
/// </summary>
internal sealed class IdempotencySqlServerStore(
    IServiceProvider serviceProvider,
    IOptions<IdempotencyOptions> options,
    ILogger<IdempotencySqlServerStore> logger)
    : IdempotencyRelationalStore<IdempotencyDbContext>(serviceProvider, options, logger)
{
    #region Methods

    /// <summary>
    ///     Determines whether a <see cref="DbUpdateException" /> was caused by the unique-key violation on
    ///     <c>CompositeKey</c> — SQL Server reports this as error number 2601 (duplicate key in a unique
    ///     index, e.g. <c>UX_CompositeKey</c>) or 2627 (unique/primary key constraint violation), not a
    ///     message substring, which is localized by the server's login language.
    /// </summary>
    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };

    /// <inheritdoc />
    protected override bool IsProviderUniqueViolation(DbUpdateException ex) => IsUniqueViolation(ex);

    #endregion
}
