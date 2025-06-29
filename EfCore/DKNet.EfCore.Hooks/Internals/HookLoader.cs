using System.Collections.Immutable;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DKNet.EfCore.Hooks.Internals;

internal class HookFactory
{
    private static bool IsBaseTypeAvailable(Type type) =>
        type.BaseType is not null && type.BaseType.IsClass && type.BaseType != typeof(object);

    /// <summary>
    /// Load all hooks keyed names for the nested DbContext.
    /// </summary>
    /// <param name="dbContext"></param>
    /// <returns></returns>
    private static string[] GetProviderKeyNames(DbContext dbContext)
    {
        //using HashSet to prevent duplication
        var name = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var type = dbContext.GetType();

        do
        {
            name.Add(type.FullName!);
            type = IsBaseTypeAvailable(type) ? type.BaseType : null;

        } while (type is not null);

        return [.. name];
    }

    /// <summary>
    /// Load all hooks for the nested DbContext.
    /// </summary>
    /// <param name="dbContext"></param>
    /// <param name="serviceProvider">The scoped service provider to resolve hooks from</param>
    public static (IEnumerable<IBeforeSaveHookAsync> beforeSaveHooks, IEnumerable<IAfterSaveHookAsync> afterSaveHooks) LoadHooks(DbContext dbContext, IServiceProvider serviceProvider)
    {
        //The Hooks of Parents also able to be used here
        var keys = GetProviderKeyNames(dbContext);
        var hooks = keys.SelectMany(serviceProvider.GetKeyedServices<IHookBaseAsync>).ToImmutableList();

        var beforeSaveHooks = hooks.OfType<IBeforeSaveHookAsync>();
        var afterSaveHooks = hooks.OfType<IAfterSaveHookAsync>();

        return (beforeSaveHooks, afterSaveHooks);
    }
}