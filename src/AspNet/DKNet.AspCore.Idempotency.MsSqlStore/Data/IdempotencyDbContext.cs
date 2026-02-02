// <copyright file="IdempotencyDbContext.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using Microsoft.EntityFrameworkCore;

namespace DKNet.AspCore.Idempotency.MsSqlStore.Data;

/// <summary>
///     Entity Framework Core DbContext for idempotency key storage in MS SQL Server.
///     Uses EF Core 10 primary constructor pattern for cleaner, more concise code.
/// </summary>
internal sealed class IdempotencyDbContext(DbContextOptions<IdempotencyDbContext> options) : DbContext(options)
{
    #region Properties

    /// <summary>
    ///     Gets or initializes the DbSet for IdempotencyKeyEntity entities.
    ///     Uses EF Core 10 'required' keyword to eliminate null suppression.
    /// </summary>
    public required DbSet<IdempotencyKeyEntity> IdempotencyKeys { get; init; }

    #endregion

    #region Methods

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // EF Core 10 auto-discovery of IEntityTypeConfiguration implementations
        // This will find and apply IdempotencyKeyConfiguration automatically
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdempotencyDbContext).Assembly);
    }

    #endregion
}