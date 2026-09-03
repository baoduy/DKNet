using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DKNet.EfCore.Hooks.Internals;

internal class HookFactory(IServiceProvider provider)
{
    #region Fields

    // The DbContext type hierarchy driving the keyed-service lookup is fixed for the lifetime of the
    // process, so it is computed once per DbContext type and reused for every SaveChanges afterwards.
    private static readonly ConcurrentDictionary<Type, string[]> _providerKeyNamesCache = new();

    #endregion

    #region Methods

    /// <summary>
    ///     Load all hooks keyed names for the nested DbContext.
    /// </summary>
    /// <param name="dbContext"></param>
    /// <returns></returns>
    private static string[] GetProviderKeyNames(DbContext dbContext) =>
        _providerKeyNamesCache.GetOrAdd(dbContext.GetType(), static type =>
        {
            //using HashSet to prevent duplication
            var name = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var current = type;

            do
            {
                name.Add(current.FullName!);
                current = IsBaseTypeAvailable(current) ? current.BaseType : null;
            } while (current is not null);

            return [.. name];
        });

    private static bool IsBaseTypeAvailable(Type type) =>
        type.BaseType is not null && type.BaseType.IsClass && type.BaseType != typeof(object);

    /// <summary>
    ///     Load all hooks for the nested DbContext.
    /// </summary>
    /// <param name="dbContext"></param>
    public (IReadOnlyList<IBeforeSaveHookAsync> beforeSaveHooks, IReadOnlyList<IAfterSaveHookAsync> afterSaveHooks)
        LoadHooks(DbContext dbContext)
    {
        //The Hooks of Parents also able to be used here
        var keys = GetProviderKeyNames(dbContext);
        var hooks = keys.SelectMany(provider.GetKeyedServices<IHookBaseAsync>).ToArray();

        var beforeSaveHooks = hooks.OfType<IBeforeSaveHookAsync>().ToArray();
        var afterSaveHooks = hooks.OfType<IAfterSaveHookAsync>().ToArray();

        return (beforeSaveHooks, afterSaveHooks);
    }

    #endregion
}