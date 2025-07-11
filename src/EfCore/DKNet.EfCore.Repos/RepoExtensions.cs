using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DKNet.EfCore.Repos;

internal static class RepoExtensions
{
    public static IEnumerable<object> GetNavigationCollection(this object obj, INavigation navigation)
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
    /// that has not yet been persisted to the database.
    /// </summary>
    /// <param name="entry">The <see cref="EntityEntry"/> to evaluate.</param>
    /// <returns>
    /// <c>true</c> if the entity is new (i.e., its state is Detached, its key is not set,
    /// or its primary key properties have null original values); otherwise, <c>false</c>.
    /// </returns>
    public static bool IsNewItem(this EntityEntry entry)
    {
        // If the entity is not in the Detached state, it is not new.
        if (entry.State != EntityState.Detached) return false;

        // If the entity's key is not set, it is considered new.
        if (!entry.IsKeySet) return true;

        // Retrieve the primary key metadata for the entity.
        var primaryKey = entry.Metadata.FindPrimaryKey();

        // If no primary key is defined, the entity is not new.
        if (primaryKey is null) return false;

        // Check if all primary key properties have null original values.
        return primaryKey.Properties.All(p => entry.OriginalValues[p] is null);
    }
}