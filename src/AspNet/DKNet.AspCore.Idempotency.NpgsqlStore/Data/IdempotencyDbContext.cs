// <copyright file="IdempotencyDbContext.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using Microsoft.EntityFrameworkCore;

namespace DKNet.AspCore.Idempotency.NpgsqlStore.Data;

/// <summary>
///     PostgreSQL idempotency key storage context. All entity mapping is shared with every other
///     relational provider via <see cref="DKNet.AspCore.Idempotency.Relational.Data.IdempotencyDbContext" />;
///     this type exists so PostgreSQL gets its own closed <see cref="DbContextOptions{TContext}" /> and
///     migrations assembly. Uses EF Core 10 primary constructor pattern for cleaner, more concise code.
/// </summary>
internal sealed class IdempotencyDbContext(DbContextOptions<IdempotencyDbContext> options)
    : DKNet.AspCore.Idempotency.Relational.Data.IdempotencyDbContext(options);
