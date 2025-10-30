// <copyright file="ModelBuilderExtensions.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.EfCore.Encryption.Attributes;
using DKNet.EfCore.Encryption.Converters;
using DKNet.EfCore.Encryption.Encryption;
using Microsoft.EntityFrameworkCore;

namespace DKNet.EfCore.Encryption.Extensions;

/// <summary>
///     Provides extension methods for configuring column encryption in Entity Framework Core.
/// </summary>
public static class ModelBuilderExtensions
{
    #region Methods

    /// <summary>
    ///     Configures automatic column encryption for all string properties marked with the <see cref="EncryptedAttribute" />.
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <param name="encryptionKeyProvider">The encryption key provider that supplies encryption keys for each entity type.</param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="modelBuilder" /> or
    ///     <paramref name="encryptionKeyProvider" /> is null.
    /// </exception>
    /// <remarks>
    ///     This method scans all entity types in the model and applies encryption value converters
    ///     to string properties that are not primary or foreign keys and are marked with the <see cref="EncryptedAttribute" />
    ///     .
    /// </remarks>
    public static void UseColumnEncryption(this ModelBuilder modelBuilder, IEncryptionKeyProvider encryptionKeyProvider)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentNullException.ThrowIfNull(encryptionKeyProvider);

        var stringProperties = modelBuilder.Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties())
            .Where(p =>
                p.ClrType == typeof(string) &&
                !p.IsPrimaryKey() &&
                !p.IsForeignKey());

        foreach (var property in stringProperties)
        {
            var propertyInfo = property.PropertyInfo;
            if (propertyInfo == null || !Attribute.IsDefined(propertyInfo, typeof(EncryptedAttribute)))
            {
                continue;
            }

            var key = encryptionKeyProvider.GetKey(propertyInfo.DeclaringType!);
            var converter = new ColumnEncryptionConverter(new AesGcmColumnEncryptionProvider(key));
            property.SetValueConverter(converter);
        }
    }

    #endregion
}