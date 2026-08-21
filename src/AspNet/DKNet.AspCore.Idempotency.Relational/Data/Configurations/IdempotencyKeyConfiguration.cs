// <copyright file="IdempotencyKeyConfiguration.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DKNet.AspCore.Idempotency.Relational.Data.Configurations;

/// <summary>
///     Shared entity configuration for <see cref="IdempotencyKeyEntity" /> using EF Core 10
///     <see cref="IEntityTypeConfiguration{TEntity}" /> pattern. Every mapping that is identical across
///     relational providers lives here; the two spots that are genuinely provider-specific — the
///     <see cref="IdempotencyKeyEntity.Body" /> column type and the <c>CK_StatusCode_Valid</c> check-constraint SQL — are left
///     to <see cref="BodyColumnType" /> and <see cref="StatusCodeCheckConstraintSql" />, which each
///     provider's own configuration subclass overrides.
/// </summary>
internal abstract class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKeyEntity>
{
    #region Properties

    /// <summary>
    ///     Gets the provider-specific column type for the cached response body
    ///     (e.g. <c>nvarchar(max)</c> for SQL Server, <c>text</c> for PostgreSQL).
    /// </summary>
    protected abstract string BodyColumnType { get; }

    /// <summary>
    ///     Gets the provider-specific check-constraint SQL enforcing the valid HTTP status code range,
    ///     which differs only in how each provider quotes the column identifier.
    /// </summary>
    protected abstract string StatusCodeCheckConstraintSql { get; }

    #endregion

    #region Methods

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<IdempotencyKeyEntity> builder)
    {
        // Primary Key
        builder.HasKey(k => k.Id);
        builder.Property(k => k.Id);

        // Idempotency key fields
        builder.Property(k => k.IdempotentKey)
            .IsRequired()
            .HasMaxLength(150)
            .IsUnicode(false);

        builder.Property(k => k.Endpoint)
            .IsRequired()
            .HasMaxLength(250)
            .IsUnicode();

        builder.Property(k => k.Method)
            .IsRequired()
            .HasMaxLength(20)
            .IsUnicode(false);

        builder.Property(k => k.CompositeKey)
            .IsRequired()
            .HasMaxLength(128)
            .IsUnicode();

        // StatusCode field - HTTP status code
        builder.Property(k => k.StatusCode)
            .IsRequired();

        // ResponseBody field - cached response payload
        builder.Property(k => k.Body)
            .HasMaxLength(1048576)
            .HasColumnType(BodyColumnType)
            .IsUnicode();

        // ContentType field - MIME type
        builder.Property(k => k.ContentType)
            .HasMaxLength(256)
            .IsUnicode(false);

        // CreatedAt field - auto-set on insert
        builder.Property(k => k.CreatedAt);

        // ExpiresAt field - for TTL and cleanup
        builder.Property(k => k.ExpiresAt);

        // Index for fast expiration cleanup queries
        builder.HasIndex(k => k.ExpiresAt);

        // Unique constraint on CompositeKey to prevent race conditions
        // Ensures only one entry per idempotency key per endpoint/method combination
        builder.HasIndex(k => k.CompositeKey)
            .IsUnique()
            .HasDatabaseName("UX_CompositeKey");

        // Database constraints for data integrity
        builder.ToTable(t => { t.HasCheckConstraint("CK_StatusCode_Valid", StatusCodeCheckConstraintSql); });
    }

    #endregion
}
