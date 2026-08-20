// <copyright file="IdempotencyKeyConfiguration.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace DKNet.AspCore.Idempotency.MsSqlStore.Data.Configurations;

/// <summary>
///     SQL Server overrides for the shared idempotency key mapping — see
///     <see cref="DKNet.AspCore.Idempotency.Relational.Data.Configurations.IdempotencyKeyConfiguration" />
///     for the mapping every relational provider shares.
/// </summary>
internal sealed class IdempotencyKeyConfiguration
    : DKNet.AspCore.Idempotency.Relational.Data.Configurations.IdempotencyKeyConfiguration
{
    #region Properties

    /// <inheritdoc />
    protected override string BodyColumnType => "nvarchar(max)";

    /// <inheritdoc />
    protected override string StatusCodeCheckConstraintSql => "[StatusCode] BETWEEN 100 AND 599";

    #endregion
}
