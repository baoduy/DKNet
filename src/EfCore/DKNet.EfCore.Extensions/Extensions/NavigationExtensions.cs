using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DKNet.EfCore.Extensions.Extensions;

public static class NavigationExtensions
{
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

    public static IEnumerable<object?> GetOriginalKeyValues(this EntityEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        // Retrieve the primary key metadata for the entity.
        var primaryKey = entry.Metadata.FindPrimaryKey();
        if (primaryKey is null) return [];

        // Get the original values for each primary key property.
        return primaryKey.Properties.Select(p => entry.OriginalValues[p]);
    }

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

    public static object? GetOriginalValue(this EntityEntry entity, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(propertyName);

        // Retrieve the property metadata for the given property name.
        var property = entity.Metadata.FindProperty(propertyName);
        if (property is null) return null;

        // Get the current value for the specified property.
        return entity.OriginalValues[property];
    }

    public static bool HasProperty(this EntityEntry entry, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(propertyName);

        // Check if the entity has a property with the specified name.
        return entry.Metadata.FindProperty(propertyName) is not null;
    }

    public static IEnumerable<object?> GetCurrentKeyValues(this EntityEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        // Retrieve the primary key metadata for the entity.
        var primaryKey = entry.Metadata.FindPrimaryKey();
        if (primaryKey is null) return [];

        // Get the original values for each primary key property.
        return primaryKey.Properties.Select(p => entry.CurrentValues[p]);
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

    public static IEnumerable<INavigation> GetCollectionNavigations(this DbContext context, Type entityType)
    {
        var type = context.Model.FindEntityType(entityType);
        if (type is null) return [];
        return type
            .GetNavigations().Where(n => n.IsCollection && !n.IsShadowProperty());
    }

    public static IEnumerable<object> GetNewEntitiesFromNavigations(this DbContext context)
    {
        var roots = context.GetPossibleUpdatingEntities();
        foreach (var root in roots)
        {
            var list = context.GetNewEntitiesFromNavigations(root);
            foreach (var o in list)
                yield return o;
        }
    }

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

    public static IEnumerable<EntityEntry> GetPossibleUpdatingEntities(this DbContext context)
    {
        context.ChangeTracker.DetectChanges();
        return context.ChangeTracker.Entries().Where(e =>
            e.State is EntityState.Detached or EntityState.Modified or EntityState.Unchanged);
    }

    public static async Task<int> AddNewEntitiesFromNavigations(this DbContext context,
        CancellationToken cancellationToken = default)
    {
        var newEntities = context.GetNewEntitiesFromNavigations().ToList();
        await context.AddRangeAsync(newEntities, cancellationToken);
        return newEntities.Count;
    }
}