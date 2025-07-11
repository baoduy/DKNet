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
}