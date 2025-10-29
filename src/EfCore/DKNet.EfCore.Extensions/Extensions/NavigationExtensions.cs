#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: NavigationExtensions.cs
// Description: Helper extension methods to work with EF Core navigation properties and entries.

using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DKNet.EfCore.Extensions.Extensions;

/// <summary>
///     Provides extension methods for working with EF Core navigations, entries and change-tracking helpers.
/// </summary>
public static class NavigationExtensions
{
    #region Methods

    /// <summary>
    ///     Finds new entities reachable from collection navigations and adds them to the DbContext in a single batch.
    /// </summary>
    /// <param name="context">The DbContext to scan for navigations and to which new entities will be added.</param>
    /// <param name="cancellationToken">A cancellation token for the asynchronous add operation.</param>
    /// <returns>The number of entities that were added to the context.</returns>
    public static async Task<int> AddNewEntitiesFromNavigations(
        this DbContext context,
        CancellationToken cancellationToken = default)
    {
        var newEntities = context.GetNewEntitiesFromNavigations().ToList();
        await context.AddRangeAsync(newEntities, cancellationToken);
        return newEntities.Count;
    }

    /// <summary>
    ///     Returns collection navigations for the provided CLR <paramref name="entityType" /> from the model.
    /// </summary>
    /// <param name="context">The DbContext whose model will be queried.</param>
    /// <param name="entityType">The CLR type of the entity to inspect.</param>
    /// <returns>An enumerable of <see cref="INavigation" /> describing collection navigations for the type.</returns>
    public static IEnumerable<INavigation> GetCollectionNavigations(this DbContext context, Type entityType)
    {
        var type = context.Model.FindEntityType(entityType);
        if (type is null) return [];

        return type
            .GetNavigations().Where(n => n.IsCollection && !n.IsShadowProperty());
    }

    /// <summary>
    ///     Returns the current primary key values for the entity represented by the provided <paramref name="entry" />.
    /// </summary>
    /// <param name="entry">The <see cref="EntityEntry" /> to examine.</param>
    /// <returns>An enumerable of current key values or an empty sequence when the entity has no primary key.</returns>
    public static IEnumerable<object?> GetCurrentKeyValues(this EntityEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        // Retrieve the primary key metadata for the entity.
        var primaryKey = entry.Metadata.FindPrimaryKey();
        if (primaryKey is null) return [];

        // Get the current values for each primary key property.
        return primaryKey.Properties.Select(p => entry.CurrentValues[p]);
    }

    /// <summary>
    ///     Retrieves the current value for the given property name from the provided <paramref name="entity" /> entry.
    /// </summary>
    /// <param name="entity">The <see cref="EntityEntry" /> to query.</param>
    /// <param name="propertyName">The name of the property to retrieve.</param>
    /// <returns>The current property value, or <c>null</c> if the property does not exist.</returns>
    public static object? GetCurrentValue(this EntityEntry entity, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(propertyName);

        // Retrieve the property metadata for the given property name.
        var property = entity.Metadata.FindProperty(propertyName);
        if (property is null) return null;

        // Get the current value for the specified property.
        return entity.CurrentValues[property];
    }

    /// <summary>
    ///     Returns the values of a collection navigation from the supplied object instance.
    /// </summary>
    /// <param name="obj">The entity instance containing the navigation.</param>
    /// <param name="navigation">Metadata describing the navigation to read.</param>
    /// <returns>An enumerable of navigation items (may be empty).</returns>
    public static IEnumerable<object> GetNavigationValues(this object obj, INavigation navigation)
    {
        ArgumentNullException.ThrowIfNull(obj);
        ArgumentNullException.ThrowIfNull(navigation);

        if (navigation.PropertyInfo is not null)
            return navigation.PropertyInfo.GetValue(obj) as IEnumerable<object> ?? [];

        if (navigation.FieldInfo is not null)
            return navigation.FieldInfo.GetValue(obj) as IEnumerable<object> ?? [];

        return [];
    }

    /// <summary>
    ///     Scans all possible root entities tracked by the context and yields new entities found in collection navigations.
    /// </summary>
    /// <param name="context">The DbContext whose change tracker is scanned.</param>
    /// <returns>An enumerable of new entities discovered on collection navigations.</returns>
    public static IEnumerable<object> GetNewEntitiesFromNavigations(this DbContext context)
    {
        var roots = context.GetPossibleUpdatingEntities();
        foreach (var root in roots)
        {
            var list = context.GetNewEntitiesFromNavigations(root);
            foreach (var o in list) yield return o;
        }
    }

    /// <summary>
    ///     Scans the collection navigations of a specific root <paramref name="entity" /> and yields any child
    ///     entities that are considered new by the context.
    /// </summary>
    /// <param name="context">The DbContext whose model and change tracker are used.</param>
    /// <param name="entity">The root entity entry to scan.</param>
    /// <returns>An enumerable of new child entities discovered under collection navigations.</returns>
    public static IEnumerable<object> GetNewEntitiesFromNavigations(this DbContext context, EntityEntry entity)
    {
        var navigations = context.GetCollectionNavigations(entity.Metadata.ClrType);
        foreach (var nav in navigations)
        foreach (var i in entity.Entity.GetNavigationValues(nav))
        {
            var item = context.Entry(i);
            if (item.IsNewEntity()) yield return i;
        }
    }

    /// <summary>
    ///     Returns the original primary key values for the specified <paramref name="entry" />.
    /// </summary>
    /// <param name="entry">The entry to examine.</param>
    /// <returns>An enumerable of original key values or an empty sequence when no primary key exists.</returns>
    public static IEnumerable<object?> GetOriginalKeyValues(this EntityEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        // Retrieve the primary key metadata for the entity.
        var primaryKey = entry.Metadata.FindPrimaryKey();
        if (primaryKey is null) return [];

        // Get the original values for each primary key property.
        return primaryKey.Properties.Select(p => entry.OriginalValues[p]);
    }

    /// <summary>
    ///     Retrieves the original value for a property on an tracked entity entry.
    /// </summary>
    /// <param name="entity">The entry to query.</param>
    /// <param name="propertyName">The property name to retrieve.</param>
    /// <returns>The original value, or <c>null</c> if the property does not exist.</returns>
    public static object? GetOriginalValue(this EntityEntry entity, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(propertyName);

        // Retrieve the property metadata for the given property name.
        var property = entity.Metadata.FindProperty(propertyName);
        if (property is null) return null;

        // Get the original value for the specified property.
        return entity.OriginalValues[property];
    }

    /// <summary>
    ///     Returns the set of entries that the context may update; includes Detached, Modified and Unchanged states
    ///     after running DetectChanges to ensure the tracker is up-to-date.
    /// </summary>
    /// <param name="context">The <see cref="DbContext" /> to inspect.</param>
    /// <returns>An enumerable of potentially updating <see cref="EntityEntry" /> instances.</returns>
    public static IEnumerable<EntityEntry> GetPossibleUpdatingEntities(this DbContext context)
    {
        context.ChangeTracker.DetectChanges();
        return context.ChangeTracker.Entries().Where(e =>
            e.State is EntityState.Detached or EntityState.Modified or EntityState.Unchanged);
    }

    /// <summary>
    ///     Checks whether the specified property exists on the provided <see cref="EntityEntry" /> metadata.
    /// </summary>
    /// <param name="entry">The entry whose metadata will be inspected.</param>
    /// <param name="propertyName">The property name to check for existence.</param>
    /// <returns><c>true</c> when the property exists; otherwise <c>false</c>.</returns>
    public static bool HasProperty(this EntityEntry entry, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(propertyName);

        // Check if the entity has a property with the specified name.
        return entry.Metadata.FindProperty(propertyName) is not null;
    }

    /// <summary>
    ///     Determines whether the given <see cref="EntityEntry" /> represents a new entity
    ///     that hasn't yet been persisted to the database.
    /// </summary>
    /// <param name="entry">The <see cref="EntityEntry" /> to evaluate.</param>
    /// <returns>
    ///     <c>true</c> if the entity is new (i.e., its state is Detached, its key is not set,
    ///     or its primary key properties have null original values); otherwise, <c>false</c>.
    /// </returns>
    public static bool IsNewEntity(this EntityEntry entry)
    {
        // If the entity's key is not set, it is considered new.
        if (!entry.IsKeySet) return true;

        if (entry.State is EntityState.Added) return true;

        // If the entity is not in the Detached state, it is not new.
        if (entry.State is EntityState.Modified or EntityState.Deleted) return false;

        var keyValues = entry.GetOriginalKeyValues().ToList();
        return keyValues.Count <= 0 || keyValues.TrueForAll(kv => kv is null);
    }

    #endregion
}

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member