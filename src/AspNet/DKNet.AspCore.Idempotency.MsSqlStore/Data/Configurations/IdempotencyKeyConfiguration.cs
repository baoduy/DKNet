// <copyright file="IdempotencyKeyConfiguration.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DKNet.AspCore.Idempotency.MsSqlStore.Data.Configurations;

/// <summary>
///     Entity configuration for IdempotencyKeyEntity using EF Core 10 IEntityTypeConfiguration pattern.
///     This separates entity mapping configuration from the DbContext for better testability and maintainability.
/// </summary>
internal sealed class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKeyEntity>
{
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
            .HasColumnType("nvarchar(max)")
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
        builder.ToTable(t => { t.HasCheckConstraint("CK_StatusCode_Valid", "[StatusCode] BETWEEN 100 AND 599"); });
    }

    #endregion
}