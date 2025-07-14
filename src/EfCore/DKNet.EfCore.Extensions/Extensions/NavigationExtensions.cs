using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DKNet.EfCore.Extensions.Extensions;

public static class NavigationExtensions
{
    public static IEnumerable<object> GetNavigationEntities(this object obj, INavigation navigation)
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
    /// Determines whether the given <see cref="EntityEntry"/> represents a new entity
    /// that hasn't yet been persisted to the database.
    /// </summary>
    /// <param name="entry">The <see cref="EntityEntry"/> to evaluate.</param>
    /// <returns>
    /// <c>true</c> if the entity is new (i.e., its state is Detached, its key is not set,
    /// or its primary key properties have null original values); otherwise, <c>false</c>.
    /// </returns>
    public static bool IsNewEntity(this EntityEntry entry)
    {
        // If the entity is not in the Detached state, it is not new.
        if (entry.State is EntityState.Modified or EntityState.Deleted) return false;

        // If the entity's key is not set, it is considered new.
        if (!entry.IsKeySet) return true;

        // Retrieve the primary key metadata for the entity.
        var primaryKey = entry.Metadata.FindPrimaryKey();

        // If no primary key is defined, the entity is not new.
        if (primaryKey is null) return false;

        // Check if all primary key properties have null original values.
        return primaryKey.Properties.All(p => entry.OriginalValues[p] is null);
    }

    public static IEnumerable<INavigation> GetCollectionNavigations<TEntity>(this DbContext context)
    {
        var entityType = context.Model.FindEntityType(typeof(TEntity));
        if (entityType is null) return [];
        return entityType
            .GetNavigations().Where(n => n.IsCollection && !n.IsShadowProperty());
    }

    public static IEnumerable<object> GetNewEntitiesFromNavigations<TEntity>(this DbContext context,
        [DisallowNull] TEntity entity)
    {
        var navigations = context.GetCollectionNavigations<TEntity>();

        foreach (var nav in navigations)
        {
            foreach (var item in entity.GetNavigationEntities(nav))
            {
                var newEntry = context.Entry(item);
                if (newEntry.IsNewEntity())
                    yield return item;
            }
        }
    }

    public static IEnumerable<EntityEntry> GetPossibleUpdatingEntities(this DbContext context)
    {
        context.ChangeTracker.DetectChanges();
        return context.ChangeTracker.Entries().Where(e =>
            e.State is EntityState.Detached or EntityState.Modified or EntityState.Unchanged);
    }

    public static async Task AddNewEntitiesFromNavigations(this DbContext context)
    {
        var entities = context.GetPossibleUpdatingEntities();
        foreach (var entity in entities)
        {
            var list = context.GetNewEntitiesFromNavigations(entity);
            await context.AddRangeAsync(list);
        }
    }
}