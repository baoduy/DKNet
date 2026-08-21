// <copyright file="IdempotencyDbContext.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using Microsoft.EntityFrameworkCore;

namespace DKNet.AspCore.Idempotency.Relational.Data;

/// <summary>
///     Shared Entity Framework Core DbContext base for relational idempotency key storage.
///     Providers derive their own <c>internal sealed</c> context from this and supply their own
///     connection/provider configuration via their DI setup extension; this base only wires the
///     entity mapping shared by every relational provider.
/// </summary>
/// <param name="options">
///     The context options. Accepted as the non-generic <see cref="DbContextOptions" /> so a single
///     base constructor works regardless of which provider's closed <see cref="DbContextOptions{TContext}" />
///     the derived context declares.
/// </param>
internal abstract class IdempotencyDbContext(DbContextOptions options) : DbContext(options)
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
        // EF Core 10 auto-discovery of IEntityTypeConfiguration implementations, scanning the derived
        // (provider) assembly — each provider supplies its own concrete IdempotencyKeyConfiguration
        // there with the column type / check-constraint SQL this base leaves provider-agnostic.
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
    }

    #endregion
}
