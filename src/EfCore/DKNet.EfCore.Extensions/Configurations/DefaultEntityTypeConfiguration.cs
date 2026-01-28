// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: DefaultEntityTypeConfiguration.cs
// Description: Base EF Core entity type configuration that applies common conventions (Id, auditing, concurrency).

using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.Extensions.Convertors;
using DKNet.Fw.Extensions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DKNet.EfCore.Extensions.Configurations;

/// <summary>
///     Base entity type configuration that applies common conventions to entity types.
///     This configuration will:
///     - Configure the primary key if an "Id" property exists (and apply value generation for numeric and Guid keys),
///     - Configure audited properties (CreatedBy, CreatedOn, UpdatedBy, UpdatedOn) when the entity implements
///     <see cref="IAuditedProperties" />, and
///     - Configure row-version concurrency token when the entity implements <see cref="IConcurrencyEntity{T}" />.
/// </summary>
/// <typeparam name="TEntity">The entity CLR type being configured.</typeparam>
public abstract class DefaultEntityTypeConfiguration<TEntity> : IEntityTypeConfiguration<TEntity> where TEntity : class
{
    #region Methods

    /// <summary>
    ///     Applies the default configuration conventions to the provided <paramref name="builder" /> for the entity type.
    ///     Implementations that inherit from this class can call the base implementation and then apply additional
    ///     configuration specific to the entity type.
    /// </summary>
    /// <param name="builder">The EF Core <see cref="EntityTypeBuilder{TEntity}" /> used to configure the entity type.</param>
    public virtual void Configure(EntityTypeBuilder<TEntity> builder)
    {
        var clrType = builder.Metadata.ClrType;

        const string idKey = nameof(IEntity<>.Id);

        // Handle IEntity<T> to set the primary key
        var idProperty = clrType.GetProperty(idKey);
        if (idProperty != null)
        {
            builder.HasKey(idKey);
            if (idProperty.PropertyType.IsNumericType())
                builder.Property(idKey)
                    .ValueGeneratedOnAdd();
            else if (idProperty.PropertyType == typeof(Guid))
                builder.Property(idKey)
                    .ValueGeneratedOnAdd()
                    .HasValueGenerator<GuidV7ValueGenerator>();
        }

        // Handle audit properties
        if (clrType.IsImplementOf<IAuditedProperties>())
        {
            builder.Property(nameof(IAuditedProperties.CreatedBy))
                .IsRequired()
                .HasMaxLength(255)
                .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

            builder.Property(nameof(IAuditedProperties.CreatedOn))
                .IsRequired()
                .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

            builder.Property(nameof(IAuditedProperties.UpdatedBy))
                .HasMaxLength(255);

            builder.Property(nameof(IAuditedProperties.UpdatedOn));
        }

        if (clrType.IsImplementOf(typeof(IConcurrencyEntity<>)))
            builder.Property(nameof(IConcurrencyEntity<>.RowVersion))
                .IsConcurrencyToken()
                .IsRowVersion()
                .ValueGeneratedOnAddOrUpdate();
    }

    #endregion
}