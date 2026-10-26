// <copyright file="ModelBuilderExtensions.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using DKNet.EfCore.Encryption.Attributes;
using DKNet.EfCore.Encryption.Converters;
using DKNet.EfCore.Encryption.Encryption;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

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
    /// <exception cref="InvalidOperationException">
    ///     Thrown when a member marked with <see cref="EncryptedAttribute" /> cannot be encrypted: a primary key or
    ///     foreign key, a member of a complex type (or a complex property itself), or a mapped member whose type is
    ///     not <see cref="string" />, such as a numeric, <c>byte[]</c>, enum or navigation member.
    /// </exception>
    /// <remarks>
    ///     This method scans all entity types in the model, owned types included, and applies encryption value
    ///     converters to string properties marked with the <see cref="EncryptedAttribute" />. Members EF Core does
    ///     not map are ignored.
    /// </remarks>
    public static void UseColumnEncryption(this ModelBuilder modelBuilder, IEncryptionKeyProvider encryptionKeyProvider)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentNullException.ThrowIfNull(encryptionKeyProvider);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var complexProperty in entityType.GetComplexProperties())
            {
                RefuseEncryptedComplexMembers(complexProperty);
            }

            foreach (var navigation in entityType.GetNavigations().Concat<IReadOnlyPropertyBase>(entityType.GetSkipNavigations()))
            {
                if (IsEncrypted(navigation.PropertyInfo))
                {
                    throw NotStringException(navigation.PropertyInfo, navigation.ClrType);
                }
            }
        }

        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetProperties()))
        {
            var propertyInfo = property.PropertyInfo;
            if (!IsEncrypted(propertyInfo))
            {
                continue;
            }

            if (property.ClrType != typeof(string))
            {
                throw NotStringException(propertyInfo, property.ClrType);
            }

            if (property.IsPrimaryKey() || property.IsForeignKey())
            {
                throw new InvalidOperationException(
                    $"Property '{propertyInfo.DeclaringType!.Name}.{propertyInfo.Name}' is marked with [Encrypted] but is a primary key or foreign key column, which is not supported.");
            }

            var key = encryptionKeyProvider.GetKey(propertyInfo.DeclaringType!);
            var converter = new ColumnEncryptionConverter(new AesGcmColumnEncryptionProvider(key));
            property.SetValueConverter(converter);
        }
    }

    private static bool IsEncrypted([NotNullWhen(true)] PropertyInfo? propertyInfo) =>
        propertyInfo != null && Attribute.IsDefined(propertyInfo, typeof(EncryptedAttribute));

    private static InvalidOperationException NotStringException(PropertyInfo propertyInfo, Type clrType) =>
        new(
            $"Property '{propertyInfo.DeclaringType!.Name}.{propertyInfo.Name}' is marked with [Encrypted] but is of type '{clrType.Name}', which is not supported. Only string properties can be encrypted.");

    private static void RefuseEncryptedComplexMembers(IReadOnlyComplexProperty complexProperty)
    {
        var members = complexProperty.ComplexType.GetProperties().Prepend<IReadOnlyPropertyBase>(complexProperty);
        foreach (var member in members)
        {
            if (IsEncrypted(member.PropertyInfo))
            {
                throw new InvalidOperationException(
                    $"Property '{member.PropertyInfo.DeclaringType!.Name}.{member.PropertyInfo.Name}' is marked with [Encrypted] but belongs to a complex type, which is not supported. Move it to an entity or owned type as a string property, or remove the attribute.");
            }
        }

        foreach (var nested in complexProperty.ComplexType.GetComplexProperties())
        {
            RefuseEncryptedComplexMembers(nested);
        }
    }

    #endregion
}