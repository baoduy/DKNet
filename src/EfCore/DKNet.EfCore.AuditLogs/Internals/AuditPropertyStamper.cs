// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: AuditPropertyStamper.cs
// Description: Shared "created by"/"updated by" stamping helpers used by EfCoreAuditHook and, via
// InternalsVisibleTo, DKNet.EfCore.DataAuthorization's DataOwnerHook.

using System.Reflection;
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.Extensions.Snapshots;
using DKNet.Fw.Extensions.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace DKNet.EfCore.AuditLogs.Internals;

/// <summary>
///     Stamps the <see cref="IAuditedProperties" /> "created by"/"updated by" pair on a snapshot entry,
///     shared between <see cref="EfCoreAuditHook" /> (signed-in-user source) and
///     <c>DataOwnerHook</c> in <c>DKNet.EfCore.DataAuthorization</c> (tenant-ownership-key source).
/// </summary>
internal static class AuditPropertyStamper
{
    #region Methods

    /// <summary>
    ///     Stamps <see cref="IAuditedProperties.CreatedBy" /> and <see cref="IAuditedProperties.CreatedOn" />
    ///     on a newly added entity, unless <see cref="IAuditedProperties.CreatedBy" /> is already set.
    /// </summary>
    /// <param name="entry">The snapshot entry for the newly added entity.</param>
    /// <param name="value">The value to stamp into <see cref="IAuditedProperties.CreatedBy" />.</param>
    public static void StampCreatedBy(SnapshotEntityEntry entry, string value)
    {
        if (entry.Entity is not IAuditedProperties au || !string.IsNullOrEmpty(au.CreatedBy)) return;

        SetOwnedProperty(entry.Entry, au, nameof(au.CreatedBy), value);
        SetOwnedProperty(entry.Entry, au, nameof(au.CreatedOn), DateTimeOffset.UtcNow);
    }

    /// <summary>
    ///     Stamps <see cref="IAuditedProperties.UpdatedBy" /> and <see cref="IAuditedProperties.UpdatedOn" />
    ///     on a modified entity, unless <see cref="HasExplicitModifier" /> reports a domain method already
    ///     recorded an explicit modifier for this change set.
    /// </summary>
    /// <param name="entry">The snapshot entry for the modified entity.</param>
    /// <param name="value">The value to stamp into <see cref="IAuditedProperties.UpdatedBy" />.</param>
    public static void StampUpdatedBy(SnapshotEntityEntry entry, string value)
    {
        if (entry.Entity is not IAuditedProperties au) return;
        if (entry.Entry.Metadata.FindProperty(nameof(IAuditedProperties.UpdatedBy)) is null) return;
        if (HasExplicitModifier(entry, au)) return;

        SetOwnedProperty(entry.Entry, au, nameof(IAuditedProperties.UpdatedBy), value);
        SetOwnedProperty(entry.Entry, au, nameof(IAuditedProperties.UpdatedOn), DateTimeOffset.UtcNow);
    }

    /// <summary>
    ///     Determines whether a domain method already recorded an explicit modifier for this change set, by
    ///     comparing <see cref="IAuditedProperties.UpdatedBy" /> and <see cref="IAuditedProperties.UpdatedOn" />
    ///     against their original values.
    /// </summary>
    /// <param name="entry">The snapshot entry for the modified entity.</param>
    /// <param name="au">The entity's audited-properties view.</param>
    public static bool HasExplicitModifier(SnapshotEntityEntry entry, IAuditedProperties au)
    {
        var originalUpdatedBy = entry.Entry.Property(nameof(IAuditedProperties.UpdatedBy)).OriginalValue as string;
        if (!string.Equals(originalUpdatedBy, au.UpdatedBy, StringComparison.Ordinal)) return true;

        if (entry.Entry.Metadata.FindProperty(nameof(IAuditedProperties.UpdatedOn)) is null) return false;

        var originalUpdatedOn =
            entry.Entry.Property(nameof(IAuditedProperties.UpdatedOn)).OriginalValue as DateTimeOffset?;
        return originalUpdatedOn != au.UpdatedOn;
    }

    /// <summary>
    ///     Sets a property's value on <paramref name="entity" />, preferring EF Core's own compiled property
    ///     accessor (<paramref name="entry" />) — which needs no reflection and reaches private setters,
    ///     init-only properties, and shadow properties that <see cref="FindWritableProperty" /> cannot. Falls
    ///     back to the type-hierarchy reflection walk only when the property is not part of the EF model at
    ///     all (for example an explicitly ignored column).
    /// </summary>
    /// <param name="entry">The EF Core entry tracking <paramref name="entity" />.</param>
    /// <param name="entity">The object to set the property on.</param>
    /// <param name="propertyName">The name of the property to set.</param>
    /// <param name="value">The value to set.</param>
    /// <exception cref="ArgumentException">
    ///     No writable property named <paramref name="propertyName" /> exists anywhere in <paramref name="entity" />'s
    ///     type hierarchy, and it is not part of the EF model either.
    /// </exception>
    public static void SetOwnedProperty(EntityEntry entry, object entity, string propertyName, object value)
    {
        if (entry.Metadata.FindProperty(propertyName) is not null)
        {
            entry.Property(propertyName).CurrentValue = value;
            return;
        }

        var property = FindWritableProperty(entity.GetType(), propertyName) ??
                       throw new ArgumentException(
                           $"Property '{propertyName}' not found on type '{entity.GetType().FullName}'.",
                           nameof(propertyName));

        entity.SetPropertyValue(property, value);
    }

    /// <summary>
    ///     Finds a property by name, walking up from <paramref name="type" /> through its base types.
    /// </summary>
    /// <remarks>
    ///     <see cref="Type.GetProperty(string, BindingFlags)" /> on a derived type only resolves non-public
    ///     accessors declared directly on that type — it does not see a non-public setter declared on a base
    ///     class, exactly the "private setter + intention-revealing method" pattern this codebase favors
    ///     (e.g. <c>AuditedEntity&lt;TKey&gt;</c>). Searching each type in the hierarchy with
    ///     <see cref="BindingFlags.DeclaredOnly" /> finds it.
    /// </remarks>
    /// <param name="type">The runtime type to start searching from.</param>
    /// <param name="propertyName">The name of the property to find.</param>
    /// <returns>The writable <see cref="PropertyInfo" />, or <c>null</c> if none exists in the hierarchy.</returns>
    private static PropertyInfo? FindWritableProperty(Type type, string propertyName)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            var property = current.GetProperty(propertyName,
                BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.DeclaredOnly);

            if (property?.GetSetMethod(true) is not null) return property;
        }

        return null;
    }

    #endregion
}
